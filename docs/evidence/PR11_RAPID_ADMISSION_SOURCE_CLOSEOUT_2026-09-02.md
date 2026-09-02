# PR11 rapid Human-input admission source closeout

This source-level note is not Human qualification for the new bytes.

## Identity

- Base: `develop@791e27172c39e5c4ce33a415b16fc1ea7f060513`
- Source/head: `3ee15fe8c70da4cd7d8070ca5344f331dec8a2cf`
- Exact game: STS2 `v0.111.0 / 41cef1ea`
- Artifact: `085a70f3cbf436bbe20784f8519494b2bfd8e26371977c2e6bc3e270e426e647`
- MVID: `d08ee098-e9f1-417e-a03f-d9986ef61cc4`
- Loaded runtime: `b4985ac775734523a33c2f70a4eaf80b`
- Environment: `e34d7ed2777ea169195d060d79f44c18b0d2c1d9d81852ba8b31beec42da16a5`
- Modset fingerprint: `f80770c0eb87c49b54bb3871976610bf9cbf8d0b63258e989e9049393007bdc1`

## Root cause and repair

`RecorderRuntime.CanOpenSemanticEvidenceWindow` treated an unresolved prior
root as a veto unless `SemanticBoundaryTracker.CanOpenNextRoot` was already
true. The gate ran before the tracker could record the next STS2-accepted Human
root, producing avoidable rapid-input `semantic_causal_overlap` dispositions.

Admission now checks only recording lifecycle / active Human scope. It does not
authorize gameplay, bind semantic state, settle a prior root, or manufacture a
successor. The tracker remains the sole causal authority and still rejects
cross-effect or otherwise unproved boundaries.

## Evidence and non-claims

Targeted component checks, full repository checks, exact-game, BOM, closeout and
whitespace checks pass on the source. The candidate was built, deployed after
retiring a stale game process, launched, and cold-load verification reported
the exact artifact/MVID and `ready` runtime with only `STS2_PLATFORM` loaded.

The prior session `session-20260902T112031Z-e75673146b8b48c587545ef1cc5da7ff`
belongs to older bytes and cannot qualify this candidate. A fresh Human canary
is required; it should cover ordinary input, rapid PlayCard→PlayCard,
rapid PlayCard→EndTurn, one PlayerChoice child, one additional action, and
clean Recorder Close.

This change does not claim zero unknowns, eliminate terminal Close unknowns,
change PlayerChoice semantics, alter Connector authority, or prove Human
qualification. It adds no polling, timers, UI heuristics, backfill, or second
accounting authority.
