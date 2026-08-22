from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys
from uuid import uuid4

from .client import DriverError, FiniteActionView, ManagedPlayerEnvironment


def _same_session(before: dict, after: dict) -> bool:
    return before.get("session") == after.get("session")


def main() -> None:
    parser = argparse.ArgumentParser(description="Run an external Python Player Environment smoke.")
    parser.add_argument("--candidate", required=True)
    parser.add_argument("--game-dir")
    parser.add_argument("--seed", default="H1PYTHONSMOKE01")
    parser.add_argument("--max-actions", type=int, default=64)
    parser.add_argument("--evidence-file")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[3]
    command = ["node", str(root / "tools" / "managed-pe-driver.mjs"), "--candidate", args.candidate]
    if args.game_dir:
        command.extend(["--game-dir", args.game_dir])
    reads = 0
    delivered = 0
    terminal = "action_limit"
    with ManagedPlayerEnvironment(command) as environment:
        snapshot = environment.reset(args.seed)
        identity = environment.episode_identity()
        provenance = identity.get("episode_provenance", {})
        if (
            provenance.get("verdict") != "provenance_pass"
            or provenance.get("requested_seed") != args.seed
            or provenance.get("actual_seed") != args.seed
        ):
            raise DriverError("Reset did not prove the requested episode seed.")
        for _ in range(args.max_actions):
            for descriptor in snapshot.get("reads", []):
                environment.read(descriptor["read_id"], snapshot["snapshot_id"])
                reads += 1
            if snapshot.get("interaction", {}).get("kind") == "game_over":
                terminal = "game_over"
                break
            view = FiniteActionView.from_snapshot(snapshot)
            if not view.action_ids:
                raise DriverError("Stable non-terminal snapshot has no executable BoundAction.")
            mutation_request_id = uuid4().hex
            selected_action_id = view.action_ids[0]
            receipt = environment.step(
                selected_action_id, view.snapshot_id, mutation_request_id
            )
            if receipt.get("delivery") != "delivered" or receipt.get("successor") is None:
                raise DriverError(
                    f"Environment-invalid delivery: {receipt.get('delivery')}:{receipt.get('reason_code')}"
                )
            if receipt.get("request_id") != mutation_request_id:
                raise DriverError("Receipt mutation request identity mismatch.")
            if receipt.get("action", {}).get("bound_action_id") != selected_action_id:
                raise DriverError("Receipt BoundAction identity mismatch.")
            if not _same_session(snapshot, receipt["successor"]):
                raise DriverError("Runtime/environment identity changed during the episode.")
            delivered += 1
            snapshot = receipt["successor"]
        report = {
            "status": "environment_smoke_pass",
            "seed": args.seed,
            "terminal": terminal,
            "actions_delivered": delivered,
            "reads_completed": reads,
            "final_snapshot_status": snapshot.get("status"),
            "final_interaction_kind": snapshot.get("interaction", {}).get("kind"),
            "final_completeness": snapshot.get("completeness"),
            "headless": environment.ready.get("headless"),
            "candidate_build": environment.ready.get("candidate_build"),
            "runtime_identity": environment.ready.get("runtime_identity"),
            "adapter_runtime_instance_id": environment.ready.get("adapter_runtime_instance_id"),
            "episode_provenance": provenance,
            "non_claims": [
                "This deterministic external consumer is not a learning or policy-transfer result.",
                "One complete episode is not long-run reliability evidence.",
            ],
        }
        rendered = json.dumps(report, indent=2)
        if args.evidence_file:
            evidence_file = Path(args.evidence_file)
            evidence_file.parent.mkdir(parents=True, exist_ok=True)
            evidence_file.write_text(rendered + "\n", encoding="utf-8")
        print(rendered)


if __name__ == "__main__":
    main()
