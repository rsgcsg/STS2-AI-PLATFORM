from __future__ import annotations

import json
import shutil
import sqlite3
import unittest
from pathlib import Path

from sts2_platform_evidence.collection_tool import CollectionTool, canonical, digest
from sts2_platform_evidence.delivery import DeliveryOutbox
from sts2_platform_evidence.delivery_cli import process_lock
from sts2_platform_evidence.transfer import _inventory
from tests import test_human_session_bundle_v3 as v3


class FixturePacker:
    release_id = "a" * 64
    manifest = {"fixture": True}

    def __init__(self, bundle: Path) -> None:
        self.bundle = bundle
        self.calls = 0

    def pack(self, session: Path, output: Path, worker: str, campaign: str) -> None:
        self.calls += 1
        if not output.exists():
            shutil.copytree(self.bundle, output)

    def verify(self):
        return self.manifest


class DeliveryTests(unittest.TestCase):
    # Reuse faithful V3 evidence builders without inheriting unrelated tests.
    setUp = v3.HumanSessionBundleV3Tests.setUp
    tearDown = v3.HumanSessionBundleV3Tests.tearDown
    _write = v3.HumanSessionBundleV3Tests._write
    _stream = v3.HumanSessionBundleV3Tests._stream
    _rows = v3.HumanSessionBundleV3Tests._rows
    _bundle = v3.HumanSessionBundleV3Tests._bundle
    _object = v3.HumanSessionBundleV3Tests._object
    _reseal = v3.HumanSessionBundleV3Tests._reseal
    def _setup_delivery(self, *, failed_only: bool = False) -> tuple[DeliveryOutbox, FixturePacker, Path]:
        bundle = self._bundle(failed_only=failed_only)
        manifest = json.loads((bundle / "raw/recording-manifest.json").read_text())
        manifest["close_schema_version"] = 1
        self._write(bundle / "raw/recording-manifest.json", manifest)
        self._write(bundle / "raw/session-close-receipt.json", {
            "schema": "sts2.human-annotator/session-close-1", "status": "closed",
            "session_id": manifest["session_id"], "timeline_id": manifest["timeline_id"],
            "closed_at": "2026-09-11T00:00:00Z"})
        self._reseal(bundle)
        recordings = self.root / "recordings"
        source = recordings / "session"
        shutil.copytree(bundle / "raw", source)
        outbox = self._outbox()
        self.assertEqual(outbox.reconcile(recordings)["enqueued"], 1)
        return outbox, FixturePacker(bundle), source

    def _outbox(self) -> DeliveryOutbox:
        return DeliveryOutbox(self.root / "outbox", worker_id="worker", campaign_id="campaign",
                              human_origin_attested=True, tool_release_id="a" * 64)

    @staticmethod
    def receive(bundle, manifest, metadata):
        return {"schema": "stpd/receive-receipt-v1", "receipt_id": "receipt1", "status": "verified",
                "content_id": manifest.content_id, "manifest_sha256": manifest.manifest_sha256}

    def test_restart_after_offline_keeps_raw_and_delivers_once(self) -> None:
        outbox, tool, source = self._setup_delivery()
        original = _inventory(source)
        def offline(*_):
            raise OSError("offline")
        self.assertEqual(outbox.drain_one(tool, offline, now=100)["status"], "pending")
        restarted = self._outbox()
        self.assertIsNone(restarted.drain_one(tool, self.receive, now=101))
        self.assertEqual(restarted.drain_one(tool, self.receive, now=103)["status"], "verified")
        self.assertEqual(restarted.reconcile(source.parent)["existing"], 1)
        self.assertIsNone(restarted.drain_one(tool, self.receive, now=200))
        self.assertEqual(_inventory(source), original)

    def test_process_crash_after_remote_receive_replays_same_immutable_content(self) -> None:
        outbox, tool, _ = self._setup_delivery()
        content = []
        class Crash(BaseException): pass
        def crash(bundle, manifest, metadata):
            content.append(manifest.manifest_sha256)
            raise Crash()
        with self.assertRaises(Crash):
            outbox.drain_one(tool, crash)
        def complete(bundle, manifest, metadata):
            content.append(manifest.manifest_sha256)
            return self.receive(bundle, manifest, metadata)
        self.assertEqual(self._outbox().drain_one(tool, complete)["status"], "verified")
        self.assertEqual(content[0], content[1])

    def test_unsealed_never_becomes_verified_by_waiting(self) -> None:
        outbox, _, source = self._setup_delivery()
        other = source.parent / "unsealed"
        shutil.copytree(source, other)
        (other / "session-close-receipt.json").unlink()
        self.assertEqual(outbox.reconcile(source.parent)["unsealed"], 1)
        self.assertEqual(len(outbox.status()), 1)

    def test_wrong_close_identity_and_changed_raw_are_incidents(self) -> None:
        outbox, tool, source = self._setup_delivery()
        (source / "extra.json").write_text("{}")
        self.assertEqual(outbox.drain_one(tool, self.receive)["status"], "incident")
        self.assertEqual(tool.calls, 0)
        other = source.parent / "bad-close"
        shutil.copytree(source, other)
        self._write(other / "session-close-receipt.json", {"status": "closed"})
        self.assertEqual(outbox.reconcile(source.parent)["incident"], 1)

    def test_failed_only_closed_session_delivered_without_erasing_failure(self) -> None:
        outbox, tool, source = self._setup_delivery(failed_only=True)
        self.assertEqual(outbox.drain_one(tool, self.receive)["status"], "verified")
        self.assertEqual((source / "canonical-transitions.jsonl").read_text(), "")
        self.assertIn("transition_unknown", (source / "semantic-boundary-trace.jsonl").read_text())

    def test_bad_bundle_and_mismatched_remote_receipt_never_report_verified(self) -> None:
        outbox, tool, _ = self._setup_delivery()
        def wrong(*_):
            return {"status": "verified", "content_id": "f" * 64}
        self.assertEqual(outbox.drain_one(tool, wrong)["status"], "incident")

    def test_write_lock_prevents_two_live_workers_and_config_cannot_drift(self) -> None:
        outbox, tool, _ = self._setup_delivery()
        connection = sqlite3.connect(outbox.root / "outbox.sqlite3")
        connection.execute("BEGIN IMMEDIATE")
        try:
            self.assertEqual(len(self._outbox().status()), 1)
            with self.assertRaises(sqlite3.OperationalError):
                outbox.drain_one(tool, self.receive)
        finally:
            connection.close()
        with self.assertRaisesRegex(ValueError, "immutable"):
            DeliveryOutbox(outbox.root, worker_id="other", campaign_id="campaign",
                           human_origin_attested=True, tool_release_id="a" * 64)

    def test_operator_attestation_is_not_inferred(self) -> None:
        with self.assertRaisesRegex(ValueError, "attestation"):
            DeliveryOutbox(self.root / "outbox", worker_id="worker", campaign_id="campaign",
                           human_origin_attested=False, tool_release_id="a" * 64)

    def test_os_lifetime_lock_survives_idle_and_releases_after_exit(self) -> None:
        with process_lock(self.root):
            with self.assertRaisesRegex(ValueError, "already owns"):
                with process_lock(self.root):
                    self.fail("second worker acquired lock")
        with process_lock(self.root):
            pass


