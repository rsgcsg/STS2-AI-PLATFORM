# Evidence

Evidence is exact and non-transferable. Source, test, build, boot, loaded,
mutation, journey, differential, performance and qualification are separate
levels. The `v1.0.1` release runtime-seal asset is the machine-readable
authority for the final tag; raw reports remain ignored under `.local/`.

## Frozen Tuple

- game: macOS arm64 `v0.111.0/41cef1ea`;
- `sts2.dll`: SHA-256 `9cb4f1ad...f12b4`, MVID `57785517...84b`;
- Managed patch: `8ced088b...87e`;
- Managed Host: `8dc622b0...8a6`, MVID `7228541c...e98`;
- Connector: `v1.1.0-rc.1/e065102`, Host `c1877f1a...7586`,
  MVID `64765ea1...5825`;
- Player Environment protocol/SDK: `1.0.0/1.0.0`.

## Current Patch Evidence

| Gate | Exact evidence | Boundary |
|---|---|---|
| Candidate build/audit | Reproducible upstream + patch ledger produces `8dc622b0...` and the exact unmodified game assembly | source/build, not runtime |
| Stable information | Current Python collector reached natural game over with 10 Combat transitions, complete finite authority and complete `run_deck`/`combat_piles` Reads | one bounded current-artifact journey, not all content |
| Receipt/successor | All collected actions had exact delivery Receipts and independently observed stable successors | action-local delivery, not business completion |
| Card reward settling | Connector `e065102` cold-loaded as `c1877f1a.../64765ea1...`; partial cards published no authority and the stable successor exposed all three cards plus Skip | exact named lifecycle |

The `v1.0.0` reset/recovery, reliability, learner-contention and Reference
comparison reports are preserved as predecessor evidence only. They bind a
different patch and Host artifact and therefore do not qualify `v1.0.1`.

## Environment-Invalid Conditions

An STPD episode/data segment is invalid if any of these occurs:

- `unknown` delivery;
- incomplete action catalog at a decision;
- settling timeout;
- missing successor;
- stale authority is replayed rather than re-observed and reselected;
- runtime/environment identity changes within an episode;
- request, BoundAction or Receipt identity differs;
- unexpected environment/driver exception.

The cheap Python smoke fails closed on these conditions. It does not replace
Reference differential or formal qualification.

## Deferred And Non-Claims

No claim is made for exhaustive semantics, every card/relic/event, arbitrary
Mods, later builds, other platforms, 72-hour/10-million soak, broad fault
matrix, high-core/cluster operation or policy quality. Receipt means native
delivery, not business completion. A same-seed terminal difference remains a
semantic investigation signal, not proof that either Host is wrong.

Historical experiments and their exact boundaries remain under
[docs/evidence](evidence/). They are useful provenance, not current release
authority.
