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

- Implemented and automated-test verified: ordinary Combat `play` (including an
  exact target when present), `end_turn`, append-only recording, independent
  audit/export, exact artifact identity, and fail-closed STPD import.
- V2 adds same-frame `run_deck` and `combat_piles`, typed ReadEvidence,
  CaptureProfile, RunJournal, portable Bundle V2, and exact generated-card
  choice select/skip observation. Ordinary-combat V2 is exact native-human
  verified; generated-card select has one audited predecessor record while skip
  and the unified artifact remain `not exercised`.
- A typed recording application service exposes Pause/Resume/Close to the
  unified Platform Live UI. These commands gate new witness admission only;
  already pending successor settlement continues, and the service never
  invokes a game action.
- Builds from the exact local STS2 `v0.111.0` assembly on macOS arm64 and
  Windows x64. Windows discovery and process inspection use the Platform Host
  Runtime component.
- Current exact-root artifact is owner-validated with a latest 28-record
  ordinary-combat session: 14 targeted plays, 9 untargeted plays and 5 end
  turns. Independent audit accepted all 28 with zero rejected records; one
  overlapping action failed closed before it could become a record. An earlier
  same-artifact 20-record run independently passed audit/export and strict STPD
  B0 with zero rejected records and no `mapping_zero`.
  Earlier exact artifacts contribute 170 predecessor records but do not lend
  identity or authority to the current artifact. Pending scope remains explicit:
  potions and non-Combat UI actions are unsupported by this recorder slice.
- Unsupported by this first slice: potions and non-Combat UI actions.

Implementation or build evidence is not human-origin evidence. See
[Status](docs/STATUS.md) and [Evidence](docs/EVIDENCE.md).

## Data Path

```text
shipped STS2 UI
  -> observer freezes Connector S + complete A(S) at native selection start
  -> game accepts a semantic action
  -> exact native references match exactly one frozen BoundAction
  -> Connector observes a different stable S' with required same-frame Reads
  -> append-only HumanDecisionRecord V2 + RunJournal + content-addressed blobs
  -> audit/export
  -> immutable HumanSessionBundle V2
  -> Platform Evidence verify/store/transfer/receive
  -> STPD research admission, corpus, split, B0 and profiling
```

Zero or multiple matches, no current or same-card staged stable pre-frame,
runtime drift, overlapping actions, or a missing stable successor are
quarantined rather than guessed.

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
npm run game-mod:verify-loaded
```

The unified `STS2_PLATFORM` assembly embeds component-specific source identity
while Connector and Annotator report one common loaded SHA/MVID. The old
component-local deploy/admit commands remain narrow standalone development
tools; they are not the production install path and must not be composed with
the unified Mod.

On Windows, the component development `launch` binds the exact candidate runtime ID and Connector source
revision before starting the native executable. `deploy`, `admit:modset`,
`launch`, `verify:loaded`, and `rollback` all fail closed if process discovery,
the executable, game assembly, source digest, artifact SHA/MVID, or admitted
Modset envelope drifts. macOS keeps its existing supported-exact launch path.
`prepare:mods` validates SettingsSave schema 8, backs up the one Windows Steam
account settings file, enables only the local Connector, Annotator and Platform
Live UI, and refuses any already-enabled third-party Mod.

## Record And Audit

After `verify:loaded` passes, start a normal single-player run and use the native
UI. Do not run an Agent/Connector controller in the same process. The runtime
writes to `.local/recordings/<session>/`:

```text
recording-manifest.json
run-0001.jsonl
run-0002.jsonl
invalidations.jsonl
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
