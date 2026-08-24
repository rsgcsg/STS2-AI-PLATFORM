"""Independent verification for portable HumanSessionBundle V2 evidence."""

from __future__ import annotations

from collections import Counter
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .core import VerificationFinding, VerificationResult, VerifierDescriptor
from .human_session_bundle_v1 import (
    BundleVerificationError,
    _boolean,
    _digest,
    _identifier,
    _jsonl,
    _load_json,
    _object,
    _opaque_identifier,
    _positive_int,
    _read_checksums,
    _semantic_hash,
    _sha256_file,
    _strings,
    _text,
)

BUNDLE_SCHEMA = "sts2.human-annotator/session-bundle-2"
PROFILE_SCHEMA = "sts2.ai-platform/human-capture-profile-2"
RECORD_SCHEMA = "sts2.human-annotator/decision-record-2"
READ_SCHEMA = "sts2.human-annotator/read-evidence-2"
JOURNAL_SCHEMA = "sts2.human-annotator/run-journal-event-2"


@dataclass(frozen=True)
class HumanSessionBundleV2:
    directory: Path
    manifest: Mapping[str, Any]
    capture_profile: Mapping[str, Any]
    session_id: str
    timeline_id: str
    worker_id: str
    campaign_id: str
    profile_id: str
    bundle_content_id: str
    bundle_sha256: str
    export_sha256: str
    record_count: int
    run_ids: tuple[str, ...]
    invalidations: int
    materialized_reads: Mapping[str, int]

    @property
    def export_path(self) -> Path:
        return self.directory / "export" / "decisions.jsonl"


DESCRIPTOR: VerifierDescriptor[HumanSessionBundleV2] = VerifierDescriptor(
    "human-session-bundle-v2",
    BUNDLE_SCHEMA,
    2,
    HumanSessionBundleV2,
)


