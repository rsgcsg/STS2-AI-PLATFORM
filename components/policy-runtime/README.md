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

## Standalone consumer package

Version `0.1.0-rc.2` provides a candidate package for external consumers. Build
from a committed component checkout with the checked-in lockfile:

```bash
npm ci
npm --prefix components/policy-runtime run check
npm --prefix components/policy-runtime run package -- --output /absolute/package-output
```

The last command creates `rsgcsg-sts2-policy-runtime-0.1.0-rc.2.tgz`,
`policy-runtime-package.json` and `checksums.sha256`. It requires committed
component source and does not publish anything. The package contains compiled
JavaScript/declarations, CLI entries, license, a component identity record and
an npm production shrinkwrap. It has no dependency on a sibling source tree.
The Connector SDK uses the exact `consumer-sdk/v1.1.0-rc.1` release URL and
SHA-512 integrity from this component's lockfile; its transitive dependencies
are also frozen in the packaged shrinkwrap.

Consumers verify the tarball SHA-256 and install that exact tarball/release URL
with their own lockfile. Invoke the installed `sts2-policy-runtime` executable,
or import the public `@rsgcsg/sts2-policy-runtime` package and `./child-port`
export. Pin the component source revision, source and public-contract digests,
package SHA-256/integrity and protocol from `policy-runtime-package.json`.
Never substitute a floating branch or raw source import for that pin.
`check:package` packs twice, compares bytes, installs outside the workspace,
checks resolved SDK integrity, exercises synthetic modes and starts/stops the
installed CLI in Human mode. This is CPU package evidence with no game contact.
It does not establish real-model, game, Full-Run or causal-successor evidence.

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
checkpoint and exact environment identity. After stop succeeds and the response finishes or disconnects,
the CLI closes its HTTP service and adapter child and exits; an embedding
library owner retains lifecycle control unless it supplies `onStopped`. The directory includes the canonical
Policy Manifest bytes and the exact child startup adapter attestation; Evidence
verification rejects any digest, identity or event-association drift.

## HTTP commands

- `GET /status`
- `POST /mode` with `{"mode":"human|shadow|one_step|auto"}`
- `POST /tick` with `{"max_ticks":1}`
- `POST /stop` with `{}`

The service is loopback-only. Every POST requires `Content-Type: application/json`
(optional UTF-8 charset), a literal supported loopback Host with the bound port,
and either no Origin (local service clients) or the exact same HTTP origin.
Cross-origin browser requests, plain-text POSTs and rebound Host headers are
rejected before Runtime dispatch. UI clients call these commands; they never
submit gameplay actions directly.

Command callers must distinguish their HTTP wait from Runtime execution. A
request timeout or malformed response after POST does not establish that the
command was unapplied. Query status, permit an explicit Human handoff or stop,
and never automatically resubmit a tick. A stopped Runtime cannot be restarted
through HTTP; launch a fresh process/run for a new exact model/Manifest.

The `successor` event is a distinct same-environment non-settling observation
obtained by polling. It is not a native causal `S'` certificate. Agent events
bind decision metadata/scores, Receipt and successor but do not archive every
pre-decision Snapshot/Read input. Research projections and evaluation protocols
remain external consumers' responsibility.
