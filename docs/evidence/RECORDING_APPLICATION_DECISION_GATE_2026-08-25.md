# Recording Application Decision Gate - 2026-08-25

## Exact runtime

- Artifact SHA-256: `d3b25e628068f4a6946be0c1182f00745fd6195f9c0b02920bc9bc699b2d0b2d`
- Artifact MVID: `ee78d9a1-791e-4582-b8fa-97cc1949cd2a`
- Runtime instance: `88db3f9cf1e940ba906cab09e87714df`
- Game: `v0.111.0 / 41cef1ea`
- Modset: exact unified `STS2_PLATFORM`
- Session: `session-20260825T115335Z-08907007f20a49318573f638ff627696`

## Live facts

Independent V2 audit passed with six valid records, zero invalid records and 21
fail-closed invalidations. All six admitted actions were
`ordinary_combat.end_turn`. Each record contains pre and successor `run_deck`
and `combat_piles`, for 24 materialized Reads and zero Read failures.

All 21 invalidations came from real `PlayCardAction` attempts with
`pre_frame_not_complete_interactive,no_same_context_authoritative_frame`.
`ordinary_combat.play_card` is declared supported, so these are recorder defects,
not out-of-scope actions. The staged complete frame captured by
`NPlayerHand.StartCardPlay` was incorrectly required to have the same snapshot
and catalog as the expected transient frame immediately before STS2 native
`TryPlayCard`.

The first Close request and `session_closed` journal event are 5.036 ms apart.
The session did not require a second Close to flush. The Live UI displayed the
command's accepted `Closing` result indefinitely; the second click merely read
the already-closed state. This is a presentation synchronization defect.

## Repair boundary

Source now reuses a staged card frame only when exact card object, runtime,
environment, interaction, monotonic sequence, bounded age and no external
controller all agree. Snapshot/catalog equality is intentionally not required
during the native card-play transition. STS2 still owns legality and the
accepted `PlayCardAction`; the recorder creates no execution authority.

Recording status revision 2 exposes four disjoint views:

- recorded by action family;
- supported but failed closed and therefore not recorded;
- supported but not observed;
- declared out of capture-profile scope.

The Live UI queries authoritative status after every recording command, so a
synchronous Close reports `Closed` after the first click.

## Evidence boundary

The session proves end-turn admission, bounded Reads and safe close on the exact
artifact above. It does not prove the staged-card repair or revised UI/status,
which require a new build, install and cold load. Generated-card skip remains
not exercised. Human origin remains owner-attested.
