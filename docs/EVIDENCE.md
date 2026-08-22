# Evidence

Evidence is exact and non-transferable. Source, test, build, boot, loaded,
mutation, journey, differential, performance and qualification are separate
levels. The `v1.0.0` release runtime-seal asset is the machine-readable
authority for the final tag; raw reports remain ignored under `.local/`.

## Frozen Tuple

- game: macOS arm64 `v0.111.0/41cef1ea`;
- `sts2.dll`: SHA-256 `9cb4f1ad...f12b4`, MVID `57785517...84b`;
- Managed patch: `ed9248b...341c2`;
- Managed Host: `a884b104...b3d66`, MVID `5b6adbd6...38b`;
- Connector: `v1.1.0-rc.1/e065102`, Host `c1877f1a...7586`,
  MVID `64765ea1...5825`;
- Player Environment protocol/SDK: `1.0.0/1.0.0`.

## Reused Exact Evidence

| Gate | Exact evidence | Boundary |
|---|---|---|
| Candidate build/audit | Reproducible upstream + patch ledger produces `a884b104...` and the exact unmodified game assembly | source/build, not runtime |
| Stable information | Ten fixed Managed episodes, 2,129 decisions and 3,944 Reads reached terminal with complete supported-scope information | bounded corpus, not all content |
| Reset/authority | Ten resets, 1,670 deliveries, 9/9 old-authority stale refusals and 10/10 duplicate Receipt replays | same exact candidate |
| Ambiguous loss | Accepted request followed by process loss closed authority; distinct exact runtime recovered | Commit intentionally unknown |
| Reliability | 8 workers, 5,600 episodes, 1,084,992 decisions, zero unknown/process failures | predecessor Headless source, same Host artifact; not formal current-tag soak |
| Python consumer | External `reset/observe/read/step` episode reached terminal | one consumer episode |
| Learner contention | 2 workers, 12 terminal episodes, 1,567 deliveries, 1,163 learner updates, zero unknown/failure | STPD integration smoke |
| Reference transfer | Two same-seed Candidate and shipped-Reference runs all reached terminal; 327 Managed and 390 Reference deliveries, zero unknown; one exact terminal outcome matched | execution transfer, not broad parity |
| Card reward settling | Connector `e065102` cold-loaded as `c1877f1a.../64765ea1...`; partial cards published no authority and the stable successor exposed all three cards plus Skip | exact named lifecycle |

The final release seal adds current-tag audit, Python smoke, reset/recovery and
worker identity gates without repeating the long campaigns above.

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
