# PR25 Full-Run follow-up: real Human coverage and exact owner repairs

G5 Annotator correlation repair; G1 Live UI wording. Base develop
`cd9a0fbb0c85577a13513abe715f91d794ac86eb`; input PR head
`4775253c2e945c87353c5a98249c2b14ecc336f6`. This report is an audit of an
immutable historical session, not qualification of the next artifact.

## Exact session and denominators

Session `session-20260910T094138Z-6ee0b484853944748d233844e7e3c32f`, source
`f01197312902b0f167c83af03b23a0ac982b1c6a`, runtime
`2dbe7edd52a24dc48871a4cfe32eecb1`; unified artifact SHA256
`3b7d140745868ebf3e4dd57143ec1201b9d7f1dda9dc014260ac19a9626c56d2`,
MVID `8f1c99a5-bd43-4443-bb8c-761bb5f7e9d8`. Exact installed game v0.111.0.

Formal recording audit: 154 valid compatibility records, zero invalid records,
67 explicit invalidations. Semantic calibration: 481 accepted decisions
(385 roots + 96 children), 478 proved and 478 durable canonical
(382 roots + 96 children), two successor-unresolved and one native cancellation.
Zero proved-but-not-canonical, zero state/action-space-unresolved among admitted
records. This does not count inputs that failed before admission. The UI's
Unresolved counter includes cancellation, so its corresponding value is three,
not the calibration successor-unresolved value two; the label now says so.

All 96 canonical children have a canonical opening parent and matching causal
root. Families: hand selector 69, reward replacement 14, rest selector 6,
merchant removal selector 4, combat-pile selector 2, generated card choice 1.
The two combat-pile inputs (sequences 464 and 478) independently retain complete
pre-state catalogs of 2 and 7 choices, selected card referents and card details,
`pile_type=draw`, and canonical successors. This session does not establish
fresh DiscardPile coverage. These are first-class decisions, not only parent
continuation annotations. Parent successor at a selector boundary is not the
completed card effect.

Native diagnostic: 301 accepted, 300 successful, one cancelled, 271 exact
memberships, 29 unknown (27 map votes, two relic picks). Its narrower semantic
catalog diagnostic is distinct from the authoritative canonical contract;
retaining those unknowns does not erase the proved map records.

## Complete remaining disposition buckets

| Population | Count | Meaning |
| --- | ---: | --- |
| Successor unresolved: act-ready, direct UI / native Vote action | 1 | Source defect; next Act was entered, but carrier never reached vote/RunManager. Eventually closed unknown. |
| Successor unresolved: EndPlayerTurnAction, GameAction | 1 | Session closed without the required successor; fail-closed tail. |
| Rejected: PlayCardAction cancelled after start | 1 | Native cancellation, not a successful transition; no forged successor. |
| Invalidation: native type mismatch / ReadyToBeginEnemyTurnAction | 32 | Internal native effect under another Human scope; diagnostic duplicate ingress, no evidence of a missing independent Human input. |
| Invalidation: native type mismatch / MoveToMapCoordAction | 27 | Internal map move accompanying Human vote; all 27 vote decisions canonical. |
| Invalidation: native type mismatch / VoteToMoveToNextActAction | 1 | Act-ready carrier source defect. |
| Invalidation: selector pre/lineage unavailable / deck card select | 4 | Two selections, preview cancel and preview confirm; real event selector inputs lost before admission. |
| Invalidation: nested continuation unavailable / deck confirm | 1 | Same screen and terminal input as above; not an additional fifth decision. |
| Invalidation: PlayCard pre-frame capture failed | 2 | No complete exact interactive H frame; both adjacent to enemy-turn activity. Evidence insufficient for a safe frame repair; preserve fail-closed. |

No persistence-failure bucket was observed. No new selector parent was truncated
by its own child in this session. Counts from older sessions are not substituted.

## First incorrect facts and implemented repairs

