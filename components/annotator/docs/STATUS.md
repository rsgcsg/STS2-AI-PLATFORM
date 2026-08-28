# Status

Historical Annotator evidence below remains bound to its original
source/artifact. Annotator `0.3.0-rc.1` is the current read-rich V2 source.
Its exact predecessor Native artifacts have ordinary-combat V2 evidence and one
audited generated-card select. Generated-card skip and the current unified
artifact's Human path remain `not exercised`. Root `../../../docs/STATUS.md` is
authoritative for the current runtime boundary.

Schema 2 is bounded Human-proved for ordinary combat and generated-card select.
Latest repair-artifact session `session-20260828T151112Z-...` accounts for 233
accepted/233 proved roots, including enemy-target, no-target and self-target
potion use. Current source writes normalized schema-3 role references to exact
content-addressed frames, gates Read-rich capture behind Snapshot-only candidate
checks, batches Read persistence and reuses the authoritative successor capture.
Legacy audit stays compatible; the new native artifact has source/test/build
evidence only and inherits no Human runtime claim.
Current Full-Run topic source preserves that execution-owned continuous timeline
and adds interaction-specific semantic Reads, reward claim/proceed, card reward
select, map travel and Combat hand selector witnesses. The first owner canary
found that a paused parent could be pruned before native finish and direct UI
commits used a non-canonical boundary name. Current source repairs both and
strengthens audit so a stopped schema-2 trace cannot pass beside a continuing
native ledger. The repair is source/test/build evidence only.

Rapid-input artifact `080701b3... / 142054a5...` is cold-loaded and Live-proved
in runtime `39fa2d2e...`. Its closed owner session
`session-20260826T062916Z-957f201043a4456a89d13407682f0541` audits 12/12 strict
records; ledger v1 records 35 accepted, started and finished roots, 12 strict
admissions, 23 strict invalidations and zero unresolved lifecycle. This proves
the bounded exact lifecycle/accounting behavior for observed bursts without
claiming an action-local successor for overlap.

That canary also exposed an evidence-fidelity gap: v1 invalidated ledger rows did
not independently retain their frozen decision pre-frame and exact BoundAction.
Current ledger v2 source adds those accepted-action facts, rejects repetition on
lifecycle rows and cross-checks admitted facts against Decision V2. It is
cold-loaded as `df5d2c61... / 9072e515...` in runtime `ebe7a9fc...`. Owner
session `session-20260826T075502Z-9fe1ac91c78a48f9a8f4eeef204a3665`
audits 8/8 records, 26 invalidations and 33 accepted roots: 31 started, 28
finished, five cancelled, eight strict-admitted and 25 strict-invalidated. Two
cancelled before start and three cancelled after start. Ledger v2 mutation is
therefore Live-exercised; player-choice pause/resume remains `not exercised`.

The first observation-only semantic-boundary artifact
`2cb46ead... / 66ed1396...` ran owner session `session-20260826T141755Z-...` in
runtime `af2e7370...`. It accounted 22/22 accepted actions, including one real
generated-card select, but exposed a causal bug: the choice executed before an
earlier accepted queued End Turn and the End Turn retained its older semantic
pre. Strengthened audit rejects that one proof with
`semantic_transition_pre_not_execution_boundary`; two Decision V2 records stay
valid. Current source settles by execution order and rebinds a precommit only
from a complete authoritative pre-execution frame. It has source/test evidence
only until a corrected artifact is cold-loaded and owner-operated.

The Recording Application Plane starts Ready, creates explicit repeatable
sessions, and exposes typed status/events plus idempotent lifecycle commands.
Unified Modset admission and unknown/no-retry persistence are now Live-proved by
six audited end-turn records with 24 bounded Reads. The same session exposed 21
supported card-play misses: staged and transient snapshots were incorrectly
required to be identical. Current source repairs that bounded native transition,
reports per-family recorded/failed/not-observed/out-of-scope status, and displays
authoritative Closed state after the first Close. Those latest changes are
cold-loaded as `887630f4... / 14761ed4...` and do not inherit predecessor
evidence. Exact owner session `session-20260826T025703Z-...` now
audits 19/19 records and explicitly separates 16 native-accepted failures.

## Implemented

- process-local Connector witness freezing one stable Snapshot and complete
  finite BoundAction catalog with exact Host-local references;
