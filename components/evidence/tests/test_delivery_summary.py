from __future__ import annotations

import json
import shutil
import unittest
from unittest.mock import patch

from sts2_platform_evidence import inspect_delivery_status
from sts2_platform_evidence.delivery import ReceiverVerificationPending
from sts2_platform_evidence.delivery_config import DeliveryConfig, inspect_outbox
from sts2_platform_evidence.delivery_summary import _update_projection
from sts2_platform_evidence.transfer import _inventory
from tests import test_delivery as delivery


class DeliverySummaryTests(unittest.TestCase):
    setUp = delivery.DeliveryTests.setUp
    tearDown = delivery.DeliveryTests.tearDown
    _write = delivery.DeliveryTests._write
    _stream = delivery.DeliveryTests._stream
    _rows = delivery.DeliveryTests._rows
    _bundle = delivery.DeliveryTests._bundle
    _object = delivery.DeliveryTests._object
    _reseal = delivery.DeliveryTests._reseal
    _setup_delivery = delivery.DeliveryTests._setup_delivery
    _outbox = delivery.DeliveryTests._outbox
    receive = staticmethod(delivery.DeliveryTests.receive)

    def config(self, outbox):
        return DeliveryConfig(self.root / "recordings", outbox.root, self.root / "tool", "a" * 64,
            "worker", "campaign", True, "https://hub.example", ["storage.example"])

    def test_pending_upload_is_visible_and_safe_without_raw_reads_or_verification(self):
        outbox, tool, _ = self._setup_delivery()
        config = self.config(outbox)
        def pending(bundle, manifest, _):
            path = outbox.root / "archives" / f"{manifest.manifest_sha256}.upload.json"
            path.parent.mkdir()
            self._write(path, {"upload_id": "exact-upload", "archive_sha256": "f" * 64,
                "status": "verification_pending", "observed_at": "2026-09-13T00:00:01Z", "archive_bytes": 123})
            raise ReceiverVerificationPending()
        outbox.drain_one(tool, pending, now=100)
        with patch("sts2_platform_evidence.delivery.verify_human_session_bundle", side_effect=AssertionError("verify on status")), \
             patch("sts2_platform_evidence.transfer._inventory", side_effect=AssertionError("inventory on status")):
            status = inspect_delivery_status(config)
        row = status["sessions"][0]
        self.assertEqual((row["status"], row["stage"], row["upload_id"]), ("pending", "verification_pending", "exact-upload"))
        self.assertEqual((row["attempts"], row["error"], row["receipt"]), (1, None, None))
        self.assertEqual(row["summary"]["counts"]["canonical"], 1)
        self.assertEqual(row["archive_bytes"], 123)
        self.assertNotIn(str(self.root), json.dumps(status))
        self.assertNotIn("source", row)
        self.assertEqual(inspect_delivery_status(config, delivery_id=row["id"])["sessions"], [row])

    def test_terminal_receipt_parsed_and_findings_redacted_not_failure_counts(self):
        outbox, tool, _ = self._setup_delivery()
        def quarantined(bundle, manifest, metadata):
            return self.receive(bundle, manifest, metadata) | {"status": "quarantined", "findings": [
                {"code": "bad_fixture", "detail": "private/path?token=secret"}], "private_extension": "secret"}
        outbox.drain_one(tool, quarantined)
        status = inspect_delivery_status(self.config(outbox))
        row = status["sessions"][0]
        self.assertEqual(row["stage"], "quarantined")
        self.assertEqual(row["receipt"]["findings"], [{"code": "bad_fixture"}])
        self.assertNotIn("secret", json.dumps(status))
        # Generic receipt identity is not guessed to be the HTTP upload ID.
        self.assertIsNone(row["upload_id"])

    def test_old_outbox_remains_readonly_then_controlled_rebuild_preserves_receipt_and_raw(self):
        outbox, tool, source = self._setup_delivery()
        outbox.drain_one(tool, self.receive)
        original = _inventory(source)
        original_state = inspect_outbox(self.config(outbox))
        receipts = _inventory(outbox.root / "receipts")
        shutil.rmtree(outbox.root / "projections")
        # Exact pre-projection schema: status must not migrate during reads.
        with outbox._db() as db:
            db.execute("ALTER TABLE sessions RENAME TO sessions_with_summary")
            db.execute("CREATE TABLE sessions AS SELECT id,source,identity,status,attempts,retry_at,content_id,error,receipt FROM sessions_with_summary")
            db.execute("DROP TABLE sessions_with_summary")
        old = inspect_delivery_status(self.config(outbox))["sessions"][0]
        self.assertIsNone(old["summary"])
        self.assertIsNone(old["session_id"])
        self.assertIsNone(old["observed_at"])
        self.assertEqual(old["status"], "verified")
        quality = inspect_delivery_status(self.config(outbox))["quality"]
        self.assertEqual((quality["summaries_missing"], quality["canonical"], quality["real_failures"]), (1, None, None))
        next((outbox.root / "transfers").glob("*.json")).write_text("malformed optional linkage")
        outbox = self._outbox()
        self.assertEqual(outbox.rebuild_summaries()["rebuilt"], 1)
        rebuilt = inspect_delivery_status(self.config(outbox))["sessions"][0]
        self.assertEqual(rebuilt["summary"]["counts"]["canonical"], 1)
        self.assertIsNone(rebuilt["enrolled_at"])
        self.assertEqual(inspect_outbox(self.config(outbox)), original_state)
        self.assertEqual(_inventory(source), original)
        self.assertEqual(_inventory(outbox.root / "receipts"), receipts)

    def test_bounded_pagination_includes_more_than_100_rows_and_exact_id(self):
        outbox, _, _ = self._setup_delivery()
        with outbox._db() as db:
            for index in range(124):
                db.execute("INSERT INTO sessions(id,source,identity,status) VALUES(?,?,?,?)",
                    (f"{index:064x}", "/private/source", "{}", "pending"))
        config = self.config(outbox)
        first = inspect_delivery_status(config, limit=100)
        second = inspect_delivery_status(config, limit=100, offset=100)
        self.assertEqual((first["total"], len(first["sessions"]), first["next_offset"]), (125, 100, 100))
        self.assertEqual((len(second["sessions"]), second["next_offset"]), (25, None))
        self.assertFalse({row["id"] for row in first["sessions"]} & {row["id"] for row in second["sessions"]})
        self.assertEqual(first["counts"]["pending"], 125)
        selected = inspect_delivery_status(config, delivery_id=f"{10:064x}")
        self.assertEqual(selected["sessions"][0]["id"], f"{10:064x}")
        for kwargs in ({"limit": 101}, {"limit": True}, {"offset": -1}, {"delivery_id": "../private"}):
            with self.assertRaises(ValueError):
                inspect_delivery_status(config, **kwargs)

    def test_missing_outbox_does_not_initialize_and_projection_io_cannot_adjudicate_delivery(self):
        outbox = self._outbox()
        shutil.rmtree(outbox.root)
        self.assertEqual(inspect_delivery_status(self.config(outbox))["total"], 0)
        self.assertFalse(outbox.root.exists())
        outbox, tool, _ = self._setup_delivery()
        real_atomic = __import__("sts2_platform_evidence.delivery", fromlist=["_atomic_json"])._atomic_json
        def fail_projection(path, value):
            if path.parent.name == "projections":
                raise OSError("projection disk failure")
            return real_atomic(path, value)
        with patch("sts2_platform_evidence.delivery._atomic_json", side_effect=fail_projection):
            self.assertFalse(_update_projection(outbox.root, "f" * 64, stage="queued"))
            self.assertEqual(outbox.drain_one(tool, self.receive)["status"], "verified")
        row = inspect_delivery_status(self.config(outbox))["sessions"][0]
        self.assertEqual(row["summary_status"], "available")
        self.assertEqual(row["status"], "verified")

    def test_quality_is_global_and_explicitly_partial_for_missing_or_historical_dispositions(self):
        outbox, tool, _ = self._setup_delivery()
        outbox.drain_one(tool, self.receive)
        with outbox._db() as db:
            db.execute("INSERT INTO sessions(id,source,identity,status) VALUES(?,?,?,?)",
                       ("b" * 64, "/private", "{}", "pending"))
        result = inspect_delivery_status(self.config(outbox), limit=1)
        self.assertIsNone(result["sessions"][0]["summary"])
        self.assertEqual(result["quality"], {"summaries_available": 1, "summaries_missing": 1,
            "canonical": 1, "real_failures": None, "partial": True})


if __name__ == "__main__":
    unittest.main()
