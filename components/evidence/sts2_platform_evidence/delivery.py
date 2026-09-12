"""Restart-safe delivery of sealed Human sessions; no game-state authority.

SQLite serializes a single local writer during each attempt. A process crash
releases that lock; immutable pack output and idempotent content delivery make
the repeated attempt safe. No lease clock can permit two local writers.
"""

from __future__ import annotations

import json
import os
import re
import sqlite3
import time
from contextlib import contextmanager
from collections.abc import Iterator
from pathlib import Path
from typing import Any, Protocol

from .collection_tool import CollectionFailure, CollectionTool, canonical, digest, read_json
from .human_session_bundle import verify_human_session_bundle
from .transfer import DirectoryTransferManifest, _inventory


class Transport(Protocol):
    def __call__(self, bundle: Path, manifest: DirectoryTransferManifest,
                 metadata: dict[str, Any]) -> dict[str, Any]: ...


def _atomic_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".partial")
    with temporary.open("wb") as stream:
        stream.write(canonical(value) + b"\n")
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)


def _session_identity(session: Path) -> dict[str, Any]:
    if session.is_symlink():
        raise ValueError("session symbolic links are not supported")
    manifest = read_json(session / "recording-manifest.json")
    close = read_json(session / "session-close-receipt.json")
    if (manifest.get("schema") != "sts2.human-annotator/recording-manifest-2"
            or manifest.get("close_schema_version") != 1
            or close.get("schema") != "sts2.human-annotator/session-close-1"
            or close.get("status") != "closed"
            or not close.get("closed_at")
            or any(not isinstance(manifest.get(k), str) or not manifest[k]
                   or close.get(k) != manifest[k] for k in ("session_id", "timeline_id"))):
        raise ValueError("exact successful Close receipt required")
    return {"session_id": manifest["session_id"], "timeline_id": manifest["timeline_id"],
            "manifest_sha256": digest(manifest), "close_sha256": digest(close),
            "raw_files": _inventory(session)}


