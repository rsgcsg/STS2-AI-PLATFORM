"""Version-aware HumanSessionBundle verification without format guessing."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from .core import VerificationFinding, VerificationResult, VerifierDescriptor
from .human_session_bundle_v1 import (
    BUNDLE_SCHEMA as BUNDLE_V1_SCHEMA,
    CollectionProfile,
    HumanSessionBundle,
    HumanSessionBundleVerifier,
    load_collection_profile,
)
from .human_session_bundle_v2 import (
    BUNDLE_SCHEMA as BUNDLE_V2_SCHEMA,
    HumanSessionBundleV2,
    HumanSessionBundleV2Verifier,
)

VerifiedHumanSessionBundle = HumanSessionBundle | HumanSessionBundleV2

DESCRIPTOR: VerifierDescriptor[VerifiedHumanSessionBundle] = VerifierDescriptor(
    "human-session-bundle",
    "sts2.human-annotator/session-bundle",
    2,
    HumanSessionBundle,
)


class VersionedHumanSessionBundleVerifier:
    """Dispatch only from the explicit bundle schema in the immutable manifest."""

    descriptor = DESCRIPTOR

    def verify(
        self,
        source: str | Path,
        expected: CollectionProfile | dict[str, object] | None = None,
    ) -> VerificationResult[Any]:
        directory = Path(source).resolve()
        try:
            value = json.loads(
                (directory / "session-bundle-manifest.json").read_text(encoding="utf-8")
            )
            schema = value.get("schema") if isinstance(value, dict) else None
        except (OSError, ValueError) as error:
            return VerificationResult(
                self.descriptor,
                "fail",
                directory,
                findings=(VerificationFinding("malformed_bundle", str(error)),),
            )
        if schema == BUNDLE_V1_SCHEMA:
            return HumanSessionBundleVerifier().verify(directory, expected)
        if schema == BUNDLE_V2_SCHEMA:
            if isinstance(expected, CollectionProfile):
                return VerificationResult(
                    self.descriptor,
                    "fail",
                    directory,
                    findings=(VerificationFinding(
                        "expected_profile_kind_mismatch",
                        "a V1 collection profile cannot admit a V2 capture profile",
                    ),),
                )
            v2_expected = expected if isinstance(expected, dict) else None
            return HumanSessionBundleV2Verifier().verify(directory, v2_expected)
        return VerificationResult(
            self.descriptor,
            "fail",
            directory,
            findings=(VerificationFinding("bundle_schema_mismatch", f"unsupported schema: {schema}"),),
        )


def verify_human_session_bundle(
    source: str | Path,
    expected: CollectionProfile | dict[str, object] | None = None,
) -> VerificationResult[Any]:
    return VersionedHumanSessionBundleVerifier().verify(source, expected)


__all__ = [
    "BUNDLE_V1_SCHEMA",
    "BUNDLE_V2_SCHEMA",
    "CollectionProfile",
    "HumanSessionBundle",
    "HumanSessionBundleV2",
    "HumanSessionBundleVerifier",
    "HumanSessionBundleV2Verifier",
    "VersionedHumanSessionBundleVerifier",
    "VerifiedHumanSessionBundle",
    "load_collection_profile",
    "verify_human_session_bundle",
]
