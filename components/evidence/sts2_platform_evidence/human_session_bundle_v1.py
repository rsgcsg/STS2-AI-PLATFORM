"""Typed V1 HumanSessionBundle verifier.

This is an independent consumer-side verifier. It intentionally depends only
on the frozen JSON/file contract and stdlib; it does not import Annotator code.
"""

from __future__ import annotations

import hashlib
import json
import re
import uuid
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any, cast

from .core import (
    VerificationFinding,
    VerificationResult,
    VerifierDescriptor,
)

BUNDLE_SCHEMA = "sts2.human-annotator/session-bundle-1"
PROFILE_SCHEMA = "stpd/human-collection-profile-v1"
RECORD_SCHEMA = "sts2.human-annotator/decision-record-1"
_IDENTIFIER = re.compile(r"^[a-z0-9][a-z0-9_-]{2,63}$")
_OPAQUE_IDENTIFIER = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{2,127}$")
_SHA256 = re.compile(r"^[0-9a-f]{64}$")
_UUID = re.compile(
    r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-"
    r"[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"
)


class BundleVerificationError(ValueError):
    def __init__(self, code: str, detail: str, path: str | None = None) -> None:
        super().__init__(detail)
        self.code = code
        self.path = path


@dataclass(frozen=True)
class CollectionProfile:
    value: Mapping[str, Any]
    profile_id: str
    sha256: str

    def validate_record(self, record: Mapping[str, Any]) -> None:
        if record.get("schema") != self.value["record_schema"]:
            raise BundleVerificationError("record_schema_drift", "record schema drift")
        environment = _object(record, "environment")
        game = _object(environment, "game")
        _require_equal(_object(self.value, "game"), "version", game, "version", "game")
        _require_equal(_object(self.value, "game"), "commit", game, "commit", "game")
        _require_equal(
            _object(self.value, "game"),
            "main_assembly_sha256",
            game,
            "main_assembly_sha256",
            "game",
        )
        _require_equal(
            _object(self.value, "game"),
            "main_assembly_mvid",
            game,
            "main_assembly_module_version_id",
            "game",
        )
        for name in ("connector", "annotator"):
            profile_artifact = _object(self.value, name)
            record_artifact = _object(environment, name)
            for profile_key, record_key in (
                ("source_revision", "source_revision"),
                ("source_digest_sha256", "source_digest_sha256"),
                ("artifact_sha256", "sha256"),
                ("mvid", "module_version_id"),
            ):
                _require_equal(profile_artifact, profile_key, record_artifact, record_key, name)
        if environment.get("player_environment_protocol") != self.value[
            "player_environment_protocol"
        ]:
            raise BundleVerificationError("protocol_drift", "Player Environment protocol drift")
        modset = _object(self.value, "modset")
        _require_equal(modset, "status", environment, "modset_status", "modset")
        _require_equal(modset, "fingerprint", environment, "modset_fingerprint", "modset")
        action = _object(record, "action")
        family = (
            "ordinary_combat.play_card"
            if record.get("decision_family") == "ordinary_combat"
            and action.get("verb") == "play"
            else f"{record.get('decision_family')}.{action.get('verb')}"
        )
        if family not in _strings(self.value, "allowed_action_families"):
            raise BundleVerificationError(
                "action_family_outside_profile",
                f"record action family is outside profile: {family}",
            )


@dataclass(frozen=True)
class HumanSessionBundle:
    directory: Path
    manifest: Mapping[str, Any]
    session_id: str
    worker_id: str
    campaign_id: str
    profile_id: str
    bundle_content_id: str
    bundle_sha256: str
    export_sha256: str
    record_count: int
    run_ids: tuple[str, ...]
    invalidations: int
    invalidations_by_reason: Mapping[str, int]


DESCRIPTOR: VerifierDescriptor[HumanSessionBundle] = VerifierDescriptor(
    "human-session-bundle-v1",
    BUNDLE_SCHEMA,
    1,
    HumanSessionBundle,
)


