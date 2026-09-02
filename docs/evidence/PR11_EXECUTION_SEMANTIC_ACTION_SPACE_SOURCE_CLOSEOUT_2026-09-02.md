# PR11 Execution Semantic Action-Space Source Closeout — 2026-09-02

## Scope and authority

This closeout reconstructs the historical Human recording data flow before
changing PR #11. Current source, exact recorded bytes and STS2 native call order
override earlier descriptions. Starting source is
`0782ad7bb56de2a2d10481ef0680b662916aa3c0` on
`cleanup/platform/authority-evidence-single-source`, based on
`develop@791e27172c39e5c4ce33a415b16fc1ea7f060513`.

The current repair has source/test/build/install/load evidence. No predecessor
Human evidence qualifies the resulting bytes. The exact candidate was built
from workspace `727bdb5be91d2b2eae94d6f9a9f89cc75389b278`, installed as the sole
`STS2_PLATFORM` Mod and cold-loaded before the Human gate.

Candidate seal:

- unified Game Mod artifact `3d54c8b85431f899654e060722f6b76226f6dc457418eac0998a7319faf498fa`, MVID `601bfbf9-7bb0-4881-8913-cb8f9b70dd2d`;
- Native Foundation / Connector / Annotator component source `aa03c84d2ca503a19635456c267419c71c6f1bbe`; Game Mod component source `c1b3144f07ba210c7f7064087d0d37bb3c9a2e66`;
- exact STS2 `v0.111.0 / 41cef1ea`, `sts2.dll` SHA `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`, MVID `57785517-0b16-42b9-8b36-bad6fb28384b`;
- loaded Connector runtime `2b0fed44f098477dbd97073d988ccc12`, environment `f079e1f45a3dfa04bd368e5563c0e6ca45dad36bfd6647791c21fc7d970ec637`, exact Modset `e8a7f7c39a212ec9680a92284a1a7e3060347e0fecf099f1dba3bd1b4f12e026`;
- install/load PASS, runtime Ready with no recording session; Human canary remains pending.

## Historical reconstruction

