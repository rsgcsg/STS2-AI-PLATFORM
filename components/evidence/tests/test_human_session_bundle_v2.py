from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from typing import Any

from sts2_platform_evidence.human_session_bundle import (
    HumanSessionBundleV2,
    verify_human_session_bundle,
)
from sts2_platform_evidence.human_session_bundle_v2 import HumanSessionBundleV2Verifier


def canonical(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(",", ":"), sort_keys=True)


def sha_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha_file(path: Path) -> str:
    return sha_bytes(path.read_bytes())


class HumanSessionBundleV2Tests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def _bundle(self, name: str = "bundle-v2") -> Path:
        bundle = self.root / name
        raw = bundle / "raw"
        for relative in ("audit", "export", "profile"):
            (bundle / relative).mkdir(parents=True)
        (raw / "blobs" / "sha256").mkdir(parents=True)
        profile = {
            "schema_version": 2,
            "schema": "sts2.ai-platform/human-capture-profile-2",
            "profile_id": "human-combat-read-rich-v2",
            "record_schema": "sts2.human-annotator/decision-record-2",
            "supported_action_families": ["ordinary_combat.play_card"],
            "reads": [
                {"phase": phase, "kind": kind, "required": True}
                for phase in ("pre", "successor")
                for kind in ("run_deck", "combat_piles")
            ],
            "non_claims": ["fixture_not_live"],
        }
        profile_sha = sha_bytes(canonical(profile).encode())
        session_id = "session-v2-test"
        timeline_id = "timeline-v2-test"
        environment = {
            "runtime_instance_id": "runtime-v2-test",
            "environment_fingerprint": "environment-v2-test",
        }
        blob_refs: dict[str, tuple[str, str]] = {}
        for kind in ("run_deck", "combat_piles"):
            payload = canonical({"kind": kind, "cards": [{"name": "Strike"}]}) + "\n"
            digest = sha_bytes(payload.encode())
            relative = f"blobs/sha256/{digest[:2]}/{digest}.json"
            path = raw / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(payload, encoding="utf-8")
            blob_refs[kind] = (relative, digest)

        def reads(phase: str, snapshot_id: str) -> list[dict[str, Any]]:
            return [
                {
                    "schema_version": 2,
                    "schema": "sts2.human-annotator/read-evidence-2",
                    "read_evidence_id": f"read-evidence-{phase}-{kind}",
                    "read_id": f"read:{kind}",
                    "kind": kind,
                    "snapshot_id": snapshot_id,
                    "runtime_instance_id": environment["runtime_instance_id"],
                    "environment_fingerprint": environment["environment_fingerprint"],
                    "status": "materialized",
                    "content_schema": f"sts2.player-environment/read/{kind}-1",
                    "completeness": {"status": "complete", "missing": []},
                    "payload_ref": blob_refs[kind][0],
                    "payload_sha256": blob_refs[kind][1],
                    "captured_at": "2026-08-25T00:00:00+00:00",
                    "error_code": None,
                    "detail": None,
                }
                for kind in ("run_deck", "combat_piles")
            ]

        record = {
            "schema_version": 2,
            "schema": "sts2.human-annotator/decision-record-2",
            "record_id": "record-v2-1",
            "session_id": session_id,
            "run_id": "run-0001",
            "timeline_id": timeline_id,
            "sequence": 1,
            "capture_profile_id": profile["profile_id"],
            "environment": environment,
            "decision_family": "ordinary_combat",
            "action": {"verb": "play"},
            "eligibility": {"status": "admitted"},
            "pre": {
                "snapshot_id": "snapshot-pre",
                "snapshot": {"snapshot_id": "snapshot-pre"},
                "reads": reads("pre", "snapshot-pre"),
            },
            "successor": {
                "snapshot_id": "snapshot-successor",
                "snapshot": {"snapshot_id": "snapshot-successor"},
                "reads": reads("successor", "snapshot-successor"),
            },
        }
        line = canonical(record) + "\n"
        (raw / "run-0001.jsonl").write_text(line, encoding="utf-8")
        (bundle / "export" / "decisions.jsonl").write_text(line, encoding="utf-8")
        recording = {
            "schema_version": 2,
            "schema": "sts2.human-annotator/recording-manifest-2",
            "session_id": session_id,
            "timeline_id": timeline_id,
            "capture_profile_id": profile["profile_id"],
            "capture_profile_sha256": profile_sha,
        }
        (raw / "recording-manifest.json").write_text(canonical(recording) + "\n", encoding="utf-8")
        (raw / "capture-profile.json").write_text(canonical(profile) + "\n", encoding="utf-8")
        (bundle / "profile" / "capture-profile.json").write_text(
            canonical(profile) + "\n", encoding="utf-8"
        )
        coverage = {
            "schema_version": 2,
            "schema": "sts2.human-annotator/coverage-2",
            "session_id": session_id,
            "admitted_records": 1,
        }
        (raw / "coverage.json").write_text(canonical(coverage) + "\n", encoding="utf-8")
        (raw / "invalidations.jsonl").write_text("", encoding="utf-8")
        journal = {
            "schema_version": 2,
            "schema": "sts2.human-annotator/run-journal-event-2",
            "event_id": "event-v2-1",
            "session_id": session_id,
            "run_id": "run-0001",
            "timeline_id": timeline_id,
            "sequence": 1,
        }
        (raw / "run-journal.jsonl").write_text(canonical(journal) + "\n", encoding="utf-8")
        audit = {
            "schema": "sts2.human-annotator/session-bundle-audit-2",
            "status": "pass",
            "valid_records": 1,
            "invalid_records": 0,
            "invalidations": 0,
        }
        (bundle / "audit" / "audit-report.json").write_text(canonical(audit) + "\n", encoding="utf-8")
        export_sha = sha_file(bundle / "export" / "decisions.jsonl")
        attestation = {
            "attested": True,
            "method": "explicit_owner_pack",
            "worker_id": "human-001",
            "machine_verifiable": False,
        }
        raw_hashes = {
            path.relative_to(raw).as_posix(): sha_file(path)
            for path in sorted(raw.rglob("*"))
            if path.is_file()
        }
        identity = {
            "schema": "sts2.human-annotator/session-bundle-2",
            "session_id": session_id,
            "timeline_id": timeline_id,
            "capture_profile_id": profile["profile_id"],
            "capture_profile_sha256": profile_sha,
            "campaign_id": "human-read-rich-2026-08",
            "worker_id": "human-001",
            "human_origin_attestation": attestation,
            "record_count": 1,
            "run_ids": ["run-0001"],
            "export_sha256": export_sha,
            "raw_file_sha256": raw_hashes,
            "audit": {
                "status": "pass",
                "valid_records": 1,
                "invalid_records": 0,
                "invalidations": 0,
            },
        }
        manifest = {
            "schema_version": 2,
            "schema": "sts2.human-annotator/session-bundle-2",
            "bundle_content_id": sha_bytes(canonical(identity).encode()),
            "session_id": session_id,
            "timeline_id": timeline_id,
            "capture_profile_id": profile["profile_id"],
            "capture_profile_sha256": profile_sha,
            "campaign_id": "human-read-rich-2026-08",
            "worker_id": "human-001",
            "human_origin_attestation": attestation,
            "record_count": 1,
            "run_ids": ["run-0001"],
            "export_sha256": export_sha,
            "audit_status": "pass",
            "content_identity": identity,
        }
        (bundle / "session-bundle-manifest.json").write_text(
            canonical(manifest) + "\n", encoding="utf-8"
        )
        lines = [
            f"{sha_file(path)}  {path.relative_to(bundle).as_posix()}"
            for path in sorted(bundle.rglob("*"))
            if path.is_file() and path.name != "checksums.sha256"
        ]
        (bundle / "checksums.sha256").write_text("\n".join(lines) + "\n", encoding="utf-8")
        return bundle

    def _refresh_checksums(self, bundle: Path) -> None:
        lines = [
            f"{sha_file(path)}  {path.relative_to(bundle).as_posix()}"
            for path in sorted(bundle.rglob("*"))
            if path.is_file() and path.name != "checksums.sha256"
        ]
        (bundle / "checksums.sha256").write_text("\n".join(lines) + "\n", encoding="utf-8")

    def test_v2_bundle_verifies_through_versioned_and_typed_paths(self) -> None:
        bundle = self._bundle("bundle-v2-blob")
        typed = HumanSessionBundleV2Verifier().verify(bundle)
        self.assertTrue(typed.passed, typed.findings)
        self.assertEqual(typed.require_value().materialized_reads, {"combat_piles": 2, "run_deck": 2})
        automatic = verify_human_session_bundle(bundle)
        self.assertTrue(automatic.passed, automatic.findings)
        self.assertIsInstance(automatic.require_value(), HumanSessionBundleV2)

    def test_v2_required_read_and_blob_tampering_fail_closed(self) -> None:
        bundle = self._bundle("bundle-v2-tampered-blob")
        export = bundle / "export" / "decisions.jsonl"
        record = json.loads(export.read_text(encoding="utf-8"))
        record["pre"]["reads"] = record["pre"]["reads"][:1]
        changed = canonical(record) + "\n"
        export.write_text(changed, encoding="utf-8")
        (bundle / "raw" / "run-0001.jsonl").write_text(changed, encoding="utf-8")
        # Preserve outer integrity to prove semantic Read admission catches the defect.
        self._rewrite_identity(bundle)
        result = verify_human_session_bundle(bundle)
        self.assertFalse(result.passed)
        self.assertEqual(result.findings[0].code, "required_read_missing")

        bundle = self._bundle()
        blob = next((bundle / "raw" / "blobs").rglob("*.json"))
        blob.write_text("{}\n", encoding="utf-8")
        self._refresh_checksums(bundle)
        result = verify_human_session_bundle(bundle)
        self.assertEqual(result.findings[0].code, "content_identity_facts_mismatch")

    def _rewrite_identity(self, bundle: Path) -> None:
        raw = bundle / "raw"
        export = bundle / "export" / "decisions.jsonl"
        manifest_path = bundle / "session-bundle-manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["export_sha256"] = sha_file(export)
        manifest["content_identity"]["export_sha256"] = manifest["export_sha256"]
        manifest["content_identity"]["raw_file_sha256"] = {
            path.relative_to(raw).as_posix(): sha_file(path)
            for path in sorted(raw.rglob("*"))
            if path.is_file()
        }
        manifest["bundle_content_id"] = sha_bytes(canonical(manifest["content_identity"]).encode())
        manifest_path.write_text(canonical(manifest) + "\n", encoding="utf-8")
        self._refresh_checksums(bundle)


if __name__ == "__main__":
    unittest.main()
