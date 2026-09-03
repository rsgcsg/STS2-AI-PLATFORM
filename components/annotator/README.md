# STS2 Native UI Human Annotator

> This is the Human Annotator component of `rsgcsg/STS2-AI-PLATFORM`. It uses
> the in-repository Host Runtime and Connector components; sibling checkouts are
> no longer part of the current development contract.

The Human Annotator records bounded human decisions made through the shipped
Slay the Spire 2 UI. It preserves the exact Human observation, complete frozen
`BoundAction` catalog, exact process-local mapping, native action
identity/lifecycle, and later authoritative Player Environment observations as
distinct facts. Offline calibration, not a runtime disposition label, decides
whether those facts satisfy a particular research transition contract.

The recorder observes. It does not click, enqueue actions, reconstruct legality,
or expose native references on a wire.

## Current Status

- Implemented at source/test: ordinary Combat `play` (including an exact target
  when present), `end_turn`, append-only recording, independent audit/export,
  exact artifact identity, and fail-closed STPD import. Exact-artifact Live
  evidence is stated separately and never inferred from this implementation.
- The current recording format uses wire schemas ending in `-2` for the
  read-rich frame, Decision, manifest, journal, audit and bundle contracts.
  These wire names are preserved for evidence identity; they are one current
  format, not a parallel V2 product. Current CLR paths use
  `CurrentDecisionRecord`, `CurrentRecordingManifest`, `RecordingSessionStore`,
  `RecordingSessionAuditor` and `SessionBundlePacker`.
- The additive schema-2 timeline is bounded Human-proved for its historical
  trace contract in ordinary combat and generated-card select. Current Full-Run
  source/test coverage adds lethal-to-reward settlement, reward claim/proceed,
  card reward select and map travel without adding another recording format.
  Neither the schema name nor `transition_proved` grants canonical one-step
  eligibility.
- Current source writes semantic evidence schema 4: ordered lifecycle and
  disposition events reference content-addressed Human and boundary frames,
  plus a typed execution semantic action-space sidecar with the exact Human
  `BoundActionId` and native binding phase. The independent auditor resolves
  and hashes every reference and applies the trace-level causal invariants.
  Historical schema-1/2/3 traces are handled only by explicit archival readers;
  the current audit requires current schema containers. The action-space
  sidecar is current schema 2 (schema 1 remains readable only to archival
  callers); missing or mismatched native evidence fails closed, while
  calibration joins proved candidates to the durable canonical stream rather
  than creating a second authority.
- A typed RecordingService exposes Query/Status, Command and ordered Event
  contracts to Platform views. Runtime startup is `Ready`; `StartNewSession`
  opens an isolated session, Pause/Resume gate new witness admission, and Close
  immediately marks any final unresolved root as
  `session_closed_before_successor_boundary` before the normal durable flush.
  A closed session can be followed by another session in the same STS2 process.
  The service never invokes a game action.
- RecordingStatus revision 3 exposes the CaptureProfile boundary as four
  explicit views: recorded, native-accepted-but-failed-closed,
  supported-not-observed, and declared out of scope. A family may have both
  successful records and accepted failures, but a rejected native UI attempt is
  never counted as either a HumanDecision or a recording failure.
- The current store writes one semantic boundary stream, one canonical stream,
  immutable frame/action-space objects and operational journal/coverage data.
  It does not create or consult `native-action-ledger.jsonl`; that sidecar and
  its schema-1/2 validator remain isolated archival readers for predecessor
  evidence. Rapid overlap remains fail-closed in the current tracker and never
  treats a later decision frame as an earlier action's successor.
- Builds from the exact local STS2 `v0.111.0` assembly on macOS arm64 and
  Windows x64. Windows discovery and process inspection use the Platform Host
  Runtime component.
