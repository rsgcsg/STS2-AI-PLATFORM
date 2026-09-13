"""Bounded, path-free public delivery status over owner-maintained projections.

Reads neither initialize an outbox nor verify bundles. Missing historical
metadata is explicit; only the controlled rebuild operation scans old bundles.
"""

from __future__ import annotations

import json
import re
import sqlite3
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from .collection_tool import canonical
from .delivery_config import DeliveryConfig

PROJECTION_SCHEMA = "sts2.evidence/delivery-projection-1"
STATUS_SCHEMA = "sts2.evidence/delivery-status-2"
_ID = re.compile(r"[A-Za-z0-9_-]{1,128}")
_HASH = re.compile(r"[0-9a-f]{64}")


def _now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _load_projection(path: Path) -> dict[str, Any]:
    if not path.is_file() or path.is_symlink():
        return {}
    try:
        with path.open("rb") as stream:
            content = stream.read(1024 * 1024 + 1)
        if len(content) > 1024 * 1024:
            return {}
        value = json.loads(content)
        return value if isinstance(value, dict) else {}
    except (OSError, ValueError):
        return {}


def _update_projection(root: Path, key: str, **values: Any) -> bool:
    from .delivery import _atomic_json
    path = root / "projections" / f"{key}.json"
    previous = _load_projection(path)
    if previous.get("schema") != PROJECTION_SCHEMA:
        previous = {"schema": PROJECTION_SCHEMA, "session_id": None, "enrolled_at": None,
                    "manifest_sha256": None}
    previous.update(values, observed_at=_now())
    try:
        _atomic_json(path, previous)
    except OSError:
        # A UI index is not delivery authority. Its absence remains explicit
        # on status and can be rebuilt without changing the terminal receipt.
        return False
    return True


def _safe_receipt(value: str | None) -> dict[str, Any] | None:
    if value is None:
        return None
    try:
        receipt = json.loads(value)
    except ValueError:
        return None
    if not isinstance(receipt, dict) or receipt.get("schema") != "stpd/receive-receipt-v1":
        return None
    # Details and arbitrary extension fields can contain paths. Public status
    # exposes exact terminal identity and bounded finding codes only.
    result = {key: receipt.get(key) for key in ("schema", "receipt_id", "status", "content_id", "manifest_sha256")}
    findings = receipt.get("findings", [])
    codes = []
    if isinstance(findings, list):
        for item in findings:
            # Hub receive-receipt-v1 publishes string reason codes. Typed
            # verifier adapters may wrap the same code with private details.
            code = item.get("code") if isinstance(item, dict) else item
            if isinstance(code, str) and re.fullmatch(r"[A-Za-z0-9_.-]{1,128}", code):
                codes.append({"code": code})
    result["findings"] = codes
    return result


