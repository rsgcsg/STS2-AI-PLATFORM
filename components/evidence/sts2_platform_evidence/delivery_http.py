"""Bounded HTTP edge adapter for the Hub upload-intent/receipt protocol.

The Hub owns verification receipts; it owns no gameplay authority. Presigned
object PUTs never carry Hub credentials and redirects are never followed.
"""

from __future__ import annotations

import gzip
import json
import os
import tarfile
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any

from .collection_tool import canonical, read_json
from .delivery import _atomic_json
from .transfer import DirectoryTransferManifest, _sha256_file


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req: object, fp: object, code: int, msg: str,
                         headers: object, newurl: str) -> None:
        return None


class HubTransport:
    def __init__(self, hub_url: str, token: str, cache_root: str | Path, *,
                 allowed_upload_hosts: list[str], allow_loopback_http: bool = False,
                 timeout: float = 60, max_archive_bytes: int = 2 * 1024**3) -> None:
        self.hub_url = hub_url.rstrip("/")
        self.token = token
        self.cache = Path(cache_root).absolute()
        self.cache.mkdir(parents=True, exist_ok=True)
        self.allowed_upload_hosts = frozenset(allowed_upload_hosts)
        self.allow_loopback_http = allow_loopback_http
        self.timeout = timeout
        self.max_archive_bytes = max_archive_bytes
        self.opener = urllib.request.build_opener(_NoRedirect())
        self._validate_url(self.hub_url, is_upload=False)
        if not token or "\n" in token or "\r" in token:
            raise ValueError("Hub device token is missing or invalid")

    def _validate_url(self, url: str, *, is_upload: bool) -> None:
        parsed = urllib.parse.urlsplit(url)
        loopback = parsed.hostname in {"127.0.0.1", "localhost", "::1"}
        if (parsed.scheme != "https" and not (self.allow_loopback_http and loopback and parsed.scheme == "http")
                or not parsed.hostname or parsed.username or parsed.password or parsed.fragment):
            raise ValueError("HTTPS endpoint required; loopback HTTP is explicit test-only")
        if is_upload and parsed.hostname not in self.allowed_upload_hosts:
            raise ValueError("upload URL host is not explicitly trusted")

    def _request(self, request: urllib.request.Request, *, json_response: bool = True) -> dict[str, Any]:
        try:
            with self.opener.open(request, timeout=self.timeout) as response:
                if not json_response:
                    response.read(1024)
                    return {}
                body = response.read(1024 * 1024 + 1)
                if len(body) > 1024 * 1024:
                    raise ValueError("Hub response exceeds limit")
                result = json.loads(body)
                if not isinstance(result, dict):
                    raise ValueError("Hub response must be an object")
                return result
        except urllib.error.HTTPError as error:
            # Never persist exception repr: it may contain signed URL credentials.
            if error.code >= 500 or error.code in {408, 429}:
                raise OSError("retryable Hub/storage response") from None
            raise ValueError(f"Hub/storage rejected request ({error.code})") from None
        except urllib.error.URLError:
            raise OSError("Hub/storage unavailable") from None

    def _json(self, method: str, path: str, value: object | None = None) -> dict[str, Any]:
        return self._request(urllib.request.Request(
            self.hub_url + path, data=None if value is None else canonical(value), method=method,
            headers={"Authorization": f"Bearer {self.token}", "Content-Type": "application/json"}))

    def _archive(self, bundle: Path, transfer: DirectoryTransferManifest) -> Path:
        archive = self.cache / f"{transfer.manifest_sha256}.tar.gz"
        if archive.exists():
            return archive
        temporary = archive.with_suffix(".partial")
        total = sum(item.bytes for item in transfer.files)
        if total > self.max_archive_bytes or len(transfer.files) > 100000:
            raise ValueError("transfer exceeds configured delivery limits")
        with temporary.open("wb") as output:
            with gzip.GzipFile(filename="", fileobj=output, mode="wb", mtime=0) as compressed:
                with tarfile.open(fileobj=compressed, mode="w|") as tar:
                    for item in transfer.files:
                        source = bundle / item.path
                        if source.is_symlink() or _sha256_file(source) != item.sha256:
                            raise ValueError("bundle changed before archive publication")
                        info = tarfile.TarInfo(item.path)
                        info.size, info.mode, info.mtime = item.bytes, 0o644, 0
                        with source.open("rb") as stream:
                            tar.addfile(info, stream)
            output.flush()
            os.fsync(output.fileno())
        if temporary.stat().st_size > self.max_archive_bytes:
            raise ValueError("archive exceeds configured delivery limit")
        os.replace(temporary, archive)
        return archive

    def __call__(self, bundle: Path, transfer: DirectoryTransferManifest,
                 metadata: dict[str, Any]) -> dict[str, Any]:
        archive = self._archive(bundle, transfer)
        archive_sha = _sha256_file(archive)
        attempt_file = self.cache / f"{transfer.manifest_sha256}.upload.json"
        if attempt_file.exists():
            previous = read_json(attempt_file)
            if previous.get("archive_sha256") != archive_sha:
                raise ValueError("persisted upload archive changed")
            upload_id = previous["upload_id"]
            status = self._json("GET", f"/v1/uploads/{upload_id}")
            if status.get("receipt"):
                return status["receipt"]
            if status.get("status") in {"pending", "verifying"}:
                raise TimeoutError("cloud verification pending")
        # Re-request by content/manifest identity to refresh an expired presigned URL.
        intent = self._json("POST", "/v1/uploads", {
            "schema": "stpd/upload-intent-v1", "transfer_manifest": transfer.to_dict(),
            "archive_sha256": archive_sha, "archive_bytes": archive.stat().st_size,
            "delivery_metadata": metadata,
        })
        upload_id = intent.get("upload_id")
        if not isinstance(upload_id, str) or not upload_id or any(c not in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_" for c in upload_id):
            raise ValueError("invalid Hub upload ID")
        _atomic_json(attempt_file, {"upload_id": upload_id, "archive_sha256": archive_sha})
        if intent.get("receipt"):
            return intent["receipt"]
        url = intent.get("upload_url")
        if not isinstance(url, str) or intent.get("upload_method") != "PUT":
            raise ValueError("unsupported upload intent")
        self._validate_url(url, is_upload=True)
        headers = intent.get("upload_headers", {})
        if not isinstance(headers, dict) or any(k.lower() in {"authorization", "cookie", "host", "content-length", "transfer-encoding"} for k in headers):
            raise ValueError("upload intent contains forbidden headers")
        with archive.open("rb") as stream:
            self._request(urllib.request.Request(url, data=stream, method="PUT",
                headers={**headers, "Content-Length": str(archive.stat().st_size)}), json_response=False)
        status = self._json("POST", f"/v1/uploads/{upload_id}/complete", {})
        if status.get("receipt"):
            return status["receipt"]
        raise TimeoutError("cloud verification pending")