- native STS2 UI scope around card play and end-turn callbacks;
- observation of accepted `PlayCardAction` and `EndPlayerTurnAction` at the
  exact game-owned `GameAction.OnEnqueued` boundary;
- bounded per-action native lifecycle accounting with explicit strict-admitted
  or strict-invalidated disposition and no fabricated overlap successor;
- additive semantic boundary coordination with separate Human observation H,
  transition S, exact A lifecycle and proved/cancelled/unknown S';
- ledger v2 accepted-action evidence containing the frozen decision pre-frame,
  exact witness/mapping and BoundAction while keeping Decision V2 unchanged;
- exact card/target reference mapping with zero/ambiguous fail closed;
- different complete interactive successor settlement;
- append-only per-run JSONL, invalidations, coverage, audit, and export;
- deterministic immutable session bundles with exact collection profile,
  pseudonymous worker/campaign fields, explicit human attestation,
  raw/audit/export provenance and complete checksums;
- exact game/Connector/Annotator SHA, MVID, source revision, source digest,
  runtime, environment, protocol, and Modset provenance;
- shared Windows Steam discovery, strict process detection, safe deployment,
  native cold launch, exact Connector game/source canaries, Modset admission,
  loaded-process verification, and rollback while retaining the macOS path;
- deterministic UTF-8/LF exports on Windows and macOS;
- STPD strict import through its existing Player Environment projection and B0.
- Decision V2, state-bound ReadEvidence, CaptureProfile, minimal RunJournal and
  portable HumanSessionBundle V2;
- required pre/successor `run_deck` and `combat_piles` captured from one
  authoritative Connector frame;
- exact generated-card choice select/skip witness using shipped native UI
  callbacks and the same frozen BoundAction authority.
- additive Full-Run semantic witnesses for reward claim/proceed and card reward
  select through exact direct UI delivery callbacks, plus map travel through the
  native `VoteForMapCoordAction` lifecycle; Decision V2 remains unchanged.
- exact Combat hand select/replace/deselect/confirm witnesses through native
  hand/container callbacks and the same direct UI mechanism; nested native
  helper calls cannot manufacture a second Human action.

## Automated Evidence

Core validation covers exact records, zero/ambiguous mapping rejection, same
snapshot rejection, nested runtime drift, catalog tampering, multi-run append,
audit, export, deterministic bundle packing, retry reuse, immutable-destination
rejection, collection-profile drift and missing-attestation rejection. Connector
tests cover duplicate-looking native objects,
target disambiguation, incomplete frames, and exact observer fingerprinting.
V2 tests additionally cover required-Read fail-closed admission, blob/content
identity, RunJournal/bundle portability, selector transition precision and
version-aware external verification. Rapid-input tests cover A1->A2 and
A1->A2->A3 accounting, targeted/untargeted/end-turn combinations, cancellation,
player-choice pause/resume, unresolved Pause/Close, bounded overflow/reset,
lifecycle ordering, legacy v1 readability, v2 accepted-evidence shape and
admitted-record identity mismatch/tamper.

## Exact Runtime Findings

The first 2026-08-22 observer artifact produced 25 independently auditable
`end_turn` records and exposed late card capture plus nested root
misclassification. Source `bc9c568...`, cold-loaded with queue-aware Connector
source `2a14504...`, then produced 64 admitted records: 51 card plays, including
27 targeted plays, and 13 end turns. All 64 mapped exact-unique and reached a
different complete interactive successor.

That run reduced the prior 22 queue-driven pre-frame misses to four clicks made
without any stable complete S. It also exposed four `mapping_zero` plays:
`TryPlayCard` ran after the selected holder had already left the active hand.
Source `6d474ce...` staged the exact frame at `NPlayerHand.StartCardPlay` and a
second owner-operated run admitted 106 records: 35 targeted plays, 48
untargeted plays and 23 end turns. Audit/export and strict STPD B0 passed with
zero rejected records. Five actions had no complete S and failed closed; one
following action exposed that the old generic latest-frame fallback could cross
a turn boundary. Source `6254967...` removed that fallback and admitted another
64 exact records, but five nonmatching game-owned `PlayCardAction` instances
claimed the human root before exact mapping. Current source exact-matches the
frozen native operands before claiming the root. The 170 predecessor records
prove same-card staging, no-fallback operation and the downstream path, but do
not validate this final ordering source artifact.

## Current Exact Runtime Seal

