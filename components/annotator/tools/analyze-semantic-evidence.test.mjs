import assert from "node:assert/strict";
import { mkdtemp, mkdir, readFile, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { analyze } from "./analyze-semantic-evidence.mjs";

function frame(snapshotId, hp, readId, payload) {
  return {
    snapshot_id: snapshotId,
    observed_at: "2026-01-01T00:00:00Z",
    interaction_kind: "combat",
    snapshot: { hp, hand_count: 5, visible_text: payload },
    reads: [{
      kind: "run_deck",
      read_evidence_id: readId,
      captured_at: "2026-01-01T00:00:00Z",
      payload_sha256: "a".repeat(64)
    }]
  };
}

test("streams repeated large frames into deterministic role references", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "semantic-analysis-"));
  try {
    await mkdir(path.join(root, "blobs", "sha256", "aa"), { recursive: true });
    await writeFile(path.join(root, "blobs", "sha256", "aa", `${"a".repeat(64)}.json`), "{}\n");
    await writeFile(path.join(root, "coverage.json"), JSON.stringify({ read_materialized: 240 }));
    const payload = "visible-card-text ".repeat(4096);
    const s0 = frame("s0", 10, "read-0", payload);
    const s0Later = frame("s0-later", 10, "read-1", payload);
    const s1 = frame("s1", 9, "read-2", payload);
    const action = { action_witness_id: "a1", record_id: "r1" };
    const events = [];
    for (let index = 0; index < 80; index++) {
      const observedAt = `2026-01-01T00:00:${String(index % 60).padStart(2, "0")}Z`;
      const source = index % 3 === 0 ? s0 : s0Later;
      events.push({
        schema_version: 2,
        schema: "legacy",
        sequence: index + 1,
        observed_at: observedAt,
        kind: index === 0 ? "action_accepted" : "transition_proved",
        action,
        human_observation: source,
        semantic_pre: source,
        semantic_successor: index === 79 ? s1 : undefined,
        boundary: { state: index === 79 ? s1 : source }
      });
    }
    const trace = `${events.map((value) => JSON.stringify(value)).join("\n")}\n`;
    const tracePath = path.join(root, "semantic-boundary-trace.jsonl");
    await writeFile(tracePath, trace);
    const before = await readFile(tracePath, "utf8");

    const result = await analyze(root);
    const repeat = await analyze(root);

    assert.deepEqual(result, repeat);
    assert.deepEqual(result.dispositions, {
      accepted: 1,
      proved: 79,
      unknown: 0,
      cancelled: 0,
      aborted: 0
    });
    assert.equal(result.inline_frames.occurrences_by_role.human_observation, 80);
    assert.equal(result.inline_frames.occurrences_by_role.semantic_pre, 80);
    assert.equal(result.inline_frames.occurrences_by_role.semantic_successor, 1);
    assert.equal(result.inline_frames.occurrences_by_role.boundary_state, 80);
    assert.equal(result.inline_frames.unique_content_digest_count, 3);
    assert.equal(result.normalized_projection.frame_record_count, 3);
    assert.equal(result.normalized_projection.event_count, 80);
    assert.equal(result.normalized_projection.role_reference_count, 241);
    assert.deepEqual(result.legacy_to_normalized, {
      legacy_event_count: 80,
      normalized_event_count: 80,
      legacy_inline_frame_occurrences: 241,
      normalized_role_reference_count: 241,
      normalized_unique_frame_records: 3,
      event_count_preserved: true,
      role_references_preserved: true,
      content_identity: "sha256(canonical exact frozen frame)"
    });
    assert.equal(result.reads.unique_payload_digest_count_in_trace, 1);
    assert.ok(result.normalized_projection.total_bytes < result.files.semantic_trace_bytes);
    assert.ok(result.normalized_projection.gzip_bytes < result.compression_control.existing_trace_gzip_bytes);
    assert.deepEqual(result.processing, {
      input: "jsonl-stream",
      raw_events_retained: false,
      retained_frame_keys: 3
    });
    assert.equal(await readFile(tracePath, "utf8"), before);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
