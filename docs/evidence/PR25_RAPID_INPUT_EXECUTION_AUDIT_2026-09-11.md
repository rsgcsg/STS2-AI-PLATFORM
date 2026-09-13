# PR25 rapid input and execution-catalog audit

G5: Annotator queued execution evidence and independent validation.
Base develop: `cd9a0fbb0c85577a13513abe715f91d794ac86eb`.
Audited head: `d514f105ac061603d1033a18b42096a443380dd0`.
STPD is unchanged; no consumer policy, legality reconstruction or Human input changes.

## Exact observed runtime

Closed session `session-20260910T220407Z-f632079e033246629fef551e08995a9d`
uses Annotator `5f62cdbdf2f2eb538cb8b86c8e92669b3a7924b4`, Connector
`80d2a77664417d39352660ea2f5dbe9092e255ea`, v4 capture profile and decision
schema 2. Runtime `802f0911b71d45d78686549ad0bec698` loaded DLL SHA256
`90b4ca33781db71747282fe8eb6fa313b43967858c2322ab5a9049680a8570b6`, MVID
`3006d635-8625-41b0-82d5-cf5a30c8ab80`. The exact game is STS2 v0.111.0 /
41cef1ea, main-assembly SHA256
`9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`.
Raw hashes, native-source inspection and command receipts are local in
`.local/pr25-rapid-audit`; proprietary source and Human bytes are not committed.

The stored accounting is 1,132 accepted, 1,112 tracker-proved, 1,111 canonical,
18 unknown and two cancelled-after-start. Old audit reports 335 legacy valid /
zero invalid. These counts do not establish correct Full-Run evidence.
All 95 invalidations: 47 internal MoveToMapCoordAction diagnostics, 30 internal
ReadyToBeginEnemyTurnAction diagnostics, 15 accepted PlayCard pre-frame failures,
two potion exact-mapping failures, one potion-discard projection failure.
Journal: five resumes, zero fresh starts, one native end, four unproved run ends.

## Historical and current rapid-input failures

| Session time on 2026-09-10 UTC | Accepted | Stored canonical | PlayCard pre-frame failures |
| --- | ---: | ---: | ---: |
| 08:15:44 | 419 | 409 | 4 |
| 09:41:38 | 481 | 478 | 2 |
| 10:49:32 | 358 | 356 | 4 |
| 12:05:47 | 321 | 311 | 10 |
| 12:35:34 | 122 | 113 | 10 |
| 12:57:25 | 356 | 350 | 3 |
| 21:12:07 | 6 | 5 | 0 |
| 22:04:07 | 1,132 | 1,111 | 15 |

These are distinct session inventories, not a matched speed/performance study.
The latest 15 failures are 12 card-start captures in native phase Start with
InCardPlay=false, and three in phase Play with InCardPlay=true. All have native
player actions enabled, local turn participation and no peeking; the public
surface has a complete but empty settling catalog. Their exact accepted card
and available target witnesses survive as failed Human occurrences. Fourteen
failed-card occurrences fence 15 prior transitions (one fences two); the other
failed-card occurrence has no pending transition to fence. One further barrier
comes from potion use. There are also one intervening-Human event transition
and one final session-close unknown.

Exact native NPlayerHand.AreCardActionsAllowed does not impose Play phase.
Its shortcut path cancels an existing card-play object before StartCardPlay;
NCardPlay.TryPlayCard and CardModel.TryManualPlay validate CanPlayTargeting and
enqueue PlayCardAction. That action is CombatPlayPhaseOnly and revalidates hand,
resources and target at execution. Human admission can therefore occur before
execution readiness; InCardPlay is not proof of an earlier card effect either.
The Recorder currently requires a complete interactive public H catalog at
card start/submission. This explains the capture gap; it is not evidence that
Human acted incorrectly or Recorder merely joined late.

This patch does not fabricate A(H), borrow a later H, widen Connector mutation
availability or recover these 15 historical occurrences. Full capture of these
inputs still needs an explicit native-admission correlation contract independent
of public deliverability, while preserving separate H and execution S/A(S).
That larger contract is not silently approximated in this repair.

## Confirmed execution evidence defect and repair

First incorrect fact: StartSemanticNativeAction stored the admission-time native
catalog in NativeActionLifecycleSubscription. ObserveBeforeActionExecution
captured fresh state but preferred that cached catalog. The validator accepted
both phases without relating phase to queued execution. Thus a correct H witness
and correct execution frame could be joined to the wrong A(S).

There are 900 stored canonical rows with this invalid phase pairing in the latest
session: 740 PlayCard, 103 EndTurn, 46 map votes, nine potion uses, two relic picks.
At least 30 execution boundaries have different energy or block between the
cached semantic state and execution frame. Examples: #280 energy 2 -> 1 and
block 19 -> 30; #294 energy 1 -> 0; #893 energy 3 -> 2. These are concrete
state changes, not an inference from timestamp spacing. Twenty-nine of those changed boundaries have canonical rows; one was cancelled.
The remaining 871 canonical rows are phase-unqualified, not proven numerically wrong. Earlier sessions also
contain the same stored admission-catalog pattern.

The lifecycle subscription can no longer store an execution catalog. Every
queued action now captures its catalog at BeforeActionExecuted with its fresh
execution frame. Exact queued UI operands/type remain retained separately.
Native direct callbacks without a queue retain their legitimate pre-admission
capture. H remains separate and no native GameAction, owner or action is invented.

Core validation and Node semantic calibration now reject an admission catalog
for game_action or any explicit queued carrier. Regression fixtures cover both
mechanisms, current execution acceptance, direct nonqueued callback acceptance,
canonical rejection, and the production session auditor with internally
consistent content hashes. The stored historical bytes remain immutable.

After rebuilding the exact CLI, the latest session audit correctly fails with
901 queued_action_requires_execution_action_space findings. The old 335 legacy
valid / zero invalid totals remain a separate structural count. Corrected
calibration finds 151 semantic candidates, 917 state/action-space unresolved,
62 successor-unresolved, two cancelled; 1,111 canonical rows still physically
exist. Neither historical stored counts nor previous green audits grant training
admission. The new validator revokes the overbroad phase claim, not the bytes.

## Validation and remaining gates

Focused Core tests, semantic calibration tests and production wiring checks
cover the repair. Exact final-head portable/exact-game, CI, build and runtime
results are recorded in the local receipt and Draft PR; source/test is not load
or new Human qualification. Review is lead-owned; no independent reviewer claim.

Remaining: the 15 H admission failures, potion-use mapping, potion-discard
projection, one event causal overlap and terminal successor coverage. The native
Act seam now has two observed owner-ready/ActEntered events, but this report does
not infer exhaustive Act or Full-Run qualification. No gameplay was automated,
no historical evidence rewritten, no STPD changes, no merge/mark-ready.

Next bounded canary on a verified new artifact: burst queued cards and EndTurn
with resource-changing earlier effects; select during turn Start; switch cards
via shortcuts while targeting; then inspect exact execution phase/membership,
failed occurrences and causal dispositions. Native cancellations remain honest
cancellations. Do not slow Human input to hide gaps. Rollback uses the owning
Game Mod snapshot with the game closed; an active Human run is not terminated
for promotion.