The V2 artifact from source `09e5c236...`, SHA `4e911dd6...`, MVID
`692e9dd9...` loaded with Connector `6c66cbf.../11f1da35...` in runtime
`abb6b2d8...`. Session
`session-20260824T153454Z-3005d3b9cea9425ab0c615f0bd961a39` admitted 30
Decision V2 records: 7 targeted plays, 16 untargeted plays and 7 end turns.
Every admitted pre-state and successor materialized both required Reads;
independent V2 audit and typed bundle verification passed. Five unstable
pre-frames failed closed. Bundle `b92778be...` passed immutable Platform
store/receive/retry and STPD imported 30/30 with zero rejection. Post-load
Annotator changes add only caller-CWD CLI evidence-path handling and this
closeout documentation; they are automated-tested, not claimed loaded. Exact
hashes are in the root V2 closeout.

Generated-card choice did not occur in this 30-record session. A later exact
three-Mod session
`session-20260825T022415Z-18df9e07f32d43f2a1beb8ad0db91c14`
audited 10/10 records and includes one generated-card select with exact-unique
mapping and an interactive successor. Skip remains `not exercised`; this
predecessor evidence does not transfer to the unified artifact.

Unified artifact `06f62285... / 17981f40...`, runtime `e3a89aae...`, has two
owner-operated sessions that independently audit 39/39 records: 25 card plays
and 14 end turns with 158 materialized Reads and zero Read failures. Immediate
Close completed in 4.139 ms; a Close with an admitted pending decision remained
Closing until bounded settlement, then closed safely. The same sessions exposed
that pre-frame invalidations were created at native UI method entry, before
STS2 accepted or rejected the attempted input. Current source defers such an
invalidation until the expected game-owned action is observed and reports it as
native-accepted-but-failed-closed. This follow-up source is loaded as
`887630f4... / 14761ed4...` in runtime `bcf2b3f1...`, but Ready/no-session is
not Human evidence and does not inherit the 39 records. A later same-runtime
session admitted 10 plays and nine end turns with 78 Reads and zero Read
failures. It separately invalidated 15 accepted card actions without a complete
pre-frame and one overlapping accepted action. Close completed 5.153 ms after
its first request. The owner-observed rejected/cancelled attempt produced no
event; its attribution remains owner-attested.

## V1 Predecessor Evidence

Source `9459d22...` was built, installed, cold-loaded and owner-operated with
Connector source `2a14504...`. Session
`session-20260822T172319Z-35064ba4aeb34a029828e5953b00903b` admitted 20
records: 8 targeted plays, 8 untargeted plays and 4 end turns. Every record had
exact one-to-one mapping and a different complete interactive successor;
audit/export and strict STPD B0 accepted all 20 with zero rejection and no
`mapping_zero`.

The latest owner-completed same-artifact session,
`session-20260822T175331Z-509754657baa4d3c8536a9215b6d7b97`, admitted 28
records: 14 targeted plays, 9 untargeted plays and 5 end turns. All 28 mapped
exact-unique and reached a different stable interactive successor. Independent
audit passed 28/0; one overlapping action was invalidated before admission. The
preceding empty bootstrap session admitted nothing. An earlier 72-action session
was launched without the exact observer Modset canary, so every action failed
closed as `exact_observer_modset_canary_missing`; that is environment evidence,
not a mapping regression.

The manifest's `not human validated` non-claim applies until owner review; the
owner has now confirmed manual native UI operation for this exact session.
Machine audit still cannot independently prove operator identity or controlled
non-interference. These runs validate the declared ordinary-combat recorder
slice. They do not validate the new offline bundle/corpus tooling against
multiple real workers, qualify a broader action family, or authorize training.

Windows x64 automated checks cover path discovery, exact candidate Connector
canaries, executable matching, deterministic export, and exact-game Mod builds.
A Windows loaded seal and human canary remain separate evidence and must be
recorded only after the corresponding cold loads and owner-operated actions.

## Declared Unsupported

Decision V2 remains the narrow combat capture profile: potion, non-combat,
arbitrary-selector, multiplayer and gameplay-affecting-Modset records are not
admitted by version `0.3.0-rc.1`. The additive semantic sidecar now has source/
test coverage for reward claim/proceed, card reward select, map travel and
Combat hand selection, but the repaired paths remain pending exact native-human
runtime evidence. Potion, event, shop, rest, treasure, run entry and terminal
Human witnesses are not implemented.
