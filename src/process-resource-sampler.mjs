import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { performance } from "node:perf_hooks";

const execFileAsync = promisify(execFile);
const GIB = 1024 ** 3;

function parsePsCpuTime(value) {
  const [dayPart, timePart = dayPart] = value.includes("-") ? value.split("-", 2) : ["0", value];
  const parts = timePart.split(":").map(Number);
  if (parts.some((part) => !Number.isFinite(part))) return null;
  const seconds = parts.length === 3
    ? parts[0] * 3600 + parts[1] * 60 + parts[2]
    : parts.length === 2
      ? parts[0] * 60 + parts[1]
      : parts[0];
  return Number(dayPart) * 86400 + seconds;
}

export async function readProcessResourceSample(pid, platform = process.platform) {
  if (!Number.isSafeInteger(pid) || pid <= 0) throw new Error("A positive process ID is required.");
  let cpuSeconds;
  let rssBytes;
  let privateBytes = null;
  if (platform === "win32") {
    const command = [
      `$p=Get-Process -Id ${pid} -ErrorAction Stop`,
      "[pscustomobject]@{cpu_seconds_total=$p.TotalProcessorTime.TotalSeconds;rss_bytes=[int64]$p.WorkingSet64;private_bytes=[int64]$p.PrivateMemorySize64}|ConvertTo-Json -Compress"
    ].join(";");
    const { stdout } = await execFileAsync(
      "powershell",
      ["-NoProfile", "-NonInteractive", "-Command", command],
      { encoding: "utf8", timeout: 3_000, windowsHide: true }
    );
    const value = JSON.parse(stdout.trim());
    cpuSeconds = Number(value.cpu_seconds_total);
    rssBytes = Number(value.rss_bytes);
    privateBytes = Number(value.private_bytes);
  } else {
    const { stdout } = await execFileAsync(
      "ps",
      ["-p", String(pid), "-o", "rss=,time="],
      { encoding: "utf8", timeout: 3_000 }
    );
    const match = stdout.trim().match(/^(\d+)\s+(\S+)$/u);
    if (!match) throw new Error("The OS process resource row was unavailable.");
    rssBytes = Number(match[1]) * 1024;
    cpuSeconds = parsePsCpuTime(match[2]);
  }
  if (!Number.isFinite(cpuSeconds) || !Number.isFinite(rssBytes) || rssBytes < 0) {
    throw new Error("The OS returned an invalid process resource sample.");
  }
  return {
    at: new Date().toISOString(),
    monotonic_ms: performance.now(),
    pid,
    cpu_seconds_total: cpuSeconds,
    rss_bytes: rssBytes,
    private_bytes: Number.isFinite(privateBytes) ? privateBytes : null
  };
}

export class ProcessResourceSampler {
  #pid;
  #platform;
  #intervalMs;
  #onSample;
  #timer = null;
  #inFlight = null;
  #samples = [];
  #errors = [];

  constructor(pid, { intervalMs = 1_000, platform = process.platform, onSample = null } = {}) {
    if (!Number.isSafeInteger(intervalMs) || intervalMs < 250) {
      throw new Error("Resource sampling interval must be an integer of at least 250ms.");
    }
    this.#pid = pid;
    this.#platform = platform;
    this.#intervalMs = intervalMs;
    this.#onSample = onSample;
  }