class DeliveryOutbox:
    """One private local outbox per fixed campaign/attestation/tool configuration."""

    def __init__(self, root: str | Path, *, worker_id: str, campaign_id: str,
                 human_origin_attested: bool, tool_release_id: str) -> None:
        if human_origin_attested is not True:
            raise ValueError("operator Human-origin attestation is required before automatic collection")
        if not all(re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,127}", x) for x in (worker_id, campaign_id)):
            raise ValueError("invalid worker/campaign identifier")
        if not re.fullmatch(r"[0-9a-f]{64}", tool_release_id):
            raise ValueError("exact tool release ID required")
        self.root = Path(root).absolute()
        self.root.mkdir(parents=True, exist_ok=True)
        self.identity = {"worker_id": worker_id, "campaign_id": campaign_id,
                         "human_origin_attested": True, "tool_release_id": tool_release_id}
        with self._db() as db:
            encoded = canonical(self.identity).decode()
            if db.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name='config'").fetchone():
                existing = db.execute("SELECT value FROM config WHERE id=1").fetchone()
                if existing is None or existing[0] != encoded:
                    raise ValueError("outbox configuration is immutable; use a new outbox for changed identity")
                return
            db.execute("PRAGMA journal_mode=WAL")
            db.executescript("""
                CREATE TABLE IF NOT EXISTS config (id INTEGER PRIMARY KEY CHECK(id=1), value TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS sessions (
                    id TEXT PRIMARY KEY, source TEXT NOT NULL, identity TEXT NOT NULL,
                    status TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0,
                    retry_at REAL NOT NULL DEFAULT 0, content_id TEXT, error TEXT, receipt TEXT);
            """)
            db.execute("INSERT OR IGNORE INTO config VALUES(1, ?)", (encoded,))
            if db.execute("SELECT value FROM config WHERE id=1").fetchone()[0] != encoded:
                raise ValueError("outbox configuration is immutable; use a new outbox for changed identity")

    @contextmanager
    def _db(self) -> Iterator[sqlite3.Connection]:
        db = sqlite3.connect(self.root / "outbox.sqlite3", timeout=0)
        db.row_factory = sqlite3.Row
        db.execute("PRAGMA synchronous=FULL")
        try:
            with db:
                yield db
        finally:
            db.close()

    def reconcile(self, recordings_root: str | Path) -> dict[str, int]:
        """Discover close seals, not later frames/timeouts. Missing seals stay local."""
        root = Path(recordings_root).absolute()
        if self.root == root or root in self.root.parents:
            raise ValueError("outbox must not be inside recording root")
        counts = {"enqueued": 0, "existing": 0, "unsealed": 0, "incident": 0}
        if not root.is_dir():
            raise ValueError("recording root is absent")
        with self._db() as db:
            db.execute("BEGIN IMMEDIATE")
            for session in sorted(root.iterdir()):
                if not session.is_dir() or not (session / "recording-manifest.json").is_file():
                    continue
                if not (session / "session-close-receipt.json").is_file():
                    counts["unsealed"] += 1
                    continue
                key = digest({"source": str(session)})
                if db.execute("SELECT 1 FROM sessions WHERE id=?", (key,)).fetchone():
                    counts["existing"] += 1
                    continue
                try:
                    identity = _session_identity(session)
                    status, error = "pending", None
                    counts["enqueued"] += 1
                except (ValueError, OSError) as error_value:
                    identity, status, error = {}, "incident", str(error_value)
                    counts["incident"] += 1
                db.execute("INSERT INTO sessions(id,source,identity,status,error) VALUES(?,?,?,?,?)",
                           (key, str(session), canonical(identity).decode(), status, error))
        return counts

    def drain_one(self, tool: CollectionTool, transport: Transport, *, now: float | None = None) -> dict[str, Any] | None:
        """Do one bounded attempt. Transport must be idempotent by content ID."""
        timestamp = time.time() if now is None else now
        if tool.release_id != self.identity["tool_release_id"]:
            raise ValueError("outbox/tool identity differs")
        with self._db() as db:
            db.execute("BEGIN IMMEDIATE")
            row = db.execute("SELECT * FROM sessions WHERE status='pending' AND retry_at<=? ORDER BY id LIMIT 1",
                             (timestamp,)).fetchone()
            if row is None:
                return None
            key, source = row["id"], Path(row["source"])
            bundle = self.root / "bundles" / key
            try:
                # Recheck all sealed source bytes before and after packing. A source change is an incident.
                if canonical(_session_identity(source)).decode() != row["identity"]:
                    raise ValueError("sealed source changed after enrollment")
                if bundle.exists():
                    tool.verify()
                else:
                    tool.pack(source, bundle, self.identity["worker_id"], self.identity["campaign_id"])
                if canonical(_session_identity(source)).decode() != row["identity"]:
                    raise ValueError("sealed source changed during packing")
                verified = verify_human_session_bundle(bundle).require_value()
                manifest = DirectoryTransferManifest.from_directory(
                    bundle, content_id=verified.bundle_content_id, artifact_type="human-session-bundle")
                manifest.write(self.root / "transfers" / f"{key}.json")
                metadata = {"schema": "sts2.evidence/delivery-metadata-1", **self.identity,
                            "source_identity": json.loads(row["identity"]),
                            "collection_tool": tool.manifest}
                _atomic_json(self.root / "metadata" / f"{key}.json", metadata)
                receipt = transport(bundle, manifest, metadata)
                if (not isinstance(receipt, dict)
                        or receipt.get("schema") != "stpd/receive-receipt-v1"
                        or receipt.get("content_id") != manifest.content_id
                        or receipt.get("manifest_sha256") != manifest.manifest_sha256
                        or receipt.get("status") not in {"verified", "quarantined"}
                        or not isinstance(receipt.get("receipt_id"), str) or not receipt["receipt_id"]):
                    raise ValueError("receiver did not return a matching terminal verification receipt")
                _atomic_json(self.root / "receipts" / f"{key}.json", receipt)
                db.execute("UPDATE sessions SET status=?,content_id=?,receipt=?,error=NULL,attempts=attempts+1 WHERE id=?",
                           (receipt["status"], manifest.content_id, canonical(receipt).decode(), key))
            except (OSError, TimeoutError) as error:
                # Network retry never repeats a game action. No secrets/URLs are retained from transport errors.
                delay = min(3600, 2 ** min(row["attempts"] + 1, 11))
                db.execute("UPDATE sessions SET attempts=attempts+1,retry_at=?,error=? WHERE id=?",
                           (timestamp + delay, type(error).__name__, key))
            except (ValueError, RuntimeError) as error:
                if isinstance(error, CollectionFailure) and error.diagnostic:
                    _atomic_json(self.root / "incidents" / f"{key}.json", {"diagnostic": error.diagnostic})
                db.execute("UPDATE sessions SET status='incident',attempts=attempts+1,error=? WHERE id=?",
                           (str(error), key))
            result = dict(db.execute("SELECT * FROM sessions WHERE id=?", (key,)).fetchone())
        return result

    def status(self) -> list[dict[str, Any]]:
        with self._db() as db:
            return [dict(row) for row in db.execute("SELECT id,source,status,attempts,retry_at,content_id,error,receipt FROM sessions ORDER BY id")]


def reconcile_and_drain(outbox: DeliveryOutbox, recordings_root: Path, tool: CollectionTool,
                        transport: Transport, *, limit: int = 10) -> dict[str, Any]:
    counts = outbox.reconcile(recordings_root)
    processed = []
    for _ in range(limit):
        value = outbox.drain_one(tool, transport)
        if value is None:
            break
        processed.append(value)
    return {"discovery": counts, "processed": processed}
