"""Local immutable content-addressed directory store."""

from __future__ import annotations

import hashlib
import json
import os
import shutil
import tempfile
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class StoreReceipt:
    status: str
    content_id: str
    directory: Path
    manifest_sha256: str


class ContentAddressedStore:
    """Store complete directories below ``objects/<sha256>`` atomically."""

    def __init__(self, root: str | Path) -> None:
        self.root = Path(root).resolve()
        self.objects = self.root / "objects"
        self.objects.mkdir(parents=True, exist_ok=True)

    def put_directory(self, source: str | Path) -> StoreReceipt:
        source_path = Path(source).resolve()
        inventory = _inventory(source_path)
        manifest = {"schema": "sts2.evidence/store-directory-1", "files": inventory}
        manifest_bytes = _canonical_json(manifest).encode("utf-8")
        content_id = hashlib.sha256(manifest_bytes).hexdigest()
        destination = self.objects / content_id
        if destination.exists():
            if _inventory(destination) != inventory:
                raise ValueError(f"content-address collision: {content_id}")
            return StoreReceipt("reused", content_id, destination, _sha256_bytes(manifest_bytes))

        staging = Path(tempfile.mkdtemp(prefix=f".{content_id}.", dir=self.objects))
        try:
            _copy_directory(source_path, staging)
            if _inventory(staging) != inventory:
                raise ValueError("staged directory inventory changed during copy")
            os.replace(staging, destination)
        except Exception:
            shutil.rmtree(staging, ignore_errors=True)
            raise
        return StoreReceipt("stored", content_id, destination, _sha256_bytes(manifest_bytes))

    def resolve(self, content_id: str) -> Path:
        if len(content_id) != 64 or any(character not in "0123456789abcdef" for character in content_id):
            raise ValueError("content ID must be a lowercase SHA-256 digest")
        destination = (self.objects / content_id).resolve()
        if destination.parent != self.objects or not destination.is_dir():
            raise FileNotFoundError(content_id)
        return destination


def _inventory(directory: Path) -> list[dict[str, object]]:
    if not directory.is_dir():
        raise ValueError(f"directory is absent: {directory}")
    entries: list[dict[str, object]] = []
    for path in sorted(directory.rglob("*")):
        if path.is_symlink():
            raise ValueError(f"symbolic links are not allowed: {path}")
        if path.is_file():
            relative = path.relative_to(directory).as_posix()
            entries.append({"path": relative, "bytes": path.stat().st_size, "sha256": _sha256_file(path)})
    return entries


def _copy_directory(source: Path, destination: Path) -> None:
    for path in sorted(source.rglob("*")):
        relative = path.relative_to(source)
        target = destination / relative
        if path.is_symlink():
            raise ValueError(f"symbolic links are not allowed: {path}")
        if path.is_dir():
            target.mkdir(parents=True, exist_ok=True)
        elif path.is_file():
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)


def _canonical_json(value: object) -> str:
    return json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(",", ":"), sort_keys=True)


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()
