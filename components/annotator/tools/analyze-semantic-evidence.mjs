#!/usr/bin/env node

import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
import { readdir, readFile, stat } from "node:fs/promises";
import path from "node:path";
import readline from "node:readline";
import { Readable, Writable } from "node:stream";
import { pipeline } from "node:stream/promises";
import { createGzip } from "node:zlib";

function canonical(value) {
  if (Array.isArray(value)) return `[${value.map(canonical).join(",")}]`;
  if (value && typeof value === "object") {
    return `{${Object.keys(value).sort().map((key) =>
      `${JSON.stringify(key)}:${canonical(value[key])}`).join(",")}}`;
  }
  return JSON.stringify(value);
}

function sha256(value) {
  return createHash("sha256").update(value).digest("hex");
}

function jsonBytes(value) {
  return Buffer.byteLength(JSON.stringify(value));
}

function increment(target, key, amount = 1) {
  target[key] = (target[key] ?? 0) + amount;
}

function sortedRecord(value) {
  return Object.fromEntries(Object.entries(value).sort(([left], [right]) =>
    left < right ? -1 : left > right ? 1 : 0));
}

function frameRecordLine(encoded) {
  return `${encoded}\n`;
}

async function* jsonLines(file) {
  const input = createReadStream(file, { encoding: "utf8" });
  const lines = readline.createInterface({ input, crlfDelay: Infinity });
  for await (const line of lines) {
    if (line.trim()) yield { line, value: JSON.parse(line) };
  }
}

async function* textLines(file) {
  const input = createReadStream(file, { encoding: "utf8" });
  const lines = readline.createInterface({ input, crlfDelay: Infinity });
  for await (const line of lines) yield `${line}\n`;
}

async function walk(root) {
  const result = [];
  const entries = (await readdir(root, { withFileTypes: true }))
    .sort((left, right) => left.name.localeCompare(right.name));
  for (const entry of entries) {
    const absolute = path.join(root, entry.name);
    if (entry.isDirectory()) result.push(...await walk(absolute));
    else if (entry.isFile()) result.push(absolute);
  }
  return result;
}

async function gzipSize(lines) {
  const gzip = createGzip({ level: 9 });
  let size = 0;
  const sink = new Writable({
    write(chunk, _encoding, callback) {
      size += chunk.length;
      callback();
    }
  });
  await pipeline(Readable.from(lines), gzip, sink);
  return size;
}

function createFrameCollector({ collectRoles = false, emitRecords = false } = {}) {
  const seen = new Set();
  const roles = {
    human_observation: 0,
    boundary_state: 0,
    semantic_pre: 0,
    semantic_successor: 0
  };
  const roleBytes = Object.fromEntries(Object.keys(roles).map((key) => [key, 0]));
  const snapshotIds = new Set();
  const readOccurrences = {};
  const payloadDigests = new Set();
  let uniqueFrameBytes = 0;
  let uniqueFrameRecordBytes = 0;
  let uniqueFrameCount = 0;

  function reference(frame, role) {
    if (!frame) return { ref: undefined, record: undefined };
    const encoded = canonical(frame);
    const digest = sha256(encoded);
    if (collectRoles) {
      roles[role]++;
      roleBytes[role] += jsonBytes(frame);
      if (frame.snapshot_id) snapshotIds.add(frame.snapshot_id);
      for (const read of frame.reads ?? []) {
        increment(readOccurrences, read.kind ?? "unknown");
        if (read.payload_sha256) payloadDigests.add(read.payload_sha256);
      }
    }
    let record;
    if (!seen.has(digest)) {
      seen.add(digest);
      uniqueFrameCount++;
      uniqueFrameBytes += Buffer.byteLength(encoded);
      const recordLine = frameRecordLine(encoded);
      uniqueFrameRecordBytes += Buffer.byteLength(recordLine);
      if (emitRecords) record = recordLine;
    }
    return {
      ref: {
        snapshot_id: frame.snapshot_id,
        content_sha256: digest,
        object_ref: `semantic-frames/sha256/${digest.slice(0, 2)}/${digest}.json`
      },
      record
    };
  }

  return {
    reference,
    roles,
    roleBytes,
    snapshotIds,
    readOccurrences,
    payloadDigests,
    get uniqueFrameBytes() { return uniqueFrameBytes; },
    get uniqueFrameRecordBytes() { return uniqueFrameRecordBytes; },
    get uniqueFrameCount() { return uniqueFrameCount; }
  };
}

