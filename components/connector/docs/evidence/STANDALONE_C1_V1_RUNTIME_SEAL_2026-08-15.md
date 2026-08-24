# Standalone C1 v1.0.0 Runtime Seal

Date: 2026-08-15

Verdict: `v1.0.0` is the published, runtime-sealed stable Player Environment
C1 baseline for the exact identity below.

## Exact Identity

- source/tag: `c38d4ad2e9d6eb029f8853ed852cce1152bc6d50` / `v1.0.0`;
- Player Environment source digest:
  `fdc91b0ee57046b9695d6f5b7c53ac04d43abf363ba2db618251a89796b4c258`;
- protocol and TypeScript SDK: `1.0.0`;
- built/installed/loaded DLL SHA-256:
  `5014224ce8a1f5a61455f21d6873a87052eac533acffce04ac3fb75195bff185`;
- built/installed/loaded MVID: `68f7a9aa-c293-4897-94cd-1e59ab6dd180`;
- native-page runtime: `d14ca976c7ea4cfe8d0d18a738d25a58`;
- final ordinary-run and reinstalled runtime:
  `81aa04efe03a4ea8ad79ee07d781cc52`;
- game: `v0.111.0/41cef1ea`, main assembly hash `1010476334`;
- Modset: `exact_player_environment_only`, with only `STS2_MCP` loaded.

## Automated Evidence

- 106/106 exact-game Host tests;
- 7/7 SDK tests, strict typecheck and production build;
- contract, boundary, CLI, Python, package, documentation and release-tool
  checks;
- independent fresh clone produced the same clean-source digest, DLL SHA and
  MVID;
- public release assets passed anonymous checksum verification;
- the verifier extracted from the public Host archive matched the loaded
  source, protocol, SHA, MVID, game and Modset.

## Live Gates

- second controller rejection, request idempotency and Receipt polling passed;
- stale action and stale Read tokens were rejected without mutation;
- native-page `run_deck` rejected a wrong runtime, opened and read 11 visible
  cards with complete declared content, returned without action authority and
  restored the exact map owner;
- a same-artifact ordinary run started at the main menu and reached
  `game_over` using two bounded conformance-runner processes;
- the final continuous segment delivered 89 inputs, performed 209 Reads,
  rejected 105 stale Reads and nine stale Snapshot actions, and observed zero
  `unknown` receipts;
- covered menu/run entry, combat, event, map, reward, card reward, shop, rest,
  treasure, deck upgrade and generated-card interactions.

The two-process harness boundary is explicit: this is a same-runtime ordinary
game run, not evidence of one uninterrupted production Agent process.

## Rollback

The stable artifact was replaced with RC2 source
`547c9addac624f7df363a93a3873ee1c2062ecc3`, protocol `1.0-rc.2`, DLL SHA
`cf7ed1454437cb796f5931b361f655222d2f3f2e3da3a21f038a752694645cc6`
and MVID `6824e21d-7486-40fd-a131-43e789fdc8d2`. It cold-loaded as runtime
`f1de33c153084e4e9b8c6f958e2a8f09`; stable was then rebuilt, reinstalled and
cold-loaded as runtime `81aa04efe03a4ea8ad79ee07d781cc52`.

The final local rollback snapshot is
`.local/deployments/2026-08-14T13-53-24-382Z`. Local paths are operational
evidence, not portable release content.

## Non-Claims

- a delivery Receipt is not STS2 business completion;
- no arbitrary game build or Modset support;
- no hidden state, reflection/coordinate mutation, Headless, training, search,
  transient VFX/SFX/history or strategy ownership;
- no evidence transfer from RC artifacts or from this later documentation
  commit to another source revision.