def load_collection_profile(path: str | Path) -> CollectionProfile:
    source = Path(path).resolve()
    value = _load_json(source)
    if _text(value, "schema") != PROFILE_SCHEMA:
        raise ValueError("unsupported collection profile schema")
    profile_id = _identifier(value, "profile_id")
    _text(value, "platform")
    game = _object(value, "game")
    _text(game, "version")
    _text(game, "commit")
    _digest(game, "main_assembly_sha256")
    _uuid_text(game, "main_assembly_mvid")
    for name in ("connector", "annotator"):
        artifact = _object(value, name)
        _commit(artifact, "source_revision")
        _digest(artifact, "source_digest_sha256")
        _digest(artifact, "artifact_sha256")
        _uuid_text(artifact, "mvid")
    _text(value, "player_environment_protocol")
    modset = _object(value, "modset")
    if _text(modset, "status") != "canary_exact_observer_modset":
        raise ValueError("unsupported collection profile modset")
    _digest(modset, "fingerprint")
    if _text(value, "record_schema") != RECORD_SCHEMA:
        raise ValueError("unsupported collection profile record schema")
    families = _strings(value, "allowed_action_families")
    if not families or len(families) != len(set(families)):
        raise ValueError("collection profile action families must be non-empty and unique")
    return CollectionProfile(value, profile_id, _semantic_hash(value))


