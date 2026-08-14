# Player Environment Protocol

Source protocol: `1.0-rc.2`

## Endpoints

```text
GET  /api/player-environment/capabilities
GET  /api/player-environment/snapshot
GET  /api/player-environment/reads/{read_id}?expected_snapshot_id=...
POST /api/player-environment/clients/register
GET  /api/player-environment/controller
POST /api/player-environment/controller/acquire|renew|release
POST /api/player-environment/actions
GET  /api/player-environment/actions/{request_id}
POST /api/player-environment/evidence/native-pages/sessions
GET  /api/player-environment/evidence/native-pages/sessions/{session_id}
POST /api/player-environment/evidence/native-pages/sessions/{session_id}/return|recover
```

## Snapshot

Capabilities carry Host/game/Modset identity, environment fingerprint and
optional Host implementation provenance. The hot Snapshot carries:

```text
snapshot_id, sequence, status, persistent,
interaction { interaction_id, kind, stage, prompt, content_schema, content, capabilities[] },
referents[], reads[], completeness,
bound_actions { status, counts, limit, ordering_semantics, actions[] },
session { runtime_instance_id, environment_fingerprint }, information_policy
```

A referent is a player-visible object or control identity. Facts create
referents independently of action publication. Exact screen, room, hand, slot
and annotation-input bindings never enter Surface facts. Optional `enabled`,
`selected` and `focused` are observed state, not global legality; current C1
does not yet claim keyboard/controller focus coverage. Interaction capabilities
describe current verbs and participant roles without enumerating operand
tuples. A finite bound action has one optional `subject_referent_id` plus role-labelled
`arguments[]`; each reference must exist in the current snapshot. Exact native
operands stay inside the Host.

`bound_actions.status=complete` proves every current finite binding was
materialized. `truncated` preserves the Snapshot but grants no consumer input
authority or interaction capability. Every public operand must already name a
current visible Referent. Counts, limit and deterministic ordering make loss
auditable. `status=interactive` is valid exactly when the complete projection
is non-empty.

`status=settling` means the Host has proved a bounded native no-input
lifecycle, not an unsupported interaction. This includes combat/room handoffs,
run-state mounting, and the short `menu_or_no_run` gap while a standard run or
the main menu mounts. The last case is capped at ten seconds and cannot hide a
real modal, menu, run owner, or unknown source. The current exact-runtime bound
is twenty seconds; after it expires the state fails
closed as visible unsupported.

`reads[]` advertises all bounded, non-authorizing information reads. Consumers
send the opaque `read_id`; C rejects stale snapshots and arbitrary fields.
Interactive consumers may read lazily. Memoryless consumers may prefetch and
aggregate selected advertised reads, but every result must retain the same
snapshot, runtime and environment identity; that aggregation is a downstream
projection, not a different C ontology.

## Action And Receipt

An action request contains request ID, expected snapshot ID, opaque bound-action
ID and controller lease identity. C rebuilds the interaction, referents and
exact native binding immediately before delivery.

Receipts are `delivered`, `not_delivered` or `unknown`. Delivered proves native input
delivery, not business completion. Unknown delivery never permits automatic
retry. The receipt repeats the public subject/arguments and may include an
immediate successor.

## Current Host Scope

The current product is a Live Host embedded in the real game. Headless,
reset/seed control, save/load, clone/fork, fast-step, scenario mutation,
training rewards and tensors are outside this contract and repository scope.

## Exclusions

C contains no source authority, SourceContract, business Outcome, hidden state,
arbitrary reflection, coordinate input, model-generated native operand,
strategy, reward or privileged simulator control.

## Native-Page Evidence

`native_pages.v1` is a default-off operator evidence profile, not a consumer
action API. Sessions are snapshot/runtime bound, reserve the current input owner,
suppress mutation, verify open/read/return and expose explicit recovery. They do
not create mutation authority or enter the action ledger. A successful return
requires the exact page close path and restoration of the pre-page input owner
in the same runtime. Opening and closing a real native page may advance the
Snapshot, so `post_snapshot_id` is a fresh successor rather than a promise that
the old token becomes current again. Any prior Snapshot/Read/action token stays
stale.