- A predecessor V1 exact-root artifact is owner-validated with a 28-record
  ordinary-combat session: 14 targeted plays, 9 untargeted plays and 5 end
  turns. Independent audit accepted all 28 with zero rejected records; one
  overlapping action failed closed before it could become a record. An earlier
  same-artifact 20-record run independently passed audit/export and strict STPD
  B0 with zero rejected records and no `mapping_zero`.
  Earlier exact artifacts contribute 170 predecessor records but do not lend
  identity or authority to the current artifact. The predecessor Decision V2
  profile is historical; broader current semantic coverage is tracked
  separately.

Unified artifact `06f62285... / 17981f40...` has two owner-operated sessions
that independently audit 39/39 records: 25 card plays and 14 end turns, with 158
materialized Reads and zero Read failures. Current source additionally delays a
capture-failure invalidation until STS2 accepts the expected native action;
cancelled, invalid-target and otherwise rejected UI attempts are not decisions.
That final status-semantics change requires a new exact-runtime validation and
does not inherit the 39-record evidence. It is now loaded as
`887630f4... / 14761ed4...`, but its current runtime remains Ready/no-session;
accepted-only accounting is therefore not inherited from predecessor evidence.
Owner session `session-20260826T025703Z-...` on that exact artifact now audits
19/19 records (10 play, nine end-turn), 78 Reads and zero Read failures. Sixteen
native-accepted card actions failed closed and remained outside admitted
records. One owner-observed cancelled attempt emitted no evidence, as designed;
that negative-action attribution is owner-attested rather than machine-proven.

Rapid-input artifact `080701b3... / 142054a5...` was subsequently cold-loaded
and owner-operated in runtime `39fa2d2e...`. Closed session
`session-20260826T062916Z-...` audits 12/12 strict records and its additive v1
ledger contains 35 accepted, started and finished roots: 12 strict-admitted and
23 strict-invalidated, with zero unresolved lifecycle. This proves accounting
and no false strict successor for the observed bursts. Current ledger v2 source
adds frozen decision/BoundAction evidence for invalidated roots; that additive
revision does not inherit this predecessor Live claim. It is now cold-loaded as
`df5d2c61... / 9072e515...` in runtime `ebe7a9fc...`; owner rapid-input
validation remains pending.

Semantic-boundary artifact `2cb46ead... / 66ed1396...` then produced owner
session `session-20260826T141755Z-...`: all 22 accepted actions were accounted,
including a real generated-card choice, but the run disproved acceptance-order
settlement. The strengthened audit rejects one End Turn proof whose earlier pre
frame crossed the choice effect. Current source uses execution-order settlement
and requires a complete pre-execution rebind; it needs a new exact artifact
canary and does not inherit that predecessor sidecar claim.

Implementation or build evidence is not human-origin evidence. See
[Status](docs/STATUS.md) and [Evidence](docs/EVIDENCE.md).

## Data Path

```text
shipped STS2 UI
  -> observer freezes Human observation H + complete Connector A(H)
  -> game accepts an exact GameAction or source-local UI Commit
  -> exact native references match exactly one frozen BoundAction
  -> native lifecycle/direct delivery and authoritative boundary frames are observed
  -> normalized current semantic trace + non-authorizing current Decision record
  -> offline calibration tests an explicit research transition contract
  -> audit/export
  -> immutable current Session Bundle (wire schema `session-bundle-2`)
  -> Platform Evidence verify/store/transfer/receive
  -> STPD research admission, corpus, split, B0 and profiling
```

Zero or multiple matches, no current or same-card staged stable pre-frame,
runtime drift, an unproven overlapping causal window, or a missing stable
successor are quarantined rather than guessed. Overlap still retains accepted
action identity and lifecycle evidence; it does not emit a current canonical
record.
Schema-4 `transition_proved` is a trace disposition, not canonical training
authority. Run the mechanical calibration before making any one-step claim:

```bash
npm --prefix components/annotator run calibrate:semantic-training -- \
  .local/recordings/<session> --output /tmp/semantic-calibration.json
```

ADR 0003 selects serialized Human input as the next candidate architecture for
canonical one-step collection. No input gate or runtime implementation is
authorized by that decision. Pending explicit owner approval, implementation
must remain stopped at this design boundary.

## Prerequisites