class HumanSessionBundleVerifier:
    descriptor = DESCRIPTOR

    def verify(
        self,
        source: str | Path,
        expected: CollectionProfile | Mapping[str, object] | None = None,
    ) -> VerificationResult[HumanSessionBundle]:
        directory = Path(source).resolve()
        try:
            profile = _expected_profile(expected)
            bundle = self._verify(directory, profile)
            return VerificationResult(self.descriptor, "pass", directory, bundle)
        except BundleVerificationError as error:
            return VerificationResult(
                self.descriptor,
                "fail",
                directory,
                findings=(VerificationFinding(error.code, str(error), error.path),),
            )
        except (OSError, TypeError, ValueError, json.JSONDecodeError) as error:
            return VerificationResult(
                self.descriptor,
                "fail",
                directory,
                findings=(VerificationFinding("malformed_bundle", str(error)),),
            )

    def _verify(
        self,
        directory: Path,
        profile: CollectionProfile | None,
    ) -> HumanSessionBundle:
        if not directory.is_dir():
            raise BundleVerificationError("bundle_absent", f"session bundle is absent: {directory}")
        checksums_path = directory / "checksums.sha256"
        checksums = _read_checksums(checksums_path)
        actual_files = {
            path.relative_to(directory).as_posix()
            for path in directory.rglob("*")
            if path.is_file() and path != checksums_path
        }
        if set(checksums) != actual_files:
            raise BundleVerificationError(
                "checksum_inventory_mismatch",
                "bundle file inventory differs from checksums.sha256",
            )
        for relative, expected_sha in checksums.items():
            actual = _sha256_file(directory / relative)
            if actual != expected_sha:
                raise BundleVerificationError(
                    "checksum_mismatch",
                    f"bundle checksum mismatch: {relative}",
                    relative,
                )

        manifest = _load_json(directory / "session-bundle-manifest.json")
        if _text(manifest, "schema") != BUNDLE_SCHEMA or manifest.get("schema_version") != 1:
            raise BundleVerificationError("bundle_schema_mismatch", "unsupported session bundle schema")
        content_identity = _object(manifest, "content_identity")
        content_id = _digest(manifest, "bundle_content_id")
        if _semantic_hash(content_identity) != content_id:
            raise BundleVerificationError("content_identity_mismatch", "bundle content identity mismatch")

        embedded_profile = _load_json(directory / "profile" / "collection-profile.json")
        embedded_sha = _semantic_hash(embedded_profile)
        if profile is not None and (
            embedded_profile != profile.value or embedded_sha != profile.sha256
        ):
            raise BundleVerificationError(
                "embedded_profile_drift",
                "embedded collection profile differs from admitted profile",
            )
        profile = profile or _profile_from_value(embedded_profile, embedded_sha)
        if _text(manifest, "collection_profile_id") != profile.profile_id:
            raise BundleVerificationError("profile_id_drift", "bundle collection profile ID drift")
        if _digest(manifest, "collection_profile_sha256") != profile.sha256:
            raise BundleVerificationError("profile_digest_drift", "bundle collection profile digest drift")

        attestation = _object(manifest, "human_origin_attestation")
        if not _boolean(attestation, "attested") or attestation.get("machine_verifiable") is not False:
            raise BundleVerificationError(
                "attestation_missing", "bundle has no explicit human-origin attestation"
            )
        worker_id = _identifier(manifest, "worker_id")
        if attestation.get("worker_id") != worker_id:
            raise BundleVerificationError("attestation_worker_drift", "attestation worker differs from bundle worker")
        if _text(manifest, "audit_status") != "pass":
            raise BundleVerificationError("audit_status_failed", "bundle audit did not pass")
        audit = _load_json(directory / "audit" / "audit-report.json")
        if audit.get("status") != "pass" or audit.get("invalid_records") != 0:
            raise BundleVerificationError("audit_failed", "independent bundle audit report did not pass")
        record_count = _positive_int(manifest, "record_count")
        if audit.get("valid_records") != record_count:
            raise BundleVerificationError("audit_count_mismatch", "bundle audit count differs from manifest")
        export_path = directory / "export" / "decisions.jsonl"
        export_sha = _sha256_file(export_path)
        if export_sha != _digest(manifest, "export_sha256"):
            raise BundleVerificationError("export_digest_mismatch", "bundle export digest differs from manifest")

        recording = _load_json(directory / "raw" / "recording-manifest.json")
        session_id = _opaque_identifier(manifest, "session_id")
        if recording.get("session_id") != session_id:
            raise BundleVerificationError("recording_session_mismatch", "raw recording manifest session differs from bundle")
        if recording.get("platform") != profile.value["platform"]:
            raise BundleVerificationError("recording_platform_mismatch", "raw recording platform differs from profile")
        coverage = _load_json(directory / "raw" / "coverage.json")
        if coverage.get("session_id") != session_id or coverage.get("admitted_records") != record_count:
            raise BundleVerificationError("coverage_mismatch", "raw coverage differs from bundle manifest")
        run_ids = tuple(_run_id(run_id) for run_id in _strings(manifest, "run_ids"))
        if not run_ids or len(run_ids) != len(set(run_ids)):
            raise BundleVerificationError("run_ids_invalid", "bundle run IDs must be non-empty and unique")
        expected_runs = {f"raw/{run_id}.jsonl" for run_id in run_ids}
        if not expected_runs.issubset(actual_files):
            raise BundleVerificationError("raw_run_missing", "bundle is missing a declared raw run")

        raw_lines = [
            line
            for run_id in sorted(run_ids)
            for line in (directory / "raw" / f"{run_id}.jsonl").read_text(encoding="utf-8").splitlines()
        ]
        expected_export = "".join(f"{line}\n" for line in raw_lines).encode("utf-8")
        if export_path.read_bytes() != expected_export:
            raise BundleVerificationError(
                "raw_export_mismatch",
                "bundle export is not the deterministic raw-session export",
            )

        raw_directory = directory / "raw"
        raw_file_sha256 = {
            path.name: _sha256_file(path)
            for path in sorted(raw_directory.iterdir())
            if path.is_file()
        }
        expected_identity = {
            "schema": BUNDLE_SCHEMA,
            "session_id": session_id,
            "collection_profile_id": profile.profile_id,
            "collection_profile_sha256": profile.sha256,
            "campaign_id": _identifier(manifest, "campaign_id"),
            "worker_id": worker_id,
            "human_origin_attestation": dict(attestation),
            "record_count": record_count,
            "run_ids": list(run_ids),
            "export_sha256": export_sha,
            "raw_file_sha256": raw_file_sha256,
            "audit": {
                "status": audit.get("status"),
                "valid_records": audit.get("valid_records"),
                "invalid_records": audit.get("invalid_records"),
                "invalidations": audit.get("invalidations"),
            },
        }
        if content_identity != expected_identity:
            raise BundleVerificationError(
                "content_identity_facts_mismatch",
                "bundle content identity differs from verified bundle facts",
            )

        export_records = _jsonl(export_path)
        if len(export_records) != record_count:
            raise BundleVerificationError("export_count_mismatch", "bundle export record count differs from manifest")
        observed_run_ids: set[str] = set()
        for line_number, record in export_records:
            if record.get("session_id") != session_id:
                raise BundleVerificationError(
                    "export_session_mismatch",
                    f"export line {line_number} has another session ID",
                )
            observed_run_ids.add(_identifier(record, "run_id"))
            profile.validate_record(record)
        if observed_run_ids != set(run_ids):
            raise BundleVerificationError("export_run_ids_mismatch", "bundle export run IDs differ from manifest")
        invalidations_by_reason = {
            str(key): int(value)
            for key, value in _object(coverage, "invalidations_by_reason").items()
        }
        return HumanSessionBundle(
            directory,
            manifest,
            session_id,
            worker_id,
            _identifier(manifest, "campaign_id"),
            profile.profile_id,
            content_id,
            _sha256_file(checksums_path),
            export_sha,
            record_count,
            run_ids,
            int(audit.get("invalidations", 0)),
            invalidations_by_reason,
        )


