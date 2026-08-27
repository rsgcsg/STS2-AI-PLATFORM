# Semantic Timeline Schema-2 Owner Closeout

Date: 2026-08-27

## Exact evidence boundary

The primary immutable owner session is
`session-20260827T042832Z-652d5bd7d9ed4943b74ac6252427cbe2`, timeline
`timeline-ae3b48160b094613bb6df45bd8683137`. It was recorded after the
following unified Platform artifact was cold-loaded:

- STS2: `v0.111.0 / 41cef1ea`;
- game assembly: `9cb4f1ad... / 57785517-0b16-42b9-8b36-bad6fb28384b`;
- artifact: `eb7ed072... / 34a36a2b-6ffa-4951-b53c-3fe72d37dd85`;
- Annotator source: `fed721c7b5131cf35d71025a3dd877266b5332fc`;
- Player Environment protocol: `1.0.0`;
- runtime: `b24a9d449a294752a6f93257bfc821b5`;
- environment fingerprint: `982ceeb769c540757f5a15175eda04d505a07314dee682bee9cb255405a6a896`;
- Modset: exact sole `STS2_PLATFORM`, fingerprint
  `ccf22e83596ee521f2c97d7691b6b70cb7501dc1b35739719c678053b3983e80`.

The game log records that exact loaded SHA, MVID and component source set at
`2026-08-27T04:08:54Z`. Runtime status and every captured Snapshot bind the
same runtime and environment. Connector observation was allowed, while
Connector mutation authority was `artifact_unqualified`; native Human input,
not Connector input, produced this recording. That authority state does not
invalidate the read-only Player Environment facts or Human/native witness.

Owner origin is attested but not machine-proven. Source, automated tests,
build, install, load and this Human session remain separate evidence levels.

## Immutable artifacts

| Artifact | SHA-256 | Rows |
| --- | --- | ---: |
| Decision V2 | `ddaf9c9c198d1964e181ae837517332b1f982785fb34e21c1820c5399b11f155` | 11 |
| invalidations | `cd9f72424747f1fd77d4b12653da81cca8eaf5d30b86f1d42b5bcba2ffe8e6f3` | 38 |
| native ledger | `1655be570186477e2e07ec7a55c0e3100adc1ffae4390096b707b6d6063a43cc` | 111 |
| semantic trace | `77202d0f068ff821b22e960cd7024d313959305b92a1a54cae0eb4382d3508b2` | 125 |
| RunJournal | `fc4abc03bca14a882619804a8058b585b9db9c42dfdb5640bac2913282658789` | 92 |

Portable V2 audit passes with 11 valid records, zero invalid records, 38
invalidations, 3,116 materialized required Reads and zero Read failures. The
38 strict Decision V2 invalidations are not semantic transition unknowns.

## Semantic dispositions

All 31 accepted Human roots have exactly one terminal semantic disposition:

- 19 proved: action sequences
  `1,2,6,7,8,11,12,13,14,16,19,20,21,22,23,25,26,27,30`;
- nine cancelled before native start and therefore not successful A:
  `3,4,5,9,17,18,28,29,31`;
- three cancelled after native start with an explicit unknown transition:
  `10,15,24`;
- zero standalone `transition_unknown`, zero pre-Commit abort and zero
  unresolved action.

The proved set contains seven card plays, eleven End Turns and one generated
card selection. Every proved pre-state equals its exact pre-execution boundary,
no proof spans another Human action start, and the explicit execution handoff
has `A26.S' == A27.S` at snapshot `state_69da46f8fe_3a4`.

Acceptance is observation-only for all 31 roots: no `action_accepted` event
contains semantic S. Every one of the 22 started roots receives S from an exact
pre-execution boundary. The generated choice observes and consumes the same
Snapshot identity, but H and S remain separate typed facts and the acceptance
event still carries no authority to bind S.

## Three unknown transitions

| Sequence | Exact action | Native lifecycle | Semantic result | Classification |
| ---: | --- | --- | --- | --- |
| 10 | `PlayCardAction`, `Play 打击 -> 幽灵船`, queue 78 | accepted -> started at `state_..._236` -> cancelled | no S' | correct fail-closed after-start cancellation |
| 15 | `PlayCardAction`, `Play 防御`, queue 95 | accepted behind A14 -> started from A14.S' `state_..._2ae` -> cancelled | no S' | correct fail-closed after-start cancellation |
| 24 | `PlayCardAction`, `Play 双重释放`, queue 116 | accepted behind A22/A23 -> started at `state_..._35d` -> cancelled | no S' | correct fail-closed after-start cancellation |

All three have exact-unique frozen BoundAction mappings. STS2 nevertheless
cancelled each action after start. The recorder correctly declines to decide
whether a successful gameplay effect occurred and does not backfill a later
state. These are not state, Read, catalog, Close or boundary-coordinator defects.

## Pattern audit

- Rapid Play is exercised. A14 settles to the state consumed by A15; A15 then
  cancels without a false successor. A22.S' equals A23.S, and A24 later
  cancels without changing either proved edge.
- Play -> End Turn/A11-style handoff is exercised by A26 -> A27. A26 is proved
  at the complete authoritative pre-execution boundary immediately consumed by
  A27, independent of interactive polling.
- Generated-card select is exercised and proved as A20. Generated-card skip is
  not exercised.
- Acceptance and execution order are identical in this session. Exact
  execution-order rebind therefore remains `not exercised` on the schema-2
  artifact; the predecessor schema-1 rebind evidence is not transferred.
- All 22 execution boundaries have complete state and required Reads. Fifteen
  are `interactive`, four `observed` and three `settling`; all happened to have
  complete catalogs. Lifecycle-status independence is Live-exercised, while a
  state-complete/catalog-incomplete handoff remains source/test evidence only.
- Close was requested after the last accepted action had cancelled before
  start. It flushed and closed in 4.162 ms with no unresolved semantic edge and
  no synthetic unknown. Earlier same-artifact session
  `session-20260827T040931Z-b158730379c74484a56ab9a1fa7bee61`
  exercised bounded draining to an explicit
  `recording_close_drain_timeout`; successful pending-edge drain-to-proof was
  not exercised.

## Verdict

Schema-2 continuous-timeline behavior is a **bounded Human-proved freeze
candidate** for ordinary combat play/End Turn, generated-card select, exact
pre-execution S binding, execution handoff, cancellation accounting and
fail-closed Close. The historical schema-1 rows remain unchanged and unknown.

No deeper STS2 instrumentation or semantic behavior change is justified by
this session. Current non-claims are exact execution reorder on the schema-2
artifact, catalog-incomplete Live handoff, successful pending-edge Close drain,
rapid lethal/combat-to-reward settlement, generated-card skip and Full Run.
The next engineering slice should be structural-only Annotator cleanup on a
new topic branch; cross-surface behavior belongs to later independent branches.
