"""Explicit credential recovery without new evidence or transport identities.

The caller supplies freshly authorized credentials to the subsequent worker.
This local operation neither authenticates an account nor sends network traffic.
Only the owner can requeue typed Hub-authentication blocks; historical incident
text is not enough to authorize recovery.
"""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

from .collection_tool import canonical, read_json
from .delivery import DeliveryOutbox, _session_identity
from .delivery_config import DeliveryConfig
from .delivery_lock import process_lock
from .delivery_summary import _update_projection
from .human_session_bundle import verify_human_session_bundle
from .transfer import DirectoryTransferManifest, _inventory, _sha256_file


class _RecoveryRejected(Exception):
    pass


def _check_identity(outbox: DeliveryOutbox, row: dict[str, Any]) -> None:
    context = json.loads(row["auth_context"] or "null")
    if (row["error"] != "hub_authentication_blocked" or not isinstance(context, dict)
            or context.get("schema") != "sts2.evidence/delivery-auth-block-1"
            or context.get("http_status") not in {401, 403}
            or not isinstance(context.get("transport"), dict)):
        raise _RecoveryRejected("recovery_identity_unavailable")
    key = row["id"]
    if row["receipt"] is not None or (outbox.root / "receipts" / f"{key}.json").exists():
        raise _RecoveryRejected("recovery_state_inconsistent")
    expected = json.loads(row["identity"])
    if canonical(_session_identity(Path(row["source"]))) != canonical(expected):
        raise _RecoveryRejected("recovery_identity_mismatch")
    bundle = outbox.root / "bundles" / key
    result = verify_human_session_bundle(bundle)
    if not result.passed:
        raise _RecoveryRejected("recovery_evidence_invalid")
    verified = result.require_value()
    manifest = DirectoryTransferManifest.from_directory(bundle, content_id=verified.bundle_content_id,
                                                        artifact_type="human-session-bundle")
    transfer_path = outbox.root / "transfers" / f"{key}.json"
    if (transfer_path.is_symlink() or DirectoryTransferManifest.read(transfer_path) != manifest
            or verified.session_id != expected.get("session_id")
            or manifest.content_id != row["content_id"]
            or manifest.content_id != context.get("content_id")
            or manifest.manifest_sha256 != context.get("manifest_sha256")
            or canonical(_inventory(bundle / "raw")) != canonical(expected.get("raw_files"))):
        raise _RecoveryRejected("recovery_identity_mismatch")
    metadata = read_json(outbox.root / "metadata" / f"{key}.json")
    if (metadata.get("schema") != "sts2.evidence/delivery-metadata-1"
            or metadata.get("source_identity") != expected
            or any(metadata.get(name) != value for name, value in outbox.identity.items())):
        raise _RecoveryRejected("recovery_identity_mismatch")
    transport = context["transport"]
    upload_id = transport.get("upload_id")
    archive_hash, archive_bytes = transport.get("archive_sha256"), transport.get("archive_bytes")
    if (not isinstance(archive_hash, str) or not re.fullmatch(r"[0-9a-f]{64}", archive_hash)
            or type(archive_bytes) is not int or archive_bytes <= 0
            or (upload_id is not None and (not isinstance(upload_id, str)
                or not re.fullmatch(r"[A-Za-z0-9_-]{1,128}", upload_id)))):
        raise _RecoveryRejected("recovery_identity_unavailable")
    archive = outbox.root / "archives" / f"{manifest.manifest_sha256}.tar.gz"
    if (archive.is_symlink() or archive.stat().st_size != archive_bytes
            or _sha256_file(archive) != archive_hash):
        raise _RecoveryRejected("recovery_identity_mismatch")
    attempt = outbox.root / "archives" / f"{manifest.manifest_sha256}.upload.json"
    if upload_id is None:
        if attempt.exists():
            raise _RecoveryRejected("recovery_identity_mismatch")
    else:
        if attempt.is_symlink():
            raise _RecoveryRejected("recovery_identity_mismatch")
        previous = read_json(attempt)
        if previous.get("upload_id") != upload_id or previous.get("archive_sha256") != archive_hash:
            raise _RecoveryRejected("recovery_identity_mismatch")


def resume_auth(config: DeliveryConfig, *, delivery_id: str | None = None) -> dict[str, Any]:
    """Requeue matching authentication blocks while the delivery worker is stopped.

    Acquires the same OS lock as the worker, then a SQLite write transaction.
    Exact-ID calls on an already pending/terminal/incident row are no-ops. The
    all-rows form selects only auth_blocked rows. Rejected validation leaves its
    original block and every evidence/transport/receipt byte intact. The next
    normal attempt alone can receive a terminal receipt or block again.
    """
    if delivery_id is not None and (not isinstance(delivery_id, str)
            or not re.fullmatch(r"[0-9a-f]{64}", delivery_id)):
        raise ValueError("invalid_delivery_id")
    report: dict[str, Any] = {"schema": "sts2.evidence/delivery-auth-recovery-1",
        "resumed": 0, "unchanged": 0, "rejected": 0, "sessions": []}
    database = config.outbox_root / "outbox.sqlite3"
    if not database.exists():
        return report
    if database.is_symlink() or not database.is_file():
        raise ValueError("invalid_outbox_database")
    with process_lock(config.outbox_root):
        outbox = DeliveryOutbox(config.outbox_root, **config.identity)
        with outbox._db() as db:
            db.execute("BEGIN IMMEDIATE")
            rows = db.execute("SELECT * FROM sessions WHERE id=?", (delivery_id,)) if delivery_id else \
                db.execute("SELECT * FROM sessions WHERE status='auth_blocked' ORDER BY id")
            for raw in list(rows):
                row = dict(raw)
                item = {"id": row["id"], "status": row["status"], "result": "unchanged", "error": None}
                if row["status"] != "auth_blocked":
                    report["unchanged"] += 1
                else:
                    error = None
                    try:
                        _check_identity(outbox, row)
                    except _RecoveryRejected as failure:
                        error = str(failure)
                    except OSError:
                        error = "recovery_io_unavailable"
                    except (ValueError, TypeError, KeyError):
                        error = "recovery_identity_invalid"
                    if error:
                        report["rejected"] += 1
                        item.update(result="rejected", error=error)
                    else:
                        db.execute("UPDATE sessions SET status='pending',retry_at=0,error=NULL WHERE id=?",
                                   (row["id"],))
                        report["resumed"] += 1
                        item.update(status="pending", result="resumed")
                        _update_projection(outbox.root, row["id"], stage="locally_verified")
                report["sessions"].append(item)
    return report