| Checkpoint | What actually worked | What it did not prove |
|---|---|---|
| Decision V2 / PendingDecision / NativeActionLedger | Frozen Human-time public frame, exact reference correlation, lifecycle accounting and durable V2 audit | Execution semantic state/action space or a universally causal successor |
| Native semantic discriminator (`05d9e8e`) | Read-only Native Foundation semantic state/catalog and exact native action membership at accepted and execution phases | Input authority, causal ordering, successor or corpus admission |
| SemanticBoundaryTracker maturation (`d97c898` through PR #6) | Exact execution ordering, typed Commit, next-root/owner-ready/PlayerChoice successor and fail-closed unknown | Semantic legality or current public deliverability |
| PR9/PR10 `develop@791e271...` | Same causal model, evidence-safe hot-path changes and immediate terminal Close disposition | Transfer of predecessor Human evidence to later source |
| PR11 through `0782ad7...` | Retired duplicate mutable causal/admission paths and made projections derive from one tracker proof | It accidentally stopped preserving execution `A_sem(S)` in the modern durable path |

The exact historical session
`session-20260830T064823Z-ed1d683fe0b44e1db312c7489cda7fba`
contains 41 accepted and 41 successful native semantic roots: PlayCard 30,
EndTurn 10 and UsePotion 1. All 41 are exact-once members of the semantic
catalog captured synchronously in the `ActionExecutor.BeforeActionExecuted`
phase from the same STS2 validators that own the current native decision. It is
not the Human observation or public delivery catalog; STS2 may already have
staged the selected object before this phase. For `game_action_9bccc666_7`, the execution sample has
semantic membership `exact_once` while the same capture's public UI frame is
`settling`, has zero actions and reports `frame_not_authoritative`.

The same session's 40 valid Decision V2 rows came from the frozen Human-time
public catalog retained by `PendingDecision.Pre`. That result proves durable
V2 compatibility and exact Human/public binding; it must not be relabelled as
40 modern canonical execution transitions.

## Fact ownership

| Fact | Historical producer/consumer | PR11 before repair | Correct owner and disposition |
|---|---|---|---|
| Human provenance `H` | UI scope -> PendingDecision/V2 | UI scope -> semantic action reference | Connector captures; Annotator records — KEEP |
| Human-visible `A(H)` | frozen Connector frame -> V2 | retained as Human observation, then incorrectly replaced during V2 projection | Connector public authority — PROJECT to V2 |
| native semantic `S` | Native semantic witness -> discriminator | public execution frame only | STS2/Native Foundation fact — EXTRACT typed read-only observation |
| native `A_sem(S)` | Native providers -> discriminator | not retained by semantic draft | STS2/Native Foundation fact — EXTRACT and durably reference |
| public `A_public` | SnapshotBuilder/Connector | used as if it were execution semantic action space | Connector delivery authority — KEEP, do not substitute |
| exact Human/native correlation | frozen binding + exact native references | SemanticActionReference | Annotator correlation evidence — KEEP |
| lifecycle / Commit | GameAction and typed native task/owner seams | SemanticBoundaryTracker observations | STS2 fact observed by Annotator — KEEP |
| causal successor `S'` | evolving ledger/tracker mechanisms | SemanticBoundaryTracker | tracker is sole causal authority — KEEP |
| canonical eligibility | historical ledger/V2 admission | derived projection plus calibration | non-authorizing join/audit — REPLACE stale join, no new authority |
| durable compatibility | Decision V2 | projected from execution public frame | PROJECT from frozen H plus tracker-proved successor |

`PendingDecision`, mutable `AcceptedHumanActionLedger`,
`SerializedEvidenceAdmission` and legacy `TrySettle` duplicated admission or
causal state and were correctly retired. The valuable Human frame/action facts
they carried already survive in `SemanticActionReference` and Human observation.
The independent semantic state/action-space fact never belonged to those
ledgers: it was produced by Native Foundation under
`PlayerEnvironmentNativeSemanticWitness`, but remained trapped in the
diagnostic discriminator lane.

## PR11 regression

The exact PR11 Human session
`session-20260901T143015Z-98e33382404a46369fffd0140729d815`
has 195 accepted roots, 194 tracker-proved successors and one correct terminal
unknown. Calibration yields 55 canonical, 139
`state_action_space_unresolved` and one `successor_unresolved`. The unresolved
action types are PlayCard 107, EndTurn 31 and UsePotion 1.

Representative failures all retain exact Human/native binding and causal
successor but lose execution action membership:

- PlayCard `game_action_b36c042f_7`: execution frame
  `state_b59a86d494_e`, public catalog 0, successor proved by exact next-root
  execution handoff.
- EndTurn `game_action_b36c042f_12`: execution public catalog 0. Other EndTurn
  samples retain non-empty catalogs but no longer contain the selected action.
- UsePotion `game_action_b36c042f_8a`: execution public catalog has 14 actions
  after the potion was withdrawn, so the selected potion is absent.
- CardReward and Treasure controls remain canonical because their typed direct
  UI execution frames contain the selected public action exactly once.

The regression is not successor settlement, Connector capture absence,
identity contamination or stale calibration. `7ba6df8` first delegated the
discriminator capture when the canonical boundary already captured a frame;
PR11 then projected `draft.SemanticPre` into both Decision V2 and canonical
evidence without carrying the independent native semantic capture. Source tests
constructed execution frames that still contained the chosen public action, so
they protected helper shape but not the real settling/withdrawal condition.

A later exact canary,
`session-20260901T170545Z-89e82a21530041edbb73b013962f00e3`, exposed the
cross-domain half of the same ownership error. Its tracker classified 145
semantic candidates, while `canonical-transitions.jsonl` contained only 38
durable rows. Nine `VoteForMapCoordAction` roots and one `PickRelicAction` root
had real typed Map/Treasure catalogs but the diagnostic adapter labelled the
executed action `outside_direct_native_catalog`. Most other missing rows were
modern rapid successors rejected by the older Decision V2 requirement that
`S'` be an interactive public frame. Thus 145 was a classifier candidate count,
not durable canonical truth; 38 was the actual durable count.

## Repair

The repair preserves one capture at the exact native action-binding boundary
and projects distinct facts from it:

```text
STS2 native state
-> Native Foundation typed semantic providers: S + A_sem(S)
-> Connector process-local read-only observation
   + independent public Snapshot/A_public projection
   + exact selected-native-action identity join
-> Annotator records H/correlation and typed semantic-decision reference
-> SemanticBoundaryTracker orders Root / Commit / S'
-> projector joins only already-authoritative facts
-> durable schema-4 semantic event + canonical schema-2 row
```

For `GameAction` roots the exact native decision may be captured at the
pre-admission binding boundary and carried through staging; if no sidecar is
available, the binding phase is `ActionExecutor.BeforeActionExecuted`. The
execution-time fair-player frame remains a separate observation and is never
combined with a later state to form a semantic epoch. Map/Treasure UI roots
retain the typed decision captured before native admission and join it to the
exact accepted `GameAction`; direct callbacks retain the same pre-admission
decision and exact owner/operand Commit witness. Public and native verbs may differ
(`activate` versus `travel`, `claim` or `select`), so action-space schema 2 binds
the exact Human `BoundActionId` to the selected native catalog key instead of
equating presentation strings.

The discriminator consumes the same immutable capture as a diagnostic and is
not promoted to authority. Modern canonical persistence precedes and is
independent of Decision V2 compatibility materialization. Calibration reports
semantic candidates and explicitly joins the durable canonical file; it no
longer creates a second canonical count. Public Snapshot publication and
execute-time revalidation are unchanged; a settling Snapshot may still publish
zero BoundActions. The action witness, Human BoundAction ID, semantic state
digest and catalog digest are sufficient exact identities, so no new
DecisionEpoch is introduced.

Exact v0.111.0 decompilation also confirms that the shared invariant does not
require a universal CLR lifecycle:

- Combat uses the native decision provider plus exact `GameAction` identity;
- Map binds the exact `MapPoint` selected by the UI to the travel catalog and
  then to the accepted `VoteForMapCoordAction`;
- Treasure select/skip bind the exact relic/room choice to
  `PickRelicAction`, whose real Commit is `TreasureRoomRelicSynchronizer.OnPicked`;
- Reward/CardReward/Treasure callback roots capture their typed provider before
  admission and retain the source-local owner/operand Commit witness;
- non-terminal Reward proceed commits through
  `RewardsSetSynchronizer.SkipLocalRewardsSet`, while terminal branches that
  actually call `ProceedFromTerminalRewardsScreen` retain the Task seam.

The boss/victory act-change branch only enqueues `VoteToMoveToNextActAction` at
`SetLocalPlayerReady`; that call is deliberately not promoted to Commit. Until
that exact nested action is migrated, the branch remains fail-closed rather
than borrowing the terminal Task claim.

Semantic evidence schema 4 and canonical evidence schema 2 remain unchanged.
The action-space sidecar alone moves to schema 2 to add the exact Human binding
and phase; schema 1 remains readable under its original same-verb identity
rules. Missing, changed, mismatched or incomplete typed action-space evidence
fails closed.

## Verification and non-claims

Targeted tests cover PlayCard, EndTurn and UsePotion exact membership with an
empty public execution catalog; exact Human/native join; persistence, reload and
audit; content tampering; typed Map and Treasure identity joins; rapid execution
handoff; terminal Close unknown; cancellation/abort; and projection's inability
to settle. Map, Reward, CardReward and Treasure remain on the same tracker; their
native providers and callback/GameAction adapters own only typed decision and
Commit facts.

Both historical sessions still audit with their original meanings: the
2026-08-30 session is V2 40/40 valid and the PR11 canary is V2 7/7 valid. Running
the new calibrator on the old PR11 bytes still reports the same 55 semantic candidates,
139 state/action-space unresolved and one successor unresolved; the repair does
not manufacture evidence for predecessor bytes.

Not claimed for this candidate until the owner canary:

- Human origin or Human qualification of the repaired bytes;
- that the predecessor 139 rows become canonical retroactively;
- exhaustive Full Run or unseen-domain semantic completeness;
- performance improvement;
- STPD research admission of canonical schema 2.
