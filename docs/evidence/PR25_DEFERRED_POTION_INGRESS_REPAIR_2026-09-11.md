# PR25 deferred native input audit and repair

G5: Annotator owns native-Human correlation and failure accounting; Connector
owns exact referent projection and execution catalogs. Native STS2 owns request,
deferred enqueue, actual acceptance and execution. No STPD/gameplay change.
Base develop: `cd9a0fbb0c85577a13513abe715f91d794ac86eb`. Audited PR head:
`41e385aefd9d48f62461a3b068cfb9ba362719af`. Continue Draft PR25; no merge.
STPD pin remains `67566db562b95f57468290b97659c9f2e3862fe1`.

## Exact predecessor audit

Session `session-20260911T054735Z-e6dded92e1c04e60a0878c2f0b748fb7` belongs to
runtime `8eede2e27aef401fa4c835f42b68b23d`, DLL SHA256
`b4d28912a5ff3930f1af8b1cf6ee2cc26770d064ca9820c4eb39e09d6d579af3`, MVID
`95669092-3779-420e-984b-330107ff424f`, Connector/Annotator source
`01235974e2f5a7f99e80786017cf3b6c3d7b299d`. Exact game v0.111.0 / 41cef1ea,
assembly SHA256 `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`.
The 692 original session files remain unchanged. Local audit is retained outside
Git; repair receipts are under `.local/pr25-deferred-potion`.

| Population | Count |
| --- | ---: |
| Trace accepted (roots / selectors) | 189 (174 / 15) |
| Proved / durable canonical / formal strict candidates | 188 / 188 / 188 |
| Successor unresolved / cancelled | 1 / 0 |
| Explicit invalidations | 12 |
| Native diagnostic exact / unknown | 143 / 0 |
| Compatible Decision audit valid / invalid | 67 / 0 |
| Native starts / ends | 0 / 0 (resumed-run fragment) |

Nine invalidations are internal MoveToMapCoordAction and two are internal
ReadyToBeginEnemyTurnAction. The remaining invalidation is an accepted
NChooseACardSelectionScreen.SelectHolder for Colorless Potion, unable to bind
its exact native UsePotionAction owner to a Human parent. The potion use itself
is absent from trace, canonical, accepted diagnostic and root invalidations.
At least 191 Human decisions are therefore known; 188 are formally canonical,
but #43 must additionally be held for causal review, not admitted to training.
Its EndTurn execution S retains Colorless Potion; owner-ready S' already has it
removed. The native log places that independent potion use before #43's green
canonical admission. Formal audit success did not detect the missing ingress.

#19 Fruit Juice on the map is canonical with independent execution catalog and
exact player target: HP/max-HP 56/80 -> 61/85 and potion removal. The previous
AnyTime repair is Human-validated for this sample. Fire Potion #92 is canonical.
All 15 traced selector decisions and their exact parents are canonical, including
#163 Hologram -> #164 Hotfix+ with eight selectable cards and durable discard
provenance. Twelve native-input rapid bindings are canonical. #189 is a final
opened card reward with Commit but no successor before Close; it correctly
remains unknown. Earlier event reward-tree variants remain unqualified.

## First incorrect fact and native mechanism

STS2 ActionQueueSynchronizer.RequestEnqueue stores CombatPlayPhaseOnly actions
in _requestedActionsWaitingForPlayerTurn during NotPlayPhase and returns without
GameAction.OnEnqueued. At PlayPhase it requests the same exact objects again.
The old potion observer kept H/failure only in its synchronous
PotionModel.EnqueueManualUse scope; that scope ended before later OnEnqueued.
An unowned callback was intentionally not invented into a Human root. This also
excluded it from native diagnostic accounting, and its selector then correctly
failed exact Human-parent resolution.

The source defect and missing use/selector are confirmed. The old session has
no durable RequestEnqueue witness, so the exact entry timestamp/branch causing
that particular lost scope remains unproved. Native deferral is an exact-source
supported mechanism matching the observed turn-boundary sequence; no historical
input frame or lineage is fabricated to fill the evidence gap.

## Repair

A read-only RequestEnqueue Prefix binds an existing exact Human context or its
existing deferred failure to the actual GameAction reference, with session,
timeline and run identity. A weak-key identity table retains it across native
re-request; no timing, FIFO, current-root lookup or new causal ledger is used.
Request alone never creates acceptance, starts an action, or settles a root.
The true OnEnqueued observer consumes the retained context through the existing
accepted-root gate. Duplicate callbacks stay idempotent. Unowned internal
actions remain unowned; cross-session/run context cannot be inherited. A failed
carrier retention disables semantic proof instead of allowing later green rows.

A complete fair-player potion H may use the existing native-input schema when
public delivery is settling. Exact native accepted potion/target operands are
reconstructed and compared with the staged references, including rejection of
slot replacement or target changes. Native null-to-owner normalization follows
PotionModel itself. No legal public BoundAction is invented at H. Connector's
native-input subject identity uses the potion namespace so its key agrees with
the independently captured execution catalog. Execution S and A(S) are still
captured at BeforeActionExecuted, never reused from H.

On accepted failure, the retained failure reaches QuarantineAcceptedHumanEffect
and the existing causal barrier before the action runs. On accepted success,
the recorder subscribes and installs the existing exact parent binding before
ActionExecutor notification. Consequently the preceding EndTurn either ends at
the independent potion's exact pre-execution S or fails closed; its successor
cannot silently include the potion's later effect. The existing selector
mechanism then resolves the actual potion parent. No permissive native-origin
fallback was added for unbound UsePotionAction selectors.

## Verification and bounds

Regressions cover deferred scope lifetime, exact object retention, duplicate
native requests/acceptance, preserved failed H, session identity/nonreplacement,
native-input potion key and operand mismatch, no fabricated public delivery,
plus EndTurn-to-potion execution handoff versus failed-input effect barrier.
Source seam guards require retention before RequestEnqueue and restoration at
actual OnEnqueued. Native source inspection proves the real deferred-request
ordering; exact-game compilation checks the actual types and patched signature.
Full root, component, exact-game, BOM, latest-head CI and install/load receipts
belong to the final candidate. Prior Human data does not qualify new bytes.

No historical row is edited or upgraded. A request never actually accepted by
STS2 is not relabelled as an accepted Human decision. Missing H/Reads/operands,
foreign session/run, canceled delivery and missing successor stay fail-closed.
This candidate does not claim exhaustive Full-Run coverage, broader event reward
lineage, multiplayer deferral, controlled performance or STPD research admission.

Next bounded Human canary: use Colorless/Skill Potion around the enemy-to-player
turn boundary and select a generated card; repeat in ordinary play phase; inspect
potion parent and selector child, preceding EndTurn successor, exact execution
membership and missing-input counts. Regress map Fruit Juice, combat Fire Potion
and a short rapid-card sequence. Complete a reward selection before Close, with
an optional deliberate unfinished choice to verify expected unresolved status.
