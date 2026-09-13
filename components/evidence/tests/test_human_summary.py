from __future__ import annotations

import copy
import json
import unittest
from unittest.mock import patch

from sts2_platform_evidence import summarize_verified_human_bundle, verify_human_session_bundle
from tests import test_human_session_bundle_v3 as v3
from tests import test_human_session_bundle_v2 as v2


class HumanSummaryTests(unittest.TestCase):
    setUp = v3.HumanSessionBundleV3Tests.setUp
    tearDown = v3.HumanSessionBundleV3Tests.tearDown
    _write = v3.HumanSessionBundleV3Tests._write
    _stream = v3.HumanSessionBundleV3Tests._stream
    _rows = v3.HumanSessionBundleV3Tests._rows
    _bundle = v3.HumanSessionBundleV3Tests._bundle
    _object = v3.HumanSessionBundleV3Tests._object
    _reseal = v3.HumanSessionBundleV3Tests._reseal

    def test_current_summary_counts_dispositions_without_counting_diagnostics_or_normal_cancel(self):
        bundle = self._bundle()
        raw = bundle / "raw"
        recording = json.loads((raw / "recording-manifest.json").read_text())
        recording["disposition_schema_version"] = 1
        self._write(raw / "recording-manifest.json", recording)
        trace = self._rows(raw / "semantic-boundary-trace.jsonl")
        for suffix, terminal in (("cancel", "action_cancelled_before_start"),
                                 ("abort", "action_aborted_before_commit"),
                                 ("unknown", "transition_unknown")):
            event = copy.deepcopy(trace[0])
            action = event["action"]
            action.update(action_witness_id=suffix, record_id="record-" + suffix)
            action["decision"].update(decision_id="decision-" + suffix, causal_root_id=suffix)
            trace.append(event | {"sequence": len(trace) + 1})
            trace.append(event | {"sequence": len(trace) + 1, "kind": terminal})
        self._stream(raw / "semantic-boundary-trace.jsonl", trace)
        common = {"schema_version": 2, "schema": "sts2.human-annotator/invalidation-2",
                  "session_id": recording["session_id"], "run_id": "run-0001"}
        failed = [common | {"disposition": "diagnostic", "detail": "private diagnostic"},
                  common | {"disposition": "unsupported"},
                  common | {"disposition": "failed_closed", "decision_failure": {
                      "kind": "persistence", "decision_witness_id": "unknown", "action_family": "ordinary_combat.play_card"}},
                  common | {"disposition": "failed_closed", "decision_failure": {
                      "kind": "capture", "decision_witness_id": "capture-lost", "action_family": "ordinary_combat.play_card"},
                      "human_occurrence": {"occurrence_id": "capture-lost", "native_action_type": "PlayCardAction",
                          "family": "ordinary_combat.play_card", "verb": "play", "native_mechanism": "game_action",
                          "disposition": "failed_closed", "native_operands": {"card": "card-lost"}}}]
        self._stream(raw / "invalidations.jsonl", failed)
        audit = json.loads((bundle / "audit/audit-report.json").read_text())
        audit["invalidations"] = len(failed)
        self._write(bundle / "audit/audit-report.json", audit)
        self._reseal(bundle)
        value = verify_human_session_bundle(bundle).require_value()
        # Projection is already inside the successful typed value, so a caller
        # can inspect it after leaving verifier staging without any raw read.
        with patch("pathlib.Path.open", side_effect=AssertionError("unexpected evidence IO")):
            summary = summarize_verified_human_bundle(value)
        self.assertEqual(summary["counts"], {
            "accepted": 4, "proved": 1, "canonical": 1, "real_failures": 2,
            "cancelled": 1, "aborted": 1, "diagnostics": 1, "unsupported": 1,
            "unresolved": 1, "invalidations": 4, "accepted_children": 0,
            "canonical_children": 0, "compatibility_valid": 0, "compatibility_invalid": 0})
        self.assertNotIn("private diagnostic", json.dumps(summary))
        self.assertNotIn(str(self.root), json.dumps(summary))
        summary["counts"]["canonical"] = 999
        self.assertEqual(summarize_verified_human_bundle(value)["counts"]["canonical"], 1)

    def test_multiple_runs_use_exact_native_boundaries_not_session_or_polling(self):
        bundle = self._bundle()
        raw = bundle / "raw"
        journal = self._rows(raw / "run-journal.jsonl")
        start = journal[0]
        definitions = [("session_started", "run-0001", None),
                       ("run_started", "run-0001", "polling is not native start"),
                       ("run_started_native", "run-0001", None),
                       ("run_ended_native", "run-0001", "RunManager.OnEnded(isVictory=false)"),
                       ("run_resumed_native", "run-0002", None),
                       ("run_ended_native", "run-0002", "other historical wording"),
                       ("session_closed", "run-unassigned", None)]
        self._stream(raw / "run-journal.jsonl", [start | {"sequence": i + 1, "kind": kind,
            "run_id": run, "detail": detail, "recorded_at": f"2026-09-13T00:00:0{i}Z"}
            for i, (kind, run, detail) in enumerate(definitions)])
        manifest = json.loads((bundle / "session-bundle-manifest.json").read_text())
        manifest["run_ids"] = ["run-0001", "run-0002", "run-unassigned"]
        self._write(bundle / "session-bundle-manifest.json", manifest)
        self._reseal(bundle)
        summary = summarize_verified_human_bundle(verify_human_session_bundle(bundle).require_value())
        first, second, unassigned = summary["runs"]
        self.assertEqual((summary["native_starts"], summary["native_ends"]), (1, 2))
        self.assertEqual((first["start_observed"], first["terminal_observed"], first["outcome"]), (True, True, "defeat"))
        self.assertEqual(first["started_at"], "2026-09-13T00:00:02Z")
        self.assertEqual((second["start_observed"], second["native_resumes"], second["outcome"]), (False, 1, None))
        self.assertIsNone(second["started_at"])
        self.assertEqual(summary["assigned_run_count"], 2)
        self.assertFalse(unassigned["assigned"])

    def test_historical_disposition_and_v2_canonical_are_unknown_not_zero(self):
        current = summarize_verified_human_bundle(verify_human_session_bundle(self._bundle()).require_value())
        self.assertEqual(current["counts"]["canonical"], 1)
        self.assertIsNone(current["counts"]["real_failures"])
        helper = v2.HumanSessionBundleV2Tests()
        helper.root = self.root
        old = summarize_verified_human_bundle(verify_human_session_bundle(helper._bundle("v2")).require_value())
        self.assertEqual(old["format"], "archival")
        self.assertIsNone(old["counts"]["canonical"])
        self.assertIsNone(old["counts"]["real_failures"])
        self.assertIsNone(old["native_starts"])
        self.assertIsNone(old["runs"][0]["start_observed"])


if __name__ == "__main__":
    unittest.main()
