# PR25 native admission and Full-Run accounting

G5: native Human correlation, versioned recording evidence and Live UI.
Base develop `cd9a0fbb0c85577a13513abe715f91d794ac86eb`; audited PR head
`bb8a13357c615385d18e9990fc797095b6c7c9bb`. PR25 remains Draft.
STPD pin `67566db562b95f57468290b97659c9f2e3862fe1` is unchanged.

## Exact Human evidence

Closed session `session-20260910T231811Z-34bd87b5c12140acb01bce6c234c2b75`
uses Annotator `68e5fa443e6f1e7b9bc78c7cf131ab35047b92d4`, Connector
`80d2a77664417d39352660ea2f5dbe9092e255ea`, v4 capture profile and decision
schema 2. Loaded artifact SHA256
`10eb3296930d486818bd1dc7594718e50b40a4a973d64881fcd4228707f7cbed`,
MVID `8e522e38-b548-4f40-9d5d-dc7f40f7295a`, runtime
`4c11106eb6644a5a906269dd4eeb7401`.
Exact STS2 main assembly SHA256
`9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`,
MVID `57785517-0b16-42b9-8b36-bad6fb28384b`.
Raw hashes and tool receipts remain local under `.local/pr25-native-admission`.

| Accounting boundary | Count |
| --- | ---: |
| Known accepted Human decisions including failed ingress | 63 |
| Durable accepted semantic decisions | 61 |
| Proved / canonical | 55 / 55 |
| Unknown | 2 |
| Cancelled before / after start | 2 / 2 |
| Explicit invalidations | 11 |
| Legacy decision audit valid / invalid | 15 / 0 |
| Strict semantic transition candidates | 54 |
| Native membership exact / unknown | 30 / 0 |
| Native starts / ends | 1 / 0 |

All invalidations: five internal MoveToMapCoordAction diagnostics, four
internal ReadyToBeginEnemyTurnAction diagnostics, two accepted PlayCard
pre-frame capture failures. The nine diagnostics are not nine extra Human
decisions. Both unknowns (one PlayCard, one EndTurn) are exact
`unrecorded_human_effect_before_successor` fences caused by these two failed
card inputs. The native diagnostic audit covers 34 queued actions including
four cancellations, not all 61 decision occurrences.

All 12 selectors are canonical: four nested selectors and eight selectors
with native origin. The remaining 43 canonical rows are roots. This bounded
session does not exercise every Full-Run surface or prove all future lineage.
The previous queued catalog fix is present: 30 execution catalogs are captured
at before_execution; 13 source-local callback catalogs legitimately use
before_native_action_admission. No queued admission catalog passes current audit.

The strict calibration accepts 54 rather than 55 canonical rows: action #37's
successor is the exact execution boundary of another PlayCard subsequently
cancelled after start, with no complete selected-action catalog there. The
canonical transition preserves a causal state boundary, but this boundary is
not proven to be an independently legal next policy decision. Keep that
consumer eligibility distinction; do not turn the cancellation into success.

## First incorrect fact and repair

At 23:40:46.29642Z and 23:40:59.050137Z native PlayCard acceptance occurred
while public delivery was settling with zero actions. In the second case,
card staging was in Start and submission in Play; in both submissions
InCardPlay was true. Native input acceptance and public delivery availability
are different boundaries. Exact native source inspected in the preceding
[rapid-input audit](PR25_RAPID_INPUT_EXECUTION_AUDIT_2026-09-11.md) proves
that UI acceptance may precede queue execution readiness. The failed fact is
Annotator requiring a public BoundAction as the only identity of an accepted
Human input. It is not player error or Recorder joining late.

Connector's process-local witness now correlates scoped expected and accepted
subject/operand references without issuing a public BoundAction. Annotator
uses this only for an exact PlayCard scope with complete fair-player state,
required Reads, exact environment/modset and no external controller. Only the
native accepted callback claims the occurrence. No polling, time-order matching,
new GameAction, altered input or widened public legality is introduced.

`RecordedNativeInput` retains the native key, verb, subject, operands and label.
The execution subscription retains the exact native selection, not a cached
catalog. At BeforeActionExecuted, native schema-3 action-space evidence must
match that input key and all operands exactly; game actions cannot use an
admission catalog. Canonical schema 3 contains exactly one of public action or
native input. The typed audit verifies this identity against the trace and
content-addressed catalog. Cancelled input retains its disposition and never
projects a successful canonical. Legacy public-BoundAction decision projection
is intentionally omitted for this input representation. Live UI still uses
RecordId for lifecycle and displays the native key separately from public ID.

## Consumer boundary and Full-Run distance

The engineering target is lossless accounting of every accepted Human decision,
with every provable completed transition canonical. It is not a requirement
that cancelled, interrupted, unsupported or incomplete outcomes be called
successful training samples. STPD needs an explicit adapter for canonical and
execution schema 3/native-input selection, preserving H versus S, occurrence
versus causal root, and cancellations. It must not infer legality or train on
unknown outcomes. Platform changes no model, reward, dataset or agent policy.

Full-Run is not qualified: this short run has no native end; fresh Human evidence
for this repair is pending; prior long runs retain potion mapping/discard and
terminal/owner successor gaps. The scoped card repair does not retroactively
recover any historical failed occurrence. No matched performance, complete
surface coverage, all-valid run, training admission or model rollout is claimed.

## Validation and bounded canary

Regression coverage includes scoped exact versus mismatched card/target,
empty public catalog, no invented BoundAction, execution phase/key/operand
mismatch, mutually exclusive canonical identity, cancellation rejection,
content-addressed store/audit round trip and UI RecordId correlation.
Final exact source/test/build/install/load receipts are recorded in the PR
handoff after clean qualification. Source/test evidence is not Human evidence.
Rollback uses the owning Game Mod deployment snapshot; historical sessions
remain immutable.

Next Human canary: (1) rapid numbered-key and targeted cards across Start/Play
and with a card already in flight; (2) an accepted queued card that native
cancels; (3) a nested selector followed by rapid play, checking independent
child and exact parent lineage; (4) potion use/discard at combat and non-combat
owners; (5) one fresh start through a native run end before closing Recorder.
Audit accepted-input accounting, zero unexplained ingress loss, execution
membership, all dispositions and exact run boundaries separately.