def inspect_delivery_status(config: DeliveryConfig, *, limit: int = 25, offset: int = 0,
                            delivery_id: str | None = None) -> dict[str, Any]:
    """Public paginated snapshot; counts describe all outbox delivery statuses.

    ``observed_at`` is this read's time, not a network contact. Row observation
    and transport observation have independent timestamps, unknown for history.
    Attempts count receipt polls too and are never a failure/upload counter.
    """
    if type(limit) is not int or not 1 <= limit <= 100 or type(offset) is not int or offset < 0:
        raise ValueError("invalid_delivery_page")
    if delivery_id is not None and not _HASH.fullmatch(delivery_id):
        raise ValueError("invalid_delivery_id")
    result: dict[str, Any] = {"schema": STATUS_SCHEMA, "observed_at": _now(),
        "sessions": [], "counts": {status: 0 for status in ("pending", "auth_blocked", "verified", "quarantined", "incident")},
        "total": 0, "limit": limit, "offset": offset, "next_offset": None,
        "quality": {"summaries_available": 0, "summaries_missing": 0, "canonical": None,
                    "real_failures": None, "partial": False}}
    database = config.outbox_root / "outbox.sqlite3"
    if not database.exists():
        return result
    if database.is_symlink() or not database.is_file():
        raise ValueError("invalid_outbox_database")
    db = sqlite3.connect(database.as_uri() + "?mode=ro", uri=True, timeout=1)
    db.row_factory = sqlite3.Row
    try:
        db.execute("BEGIN")
        identity = db.execute("SELECT value FROM config WHERE id=1").fetchone()
        if identity is None or identity[0] != canonical(config.identity).decode():
            raise ValueError("outbox_identity_mismatch")
        result["counts"].update(dict(db.execute("SELECT status,count(*) FROM sessions GROUP BY status")))
        result["total"] = sum(result["counts"].values())
        columns = {row[1] for row in db.execute("PRAGMA table_info(sessions)")}
        has_summary = {"summary", "summary_canonical", "summary_real_failures"} <= columns
        if has_summary:
            quality = db.execute("SELECT count(summary),sum(summary_canonical),sum(summary_real_failures),"
                                 "count(summary_real_failures) FROM sessions").fetchone()
            available, canonical_count, failures, known_failures = quality
        else:
            available, canonical_count, failures, known_failures = 0, None, None, 0
        result["quality"] = {"summaries_available": available, "summaries_missing": result["total"] - available,
            "canonical": canonical_count, "real_failures": failures,
            "partial": available < result["total"] or known_failures < available}
        selection = "SELECT id,status,attempts,retry_at,content_id,error,receipt," + (
            "summary" if has_summary else "NULL AS summary") + " FROM sessions"
        if delivery_id is not None:
            rows = list(db.execute(selection + " WHERE id=?", (delivery_id,)))
        else:
            rows = list(db.execute(selection + " ORDER BY rowid DESC LIMIT ? OFFSET ?", (limit, offset)))
            if offset + len(rows) < result["total"]:
                result["next_offset"] = offset + len(rows)
    finally:
        db.close()
    for row in rows:
        projected = _load_projection(config.outbox_root / "projections" / f"{row['id']}.json")
        if projected.get("schema") != PROJECTION_SCHEMA:
            projected = {}
        summary = json.loads(row["summary"]) if row["summary"] is not None else None
        if not isinstance(summary, dict) or (row["content_id"] is not None and summary.get("content_id") != row["content_id"]):
            summary = None
        receipt = _safe_receipt(row["receipt"])
        manifest_id = projected.get("manifest_sha256")
        transport = {}
        if isinstance(manifest_id, str) and _HASH.fullmatch(manifest_id):
            transport = _load_projection(config.outbox_root / "archives" / f"{manifest_id}.upload.json")
        upload_id = transport.get("upload_id")
        if not isinstance(upload_id, str) or not _ID.fullmatch(upload_id):
            upload_id = None
        status = row["status"]
        stage = status
        if status == "pending":
            stage = "retry_wait" if row["error"] else transport.get("status")
            if stage not in {"retry_wait", "awaiting_upload", "verification_pending"}:
                stage = "locally_verified" if summary else projected.get("stage", "queued")
            if stage not in {"retry_wait", "awaiting_upload", "verification_pending", "locally_verified", "packing", "queued"}:
                stage = "queued"
        safe_error = None
        if row["error"]:
            safe_error = row["error"] if row["error"] in {"OSError", "TimeoutError", "ConnectionError", "hub_authentication_blocked"} else "delivery_error"
        result["sessions"].append({"id": row["id"], "session_id": (summary or {}).get("session_id", projected.get("session_id")),
            "worker_id": config.worker_id, "campaign_id": config.campaign_id,
            "upload_id": upload_id, "content_id": row["content_id"] or (summary or {}).get("content_id"),
            "status": status, "stage": stage, "attempts": row["attempts"], "retry_at": row["retry_at"],
            "error": safe_error, "receipt": receipt, "summary": summary,
            "summary_status": "available" if summary else "not_materialized",
            "enrolled_at": projected.get("enrolled_at"), "observed_at": projected.get("observed_at"),
            "transport_observed_at": transport.get("observed_at"),
            "archive_bytes": transport.get("archive_bytes")})
    return result
