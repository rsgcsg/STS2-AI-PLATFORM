import { spawnSync } from "node:child_process";

export function readGameProcessStartedAt(pid, platform = process.platform, { spawnProcess = spawnSync } = {}) {
  if (!/^\d+$/u.test(String(pid))) throw new Error("game_process_id_invalid");
  const command = platform === "win32" ? "powershell.exe" : "ps";
  const arguments_ = platform === "win32"
    ? ["-NoProfile", "-NonInteractive", "-Command", `(Get-Process -Id ${pid} -ErrorAction Stop).StartTime.ToUniversalTime().ToString('o')`]
    : ["-p", String(pid), "-o", "lstart="];
  const result = spawnProcess(command, arguments_, { encoding: "utf8", windowsHide: true,
    env: { ...process.env, LC_ALL: "C" }, timeout: 3000 });
  const instant = Date.parse(result.stdout?.trim() ?? "");
  if (result.error || result.status !== 0 || !Number.isFinite(instant)) throw new Error("game_process_generation_unavailable");
  return new Date(instant).toISOString();
}

export function listGameProcesses(
  platform = process.platform,
  { spawnProcess = spawnSync, failClosed = false } = {}
) {
  if (platform === "win32") {
    const result = spawnProcess("tasklist", ["/FO", "CSV", "/NH"], {
      encoding: "utf8",
      windowsHide: true
    });
    if (result.error || result.status !== 0) {
      if (failClosed) {
        throw new Error(
          `Could not enumerate Windows STS2 processes: ${
            result.error?.message ?? result.stderr ?? `tasklist exited ${result.status}`
          }`
        );
      }
      return [];
    }
    return result.stdout.split("\n")
      .map((line) => line.trim())
      .filter((line) => /^"?SlayTheSpire2\.exe"?(?:,|$)/iu.test(line));
  }
  // Inspect the executable name, not arguments (a setup --game-dir is not a game).
  const result = spawnProcess("ps", ["-Ao", "pid=,comm="], { encoding: "utf8" });
  if (result.error || result.status !== 0) {
    if (failClosed) {
      throw new Error(
        `Could not enumerate STS2 processes: ${
          result.error?.message ?? result.stderr ?? `ps exited ${result.status}`
        }`
      );
    }
    return [];
  }
  return result.stdout.split("\n")
    .map((line) => line.trim())
    .filter((line) => /(?:^\d+\s+|\/)(?:Slay the Spire 2|SlayTheSpire2)$/u.test(line));
}
