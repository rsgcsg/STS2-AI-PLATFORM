from __future__ import annotations

import contextlib
import io
import json
import sqlite3
import threading
import unittest
from dataclasses import asdict
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

from sts2_platform_evidence import (AuthenticationBlocked, HubTransport, inspect_delivery_status,
                                    resume_auth, verify_human_session_bundle)
from sts2_platform_evidence.delivery_cli import main, process_lock
from sts2_platform_evidence.delivery_config import DeliveryConfig
from sts2_platform_evidence.transfer import _inventory
from tests import test_delivery as delivery


class _Hub:
    """Actual HTTP edges: credential failure can follow an accepted object PUT."""
    def __init__(self, *, block_at="complete", hub_status=401, storage_status=200):
        self.block_at, self.hub_status, self.storage_status = block_at, hub_status, storage_status
        self.authorized = False
        self.puts, self.intents, self.gets = 0, 0, 0
        self.manifest = None
        self.uploaded = False
        self.hub_auth, self.storage_auth = [], []
        owner = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *_): pass
            def reply(self, value, status=200):
                body = json.dumps(value).encode()
                self.send_response(status)
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            def blocked(self, at):
                owner.hub_auth.append(self.headers.get("Authorization"))
                if at == owner.block_at and not owner.authorized:
                    self.reply({"detail": "private diagnostic must not escape"}, owner.hub_status)
                    return True
                return False
            def receipt(self):
                manifest = owner.manifest
                return {"schema": "stpd/receive-receipt-v1", "receipt_id": "original-upload", "status": "verified",
                    "content_id": manifest.content_id, "manifest_sha256": manifest.manifest_sha256}
            def do_POST(self):
                body = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
                if self.path == "/v1/uploads":
                    owner.intents += 1
                    if self.blocked("intent"): return
                    self.reply({"upload_id": "original-upload", "status": "awaiting_upload",
                        "upload_url": f"http://127.0.0.1:{self.server.server_port}/object?signature=fixture-private",
                        "upload_method": "PUT", "upload_headers": {}})
                else:
                    if self.blocked("complete"): return
                    self.reply({"upload_id": "original-upload", "status": "verified", "receipt": self.receipt()})
            def do_PUT(self):
                self.rfile.read(int(self.headers["Content-Length"]))
                owner.puts += 1
                owner.storage_auth.append(self.headers.get("Authorization"))
                owner.uploaded = owner.storage_status == 200
                self.reply({}, owner.storage_status)
            def do_GET(self):
                owner.gets += 1
                if self.blocked("get"): return
                if owner.uploaded:
                    self.reply({"upload_id": "original-upload", "status": "verified", "receipt": self.receipt()})
                else:
                    self.reply({"upload_id": "original-upload", "status": "awaiting_upload"})
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()

    def close(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join()

    def transport(self, root, token="old-fixture-device"):
        edge = HubTransport(f"http://127.0.0.1:{self.server.server_port}", token, root / "archives",
            allowed_upload_hosts=["127.0.0.1"], allow_loopback_http=True)
        def send(bundle, manifest, metadata):
            self.manifest = manifest
            return edge(bundle, manifest, metadata)
        return send


class DeliveryRecoveryTests(unittest.TestCase):
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

    def hub(self, **kwargs):
        hub = _Hub(**kwargs)
        self.addCleanup(hub.close)
        return hub

    def block(self, *, hub_status=401):
        outbox, tool, source = self._setup_delivery()
        hub = self.hub(hub_status=hub_status)
        row = outbox.drain_one(tool, hub.transport(outbox.root), now=100)
        self.assertEqual(row["status"], "auth_blocked")
        return outbox, tool, source, hub, row

    def test_actual_complete_401_preserves_original_upload_and_explicit_resume_is_idempotent(self):
        outbox, tool, source, hub, row = self.block()
        config = self.config(outbox)
        before = {name: _inventory(outbox.root / name) for name in ("bundles", "archives", "transfers", "metadata")}
        raw = _inventory(source)
        self.assertEqual((row["attempts"], row["receipt"], row["error"]), (1, None, "hub_authentication_blocked"))
        self.assertIsNone(self._outbox().drain_one(tool, self.receive, now=1000000))
        view = inspect_delivery_status(config)
        self.assertEqual(view["counts"]["auth_blocked"], 1)
        self.assertEqual(view["sessions"][0]["stage"], "auth_blocked")
        self.assertEqual(view["sessions"][0]["upload_id"], "original-upload")
        self.assertEqual(view["sessions"][0]["error"], "hub_authentication_blocked")
        self.assertNotIn("fixture-device", json.dumps(view))
        self.assertNotIn(str(self.root), json.dumps(view))
        report = resume_auth(config, delivery_id=row["id"])
        self.assertEqual((report["resumed"], report["rejected"]), (1, 0))
        self.assertEqual(resume_auth(config, delivery_id=row["id"])["unchanged"], 1)
        self.assertEqual(resume_auth(config)["resumed"], 0)
        self.assertEqual({name: _inventory(outbox.root / name) for name in before}, before)
        pending = outbox.status()[0]
        self.assertEqual((pending["attempts"], pending["content_id"], pending["retry_at"]), (1, row["content_id"], 0))
        # A still-rejected replacement credential blocks again on the original
        # receipt GET; explicit resume does not claim successful authentication.
        hub.block_at = "get"
        blocked_again = self._outbox().drain_one(tool, hub.transport(outbox.root, "new-fixture-device"), now=101)
        self.assertEqual((blocked_again["status"], blocked_again["attempts"]), ("auth_blocked", 2))
        self.assertEqual(resume_auth(config)["resumed"], 1)
        hub.authorized = True
        final = self._outbox().drain_one(tool, hub.transport(outbox.root, "new-fixture-device"), now=101)
        self.assertEqual((final["status"], final["content_id"], final["attempts"]), ("verified", row["content_id"], 3))
        self.assertEqual((hub.intents, hub.puts, hub.gets), (1, 1, 2))
        self.assertEqual(hub.storage_auth, [None])
        self.assertIn("Bearer new-fixture-device", hub.hub_auth)
        self.assertEqual(json.loads(final["receipt"])["receipt_id"], "original-upload")
        receipts = _inventory(outbox.root / "receipts")
        self.assertEqual(resume_auth(config, delivery_id=row["id"])["unchanged"], 1)
        self.assertEqual(_inventory(outbox.root / "receipts"), receipts)
        self.assertEqual(_inventory(source), raw)
        self.assertEqual(tool.calls, 1)

    def test_hub_intent_403_is_recoverable_before_upload_identity_exists(self):
        outbox, tool, _, = self._setup_delivery()
        hub = self.hub(block_at="intent", hub_status=403)
        row = outbox.drain_one(tool, hub.transport(outbox.root))
        self.assertEqual((row["status"], hub.puts), ("auth_blocked", 0))
        self.assertIsNone(json.loads(row["auth_context"])["transport"]["upload_id"])
        self.assertEqual(resume_auth(self.config(outbox))["resumed"], 1)
        hub.authorized = True
        self.assertEqual(outbox.drain_one(tool, hub.transport(outbox.root))["status"], "verified")
        self.assertEqual(hub.puts, 1)

    def test_storage_403_is_an_incident_and_never_credential_reclassified(self):
        outbox, tool, _ = self._setup_delivery()
        hub = self.hub(block_at=None, storage_status=403)
        row = outbox.drain_one(tool, hub.transport(outbox.root))
        self.assertEqual((row["status"], row["error"], row["auth_context"]),
            ("incident", "Hub/storage rejected request (403)", None))
        self.assertEqual(resume_auth(self.config(outbox), delivery_id=row["id"])["unchanged"], 1)
        self.assertEqual(outbox.status()[0]["status"], "incident")
        self.assertIsNone(outbox.drain_one(tool, self.receive))
        self.assertEqual(hub.puts, 1)

    def test_resume_revalidates_seal_bundle_transfer_archive_and_upload_identity(self):
        outbox, _, source, _, row = self.block()
        context = json.loads(row["auth_context"])
        transfer = outbox.root / "transfers" / f"{row['id']}.json"
        attempt = outbox.root / "archives" / f"{context['manifest_sha256']}.upload.json"
        archive = outbox.root / "archives" / f"{context['manifest_sha256']}.tar.gz"
        bundle = outbox.root / "bundles" / row["id"]
        targets = [source / "session-close-receipt.json", bundle / "raw/recording-manifest.json", transfer, archive, attempt]
        for path in targets:
            with self.subTest(path=path.name):
                original = path.read_bytes()
                try:
                    path.write_bytes(b"tampered")
                    report = resume_auth(self.config(outbox))
                    self.assertEqual((report["resumed"], report["rejected"]), (0, 1))
                    self.assertNotIn(str(self.root), json.dumps(report))
                    self.assertEqual(outbox.status()[0]["status"], "auth_blocked")
                finally:
                    path.write_bytes(original)
        original = attempt.read_bytes()
        attempt.unlink()
        self.assertEqual(resume_auth(self.config(outbox))["rejected"], 1)
        attempt.write_bytes(original)
        self.assertEqual(resume_auth(self.config(outbox))["resumed"], 1)

    def test_resealed_different_bundle_is_rejected_against_original_prepared_identity(self):
        outbox, tool, _, _, row = self.block()
        self.assertEqual(resume_auth(self.config(outbox))["resumed"], 1)
        bundle = outbox.root / "bundles" / row["id"]
        # Validly resealed bytes still cannot replace a previously prepared transfer.
        (bundle / "extra.json").write_text("{}")
        self._reseal(bundle)
        self.assertTrue(verify_human_session_bundle(bundle).passed)
        final = outbox.drain_one(tool, lambda *_: self.fail("changed identity reached transport"))
        self.assertEqual(final["status"], "incident")
        self.assertEqual(final["content_id"], row["content_id"])

    def test_stopped_worker_lock_database_writer_and_fixed_campaign_are_enforced(self):
        outbox, _, _, _, row = self.block()
        config = self.config(outbox)
        with process_lock(outbox.root):
            with self.assertRaisesRegex(ValueError, "already owns"):
                resume_auth(config)
        db = sqlite3.connect(outbox.root / "outbox.sqlite3")
        db.execute("BEGIN IMMEDIATE")
        try:
            with self.assertRaises(sqlite3.OperationalError):
                resume_auth(config)
        finally:
            db.close()
        changed = DeliveryConfig(**(asdict(config) | {"campaign_id": "other"}))
        with self.assertRaisesRegex(ValueError, "immutable"):
            resume_auth(changed)
        self.assertEqual(outbox.status()[0]["status"], "auth_blocked")
        self.assertEqual(resume_auth(config, delivery_id=row["id"])["resumed"], 1)

    def test_cli_resume_and_legacy_incident_no_migration(self):
        outbox, _, _, _, row = self.block()
        config = self.config(outbox)
        value = asdict(config) | {"schema": "sts2.evidence/delivery-config-1"}
        path = self.root / "config.json"
        path.write_text(json.dumps(value, default=str))
        with contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(main(["resume-auth", "--config", str(path), "--delivery-id", row["id"]]), 0)
        self.assertEqual(json.loads(output.getvalue())["resumed"], 1)
        with outbox._db() as db:
            db.execute("UPDATE sessions SET status='incident',error='Hub/storage rejected request (401)',auth_context=NULL")
        before = outbox.status()
        self.assertEqual(resume_auth(config)["resumed"], 0)
        self.assertEqual(resume_auth(config, delivery_id=row["id"])["unchanged"], 1)
        self.assertEqual(outbox.status(), before)

    def test_missing_outbox_invalid_id_and_unproven_auth_context_fail_closed(self):
        outbox = self._outbox()
        config = self.config(outbox)
        # No initialization through public recovery when nothing was enrolled.
        empty = DeliveryConfig(**(asdict(config) | {"outbox_root": self.root / "absent"}))
        self.assertEqual(resume_auth(empty)["resumed"], 0)
        self.assertFalse(empty.outbox_root.exists())
        with self.assertRaisesRegex(ValueError, "invalid_delivery_id"):
            resume_auth(config, delivery_id="../unsafe")
        outbox, tool, _ = self._setup_delivery()
        def unproven(*_):
            raise AuthenticationBlocked(401)
        row = outbox.drain_one(tool, unproven)
        self.assertEqual(row["status"], "auth_blocked")
        report = resume_auth(self.config(outbox))
        self.assertEqual(report["sessions"][0]["error"], "recovery_identity_unavailable")
        self.assertEqual(outbox.status()[0]["status"], "auth_blocked")


if __name__ == "__main__":
    unittest.main()
