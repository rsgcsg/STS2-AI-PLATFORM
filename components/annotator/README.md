# STS2 Native UI Human Annotator

> This is the Human Annotator component of `rsgcsg/STS2-AI-PLATFORM`. It uses
> the in-repository Host Runtime and Connector components; sibling checkouts are
> no longer part of the current development contract.

The Human Annotator records bounded human decisions made through the shipped
Slay the Spire 2 UI. It combines the exact pre-action Player Environment frame
from the Platform Connector component, a native action
already accepted by the game, an exact process-local mapping to one frozen
`BoundAction`, and the next stable Player Environment snapshot.

The recorder observes. It does not click, enqueue actions, reconstruct legality,
or expose native references on a wire.

## Current Status

- Implemented at source/test: ordinary Combat `play` (including an exact target
  when present), `end_turn`, append-only recording, independent audit/export,
  exact artifact identity, and fail-closed STPD import. Exact-artifact Live
  evidence is stated separately and never inferred from this implementation.
- V2 adds same-frame `run_deck` and `combat_piles`, typed ReadEvidence,
  CaptureProfile, RunJournal, portable Bundle V2, and exact generated-card
  choice select/skip observation. Ordinary-combat V2 is exact native-human
  verified; generated-card select has one audited predecessor record while skip
  and the unified artifact remain `not exercised`.
- A typed RecordingService exposes Query/Status, Command and ordered Event
  contracts to Platform views. Runtime startup is `Ready`; `StartNewSession`
  opens an isolated session, Pause/Resume gate new witness admission, and Close
  waits for an admitted pending decision before flushing. A closed session can
  be followed by another session in the same STS2 process. The service never
  invokes a game action.
- RecordingStatus revision 3 exposes the CaptureProfile boundary as four
  explicit views: recorded, native-accepted-but-failed-closed,
  supported-not-observed, and declared out of scope. A family may have both
  successful records and accepted failures, but a rejected native UI attempt is
  never counted as either a HumanDecision or a recording failure.
- Accepted `GameAction` roots now enter a bounded additive native-action ledger
  at exact `GameAction.OnEnqueued`. The ledger records native started,
  player-choice pause/resume, cancelled and finished facts. Rapid overlap
  accounts every accepted root but invalidates strict transition admission for
  every action in the causal window; it never treats a later decision frame as
  an earlier action's successor. `HumanDecisionRecordV2` remains unchanged.
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
  identity or authority to the current artifact. Pending scope remains explicit:
  potions and non-Combat UI actions are unsupported by this recorder slice.
- Unsupported by this first slice: potions and non-Combat UI actions.

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

Implementation or build evidence is not human-origin evidence. See
[Status](docs/STATUS.md) and [Evidence](docs/EVIDENCE.md).

## Data Path

```text
shipped STS2 UI
  -> observer freezes Connector S + complete A(S) at native selection start
  -> game accepts a semantic action and assigns an exact GameAction ID
  -> exact native references match exactly one frozen BoundAction
  -> additive ledger observes the game-owned action lifecycle
  -> Connector observes a different stable S' with required same-frame Reads
  -> append-only HumanDecisionRecord V2 + RunJournal + content-addressed blobs
  -> audit/export
  -> immutable HumanSessionBundle V2
  -> Platform Evidence verify/store/transfer/receive
  -> STPD research admission, corpus, split, B0 and profiling
```

Zero or multiple matches, no current or same-card staged stable pre-frame,
runtime drift, an unproven overlapping causal window, or a missing stable
successor are quarantined rather than guessed. Overlap still retains accepted
action identity and lifecycle evidence; it does not emit a strict V2 record.

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
settles or invalidates an already pending witness, flushes the session, and
allows another New Session without restarting STS2. The runtime writes to
`.local/recordings/<session>/`:

```text
recording-manifest.json
run-0001.jsonl
run-0002.jsonl
invalidations.jsonl
native-action-ledger.jsonl
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
  --profile "$COLLECTION_PROFILE" \
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
