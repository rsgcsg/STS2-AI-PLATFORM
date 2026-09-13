# STS2 Human Annotator

Annotator observes real native Human decisions without changing gameplay.
STS2 owns rules, legality and Commit; Connector owns fair-player state, Reads
and action bindings; Annotator owns exact process-local mapping, immutable
Human evidence and one causal tracker. STPD owns research admission/training.

## Current data path

Human observation **H** is separate from execution **S + A(S)**. Native
acceptance, execution, Commit and successor proof are separate facts. Nested
inputs are first-class decisions with their own evidence and exact parent/root
lineage; independent native choices retain their real native context.

```text
native Human input -> exact native owner/binding -> semantic trace 4
  -> sole SemanticBoundaryTracker -> canonical transition 3
  -> current canonical export / session bundle 3 -> Evidence verification
  -> version-pinned external consumer
```

Frames, execution catalogs and required Reads are immutable content-addressed
objects. Missing ownership, state, membership, successor or persistence stays
failed closed. No later frame, global current-root lookup, timer, native retry,
input serialization gate or second legality engine supplies missing proof.
Pending means accepted with no terminal disposition; ordinary native cancellation
is separate from unresolved recording failure.

The optional `decision-record-2` projection and explicit compatibility bundle2
remain for real existing consumers. They omit decisions they cannot represent
and never count all successful decisions. Historical readers preserve original
schema meaning; no old evidence is rewritten or upgraded. See
[Data contract](docs/DATA_CONTRACT.md) and [Session bundles](docs/SESSION_BUNDLES.md).

## Operating and auditing

Use the visible **Platform** launcher, then Human Recorder. Start a new isolated
session before native run launch. Minimize shows recorded canonical decisions,
real failures and the latest three entries; expand for lifecycle, health,
coverage and exact evidence. Missing/incomplete failure accounting shows `—`,
not zero. K has no Recorder shortcut.

From the Platform root:

```bash
npm run verify:loaded
npm run annotator:audit -- /absolute/session-directory
npm --prefix components/annotator run audit:native-semantic -- /absolute/session-directory
npm --prefix components/annotator run calibrate:semantic-training -- /absolute/session-directory
npm run annotator:pack-session -- /absolute/session-directory --worker collector-id --campaign candidate-id --attest-human-origin \
  --output /absolute/bundle-directory
```

The exact pack command/options are owned by [Session bundles](docs/SESSION_BUNDLES.md).
Structural audit, canonical status, transport verification and Human Full-Run
qualification are distinct. Zero compatible invalid records does not prove no
lost input. No unchanged-artifact or historical test covers an unseen family.

## Development and support

Use `npm run check` and `npm run check:exact-game` at root. Build/install/load
only an exact clean candidate through Game Mod lifecycle. Keep raw data, saves,
game files, local receipts and credentials outside Git. Automatic upload requires an explicitly configured fixed campaign and
operator attestation through the [Evidence delivery service](../evidence/DELIVERY.md). Distribution, private incident collection and versioned fixes are
specified in [Support and distribution](../../docs/ANNOTATOR_COLLECTION.md).

Read [Status](docs/STATUS.md), [Architecture](docs/ARCHITECTURE.md), the
[Document map](docs/DOCUMENT_MAP.md) and [AGENTS.md](AGENTS.md). Dated evidence
under Platform `docs/evidence` and the component's
[historical evidence](docs/LIVE_EVIDENCE_2026-08-22.md) records predecessor facts.
