# STS2 Native UI Human Annotator

`STS2-human-Annotator` records ordinary human decisions made through the shipped
Slay the Spire 2 UI. It combines the exact pre-action Player Environment frame
from [STS2-Connector](https://github.com/rsgcsg/STS2-Connector), a native action
already accepted by the game, an exact process-local mapping to one frozen
`BoundAction`, and the next stable Player Environment snapshot.

The recorder observes. It does not click, enqueue actions, reconstruct legality,
or expose native references on a wire.

## Current Status

- Implemented and automated-test verified: ordinary Combat `play` (including an
  exact target when present), `end_turn`, append-only recording, independent
  audit/export, exact artifact identity, and fail-closed STPD import.
- Built from the exact local STS2 `v0.111.0` assembly on macOS arm64.
- Current exact-root artifact is owner-validated with 20 ordinary-combat
  records: 8 targeted plays, 8 untargeted plays and 4 end turns. Independent
  audit/export and strict STPD B0 accepted all 20 with zero rejected records;
  no `mapping_zero` remained. Three clicks without a complete S failed closed.
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
  -> Connector observes a different stable S'
  -> append-only HumanDecisionRecord
  -> audit/export
  -> STPD fail-closed import and B0
```

Zero or multiple matches, no current or same-card staged stable pre-frame,
runtime drift, overlapping actions, or a missing stable successor are
quarantined rather than guessed.

## Prerequisites

- macOS arm64 with Slay the Spire 2 installed from Steam at the default path, or
  `STS2_GAME_DIR` set to another exact installation;
- .NET SDK 9 and Node.js 20 or newer;
- sibling checkouts named `STS2-Connector` and `STS2-human-Annotator` under the
  same parent directory;
- a clean, exact commit in both repositories for evidence-bearing deployment.

No game DLL, decompiled source, save, raw recording, or local deployment artifact
belongs in Git.

## Build And Check

Build the exact Connector first, then the recorder:

```bash
cd ../STS2-Connector
npm run check
npm run test
npm run build

cd ../STS2-human-Annotator
npm run check
npm run build
```

`npm run check` runs the public core tests, architecture boundary checks,
documentation checks, and an exact-game Mod build when the local game is
available.

## Deploy And Cold Load

Fully close the game before each deployment or cold load:

```bash
cd ../STS2-Connector
npm run deploy

cd ../STS2-human-Annotator
npm run deploy
npm run launch
```

The first launch discovers the complete Connector + observer Modset fingerprint
and remains fail closed. Quit the game, then explicitly pin that exact process
envelope and cold-load again:

```bash
npm run admit:modset
npm run launch
npm run verify:loaded
```

The canary identifies one exact non-gameplay observer Modset for recording. It
does **not** enable Connector mutation, qualify the Modset, or claim generic Mod
compatibility.

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

Then audit and export a session:

```bash
npm run audit -- .local/recordings/<session>
npm run export -- .local/recordings/<session> .local/exports/<session>.jsonl
```

Import the audited export from the sibling STPD repository:

```bash
cd ../STPD
uv run python tools/import_human_recording.py \
  ../STS2-human-Annotator/.local/exports/<session>.jsonl \
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