  async #sample() {
    if (this.#inFlight) return this.#inFlight;
    this.#inFlight = readProcessResourceSample(this.#pid, this.#platform)
      .then((sample) => {
        this.#samples.push(sample);
        this.#onSample?.(sample);
      })
      .catch((error) => {
        this.#errors.push({
          at: new Date().toISOString(),
          monotonic_ms: performance.now(),
          error: error instanceof Error ? error.message : String(error)
        });
      })
      .finally(() => {
        this.#inFlight = null;
      });
    return this.#inFlight;
  }

  async start() {
    await this.#sample();
    this.#timer = setInterval(() => void this.#sample(), this.#intervalMs);
  }

  async stop() {
    if (this.#timer) clearInterval(this.#timer);
    this.#timer = null;
    if (this.#inFlight) await this.#inFlight;
    await this.#sample();
    return { samples: [...this.#samples], errors: [...this.#errors] };
  }
}

function samplesForWindow(samples, startMs, endMs) {
  if (!Number.isFinite(startMs) || !Number.isFinite(endMs) || endMs <= startMs) return [];
  const ordered = [...samples].sort((a, b) => a.monotonic_ms - b.monotonic_ms);
  const inside = ordered.filter((sample) =>
    sample.monotonic_ms >= startMs && sample.monotonic_ms <= endMs);
  const before = [...ordered].reverse().find((sample) => sample.monotonic_ms < startMs);
  const after = ordered.find((sample) => sample.monotonic_ms > endMs);
  return [before, ...inside, after]
    .filter(Boolean)
    .filter((sample, index, all) => index === 0 || sample !== all[index - 1]);
}

export function summarizeHostPerformance({
  samples,
  sampleErrors = [],
  decisionWindowStartedMs,
  decisionWindowEndedMs,
  deliveredDecisions
}) {
  const windowSamples = samplesForWindow(samples, decisionWindowStartedMs, decisionWindowEndedMs);
  const decisionWindowSampleErrors = sampleErrors.filter((error) =>
    Number.isFinite(error.monotonic_ms)
      ? error.monotonic_ms >= decisionWindowStartedMs && error.monotonic_ms <= decisionWindowEndedMs
      : true);
  const windowSeconds = Number.isFinite(decisionWindowStartedMs)
    && Number.isFinite(decisionWindowEndedMs)
    && decisionWindowEndedMs > decisionWindowStartedMs
    ? (decisionWindowEndedMs - decisionWindowStartedMs) / 1000
    : null;
  const peakRss = windowSamples.length > 0
    ? Math.max(...windowSamples.map((sample) => sample.rss_bytes))
    : null;
  const peakPrivate = windowSamples.some((sample) => sample.private_bytes != null)
    ? Math.max(...windowSamples.flatMap((sample) =>
      sample.private_bytes == null ? [] : [sample.private_bytes]))
    : null;
  const cpuSeconds = windowSamples.length >= 2
    ? Math.max(0, windowSamples.at(-1).cpu_seconds_total - windowSamples[0].cpu_seconds_total)
    : null;
  const sampledSeconds = windowSamples.length >= 2
    ? (windowSamples.at(-1).monotonic_ms - windowSamples[0].monotonic_ms) / 1000
    : null;
  const averageCores = cpuSeconds != null && sampledSeconds > 0 ? cpuSeconds / sampledSeconds : null;
  const decisionsPerSecond = windowSeconds > 0 ? deliveredDecisions / windowSeconds : null;
  return {
    status: windowSamples.length >= 2
      && decisionsPerSecond != null
      && decisionWindowSampleErrors.length === 0
      ? "measured"
      : decisionWindowSampleErrors.length > 0
        ? "measurement_error"
        : "insufficient_samples",
    delivered_normalized_semantic_decisions: deliveredDecisions,
    decision_window_seconds: windowSeconds,
    normalized_semantic_decisions_per_second: decisionsPerSecond,
    sample_count: windowSamples.length,
    sample_errors: sampleErrors,
    decision_window_sample_errors: decisionWindowSampleErrors,
    cpu_seconds: cpuSeconds,
    sampled_wall_seconds: sampledSeconds,
    average_cpu_cores: averageCores,
    peak_rss_bytes: peakRss,
    peak_private_bytes: peakPrivate,
    normalized_semantic_decisions_per_second_per_core:
      decisionsPerSecond != null && averageCores > 0 ? decisionsPerSecond / averageCores : null,
    normalized_semantic_decisions_per_second_per_gib:
      decisionsPerSecond != null && peakRss > 0 ? decisionsPerSecond / (peakRss / GIB) : null
  };
}