class CollectionToolTests(unittest.TestCase):
    def test_release_requires_external_pin_and_rejects_changed_dependency_or_extra_file(self) -> None:
        import tempfile
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "sts2-human-annotator.dll").write_bytes(b"fixture")
            (root / "platform-bom.json").write_text("{}")
            identity = {"worktree": "clean", "source_revision": "c" * 40,
                        "entrypoint": "sts2-human-annotator.dll",
                        "supported_recording_schema": "sts2.human-annotator/recording-manifest-2",
                        "files": [{"path": p, "bytes": n, "sha256": h} for p,n,h in _inventory(root)]}
            release_id = digest(identity)
            (root / "collection-tool.json").write_bytes(canonical({"schema": "sts2.evidence/collection-tool-1",
                "release_id": release_id, "identity": identity}))
            CollectionTool(root, release_id)
            with self.assertRaisesRegex(ValueError, "trusted"):
                CollectionTool(root, "f" * 64)
            (root / "sts2-human-annotator.dll").write_bytes(b"changed")
            with self.assertRaisesRegex(ValueError, "bytes"):
                CollectionTool(root, release_id)
            (root / "sts2-human-annotator.dll").write_bytes(b"fixture")
            (root / "extra.dll").write_bytes(b"extra")
            with self.assertRaisesRegex(ValueError, "bytes"):
                CollectionTool(root, release_id)
