from __future__ import annotations

import io
import json
import tarfile
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

from sts2_platform_evidence.delivery import ReceiverVerificationPending
from sts2_platform_evidence.delivery_http import HubTransport
from sts2_platform_evidence.transfer import DirectoryTransferManifest


class HubTransportTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.bundle = self.root / "bundle"
        self.bundle.mkdir()
        (self.bundle / "evidence.json").write_text('{"fixture":true}')
        self.transfer = DirectoryTransferManifest.from_directory(self.bundle, content_id="a" * 64,
                                                                  artifact_type="human-session-bundle")

    def tearDown(self) -> None:
        self.temp.cleanup()

    def test_pending_receipt_restart_does_not_reupload_or_leak_hub_token(self) -> None:
        facts = {"puts": 0, "ready": False}
        transfer = self.transfer
        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *_): pass
            def reply(self, value):
                payload = json.dumps(value).encode()
                self.send_response(200)
                self.send_header("Content-Length", str(len(payload)))
                self.end_headers()
                self.wfile.write(payload)
            def do_POST(self):
                body = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
                facts.setdefault("hub_auth", []).append(self.headers.get("Authorization"))
                if self.path == "/v1/uploads":
                    facts["intent"] = body
                    self.reply({"upload_id": "upload1", "upload_method": "PUT", "upload_headers": {},
                                "upload_url": f"http://127.0.0.1:{self.server.server_port}/object", "status": "awaiting_upload"})
                else:
                    self.reply({"upload_id": "upload1", "status": "verification_pending"})
            def do_PUT(self):
                facts["puts"] += 1
                facts["storage_auth"] = self.headers.get("Authorization")
                facts["archive"] = self.rfile.read(int(self.headers["Content-Length"]))
                self.reply({})
            def do_GET(self):
                if not facts["ready"]:
                    self.reply({"upload_id": "upload1", "status": "verification_pending", "receipt": None})
                    return
                self.reply({"upload_id": "upload1", "status": "verified", "receipt": {
                    "receipt_id": "upload1", "status": "verified", "content_id": transfer.content_id,
                    "manifest_sha256": transfer.manifest_sha256}})
        server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            def transport():
                return HubTransport(f"http://127.0.0.1:{server.server_port}", "fixture-secret", self.root / "cache",
                                    allowed_upload_hosts=["127.0.0.1"], allow_loopback_http=True)
            with self.assertRaises(ReceiverVerificationPending):
                transport()(self.bundle, transfer, {"tool_release_id": "b" * 64})
            with self.assertRaises(ReceiverVerificationPending):
                transport()(self.bundle, transfer, {})
            facts["ready"] = True
            self.assertEqual(transport()(self.bundle, transfer, {})["status"], "verified")
            self.assertEqual(facts["puts"], 1)
            self.assertIsNone(facts["storage_auth"])
            self.assertEqual(facts["hub_auth"], ["Bearer fixture-secret", "Bearer fixture-secret"])
            with tarfile.open(fileobj=io.BytesIO(facts["archive"]), mode="r:gz") as archive:
                self.assertEqual(archive.getnames(), ["evidence.json"])
                self.assertEqual(archive.extractfile("evidence.json").read(), (self.bundle / "evidence.json").read_bytes())
        finally:
            server.shutdown()
            server.server_close()
            thread.join()

    def test_url_allowlist_and_https_are_required(self) -> None:
        with self.assertRaisesRegex(ValueError, "HTTPS"):
            HubTransport("http://example.com", "secret", self.root / "cache", allowed_upload_hosts=[])
        transport = HubTransport("https://hub.example.com", "secret", self.root / "cache",
                                 allowed_upload_hosts=["storage.example.com"])
        for url in ("https://evil.example.com/object", "https://user:pass@storage.example.com/object",
                    "http://storage.example.com/object"):
            with self.assertRaises(ValueError):
                transport._validate_url(url, is_upload=True)

    def test_archive_is_deterministic_and_changed_bundle_is_rejected(self) -> None:
        transport = HubTransport("https://hub.example.com", "secret", self.root / "cache", allowed_upload_hosts=[])
        first = transport._archive(self.bundle, self.transfer).read_bytes()
        second = HubTransport("https://hub.example.com", "secret", self.root / "other", allowed_upload_hosts=[])
        self.assertEqual(second._archive(self.bundle, self.transfer).read_bytes(), first)
        (self.bundle / "evidence.json").write_text("tampered")
        third = HubTransport("https://hub.example.com", "secret", self.root / "third", allowed_upload_hosts=[])
        with self.assertRaisesRegex(ValueError, "changed"):
            third._archive(self.bundle, self.transfer)

    def test_receiver_exhausted_transport_failure_is_not_retried_automatically(self) -> None:
        transport = HubTransport("https://hub.example.com", "secret", self.root / "cache", allowed_upload_hosts=[])
        transport._json = lambda *_: {"status": "transfer_failed", "upload_id": "upload1"}
        with self.assertRaisesRegex(ValueError, "operator retry"):
            transport(self.bundle, self.transfer, {})

    def test_fresh_client_recovers_existing_terminal_receipt_without_put(self) -> None:
        transport = HubTransport("https://hub.example.com", "secret", self.root / "cache", allowed_upload_hosts=[])
        calls = []
        def request(method, path, *_):
            calls.append((method, path))
            if method == "POST":
                return {"status": "verified", "upload_id": "upload1"}
            return {"status": "verified", "upload_id": "upload1", "receipt": {
                "status": "verified", "content_id": self.transfer.content_id}}
        transport._json = request
        self.assertEqual(transport(self.bundle, self.transfer, {})["status"], "verified")
        self.assertEqual(calls, [("POST", "/v1/uploads"), ("GET", "/v1/uploads/upload1")])

    def test_unknown_receiver_state_does_not_authorize_put_or_invent_pending(self) -> None:
        for status in ("unexpected", "pending", "verifying", "verified"):
            with self.subTest(status=status):
                with self.assertRaises(ValueError):
                    HubTransport._disposition({"status": status}, allow_upload=True)


if __name__ == "__main__":
    unittest.main()