class HumanSessionBundleV2Verifier:
    descriptor = DESCRIPTOR

    def verify(
        self,
        source: str | Path,
        expected: Mapping[str, object] | None = None,
    ) -> VerificationResult[HumanSessionBundleV2]:
        directory = Path(source).resolve()
        try:
            value = self._verify(directory, expected)
            return VerificationResult(self.descriptor, "pass", directory, value)
        except BundleVerificationError as error:
            return VerificationResult(
                self.descriptor,
                "fail",
                directory,
                findings=(VerificationFinding(error.code, str(error), error.path),),
            )
        except (OSError, TypeError, ValueError) as error:
            return VerificationResult(
                self.descriptor,
                "fail",
                directory,
                findings=(VerificationFinding("malformed_bundle", str(error)),),
            )

    def _verify(
        self,
        directory: Path,
        expected: Mapping[str, object] | None,
    ) -> HumanSessionBundleV2:
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
            if _sha256_file(directory / relative) != expected_sha:
                raise BundleVerificationError(
                    "checksum_mismatch", f"bundle checksum mismatch: {relative}", relative
                )

        manifest = _load_json(directory / "session-bundle-manifest.json")
        if _text(manifest, "schema") != BUNDLE_SCHEMA or manifest.get("schema_version") != 2:
            raise BundleVerificationError("bundle_schema_mismatch", "unsupported V2 bundle schema")
        content_identity = _object(manifest, "content_identity")
        content_id = _digest(manifest, "bundle_content_id")
        if _semantic_hash(content_identity) != content_id:
            raise BundleVerificationError("content_identity_mismatch", "bundle content identity mismatch")

        profile = _load_json(directory / "profile" / "capture-profile.json")
        _validate_profile(profile)
        profile_sha = _semantic_hash(profile)
        if expected is not None:
            expected_profile_id = expected.get("profile_id")
            expected_profile_sha = expected.get("profile_sha256")
            if expected_profile_id is not None and expected_profile_id != profile.get("profile_id"):
                raise BundleVerificationError("capture_profile_drift", "capture profile ID drift")
            if expected_profile_sha is not None and expected_profile_sha != profile_sha:
                raise BundleVerificationError("capture_profile_drift", "capture profile digest drift")
        profile_id = _identifier(profile, "profile_id")
        if manifest.get("capture_profile_id") != profile_id:
            raise BundleVerificationError("capture_profile_drift", "bundle capture profile ID drift")
        if _digest(manifest, "capture_profile_sha256") != profile_sha:
            raise BundleVerificationError("capture_profile_drift", "bundle capture profile digest drift")

        attestation = _object(manifest, "human_origin_attestation")
        if not _boolean(attestation, "attested") or attestation.get("machine_verifiable") is not False:
            raise BundleVerificationError("attestation_missing", "bundle has no human-origin attestation")
        worker_id = _identifier(manifest, "worker_id")
        if attestation.get("worker_id") != worker_id:
            raise BundleVerificationError("attestation_worker_drift", "attestation worker differs")
        if _text(manifest, "audit_status") != "pass":
            raise BundleVerificationError("audit_status_failed", "bundle audit did not pass")
        audit = _load_json(directory / "audit" / "audit-report.json")
        if audit.get("status") != "pass" or audit.get("invalid_records") != 0:
            raise BundleVerificationError("audit_failed", "independent V2 audit did not pass")
        record_count = _positive_int(manifest, "record_count")
        if audit.get("valid_records") != record_count:
            raise BundleVerificationError("audit_count_mismatch", "audit count differs from manifest")

        export_path = directory / "export" / "decisions.jsonl"
        export_sha = _sha256_file(export_path)
        if export_sha != _digest(manifest, "export_sha256"):
            raise BundleVerificationError("export_digest_mismatch", "export digest differs from manifest")
        raw = directory / "raw"
        recording = _load_json(raw / "recording-manifest.json")
        session_id = _opaque_identifier(manifest, "session_id")
        timeline_id = _opaque_identifier(manifest, "timeline_id")
        if (
            recording.get("schema") != "sts2.human-annotator/recording-manifest-2"
            or recording.get("session_id") != session_id
            or recording.get("timeline_id") != timeline_id
            or recording.get("capture_profile_id") != profile_id
            or recording.get("capture_profile_sha256") != profile_sha
        ):
            raise BundleVerificationError("recording_manifest_drift", "raw manifest differs from V2 bundle")
        coverage = _load_json(raw / "coverage.json")
        if (
            coverage.get("schema") != "sts2.human-annotator/coverage-2"
            or coverage.get("session_id") != session_id
            or coverage.get("admitted_records") != record_count
        ):
            raise BundleVerificationError("coverage_mismatch", "raw coverage differs from V2 bundle")

        run_ids = tuple(_strings(manifest, "run_ids"))
        if not run_ids or len(run_ids) != len(set(run_ids)):
            raise BundleVerificationError("run_ids_invalid", "bundle run IDs must be unique")
        raw_lines = []
        for run_id in sorted(run_ids):
            path = raw / f"{run_id}.jsonl"
            if not path.is_file():
                raise BundleVerificationError("raw_run_missing", f"missing raw run: {run_id}")
            raw_lines.extend(path.read_text(encoding="utf-8").splitlines())
        expected_export = "".join(f"{line}\n" for line in raw_lines).encode("utf-8")
        if export_path.read_bytes() != expected_export:
            raise BundleVerificationError("raw_export_mismatch", "export is not deterministic raw output")

        raw_hashes = {
            path.relative_to(raw).as_posix(): _sha256_file(path)
            for path in sorted(raw.rglob("*"))
            if path.is_file()
        }
        expected_identity = {
            "schema": BUNDLE_SCHEMA,
            "session_id": session_id,
            "timeline_id": timeline_id,
            "capture_profile_id": profile_id,
            "capture_profile_sha256": profile_sha,
            "campaign_id": _identifier(manifest, "campaign_id"),
            "worker_id": worker_id,
            "human_origin_attestation": dict(attestation),
            "record_count": record_count,
            "run_ids": list(run_ids),
            "export_sha256": export_sha,
            "raw_file_sha256": raw_hashes,
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
                "bundle content identity differs from verified V2 facts",
            )

        required = _required_reads(profile)
        records = _jsonl(export_path)
        if len(records) != record_count:
            raise BundleVerificationError("export_count_mismatch", "export count differs from manifest")
        seen_ids: set[str] = set()
        seen_runs: set[str] = set()
        previous_sequence = 0
        read_counts: Counter[str] = Counter()
        for line_number, record in records:
            _validate_record(
                record,
                line_number=line_number,
                raw=raw,
                session_id=session_id,
                timeline_id=timeline_id,
                profile_id=profile_id,
                profile=profile,
                required_reads=required,
                seen_ids=seen_ids,
                previous_sequence=previous_sequence,
                read_counts=read_counts,
            )
            previous_sequence = int(record["sequence"])
            seen_runs.add(_text(record, "run_id"))
        if seen_runs != set(run_ids):
            raise BundleVerificationError("export_run_ids_mismatch", "export run IDs differ")
        _validate_journal(raw / "run-journal.jsonl", session_id, timeline_id)
        return HumanSessionBundleV2(
            directory,
            manifest,
            profile,
            session_id,
            timeline_id,
            worker_id,
            _identifier(manifest, "campaign_id"),
            profile_id,
            content_id,
            _sha256_file(checksums_path),
            export_sha,
            record_count,
            run_ids,
            int(audit.get("invalidations", 0)),
            dict(sorted(read_counts.items())),
        )