def _expected_profile(expected: CollectionProfile | Mapping[str, object] | None) -> CollectionProfile | None:
    if expected is None:
        return None
    if isinstance(expected, CollectionProfile):
        return expected
    value = cast(Mapping[str, Any], expected.get("value", expected))
    return _profile_from_value(value, _semantic_hash(value))


def _profile_from_value(value: Mapping[str, Any], sha256: str) -> CollectionProfile:
    profile = load_collection_profile_from_value(value, sha256)
    return profile


def load_collection_profile_from_value(value: Mapping[str, Any], sha256: str | None = None) -> CollectionProfile:
    profile_id = _validate_profile_value(value)
    if sha256 is None:
        sha256 = _semantic_hash(value)
    return CollectionProfile(value, profile_id, sha256)


def _validate_profile_value(value: Mapping[str, Any]) -> str:
    if _text(value, "schema") != PROFILE_SCHEMA:
        raise ValueError("unsupported collection profile schema")
    profile_id = _identifier(value, "profile_id")
    _text(value, "platform")
    game = _object(value, "game")
    _text(game, "version")
    _text(game, "commit")
    _digest(game, "main_assembly_sha256")
    _uuid_text(game, "main_assembly_mvid")
    for name in ("connector", "annotator"):
        artifact = _object(value, name)
        _commit(artifact, "source_revision")
        _digest(artifact, "source_digest_sha256")
        _digest(artifact, "artifact_sha256")
        _uuid_text(artifact, "mvid")
    _text(value, "player_environment_protocol")
    modset = _object(value, "modset")
    if _text(modset, "status") != "canary_exact_observer_modset":
        raise ValueError("unsupported collection profile modset")
    _digest(modset, "fingerprint")
    if _text(value, "record_schema") != RECORD_SCHEMA:
        raise ValueError("unsupported collection profile record schema")
    families = _strings(value, "allowed_action_families")
    if not families or len(families) != len(set(families)):
        raise ValueError("collection profile action families must be non-empty and unique")
    return profile_id


def _run_id(value: str) -> str:
    if not value or Path(value).name != value or value in {".", ".."}:
        raise BundleVerificationError("run_id_path_traversal", f"unsafe run ID: {value}")
    return value


def _read_checksums(path: Path) -> dict[str, str]:
    result: dict[str, str] = {}
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        if not line.strip():
            continue
        parts = line.split("  ", 1)
        if len(parts) != 2 or not _SHA256.fullmatch(parts[0]) or not parts[1]:
            raise BundleVerificationError("checksum_manifest_invalid", f"invalid checksum line {line_number}")
        relative = _safe_relative(parts[1])
        if relative in result:
            raise BundleVerificationError("checksum_manifest_duplicate", f"duplicate checksum path: {relative}")
        result[relative] = parts[0]
    return result