1. **Act-ready carrier: CONFIRMED_DEFECT.** Reward Prefix bound the screen;
   enqueue Prefix attempted to bind the same witness to the vote, violating the
   correct one-to-one table. Native log explicitly reports the collision.
   Sequence 276 remained unknown while sequence 277 entered the next Act.
   Transfer the exact witness-bound NRewardsScreen to the queued vote, then
   transfer the vote to RunManager; no ambient overlay/current action lookup.
2. **Event selector opening: CONFIRMED_DEFECT.** NEventRoom.OptionButtonClicked
   invokes EventSynchronizer/Chosen synchronously. Annotator bound EventOption
   only in UI Postfix, after Chosen's factory Prefix needed it. Sequence 6 was
   the Human 'dark door' choice in DOORS_OF_LIGHT_AND_DARK; four selector inputs
   followed at 09:42:00–09:42:04. This is not an unowned event-entry selector.
   Stage the exact option in Prefix, admit Human input in Postfix, preserve the
   async selector scope and hand the option to its exact Task afterward.
3. **Early carrier failure disposition: CONFIRMED_DEFECT.** The act collision
   attempted PreviewUnknown before the UI root was accepted, producing
   `Unknown semantic action witness` instead of the intended disposition.
   Retain the failure on the matching Human scope; persist it after durable
   admission. Never create a phantom accepted root just to report a failure.
4. **Fresh-run provenance: CONFIRMED_DEFECT.** Launch is also used after
   SetUpSavedSingleplayer. Both launches in this session were journaled as
   `run_started_native`; the first loaded a FinishedCombat save. Thus its one
   native end does not establish a naturally complete run. Bind the exact
   RunState at new/saved setup and classify Launch as new, resumed or origin
   unknown. Polling cannot create or suppress native provenance. Existing raw
   journal events remain unchanged. New journal kinds are additive.

Owning implementation: Annotator NativeUiPatches.cs, RecorderRuntime.cs,
HumanActionScope.cs and NativeRunLaunchProvenance.cs. No Connector legality,
STPD policy, native mutation, root synthesis or retroactive parent assignment.
DecisionOccurrence schema and `CausalRoot != DecisionOccurrence` remain intact.
Tests cover exact screen→vote→RunManager transfer and collisions; synchronous
factory→async completion despite option carrier consumption; early failure
admission ordering; fresh/saved/unknown/conflicting setup provenance; UI counts.

## Distance to annotation and STPD handoff

The recording foundation now has extensive real first-class child evidence;
this is not yet a qualified complete Full Run. The confirmed event/Act defects
need fresh Human verification on the repaired artifact. The two pre-frame
failures remain unexplained at the precise observation seam; do not reuse an
older complete frame. Older hook/generated selector families lacking a Human
parent remain unsupported unless exact causal origin is independently proved.
One fresh run must cover natural start through native end without unexplained
Human decision loss, and the required family matrix must be covered separately.
No observed-session success ratio measures all possible game surfaces.

STPD remains untouched at `67566db562b95f57468290b97659c9f2e3862fe1`.
Its Full-Run admission AND reload intentionally accept only SyntheticSourceAdapter.
The Platform adapter remains a real integration gate. Import canonical evidence,
trace, journal, immutable states/Reads and complete catalogs, preserving child
identity/lineage; do not import only the 154 compatibility records. Whole-run
splits, annotation Gold and train/live representation parity remain separate
research gates. A child cancel/confirm is not a duplicate parent effect label;
unknowns are not negative labels. Existing model scoring is not Full-Run model
qualification. See PR25_DECISION_UI_ALIGNMENT_2026-09-10.md for the consumer map.

Bounded next Human canary: (1) event card removal with select/cancel/reselect/
confirm; (2) boss rewards→act-ready→next Act interactive map; (3) fresh new run
versus saved resume and one natural end; (4) draw/discard selector with UI parent,
child and pile details; (5) turn-boundary card input timing, without forcing an
unproved H frame to valid. Do not spend another broad full run before these
checks. Keep PR25 Draft; no merge/mark-ready, STPD edits, gameplay automation,
or historical evidence rewriting. Runtime and CI receipts identify the exact
final candidate independently; new Human qualification is not claimed.
