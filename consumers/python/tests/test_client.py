import sys
import unittest

from sts2_headless.client import DriverError, FiniteActionView, ManagedPlayerEnvironment


FAKE_DRIVER = r'''
import json, sys
snapshot = {
  "snapshot_id":"s1",
  "bound_actions":{"status":"complete","actions":[{"bound_action_id":"a1","verb":"activate"}]}
}
print(json.dumps({"type":"ready","protocol":"test"}), flush=True)
for line in sys.stdin:
    request = json.loads(line)
    base = {"request_id":request["request_id"]}
    if request["command"] == "reset":
        print(json.dumps({**base,"type":"reset_result","snapshot":snapshot}), flush=True)
    elif request["command"] == "observe":
        print(json.dumps({**base,"type":"observe_result","snapshot":snapshot}), flush=True)
    elif request["command"] == "read":
        print(json.dumps({**base,"type":"read_result","read":{"kind":"detail"}}), flush=True)
    elif request["command"] == "step":
        print(json.dumps({**base,"type":"step_result","receipt":{"delivery":"delivered","successor":snapshot}}), flush=True)
    elif request["command"] == "close":
        print(json.dumps({**base,"type":"close_result","exit":{"code":0}}), flush=True)
        break
'''


class ClientTest(unittest.TestCase):
    def test_round_trip_and_finite_projection(self):
        with ManagedPlayerEnvironment([sys.executable, "-u", "-c", FAKE_DRIVER]) as environment:
            snapshot = environment.reset("SEED")
            view = FiniteActionView.from_snapshot(snapshot)
            self.assertEqual(view.action_ids, ("a1",))
            receipt = environment.step(view.action_ids[0], view.snapshot_id)
            self.assertEqual(receipt["delivery"], "delivered")
            self.assertEqual(environment.observe()["snapshot_id"], "s1")

    def test_rejects_incomplete_action_projection(self):
        with self.assertRaises(DriverError):
            FiniteActionView.from_snapshot({"snapshot_id": "s", "bound_actions": {"status": "partial"}})


if __name__ == "__main__":
    unittest.main()
