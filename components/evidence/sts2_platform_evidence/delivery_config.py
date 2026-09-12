"""Owner-defined delivery configuration and read-only terminal preflight.

Preflight neither enrolls sessions nor opens the network. A ready local tool
does not attest historical sessions, verify a cloud account, or admit research.
"""

from __future__ import annotations

import os
import json
import re
import sqlite3
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from urllib.parse import urlsplit

from .collection_tool import CollectionTool, canonical, read_json


@dataclass(frozen=True)
class DeliveryConfig:
    recordings_root: Path
    outbox_root: Path
    tool_directory: Path
    tool_release_id: str
    worker_id: str
    campaign_id: str
    human_origin_attested: bool
    hub_url: str
    allowed_upload_hosts: list[str]
    poll_seconds: float = 5
    dotnet: str = "dotnet"
    allow_loopback_http: bool = False

    @property
    def identity(self) -> dict[str, Any]:
        return {name: getattr(self, name) for name in (
            "worker_id", "campaign_id", "human_origin_attested", "tool_release_id")}

    @classmethod
    def load(cls, path: Path) -> DeliveryConfig:
        value = read_json(path)
        required = {"schema", "recordings_root", "outbox_root", "tool_directory",
                    "tool_release_id", "worker_id", "campaign_id", "human_origin_attested",
                    "hub_url", "allowed_upload_hosts"}
        if (set(value) - required - {"poll_seconds", "dotnet", "allow_loopback_http"}
                or required - set(value)
                or value.get("schema") != "sts2.evidence/delivery-config-1"):
            raise ValueError("invalid_delivery_config")
        paths = {}
        for name in ("recordings_root", "outbox_root", "tool_directory"):
            raw = value[name]
            if not isinstance(raw, str) or not Path(raw).is_absolute() or Path(raw).is_symlink():
                raise ValueError("absolute_non_symlink_paths_required")
            paths[name] = Path(raw).resolve()
        recording, outbox = paths["recordings_root"], paths["outbox_root"]
        if recording == outbox or recording in outbox.parents or outbox in recording.parents:
            raise ValueError("recording_and_outbox_roots_must_be_separate")
        if value["human_origin_attested"] is not True:
            raise ValueError("operator_attestation_required")
        for name in ("worker_id", "campaign_id"):
            if not isinstance(value[name], str) or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,127}", value[name]):
                raise ValueError("invalid_worker_or_campaign")
        pin = value["tool_release_id"]
        if not isinstance(pin, str) or not re.fullmatch(r"[0-9a-f]{64}", pin):
            raise ValueError("invalid_tool_release_id")
        loopback = value.get("allow_loopback_http", False)
        if not isinstance(loopback, bool):
            raise ValueError("invalid_loopback_option")
        hub = value["hub_url"]
        if not isinstance(hub, str):
            raise ValueError("invalid_hub_endpoint")
        parsed = urlsplit(hub)
        if (not parsed.hostname or parsed.username or parsed.password or parsed.query
                or parsed.fragment or parsed.path not in {"", "/"}
                or not (parsed.scheme == "https" or (loopback and parsed.scheme == "http"
                    and parsed.hostname in {"localhost", "127.0.0.1", "::1"}))):
            raise ValueError("invalid_hub_endpoint")
        _ = parsed.port
        hosts = value["allowed_upload_hosts"]
        if (not isinstance(hosts, list) or not hosts or any(
                not isinstance(host, str) or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9.-]*|::1", host)
                for host in hosts) or len(set(hosts)) != len(hosts)):
            raise ValueError("explicit_upload_hosts_required")
        interval = value.get("poll_seconds", 5)
        if isinstance(interval, bool) or not isinstance(interval, (int, float)) or not 1 <= interval <= 300:
            raise ValueError("invalid_poll_interval")
        dotnet = value.get("dotnet", "dotnet")
        if not isinstance(dotnet, str) or not dotnet or "\0" in dotnet:
            raise ValueError("invalid_dotnet_command")
        return cls(**paths, tool_release_id=pin, worker_id=value["worker_id"],
                   campaign_id=value["campaign_id"], human_origin_attested=True,
                   hub_url=hub.rstrip("/"), allowed_upload_hosts=hosts,
                   poll_seconds=interval, dotnet=dotnet, allow_loopback_http=loopback)


