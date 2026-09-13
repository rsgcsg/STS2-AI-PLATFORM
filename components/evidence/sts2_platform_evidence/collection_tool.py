"""Execute an operator-pinned collection tool without trusting a mutable checkout.

The release ID authenticates bytes only against the caller's trusted pin. It is
not a signature, Human attestation, game compatibility or research admission.
"""

from __future__ import annotations

import hashlib
import json
import re
import subprocess
from pathlib import Path
from typing import Any

from .transfer import _inventory


def canonical(value: object) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False).encode()


def digest(value: object) -> str:
    return hashlib.sha256(canonical(value)).hexdigest()


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError("expected JSON object")
    return value


class CollectionTool:
    """Release built from clean source; verify all executable/dependency bytes."""

    def __init__(self, directory: str | Path, expected_release_id: str, *, dotnet: str = "dotnet") -> None:
        self.directory = Path(directory).absolute()
        self.release_id = expected_release_id
        self.dotnet = dotnet
        self.manifest = self.verify()

    def verify(self) -> dict[str, Any]:
        if self.directory.is_symlink():
            raise ValueError("collection tool directory cannot be a symbolic link")
        manifest = read_json(self.directory / "collection-tool.json")
        identity = manifest.get("identity")
        if (manifest.get("schema") != "sts2.evidence/collection-tool-1"
                or not isinstance(identity, dict)
                or not re.fullmatch(r"[0-9a-f]{64}", self.release_id)
                or manifest.get("release_id") != self.release_id
                or digest(identity) != self.release_id):
            raise ValueError("collection tool trusted release ID mismatch")
        if (identity.get("worktree") != "clean"
                or not re.fullmatch(r"[0-9a-f]{40}", str(identity.get("source_revision", "")))
                or identity.get("entrypoint") != "sts2-human-annotator.dll"
                or identity.get("supported_recording_schema") != "sts2.human-annotator/recording-manifest-2"):
            raise ValueError("unsupported collection tool identity")
        actual = [{"path": p, "bytes": n, "sha256": h} for p, n, h in _inventory(self.directory)
                  if p != "collection-tool.json"]
        if identity.get("files") != actual:
            raise ValueError("collection tool release bytes differ")
        if not any(row["path"] == "platform-bom.json" for row in actual):
            raise ValueError("collection tool BOM is missing")
        return manifest

    def pack(self, session: Path, output: Path, worker: str, campaign: str) -> None:
        manifest = self.verify()
        try:
            result = subprocess.run(
                [self.dotnet, str(self.directory / manifest["identity"]["entrypoint"]), "pack-session",
                 str(session), worker, campaign, str(output), manifest["identity"]["source_revision"],
                 "human_origin_attested"], capture_output=True, text=True, timeout=600, check=False,
            )
        except subprocess.TimeoutExpired:
            raise CollectionFailure("collection tool exceeded bounded runtime") from None
        if result.returncode != 0:
            # Tool output can contain private paths/records. Preserve only in local incident diagnostics.
            raise CollectionFailure("collection tool audit/pack failed", result.stderr[-16384:])


class CollectionFailure(ValueError):
    def __init__(self, message: str, diagnostic: str = "") -> None:
        super().__init__(message)
        self.diagnostic = diagnostic