def _validate_profile(profile: Mapping[str, Any]) -> None:
    if profile.get("schema_version") != 2 or profile.get("schema") != PROFILE_SCHEMA:
        raise BundleVerificationError("capture_profile_invalid", "unsupported capture profile")
    _identifier(profile, "profile_id")
    if profile.get("record_schema") != RECORD_SCHEMA:
        raise BundleVerificationError("capture_profile_invalid", "profile record schema drift")
    families = _strings(profile, "supported_action_families")
    if not families or len(families) != len(set(families)):
        raise BundleVerificationError("capture_profile_invalid", "profile families are invalid")
    reads = profile.get("reads")
    if not isinstance(reads, Sequence) or isinstance(reads, (str, bytes)) or not reads:
        raise BundleVerificationError("capture_profile_invalid", "profile Reads are missing")
    keys = []
    for item in reads:
        if not isinstance(item, Mapping):
            raise BundleVerificationError("capture_profile_invalid", "profile Read is not an object")
        phase = _text(item, "phase")
        kind = _text(item, "kind")
        if phase not in {"pre", "successor"} or not isinstance(item.get("required"), bool):
            raise BundleVerificationError("capture_profile_invalid", "profile Read is invalid")
        keys.append((phase, kind))
    if len(keys) != len(set(keys)):
        raise BundleVerificationError("capture_profile_invalid", "profile Reads are duplicated")


def _required_reads(profile: Mapping[str, Any]) -> dict[str, set[str]]:
    result = {"pre": set(), "successor": set()}
    for value in profile["reads"]:
        if value.get("required") is True:
            result[str(value["phase"])].add(str(value["kind"]))
    return result


