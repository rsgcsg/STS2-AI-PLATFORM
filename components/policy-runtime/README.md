# Policy Runtime

The Policy Runtime is a model-neutral Connector consumer. It owns controller
lifecycle, Human/Shadow/One-Step/Auto modes, stale refresh, delivery safety,
stable-successor polling, and Agent-run evidence. It does not own game legality,
model inference, or native operands.

The Connector supplies one complete ordered `BoundAction` catalog. A policy
adapter receives that exact Snapshot and Read bundle, echoes the catalog digest,
returns one score per candidate and an optional selected index, and never returns
an action object. The Runtime resolves the index against the same current catalog
and submits only that Connector-owned `bound_action_id`.

Before observation, each Manifest must exactly pin the Connector environment:
host kind, Connector version/source revision/artifact SHA-256/module version ID,
Modset status/fingerprint, and the complete ordered list of loaded Mod IDs. Any
field drift fails closed before Snapshot observation or policy scoring.

## Process boundary

`sts2-policy-runtime` starts a loopback service and a decision-only NDJSON child.
The child command is consumer-owned; the example below uses STPD:

```bash
npm --prefix components/policy-runtime run build
node components/policy-runtime/dist/cli.js \
  --manifest ../STPD/policy-manifests/s1-policy-adapter-v1.json \
  --adapter-command ../STPD/.venv/bin/python \
  --adapter-cwd ../STPD \
  --adapter-arg tools/policy_adapter.py \
  --adapter-arg=--manifest \
  --adapter-arg policy-manifests/s1-policy-adapter-v1.json
```

Arguments are passed literally. If a child argument begins with `--`, use the
`--adapter-arg=value` form. The service defaults to Connector
`http://127.0.0.1:15526` and Policy Runtime `http://127.0.0.1:15527`.

The command fails before startup when the policy artifact is absent, its
SHA-256 differs from the Policy Manifest, or the adapter's bounded code digest
drifts. The child must first attest that exact adapter identity; the loopback
service is not published until the parent verifies it. At runtime, any pinned
environment field drift fails before observation,
scoring or controller acquisition. Adapter decisions time out after 30 seconds
and return to Human before controller acquisition. The CLI publishes its exact
startup identity before enabling Shadow/Auto drive. `unknown` delivery taints the
run and is never retried. `POST /stop` or process termination releases the
controller and seals an Agent evidence directory bound to Runtime code, Manifest,
checkpoint and exact environment identity. The directory includes the canonical
Policy Manifest bytes and the exact child startup adapter attestation; Evidence
verification rejects any digest, identity or event-association drift.

## HTTP commands

- `GET /status`
- `POST /mode` with `{"mode":"human|shadow|one_step|auto"}`
- `POST /tick` with `{"max_ticks":1}`
- `POST /stop` with `{}`

The service is loopback-only. UI clients call these commands; they never submit
gameplay actions directly.
