# Status

Release: `v1.0.0` STPD v0 operational baseline

Verdict: **operationally frozen for STPD v0; formal H1.0 qualification is not
claimed.**

## Exact Baseline

| Layer | Frozen identity |
|---|---|
| Game | macOS arm64 STS2 `v0.111.0`, commit `41cef1ea` |
| Game assembly | SHA-256 `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`, MVID `57785517-0b16-42b9-8b36-bad6fb28384b` |
| Managed upstream | `wuhao21/sts2-cli@d11aa883b582dd68bd39b331f3370746b30d447e` |
| Managed patch | SHA-256 `8ced088bffbfeaa378d4580ca2c254c41cd89ae58b5b1e9b6ee1a764f8eef87e` |
| Managed Host | SHA-256 `8dc622b0c003dc632a753e0bb524a82690290930dfe9573a4c71cb8234a6d8a6`, MVID `7228541c-d4f4-4033-9ff5-30f4c9997e98` |
| Connector | `v1.1.0-rc.1`, source `e0651024117d22bdeb95142766917103d87c0185` |
| Connector Host | SHA-256 `c1877f1af1b311904b0d536fdfc08cd5c425281f4cc93eed2ff11729380c7586`, MVID `64765ea1-29fe-4475-9b7d-3b0d65955825` |
| Player Environment | protocol `1.0.0`, SDK `1.0.0` |

The tag and its runtime-seal asset bind the final Headless source identity.
Generated candidates, game files, saves and raw reports remain local and are
not release assets.

## Operationally Verified

- exact identity admission and wrong-identity fail closed;
- complete finite action authority only on stable snapshots;
- state-bound Reads and actions, stale refusal and exact duplicate Receipt
  replay;
- request idempotency and unknown-no-retry;
- real state -> legal BoundAction -> native Commit -> stable successor or
  terminal;
- reset invalidates old authority;
- ambiguous delivery loss quarantines the process and recovers only through a
  distinct exact replacement runtime;
- independent Python `reset/observe/read/step`;
- two-worker STPD actor/learner contention with exact episode provenance;
- two Candidate-trained-policy executions on shipped Reference, both reaching
  terminal with zero unknown delivery; one exact terminal result matched;
- Connector card-reward mounting remains `settling` with no mutation
  authority until the complete selectable-card catalog is ready.

The long one-million-decision capacity run is retained as predecessor
reliability evidence for the same Managed artifact, not as formal current-tag
qualification.

## Ownership

- STS2 owns gameplay rules, RNG, legality, effects and Commit.
- Headless owns process/runtime lifecycle, exact compatibility, reset,
  recovery, worker orchestration and evidence.
- Connector owns the Host-neutral Player Environment contract, Host-local
  binding, one controller, delivery Receipt and successor.
- STPD owns strategy, rewards, learner state, data and evaluation.

## Deferred Qualification And Non-Claims

- no 72-hour or 10-million-decision soak;
- no exhaustive/randomized CrossHost corpus or all-content coverage;
- no full fault matrix, cluster/high-core or cross-platform qualification;
- no real alternate-build requalification campaign;
- no arbitrary game version, Modset or future patch support;
- no claim that two transfer episodes prove broad semantic parity or policy
  quality;
- no claim that a delivery Receipt proves business completion;
- no hidden-state, arbitrary reflection/coordinate input or second gameplay
  rules engine.

Any game/assembly, Managed patch/artifact, Connector source/artifact/protocol,
Modset, information-policy or hard-shell change reopens qualification. See
[Evidence](EVIDENCE.md), [Compatibility](COMPATIBILITY.md), and
[Operations](OPERATIONS.md).