- macOS arm64 or Windows x64 with Slay the Spire 2 installed from Steam, or
  `STS2_GAME_DIR` set to another exact installation;
- .NET SDK 9 and Node.js 20 or newer;
- one clean `STS2-AI-PLATFORM` checkout with exact component identities.

No game DLL, decompiled source, save, raw recording, or local deployment artifact
belongs in Git.

## Build And Check

From the Platform root:

```bash
npm ci
npm run check
npm run check:exact-game
npm run build
```

`npm run check` runs the public core tests, architecture boundary checks,
documentation checks, and an exact-game Mod build when the local game is
available.

## Deploy And Cold Load

The normal Platform path installs one Mod. Fully close the game before each
deployment, launch, or rollback:

```bash
npm run game-mod:build
npm run game-mod:deploy
npm run game-mod:launch
npm run verify:loaded
```

The unified `STS2_PLATFORM` assembly embeds component-specific source identity
while Connector and Annotator report one common loaded SHA/MVID. The old
component-local deploy/admit commands remain narrow standalone development
tools; they are not the production install path and must not be composed with
the unified Mod. `npm run verify:loaded` is the canonical Platform-root loaded
identity check; `npm run game-mod:verify-loaded` is its component-level
equivalent for game-Mod development.

On Windows, the component development `launch` binds the exact candidate runtime ID and Connector source
revision before starting the native executable. `deploy`, `admit:modset`,
`launch`, `verify:loaded`, and `rollback` all fail closed if process discovery,
the executable, game assembly, source digest, artifact SHA/MVID, or admitted
Modset envelope drifts. macOS keeps its existing supported-exact launch path.
`prepare:mods` validates SettingsSave schema 8, backs up the one Windows Steam
account settings file, enables only the local Connector, Annotator and Platform
Live UI, and refuses any already-enabled third-party Mod.

## Record And Audit

After `verify:loaded` passes, press `K`, open **Human Data**, and choose **New
Session** before using the native game UI. Do not run an Agent/Connector
controller in the same process. Pause stops admission of new witnesses; Close
terminates an unresolved final root as
`session_closed_before_successor_boundary` without inventing a successor,
flushes the session, and allows another New Session without restarting STS2.
The runtime writes to
`.local/recordings/<session>/`:

```text
recording-manifest.json
run-0001.jsonl
run-0002.jsonl
invalidations.jsonl
semantic-boundary-trace.jsonl
canonical-transitions.jsonl
semantic-frames/sha256/<digest>.json
semantic-action-spaces/sha256/<digest>.json
native-semantic-discriminator.jsonl (diagnostic only)
run-journal.jsonl
coverage.json
```

Then audit and pack a session. Packing requires an explicit human-origin
attestation and an exact collection profile; it never infers operator identity:

```bash
npm --prefix components/annotator run audit -- .local/recordings/<session>
npm --prefix components/annotator run export -- \
  .local/recordings/<session> .local/exports/<session>.jsonl
npm --prefix components/annotator run pack-session -- .local/recordings/<session> \
  --worker human-001 \
  --campaign human-combat-smoke-2026-08 \
  --attest-human-origin
```

The single-session importer remains useful for diagnosis. Multi-worker
collection uses the versioned bundle/registry/corpus workflow documented in
[Session bundles](docs/SESSION_BUNDLES.md).

```bash
cd /path/to/STS2-The-Perfect-Defect
uv run python tools/import_human_recording.py \
  /path/to/STS2-AI-PLATFORM/components/annotator/.local/exports/<session>.jsonl \
  --output .local/human-dataset \
  --split-salt human-v1
```

Raw recordings are local/private by default and are ignored by Git.
The reviewed predecessor summary is
[documented separately](docs/LIVE_EVIDENCE_2026-08-22.md); it contains no raw
gameplay records.

## Documentation

Start with the [document map](docs/DOCUMENT_MAP.md). Contributors should also
read [AGENTS.md](AGENTS.md), [CONTRIBUTING.md](CONTRIBUTING.md), and
[SECURITY.md](SECURITY.md).
