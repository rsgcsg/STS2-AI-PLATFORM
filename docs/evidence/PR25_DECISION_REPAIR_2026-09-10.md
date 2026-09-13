# PR25 decision semantics repair — source record

Date: 2026-09-10

This is a bounded source/evidence record. Later latest-head CI, package,
install/load identity and rollback receipts are reported on Draft PR #25.
No new Human session was recorded or gameplay performed for this repair.

## Exact starting truth and scope

Repository: `rsgcsg/STS2-AI-PLATFORM`, PR #25, Draft, target `develop`.
Fetched base: `cd9a0fbb0c85577a13513abe715f91d794ac86eb`.
Starting exact head: `783721ab5db6e9e562979e3a93ed6fc40ddedd51`.
The existing clean exact-head worktree was reused. Change classes are G2/G3/G4
and G5 source preparation; Human G5 qualification remains an owner gate.
No STPD pin or source changed. Rollback uses the exact Game Mod deployment
backup, never a rewrite of historical recordings.

## First incorrect facts and repairs

| Owning layer | First incorrect fact | Repair |
| --- | --- | --- |
| Annotator lifecycle | A RunState poll setting active suppressed a later native Launch witness | Activity and native-start/end provenance have separate state; preserve one run identity for poll then Launch |
| Annotator projection | `OnAlternateRewardSelected` had no admitted canonical family | Add the existing replacement-selection family; first-class reward inputs also retain decision lineage |
| Annotator correlation | A nested hand input was treated as the next external Human effect | Exact factory/PlayerChoice parent binding and one decision per native input within the same causal root |
| Annotator task binding | Synchronous native calls reached completion seams before UI Postfix inserted owner binding | Stage exact owners in Prefix for reward claim, chest, treasure/reward proceed; never reinsert consumed carriers; collisions fail closed and unclaimed exceptional exits release only their exact binding |
| Annotator acceptance | A reward/merchant callback returning native false was counted as accepted | Observe exact already-completed `Task<bool>` rejection before semantic admission; retain a rejected-input journal fact |
| Annotator pre-match | Merchant removal was described as `activate` while Connector publishes `open` | Match the existing public verb, without reconstructing merchant legality |
| Annotator Human origin | Native automatic deselection/completion could be labeled Human | Bind hand deselect to actual input callbacks, suppress native automatic paths, require exact active input owner for terminal Human continuation |

Native selection completion freezes the pre-input Task reference so synchronous
REROLL replacement cannot erase the accepted occurrence. Native pause is never
manufactured when the actual parent pause has not been observed.

## Architecture and durable evidence

[ADR-0006](../adr/0006-decision-occurrences-within-causal-roots.md) is the design
owner. `DecisionOccurrenceIdentity` is versioned within the current semantic
trace/canonical contract. New manifests require decision metadata; audit checks
parent/session/timeline/run/order, immutable identity, exact nested owner,
surface grounding and canonical equality. Old manifests retain their original
meaning and cannot acquire independent selector decisions by reinterpretation.

Connector adds a read-only process-local owner/operation/operand correlation
query over its frozen complete catalog. It does not change wire action delivery,
legal action generation or public owner visibility. Annotator captures separate
select/deselect/preview/cancel/confirm occurrences with their own state/catalog
and exact lineage. Complete in-place selector states can be successors;
terminal results wait for a separate causal boundary. Missing evidence retains
an explicit failed-closed Human occurrence or unresolved transition.

Selected card identity and DrawPile/DiscardPile provenance use the same
content-addressed Snapshot/Read evidence. A future canary must verify these
bytes; old continuation-only rows cannot supply an independent selector state
or action space.

## Forensic baseline and limits

The immutable target session is
`session-20260909T151736Z-a735ecdb59544af3a7b6b20c6898c3c9`.
Its predecessor results remain 299 accepted, 250 proved, 49 unresolved,
249 canonical, 112 invalidations, 137 native diagnostic exact, 34 unknown
(32 map, 2 relic), 0 native starts and 2 native ends. These are historical
counts, not measurements of the repaired runtime.

The 13 hand-selector interruptions and missing Skip canonical projection have
source explanations. Reward zero-match observations cannot all be called lost
accepted decisions: the native reward method returns false for rejected
procurement and re-enables the reward button. The exact completion-result guard
is authoritative; potion-belt inference is not acceptance authority.
Three PlayCard failed-pre snapshots were not durably available, so their first
incorrect fact cannot be proven from the session. They remain fail-closed;
there is no guessed staging, polling or backfill repair. Diagnostic unknowns
and genuine external overlap are not promoted into canonical evidence.

## Verification and review

Focused Annotator tests cover native run permutations, parent handoff, separate
select/deselect transitions, missing pause/owner rejection, lineage round-trip
and corruption, and nested canonical projection with its own frames/catalog.
Connector exact-game tests cover native reference disambiguation, wrong
owner/operation/subject, ambiguity and incomplete catalogs. Existing external
Human overlap and persistence tests remain in the same tracker suite.

Two bounded independent reviewers examined lifecycle/task binding and decision
semantics against current source and exact macOS native code. Their findings
led to the automatic-input, owner metadata, synchronous Task replacement,
exception cleanup and surface-validation repairs. Lead verified the changes;
reviewer prose is not runtime evidence.

Exact native assembly used for source checks: STS2 `v0.111.0/41cef1ea`,
SHA-256 `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`,
MVID `57785517-0b16-42b9-8b36-bad6fb28384b`. No proprietary source or raw
Human recording is committed.

## Bounded Human canary and non-claims

1. Start Recorder before a new run; finish/end it and start another. Verify
   exact native start/end counts and run identity without poll suppression.
2. Play a hand-selector card; select, deselect, replace and confirm. Verify
   parent proof, separate child decisions, exact shared root/parent and no
   subsequent-Human truncation caused by the child.
3. Exercise DrawPile and DiscardPile selectors. Verify durable selected card,
   pile provenance, complete independent catalogs and exact successors.
4. Exercise card-reward Skip/REROLL and merchant removal preview/cancel/confirm;
   verify each accepted decision and reject native false outcomes accurately.
5. Exercise chest/reward proceed and close the session only after a bounded
   successor opportunity. Audit persistence and retain honest unresolved ends.

No new Human/Full-Run qualification, exhaustive surface coverage, performance
improvement, Windows runtime refresh or elimination of diagnostic unknown is
claimed. STPD follow-up is a version-aware adapter consuming decision lineage
and raw decision sequences after Platform qualification. Forward-only filtering,
policy, training and research admission stay outside Platform.
