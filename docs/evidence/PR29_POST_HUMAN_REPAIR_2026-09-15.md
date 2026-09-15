# PR29 daily collection: post-Human repair

The tested Platform workspace was `0cd01132b6bd056f0035d5a42b5d9ec4b322928d`, with
Mod SHA256 `38210d6d5c670aa5e06edf93d0da9632e6ed59b743d8859d027d297faf6abc95`
and MVID `6572eae4-75f3-44db-9e6a-4b2feebbee07`. The game was v0.111.0 / 41cef1ea,
assembly SHA256 `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`.
The actual member recorded a new native start through the beginning of act 2, then closed
the Recorder. This was an uninterrupted recorded prefix, not a natural-terminal Full Run.

## Actual result

270 accepted decisions (234 roots, 36 children); 269 proved/canonical; one real unresolved
decision. All 36 children had canonical parents. Native semantic diagnostics: 184 exact,
zero unknown. The compatibility projection contained 99 valid and zero invalid records;
it is not the canonical denominator. Native starts/ends: 1/0. Capture/persistence failures: 0/0.

All 45 explicit invalidations were internal native action diagnostics: 28
`ReadyToBeginEnemyTurnAction` and 17 `MoveToMapCoordAction`. They were not failed Human
decisions. All observed 121 PlayCard, 41 End Turn, four potion and 36 nested decisions were
canonical. No exhaustive-content or unseen rapid-input claim follows from these counts.

Automatic Close-to-outbox-to-Hub/R2 delivery, exact member association/sharing, member download
and stopped/reopened queue identity passed independently. The failed decision remains in the
accepted evidence archive. All 932 raw files were unchanged by audit. No historical successor
was supplied from a later Snapshot. Cloud acceptance is not Human all-valid or research admission.

## Owning corrections

1. **Event Proceed map readiness.** The last event Continue successfully committed
   `EventOption.Chosen`, opened its map synchronously, then became unresolved at Close 3.44
   seconds later. The native `NEventRoom.Proceed` path sets travel, opens the map and completes
   synchronously. Annotator observed Commit but omitted that distinct exact map-ready boundary.
   Five event Continues exercised the path; the earlier four settled only at the next Human
   pre-execution boundary. One actual loss is attributable to this source defect.

   Commit `7f5257034a02615c61d928052960dfb1ebe646ae` retains the pre-callback map object/open state,
   requires successful synchronous completion and the exact bound/current invocation root,
   records Commit and emits the Native Foundation's separately validated owner-ready observation.
   The existing tracker still requires complete Connector state/action-space evidence. Reward
   and event Proceed share only a stateless exact handoff predicate. No polling, delayed frame,
   current-root guess, additional tracker or post-Close repair was introduced. Native-frozen
   event Proceed semantics also supply the correct family instead of inferring from public
   `activate`. Old family labels remain unchanged.

2. **Closed Recorder root projection.** Successful Close disposes the store, retains the last
   session ID and publishes the configured recording root. The Game Mod setup helper incorrectly
   treated that root as an active session subdirectory. Commit
   `aae6702c01b81312eadd13459da768f2ac8d0ce3` handles that explicit completed-Close state, while
   preserving wrong-root, malformed, active/closing and current-process identity failures.

3. **Safe fixed-tool retirement.** Installing another tool must not relabel an existing outbox.
   Evidence rc.9's `completed_delivery` holds worker ownership, reads SQLite without constructing
   or migrating the outbox, verifies every sealed source and its bundle/transfer/archive/receipt,
   rejects extra or unfinished work and rechecks after the caller's operation. A verified bundle
   with honest recording failures qualifies for transfer completion, not Human success. STPD
   consumes this proof to prepare fresh directories under unchanged v2 consent. Platform does
   not own membership, consent renewal or the STPD active-profile pointer.

## Evidence and remaining gate

Focused and complete Annotator source tests passed: Core 273/273 plus portable Node suites.
The clean native repair head passed exact-game compilation with zero warnings/errors. Evidence
rc.9 passed 101 tests. Its committed-source check of the actual old queue confirmed one verified
session, 932 raw files and 944 persistent outbox files unchanged; deterministic completion SHA256
`81242a4dc2e288d5122e3363b8709ab44c353e9d40597632dfd3654343307357`.
These are source/test and old-evidence-integrity results, not a new installed/Human receipt.

The integrated Mod, fixed tool, BOM, cross-repository pin, CI, cold-load and Human gate must use
one new exact candidate. Required Human regression: event Continue opens map, then Recorder
Close without another gameplay input; verify canonical success and correct closed setup status.
An uninterrupted native start through natural terminal remains a separate Full-Run gate.
Keep the tested predecessor Mod/tool/config and raw failed session for rollback and comparison.
