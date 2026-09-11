# PR25 potion native-input persistence repair

G5; Annotator owns the incorrect native-input correlation validator. No native
legality, Connector, UI, STPD or gameplay change. Base develop:
`cd9a0fbb0c85577a13513abe715f91d794ac86eb`. Audited head:
`34a4e1196df1b6ee170c656af7c8746a4f11315c`. Continue Draft PR25, no merge.
STPD pin remains `67566db562b95f57468290b97659c9f2e3862fe1`.

## Exact Human audit

Session `session-20260911T063648Z-fc192099edc74dbebee90302e695e336` closed at
06:39:36 UTC. Loaded runtime `770866d3419b40f2a21215406e31c6bb`, artifact SHA256
`88c5cb6b6ed179d3c054c01322086b2ecfdf81776e96bd0e59709fbf32e92341`, MVID
`15844780-74b3-4617-a475-dd61a668a236`. Annotator source
`7f7189c599cb5e0bd7a4d6d1016a413a3685cdc3`, Connector source
`08cc7f0449a17562ba85eb841ab6d28732519775`. Exact game v0.111.0 / 41cef1ea,
assembly SHA256 `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`.

| Population | Count |
| --- | ---: |
| Accepted roots / nested decisions | 104 / 15 |
| Trace proved / durable canonical | 116 / 115 |
| Native cancellation before execution | 2 |
| Successor unresolved | 1 |
| Explicit invalidations | 12 |
| Native diagnostic accepted / successful exact / cancelled / unknown | 90 / 88 / 2 / 0 |
| Compatible Decision valid / invalid | 30 / 0 |
| Native starts / ends / observed in progress | 0 / 0 / 1 |

Accepted families: 68 PlayCard, 15 EndTurn, 12 hand-selector inputs,
9 reward claims, 4 map travels, 3 potion uses, 3 card-reward selections,
3 reward proceeds and 2 event options. All 15 selector decisions and their
exact parents are canonical. Seven rapid native-input rebindings are mechanical
semantic candidates. #81/#82 were cancelled by STS2 before execution during a
hand-selector sequence, correctly not canonical. #119 opened the final card
reward but Close preceded a selector decision/successor; expected unknown.

All three native potion-use log entries match traced Fruit Juice uses:
#1 event (56/70 -> 61/75), #19 combat (61/75 -> 66/80), and #33 while card-reward
selection remained open (66/80 -> 71/85). Each has exact player target and potion
removal evidence. Console staging is a separate intervention, not an ordinary
Human gameplay decision or training admission. No new silent potion loss was
found. Colorless/Skill Potion generation was not exercised in this session.

Invalidations are fully accounted: four internal MoveToMapCoordAction and seven
internal ReadyToBeginEnemyTurnAction type mismatches (diagnostics, not eleven
lost Human decisions); one real `semantic_projection_persistence_unknown` for
#19 UsePotionAction. Its detail is `native_input_correlation_invalid`.

Canonical proportion is 115/119 = 96.64% of accepted inputs. Among 116 proved
transitions, 115/116 = 99.14% persisted. These denominators distinguish native
cancellation and unfinished recording from a source defect; neither is Full-Run
qualification. This is an observed-run fragment with no native start/end.

## First incorrect fact and repair

`RecordedNativeInputValidator.Validate` admitted only PlayCardAction/play and
required a PlayCardAction witness, although the exact native-input producer also
supports UsePotionAction/use. #19 has H, exact accepted native operands, independent
execution S and exactly-once potion membership, native Commit and a proved
successor. Canonical projection calls the shared validator and throws before
append; this is a semantic validation mismatch, not evidence of disk failure.
The predecessor formal session auditor rejects the same valid potion input.

The validator now accepts precisely the two producer type/verb pairs and requires
the witness type to match the action type. It still requires the exact mapping,
subject, nonempty operands, game_action mechanism and absence of a fabricated
public BoundAction. Execution phase, exact selected key/operands and catalog
membership remain independently checked. No generic action acceptance or
legality reconstruction is introduced.

The previous causal repair is positively observed: EndTurn #18 ends at
`state_a322a26001_3d`, exactly #19's pre-execution frame, which still contains the
potion. #19 ends at `state_a322a26001_3f`, before #20 executes, with the potion
effect present. The EndTurn successor is not contaminated by the independent use.
This does not prove the historical initial RequestEnqueue deferral branch.

## Regression and non-claims

A synthetic production tracker -> action-space persistence -> canonical
projection -> append -> final RecordingSessionAuditor regression reproduces the
same potion failure before the fix (one failed, three existing cases passed).
After the fix all four cases pass, including rejecting a hash-consistent catalog
with an admission phase mislabeled as execution. Six type/verb/witness matrix
cases preserve unsupported/mismatched fail-closed behavior. Core total: 229 pass.
The final candidate requires full root, exact-game, clean identity/BOM, latest-head
CI, install/cold-load and verify-loaded receipts under `.local/pr25-canary-063648`.

The rebuilt reader structurally accepts the unchanged old evidence (30 compatible
valid, zero invalid), but that does not recreate the missing canonical #19 or
remove its recorded invalidation: canonical count stays 115. Historical raw
files and the native log are preserved. No backfill or evidence rewrite occurs.
Remaining non-claims: exhaustive Full-Run, generated potion selector after deferred
input, earlier event reward-tree variants, controlled performance and STPD research
admission. A new artifact needs its own Human validation.

Next bounded Human canary: Fruit Juice at a turn boundary and on a non-combat
surface; Colorless/Skill Potion plus generated-card selection; short rapid-card
and hand-selector sequence; complete a reward choice before closing. Audit
canonical persistence, exact parent/root lineage, prior EndTurn handoff, and
native cancellations separately. No automated gameplay is used.
