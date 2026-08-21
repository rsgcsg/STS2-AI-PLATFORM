# Python Consumer

This package is a strategy-free external consumer of the same canonical Player
Environment used by Headless qualification. It does not define game legality,
rewards, tensors, or STPD policy.

```bash
python3 -m pip install -e consumers/python
sts2-headless-smoke --candidate .local/candidates/<exact-candidate>
```

`ManagedPlayerEnvironment` exposes `reset`, `observe`, state-bound `read`, and
exact BoundAction `step`. `FiniteActionView` is a consumer projection over the
complete action catalog. `SyncVectorPlayerEnvironment` only coordinates
independent environments; each still has one Host-local binding/executor.

The current JSONL driver is a development transport for the managed candidate,
not a new gameplay protocol or release support claim.