def _validate_record(
    record: Mapping[str, Any],
    *,
    line_number: int,
    raw: Path,
    session_id: str,
    timeline_id: str,
    profile_id: str,
    profile: Mapping[str, Any],
    required_reads: Mapping[str, set[str]],
    seen_ids: set[str],
    previous_sequence: int,
    read_counts: Counter[str],
) -> None:
    if record.get("schema_version") != 2 or record.get("schema") != RECORD_SCHEMA:
        raise BundleVerificationError("record_schema_drift", f"line {line_number} schema drift")
    if (
        record.get("session_id") != session_id
        or record.get("timeline_id") != timeline_id
        or record.get("capture_profile_id") != profile_id
    ):
        raise BundleVerificationError("record_identity_drift", f"line {line_number} identity drift")
    record_id = _text(record, "record_id")
    if record_id in seen_ids:
        raise BundleVerificationError("duplicate_record_id", f"duplicate record: {record_id}")
    seen_ids.add(record_id)
    sequence = _positive_int(record, "sequence")
    if sequence <= previous_sequence:
        raise BundleVerificationError("sequence_not_monotonic", f"line {line_number} sequence drift")
    if _object(record, "eligibility").get("status") != "admitted":
        raise BundleVerificationError("record_not_admitted", f"line {line_number} is not admitted")

    action = _object(record, "action")
    family = (
        "ordinary_combat.play_card"
        if record.get("decision_family") == "ordinary_combat" and action.get("verb") == "play"
        else f"{record.get('decision_family')}.{action.get('verb')}"
    )
    if family not in set(_strings(profile, "supported_action_families")):
        raise BundleVerificationError("action_family_outside_profile", f"unsupported family: {family}")
    environment = _object(record, "environment")
    runtime_id = _text(environment, "runtime_instance_id")
    environment_fingerprint = _text(environment, "environment_fingerprint")
    for phase in ("pre", "successor"):
        frame = _object(record, phase)
        snapshot_id = _text(frame, "snapshot_id")
        snapshot = _object(frame, "snapshot")
        if snapshot.get("snapshot_id") != snapshot_id:
            raise BundleVerificationError("snapshot_binding_drift", f"line {line_number} {phase}")
        reads = frame.get("reads")
        if not isinstance(reads, Sequence) or isinstance(reads, (str, bytes)):
            raise BundleVerificationError("read_evidence_invalid", f"line {line_number} {phase}")
        by_kind: dict[str, Mapping[str, Any]] = {}
        read_ids: set[str] = set()
        for item in reads:
            if not isinstance(item, Mapping) or item.get("schema") != READ_SCHEMA or item.get("schema_version") != 2:
                raise BundleVerificationError("read_evidence_invalid", f"line {line_number} {phase}")
            kind = _text(item, "kind")
            evidence_id = _text(item, "read_evidence_id")
            if kind in by_kind or evidence_id in read_ids:
                raise BundleVerificationError("read_evidence_duplicate", f"line {line_number} {phase}")
            by_kind[kind] = item
            read_ids.add(evidence_id)
            if (
                item.get("snapshot_id") != snapshot_id
                or item.get("runtime_instance_id") != runtime_id
                or item.get("environment_fingerprint") != environment_fingerprint
            ):
                raise BundleVerificationError("read_binding_drift", f"line {line_number} {phase}/{kind}")
            status = item.get("status")
            if status == "materialized":
                relative = _safe_blob_ref(_text(item, "payload_ref"))
                digest = _digest(item, "payload_sha256")
                blob = raw / relative
                if not blob.is_file() or _sha256_file(blob) != digest:
                    raise BundleVerificationError("read_blob_missing_or_changed", relative, relative)
                _text(item, "content_schema")
                if not isinstance(item.get("completeness"), Mapping):
                    raise BundleVerificationError("read_completeness_missing", relative, relative)
                read_counts[kind] += 1
            elif status not in {"not_available", "failed", "stale"}:
                raise BundleVerificationError("read_status_invalid", f"line {line_number} {phase}/{kind}")
        missing = required_reads[phase] - {
            kind for kind, value in by_kind.items() if value.get("status") == "materialized"
        }
        if missing:
            raise BundleVerificationError(
                "required_read_missing",
                f"line {line_number} {phase} missing materialized Reads: {sorted(missing)}",
            )


def _safe_blob_ref(value: str) -> str:
    path = Path(value)
    if (
        path.is_absolute()
        or ".." in path.parts
        or "\\" in value
        or len(path.parts) != 4
        or path.parts[:2] != ("blobs", "sha256")
        or path.suffix != ".json"
    ):
        raise BundleVerificationError("read_blob_path_invalid", f"unsafe Read blob path: {value}")
    return path.as_posix()


def _validate_journal(path: Path, session_id: str, timeline_id: str) -> None:
    events = _jsonl(path)
    if not events:
        raise BundleVerificationError("run_journal_empty", "V2 run journal is empty")
    previous = 0
    for line_number, event in events:
        if (
            event.get("schema") != JOURNAL_SCHEMA
            or event.get("schema_version") != 2
            or event.get("session_id") != session_id
            or event.get("timeline_id") != timeline_id
        ):
            raise BundleVerificationError("run_journal_invalid", f"journal line {line_number}")
        sequence = _positive_int(event, "sequence")
        if sequence <= previous:
            raise BundleVerificationError("run_journal_sequence_invalid", f"journal line {line_number}")
        previous = sequence