def inspect_outbox(config: DeliveryConfig) -> list[dict[str, Any]]:
    """Read logical state without initializing or enrolling an outbox.

    SQLite may create reader-coordination WAL/shm sidefiles. mode=ro must see
    the live committed WAL; immutable=1 would silently read stale main bytes.
    """
    database = config.outbox_root / "outbox.sqlite3"
    if not database.exists():
        return []
    if database.is_symlink() or not database.is_file():
        raise ValueError("invalid_outbox_database")
    db = sqlite3.connect(database.as_uri() + "?mode=ro", uri=True, timeout=1)
    db.row_factory = sqlite3.Row
    try:
        existing = db.execute("SELECT value FROM config WHERE id=1").fetchone()
        if existing is None or existing[0] != canonical(config.identity).decode():
            raise ValueError("outbox_identity_mismatch")
        return [dict(row) for row in db.execute(
            "SELECT id,source,status,attempts,retry_at,content_id,error,receipt FROM sessions ORDER BY id")]
    finally:
        db.close()


def outbox_destination_available(path: Path) -> bool:
    """Check the nearest existing directory without creating a write probe."""
    parent = path
    while not parent.exists() and parent != parent.parent:
        parent = parent.parent
    return parent.is_dir() and os.access(parent, os.W_OK | os.X_OK)


def runtime_available(tool: CollectionTool) -> bool:
    """Let .NET load the pinned tool and run its existing read-only identity command."""
    entry = tool.manifest["identity"]["entrypoint"]
    expected = next((row["sha256"] for row in tool.manifest["identity"]["files"]
                     if row["path"] == entry), None)
    if expected is None:
        return False
    executable = str(tool.directory / entry)
    result = subprocess.run([tool.dotnet, executable, "identity", executable], capture_output=True,
                            text=True, timeout=10, check=False)
    if result.returncode:
        return False
    identity = json.loads(result.stdout)
    return isinstance(identity, dict) and identity.get("sha256") == expected


def doctor(path: Path) -> dict[str, Any]:
    checks: dict[str, dict[str, Any]] = {}
    report: dict[str, Any] = {"schema": "sts2.evidence/delivery-doctor-1", "status": "BLOCKED",
                              "checks": checks, "discovered_sessions": None,
                              "non_claims": ["Human origin", "cloud authorization", "native loaded identity",
                                             "research admission", "existing session campaign consent"]}
    try:
        config = DeliveryConfig.load(path)
    except (OSError, ValueError, TypeError, KeyError):
        checks["configuration"] = {"status": "INVALID"}
        return report
    checks["configuration"] = {"status": "PASS"}
    report["hub_url"] = config.hub_url
    report["tool_release_id"] = config.tool_release_id
    checks["recordings_root"] = {"status": "PASS" if config.recordings_root.is_dir() else "ABSENT"}
    if config.recordings_root.is_dir():
        try:
            report["discovered_sessions"] = sum(
                1 for path in config.recordings_root.iterdir()
                if path.is_dir() and (path / "recording-manifest.json").is_file())
        except OSError:
            checks["recordings_root"] = {"status": "UNREADABLE"}
    token = os.environ.get("STPD_HUB_TOKEN", "")
    checks["credential"] = {"status": "PASS" if token and "\n" not in token and "\r" not in token else "MISSING_OR_INVALID"}
    checks["outbox_destination"] = {"status": "PASS" if outbox_destination_available(config.outbox_root)
                                  else "NOT_A_WRITABLE_DIRECTORY"}
    try:
        inspect_outbox(config)
        checks["outbox_identity"] = {"status": "PASS"}
    except (OSError, ValueError, sqlite3.Error):
        checks["outbox_identity"] = {"status": "MISMATCH_OR_UNREADABLE"}
    try:
        tool = CollectionTool(config.tool_directory, config.tool_release_id, dotnet=config.dotnet)
        checks["collection_release"] = {"status": "PASS"}
    except (OSError, ValueError, TypeError, KeyError):
        checks["collection_release"] = {"status": "MISSING_OR_MISMATCH"}
    else:
        try:
            available = runtime_available(tool)
        except (OSError, ValueError, TypeError, KeyError, subprocess.SubprocessError):
            available = False
        checks["dotnet_runtime"] = {"status": "PASS" if available else "MISSING_OR_UNSUPPORTED"}
    report["status"] = "PASS" if all(value["status"] == "PASS" for value in checks.values()) else "BLOCKED"
    return report