def _jsonl(path: Path) -> list[tuple[int, Mapping[str, Any]]]:
    result: list[tuple[int, Mapping[str, Any]]] = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        if not line.strip():
            continue
        value = json.loads(line)
        if not isinstance(value, Mapping):
            raise BundleVerificationError("record_not_object", f"JSONL line {line_number} is not an object")
        result.append((line_number, cast(Mapping[str, Any], value)))
    return result


def _load_json(path: Path) -> Mapping[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, Mapping):
        raise BundleVerificationError("json_not_object", f"JSON document is not an object: {path}")
    return cast(Mapping[str, Any], value)


def _semantic_hash(value: Any) -> str:
    encoded = json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(",", ":"), sort_keys=True).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _sha256_file(path: Path) -> str:
    with path.open("rb") as handle:
        digest = hashlib.sha256()
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
        return digest.hexdigest()


def _object(value: Mapping[str, Any], key: str) -> Mapping[str, Any]:
    item = value.get(key)
    if not isinstance(item, Mapping):
        raise BundleVerificationError("field_object_missing", f"missing object: {key}")
    return cast(Mapping[str, Any], item)


def _text(value: Mapping[str, Any], key: str) -> str:
    item = value.get(key)
    if not isinstance(item, str) or not item:
        raise BundleVerificationError("field_text_missing", f"missing text: {key}")
    return item


def _identifier(value: Mapping[str, Any], key: str) -> str:
    item = _text(value, key)
    if not _IDENTIFIER.fullmatch(item):
        raise BundleVerificationError("identifier_invalid", f"invalid identifier: {key}")
    return item


def _opaque_identifier(value: Mapping[str, Any], key: str) -> str:
    item = _text(value, key)
    if not _OPAQUE_IDENTIFIER.fullmatch(item):
        raise BundleVerificationError("opaque_identifier_invalid", f"invalid opaque identifier: {key}")
    return item


def _digest(value: Mapping[str, Any], key: str) -> str:
    item = _text(value, key)
    if not _SHA256.fullmatch(item):
        raise BundleVerificationError("digest_invalid", f"invalid SHA-256 digest: {key}")
    return item


def _commit(value: Mapping[str, Any], key: str) -> str:
    item = _text(value, key)
    if len(item) != 40 or any(character not in "0123456789abcdef" for character in item):
        raise BundleVerificationError("commit_invalid", f"invalid Git SHA: {key}")
    return item


def _uuid_text(value: Mapping[str, Any], key: str) -> str:
    item = _text(value, key)
    if not _UUID.fullmatch(item):
        raise BundleVerificationError("uuid_invalid", f"invalid UUID: {key}")
    return item


def _positive_int(value: Mapping[str, Any], key: str) -> int:
    item = value.get(key)
    if not isinstance(item, int) or isinstance(item, bool) or item <= 0:
        raise BundleVerificationError("positive_int_invalid", f"invalid positive integer: {key}")
    return item


def _boolean(value: Mapping[str, Any], key: str) -> bool:
    item = value.get(key)
    if not isinstance(item, bool):
        raise BundleVerificationError("boolean_invalid", f"invalid boolean: {key}")
    return item


def _strings(value: Mapping[str, Any], key: str) -> tuple[str, ...]:
    item = value.get(key)
    if not isinstance(item, Sequence) or isinstance(item, (str, bytes)):
        raise BundleVerificationError("string_array_invalid", f"invalid string array: {key}")
    if not all(isinstance(entry, str) and entry for entry in item):
        raise BundleVerificationError("string_array_invalid", f"invalid string array: {key}")
    return tuple(cast(Sequence[str], item))


def _safe_relative(value: str) -> str:
    path = Path(value)
    if path.is_absolute() or ".." in path.parts or not value or "\\" in value:
        raise BundleVerificationError("path_traversal", f"unsafe relative path: {value}")
    return path.as_posix()


def _require_equal(
    left: Mapping[str, Any], left_key: str, right: Mapping[str, Any], right_key: str, name: str
) -> None:
    if left.get(left_key) != right.get(right_key):
        raise BundleVerificationError("record_profile_identity_drift", f"{name} identity drift: {left_key}")
