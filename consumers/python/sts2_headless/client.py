from __future__ import annotations

from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass
import json
import subprocess
from typing import Any, Iterable, Mapping, Sequence
from uuid import uuid4


class DriverError(RuntimeError):
    pass


@dataclass(frozen=True)
class FiniteActionView:
    snapshot_id: str
    action_ids: tuple[str, ...]
    actions: tuple[Mapping[str, Any], ...]

    @classmethod
    def from_snapshot(cls, snapshot: Mapping[str, Any]) -> "FiniteActionView":
        catalog = snapshot.get("bound_actions")
        if not isinstance(catalog, Mapping) or catalog.get("status") != "complete":
            raise DriverError("Snapshot does not contain a complete finite BoundAction projection.")
        actions = catalog.get("actions")
        if not isinstance(actions, list):
            raise DriverError("Snapshot BoundActions must be a list.")
        ids = tuple(str(action["bound_action_id"]) for action in actions)
        if len(ids) != len(set(ids)):
            raise DriverError("Snapshot contains duplicate BoundAction identities.")
        return cls(str(snapshot["snapshot_id"]), ids, tuple(actions))


class ManagedPlayerEnvironment:
    def __init__(self, command: Sequence[str]):
        if not command:
            raise ValueError("A non-empty driver command is required.")
        self._process = subprocess.Popen(
            list(command),
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            bufsize=1,
        )
        self._closed = False
        self.ready = self._read_message()
        if self.ready.get("type") != "ready":
            self.close(force=True)
            raise DriverError(f"Driver did not become ready: {self.ready!r}")

    def _read_message(self) -> dict[str, Any]:
        assert self._process.stdout is not None
        line = self._process.stdout.readline()
        if not line:
            stderr = ""
            if self._process.stderr is not None:
                stderr = self._process.stderr.read()
            raise DriverError(f"Driver exited before replying: {stderr.strip()}")
        value = json.loads(line)
        if not isinstance(value, dict):
            raise DriverError("Driver response must be a JSON object.")
        return value

    def _exchange(self, command: str, **payload: Any) -> dict[str, Any]:
        if self._closed:
            raise DriverError("Driver is closed.")
        request_id = uuid4().hex
        request = {"command": command, "request_id": request_id, **payload}
        assert self._process.stdin is not None
        self._process.stdin.write(json.dumps(request, separators=(",", ":")) + "\n")
        self._process.stdin.flush()
        response = self._read_message()
        if response.get("request_id") != request_id:
            raise DriverError("Driver response request identity mismatch.")
        if response.get("type") == "error":
            raise DriverError(str(response.get("message", "driver request failed")))
        return response

    def reset(self, seed: str) -> Mapping[str, Any]:
        return self._exchange("reset", seed=seed)["snapshot"]

    def observe(self) -> Mapping[str, Any]:
        return self._exchange("observe")["snapshot"]

    def read(self, read_id: str, expected_snapshot_id: str) -> Mapping[str, Any]:
        return self._exchange(
            "read", read_id=read_id, expected_snapshot_id=expected_snapshot_id
        )["read"]

    def step(
        self,
        bound_action_id: str,
        expected_snapshot_id: str,
        request_id: str | None = None,
    ) -> Mapping[str, Any]:
        return self._exchange(
            "step",
            bound_action_id=bound_action_id,
            expected_snapshot_id=expected_snapshot_id,
            mutation_request_id=request_id or uuid4().hex,
        )["receipt"]

    def episode_identity(self) -> Mapping[str, Any]:
        return self._exchange("episode_identity")["identity"]

    def close(self, force: bool = False) -> None:
        if self._closed:
            return
        try:
            if not force and self._process.poll() is None:
                self._exchange("close")
        finally:
            self._closed = True
            if self._process.poll() is None:
                self._process.terminate()
            try:
                self._process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self._process.kill()
                self._process.wait(timeout=5)
            for stream in (self._process.stdin, self._process.stdout, self._process.stderr):
                if stream is not None and not stream.closed:
                    stream.close()

    def __enter__(self) -> "ManagedPlayerEnvironment":
        return self

    def __exit__(self, *_: object) -> None:
        self.close()


class SyncVectorPlayerEnvironment:
    def __init__(self, environments: Iterable[ManagedPlayerEnvironment]):
        self.environments = tuple(environments)
        if not self.environments:
            raise ValueError("At least one environment is required.")

    def reset(self, seeds: Sequence[str]) -> tuple[Mapping[str, Any], ...]:
        if len(seeds) != len(self.environments):
            raise ValueError("One seed per environment is required.")
        return tuple(environment.reset(seed) for environment, seed in zip(self.environments, seeds))

    def step(
        self, actions: Sequence[tuple[str, str]]
    ) -> tuple[Mapping[str, Any], ...]:
        if len(actions) != len(self.environments):
            raise ValueError("One action per environment is required.")
        return tuple(
            environment.step(bound_action_id, snapshot_id)
            for environment, (bound_action_id, snapshot_id) in zip(self.environments, actions)
        )

    def close(self) -> None:
        for environment in self.environments:
            environment.close()


class ThreadedVectorPlayerEnvironment(SyncVectorPlayerEnvironment):
    def reset(self, seeds: Sequence[str]) -> tuple[Mapping[str, Any], ...]:
        if len(seeds) != len(self.environments):
            raise ValueError("One seed per environment is required.")
        pairs = tuple(zip(self.environments, seeds))
        with ThreadPoolExecutor(max_workers=len(pairs)) as executor:
            return tuple(executor.map(lambda pair: pair[0].reset(pair[1]), pairs))

    def step(
        self, actions: Sequence[tuple[str, str]]
    ) -> tuple[Mapping[str, Any], ...]:
        if len(actions) != len(self.environments):
            raise ValueError("One action per environment is required.")
        pairs = tuple(zip(self.environments, actions))
        with ThreadPoolExecutor(max_workers=len(pairs)) as executor:
            return tuple(executor.map(
                lambda pair: pair[0].step(pair[1][0], pair[1][1]), pairs
            ))