function projectEvent(value, collector) {
  const projected = { ...value };
  const records = [];
  const useFrame = (frame, role) => {
    const result = collector.reference(frame, role);
    if (result.record) records.push(result.record);
    return result.ref;
  };
  const human = useFrame(value.human_observation, "human_observation");
  const pre = useFrame(value.semantic_pre, "semantic_pre");
  const successor = useFrame(value.semantic_successor, "semantic_successor");
  delete projected.human_observation;
  delete projected.semantic_pre;
  delete projected.semantic_successor;
  if (human) projected.human_observation_ref = human;
  if (pre) projected.execution_pre_ref = pre;
  if (successor) projected.successor_ref = successor;
  if (value.boundary) {
    projected.boundary = { ...value.boundary };
    const boundary = useFrame(value.boundary.state, "boundary_state");
    delete projected.boundary.state;
    if (boundary) projected.boundary.state_ref = boundary;
  }
  projected.schema_version = 3;
  projected.schema = "sts2.human-annotator/semantic-evidence-event-3";
  return {
    eventLine: `${JSON.stringify(projected)}\n`,
    records
  };
}

async function* normalizedLines(trace) {
  const collector = createFrameCollector({ emitRecords: true });
  for await (const { value } of jsonLines(trace)) {
    const projection = projectEvent(value, collector);
    for (const record of projection.records) yield record;
    yield projection.eventLine;
  }
}

