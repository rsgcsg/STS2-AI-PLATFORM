"""Generic typed verifier registry primitives.

The core deliberately knows nothing about a producer implementation or a
particular evidence format. Adapters register their own typed descriptor and
return a typed verification result.
"""

from __future__ import annotations

from collections.abc import Callable, Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Generic, TypeVar

T = TypeVar("T")


@dataclass(frozen=True)
class VerifierDescriptor(Generic[T]):
    type_id: str
    schema: str
    version: int
    value_type: type[T]

    def __post_init__(self) -> None:
        if not self.type_id or not self.schema or self.version < 1:
            raise ValueError("verifier descriptor identity is incomplete")


@dataclass(frozen=True)
class VerificationFinding:
    code: str
    detail: str
    path: str | None = None


@dataclass(frozen=True)
class VerificationResult(Generic[T]):
    descriptor: VerifierDescriptor[T]
    status: str
    source: Path
    value: T | None = None
    findings: tuple[VerificationFinding, ...] = ()

    @property
    def passed(self) -> bool:
        return self.status == "pass"

    @property
    def failed(self) -> bool:
        return self.status == "fail"

    def require_value(self) -> T:
        if not self.passed or self.value is None:
            detail = "; ".join(finding.detail for finding in self.findings)
            raise ValueError(detail or "verification did not pass")
        return self.value


Verifier = Callable[[Path, Mapping[str, object] | None], VerificationResult[T]]


class VerifierRegistry:
    """Typed registry with fail-closed duplicate and unknown handling."""

    def __init__(self) -> None:
        self._entries: dict[str, tuple[VerifierDescriptor[object], Verifier[object]]] = {}

    def register(
        self,
        descriptor: VerifierDescriptor[T],
        verifier: Verifier[T],
    ) -> None:
        if descriptor.type_id in self._entries:
            raise ValueError(f"verifier already registered: {descriptor.type_id}")
        self._entries[descriptor.type_id] = (
            descriptor,  # type: ignore[assignment]
            verifier,  # type: ignore[assignment]
        )

    def descriptor(self, type_id: str) -> VerifierDescriptor[object]:
        try:
            return self._entries[type_id][0]
        except KeyError as error:
            raise KeyError(f"unknown verifier: {type_id}") from error

    def verify(
        self,
        type_id: str,
        source: str | Path,
        expected: Mapping[str, object] | None = None,
    ) -> VerificationResult[object]:
        try:
            _, verifier = self._entries[type_id]
        except KeyError as error:
            raise KeyError(f"unknown verifier: {type_id}") from error
        return verifier(Path(source), expected)
