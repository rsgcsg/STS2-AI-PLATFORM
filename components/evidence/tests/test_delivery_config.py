from __future__ import annotations

import contextlib
import hashlib
import io
import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from sts2_platform_evidence.collection_tool import canonical, digest
from sts2_platform_evidence.delivery import DeliveryOutbox
from sts2_platform_evidence.delivery_cli import main
from sts2_platform_evidence.delivery_config import DeliveryConfig, doctor, inspect_outbox
from sts2_platform_evidence.transfer import _inventory


class DeliveryPreflightTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.recordings = self.root / "recordings"
        self.recordings.mkdir()
        self.tool = self.root / "tool"
        self.tool.mkdir()
        (self.tool / "platform-bom.json").write_text("{}")
        (self.tool / "sts2-human-annotator.dll").write_bytes(b"fixture")
        (self.tool / "sts2-human-annotator.runtimeconfig.json").write_text(json.dumps({
            "runtimeOptions": {"framework": {"name": "Microsoft.NETCore.App", "version": "9.0.0"}}}))
        identity = {"worktree": "clean", "source_revision": "a" * 40,
                    "entrypoint": "sts2-human-annotator.dll",
                    "supported_recording_schema": "sts2.human-annotator/recording-manifest-2",
                    "files": [{"path": p, "bytes": n, "sha256": h} for p, n, h in _inventory(self.tool)]}
        self.pin = digest(identity)
        (self.tool / "collection-tool.json").write_bytes(canonical({
            "schema": "sts2.evidence/collection-tool-1", "identity": identity, "release_id": self.pin}))
        self.config = self.root / "delivery.json"
        self.value = {"schema": "sts2.evidence/delivery-config-1",
                      "recordings_root": str(self.recordings), "outbox_root": str(self.root / "outbox"),
                      "tool_directory": str(self.tool), "tool_release_id": self.pin,
                      "worker_id": "worker", "campaign_id": "campaign", "human_origin_attested": True,
                      "hub_url": "https://hub.example", "allowed_upload_hosts": ["storage.example"]}
        self.write_config()
        self.environment = patch.dict(os.environ, {"STPD_HUB_TOKEN": "fixture-device-secret"})
        self.environment.start()
        self.addCleanup(self.environment.stop)
        self.runtime = patch("sts2_platform_evidence.delivery_config.subprocess.run", return_value=
                             subprocess.CompletedProcess([], 0,
                                 json.dumps({"sha256": hashlib.sha256(b"fixture").hexdigest()}), ""))
        self.runtime_mock = self.runtime.start()
        self.addCleanup(self.runtime.stop)

    def write_config(self) -> None:
        self.config.write_text(json.dumps(self.value))

    def test_preflight_and_status_never_enroll_historical_sessions_or_create_outbox(self) -> None:
        historical = self.recordings / "historical"
        historical.mkdir()
        (historical / "recording-manifest.json").write_text("{}")
        report = doctor(self.config)
        self.assertEqual(report["status"], "PASS")
        self.assertEqual(report["discovered_sessions"], 1)
        self.assertNotIn("fixture-device-secret", json.dumps(report))
        self.assertFalse((self.root / "outbox").exists())
        with contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(main(["status", "--config", str(self.config)]), 0)
        self.assertEqual(json.loads(output.getvalue())["sessions"], [])
        self.assertFalse((self.root / "outbox").exists())
        with contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(main(["status", "--summary", "--config", str(self.config), "--limit", "25"]), 0)
        summary = json.loads(output.getvalue())
        self.assertEqual(summary["schema"], "sts2.evidence/delivery-status-2")
        self.assertEqual(summary["sessions"], [])
        self.assertIsNone(summary["quality"]["real_failures"])
        self.assertFalse((self.root / "outbox").exists())

    def test_missing_token_blocks_run_before_outbox_or_upload(self) -> None:
        with patch.dict(os.environ, {"STPD_HUB_TOKEN": ""}):
            with contextlib.redirect_stdout(io.StringIO()) as output:
                self.assertEqual(main(["run", "--once", "--config", str(self.config)]), 1)
        self.assertEqual(json.loads(output.getvalue())["checks"]["credential"]["status"], "MISSING_OR_INVALID")
        self.assertFalse((self.root / "outbox").exists())

    def test_release_tampering_and_missing_runtime_block(self) -> None:
        self.runtime_mock.return_value = subprocess.CompletedProcess([], 1, "", "missing framework")
        self.assertEqual(doctor(self.config)["checks"]["dotnet_runtime"]["status"], "MISSING_OR_UNSUPPORTED")
        (self.tool / "sts2-human-annotator.dll").write_bytes(b"tampered")
        self.assertEqual(doctor(self.config)["checks"]["collection_release"]["status"], "MISSING_OR_MISMATCH")

    def test_wrong_release_pin_and_absent_root_block(self) -> None:
        self.value["tool_release_id"] = "f" * 64
        self.value["recordings_root"] = str(self.root / "absent")
        self.write_config()
        report = doctor(self.config)
        self.assertEqual(report["status"], "BLOCKED")
        self.assertEqual(report["checks"]["recordings_root"]["status"], "ABSENT")
        self.assertEqual(report["checks"]["collection_release"]["status"], "MISSING_OR_MISMATCH")

    def test_outbox_file_and_non_directory_ancestor_block_without_write_probe(self) -> None:
        blocker = self.root / "file"
        blocker.write_text("preserve")
        for destination in (blocker, blocker / "child"):
            with self.subTest(destination=destination):
                self.value["outbox_root"] = str(destination)
                self.write_config()
                report = doctor(self.config)
                self.assertEqual(report["status"], "BLOCKED")
                self.assertEqual(report["checks"]["outbox_destination"]["status"], "NOT_A_WRITABLE_DIRECTORY")
                self.assertEqual(blocker.read_text(), "preserve")

    def test_invalid_configuration_is_safe_and_never_initializes_outbox(self) -> None:
        for field, value in (("human_origin_attested", False), ("worker_id", 7),
                             ("tool_release_id", None), ("allowed_upload_hosts", []),
                             ("allowed_upload_hosts", ["https://storage.example"]),
                             ("poll_seconds", True), ("hub_url", "https://secret@hub.example"),
                             ("outbox_root", str(self.recordings / "nested"))):
            with self.subTest(field=field, value=value):
                original = self.value[field] if field in self.value else None
                self.value[field] = value
                self.write_config()
                report = doctor(self.config)
                self.assertEqual(report["status"], "BLOCKED")
                self.assertNotIn("secret", json.dumps(report))
                if original is None:
                    del self.value[field]
                else:
                    self.value[field] = original
        self.assertFalse((self.root / "outbox").exists())

    def test_readonly_existing_outbox_identity_check(self) -> None:
        config = DeliveryConfig.load(self.config)
        outbox = DeliveryOutbox(config.outbox_root, **config.identity)
        self.assertEqual(inspect_outbox(config), [])
        self.assertEqual(doctor(self.config)["status"], "PASS")
        self.value["campaign_id"] = "other"
        self.write_config()
        self.assertEqual(doctor(self.config)["checks"]["outbox_identity"]["status"], "MISMATCH_OR_UNREADABLE")
        self.assertEqual(outbox.status(), [])

    def test_cli_doctor_returns_machine_readable_failure(self) -> None:
        self.config.write_text("not JSON")
        with contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(main(["doctor", "--config", str(self.config)]), 1)
        self.assertEqual(json.loads(output.getvalue())["schema"], "sts2.evidence/delivery-doctor-1")