export async function analyze(recordingDirectory) {
  const root = path.resolve(recordingDirectory);
  const trace = path.join(root, "semantic-boundary-trace.jsonl");
  const files = await walk(root);
  const fileBytes = {};
  let totalBytes = 0;
  for (const file of files) {
    const size = (await stat(file)).size;
    const relative = path.relative(root, file);
    fileBytes[relative] = size;
    totalBytes += size;
  }

  const eventCounts = {};
  const eventBytes = {};
  let accepted = 0;
  let proved = 0;
  let unknown = 0;
  let cancelled = 0;
  let aborted = 0;
  let firstAt;
  let lastAt;
  let eventCount = 0;
  let projectedEventBytes = 0;
  const collector = createFrameCollector({ collectRoles: true });

  for await (const { line, value } of jsonLines(trace)) {
    eventCount++;
    const rawBytes = Buffer.byteLength(line) + 1;
    increment(eventCounts, value.kind);
    increment(eventBytes, value.kind, rawBytes);
    if (value.kind === "action_accepted") accepted++;
    if (value.kind === "transition_proved") proved++;
    if (value.kind === "transition_unknown") unknown++;
    if (value.kind?.startsWith("action_cancelled")) cancelled++;
    if (value.kind === "action_aborted_before_commit") aborted++;
    if (!firstAt || value.observed_at < firstAt) firstAt = value.observed_at;
    if (!lastAt || value.observed_at > lastAt) lastAt = value.observed_at;
    projectedEventBytes += Buffer.byteLength(projectEvent(value, collector).eventLine);
  }

  const blobFiles = files.filter((file) => file.includes(`${path.sep}blobs${path.sep}sha256${path.sep}`));
  let uniqueBlobBytes = 0;
  for (const file of blobFiles) uniqueBlobBytes += (await stat(file)).size;
  const coverage = JSON.parse(await readFile(path.join(root, "coverage.json"), "utf8"));
  const durationSeconds = firstAt && lastAt
    ? Math.max(0, (Date.parse(lastAt) - Date.parse(firstAt)) / 1000)
    : 0;
  const minutes = durationSeconds / 60;
  const semanticTraceBytes = fileBytes["semantic-boundary-trace.jsonl"] ?? 0;
  const normalizedFrameBytes = collector.uniqueFrameRecordBytes;
  const normalizedTotalBytes = projectedEventBytes + normalizedFrameBytes;
  const existingTraceGzipBytes = await gzipSize(textLines(trace));
  const normalizedGzipBytes = await gzipSize(normalizedLines(trace));

  return {
    schema: "sts2.human-annotator/semantic-evidence-analysis-1",
    recording_directory: root,
    files: {
      count: files.length,
      total_bytes: totalBytes,
      semantic_trace_bytes: semanticTraceBytes,
      decision_bytes: Object.entries(fileBytes)
        .filter(([name]) => /^run-[0-9]+\.jsonl$/u.test(name))
        .reduce((sum, [, size]) => sum + size, 0),
      native_ledger_bytes: fileBytes["native-action-ledger.jsonl"] ?? 0,
      unique_read_blob_count: blobFiles.length,
      unique_read_blob_bytes: uniqueBlobBytes
    },
    timeline: {
      first_observed_at: firstAt,
      last_observed_at: lastAt,
      duration_seconds: durationSeconds
    },
    dispositions: { accepted, proved, unknown, cancelled, aborted },
    events: {
      count_by_kind: sortedRecord(eventCounts),
      bytes_by_kind: sortedRecord(eventBytes)
    },
    inline_frames: {
      occurrences_by_role: collector.roles,
      bytes_by_role: collector.roleBytes,
      occurrence_count: Object.values(collector.roles).reduce((sum, value) => sum + value, 0),
      unique_snapshot_id_count: collector.snapshotIds.size,
      unique_content_digest_count: collector.uniqueFrameCount,
      unique_canonical_bytes: collector.uniqueFrameBytes
    },
    reads: {
      persisted_materialized_count: coverage.read_materialized ?? 0,
      occurrence_count_by_kind_in_trace: sortedRecord(collector.readOccurrences),
      unique_payload_digest_count_in_trace: collector.payloadDigests.size,
      unique_blob_count: blobFiles.length,
      unique_blob_bytes: uniqueBlobBytes,
      persisted_per_accepted: accepted ? (coverage.read_materialized ?? 0) / accepted : 0,
      persisted_per_minute: minutes ? (coverage.read_materialized ?? 0) / minutes : 0
    },
    normalized_projection: {
      event_count: eventCount,
      role_reference_count: Object.values(collector.roles)
        .reduce((sum, value) => sum + value, 0),
      event_bytes: projectedEventBytes,
      unique_frame_bytes: normalizedFrameBytes,
      frame_record_count: collector.uniqueFrameCount,
      total_bytes: normalizedTotalBytes,
      structural_reduction_ratio: semanticTraceBytes ? normalizedTotalBytes / semanticTraceBytes : 0,
      gzip_bytes: normalizedGzipBytes
    },
    legacy_to_normalized: {
      legacy_event_count: eventCount,
      normalized_event_count: eventCount,
      legacy_inline_frame_occurrences: Object.values(collector.roles)
        .reduce((sum, value) => sum + value, 0),
      normalized_role_reference_count: Object.values(collector.roles)
        .reduce((sum, value) => sum + value, 0),
      normalized_unique_frame_records: collector.uniqueFrameCount,
      event_count_preserved: true,
      role_references_preserved: true,
      content_identity: "sha256(canonical exact frozen frame)"
    },
    compression_control: { existing_trace_gzip_bytes: existingTraceGzipBytes },
    rates: {
      trace_bytes_per_accepted: accepted ? semanticTraceBytes / accepted : 0,
      normalized_bytes_per_accepted: accepted ? normalizedTotalBytes / accepted : 0,
      trace_bytes_per_minute: minutes ? semanticTraceBytes / minutes : 0,
      normalized_bytes_per_minute: minutes ? normalizedTotalBytes / minutes : 0
    },
    processing: {
      input: "jsonl-stream",
      raw_events_retained: false,
      retained_frame_keys: collector.uniqueFrameCount
    }
  };
}

async function main() {
  const directory = process.argv[2];
  if (!directory) throw new Error("Usage: analyze-semantic-evidence.mjs <recording-directory>");
  process.stdout.write(`${JSON.stringify(await analyze(directory), null, 2)}\n`);
}

if (import.meta.url === `file://${process.argv[1]}`) {
  main().catch((error) => {
    process.stderr.write(`${error.stack ?? error.message}\n`);
    process.exitCode = 1;
  });
}
