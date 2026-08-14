# Standalone C1 RC2 Runtime Seal

Date: 2026-08-14

Verdict: `v1.0.0-rc.2` is the published, runtime-sealed C1 prerelease and the
temporary Player Environment freeze baseline.

## Exact Identity

- source/tag: `547c9addac624f7df363a93a3873ee1c2062ecc3` /
  `v1.0.0-rc.2`;
- Player Environment source digest:
  `a04ae9cf77ea22532e27a120b6df4ed5975e40f9ce6f4699bf6503ca17de4484`;
- protocol: `1.0-rc.2`;
- built/installed/loaded DLL SHA-256:
  `cf7ed1454437cb796f5931b361f655222d2f3f2e3da3a21f038a752694645cc6`;
- built/installed/loaded MVID: `6824e21d-7486-40fd-a131-43e789fdc8d2`;
- Journey runtime: `6c222d6fc32b48e6b41270fd21ebd6a2`;
- final reinstalled runtime: `bfadadc4a3be40d0af9b776f8478488f`;
- game: `v0.111.0/41cef1ea`, main assembly hash `1010476334`;
- Modset: `exact_player_environment_only`, with only `STS2_MCP` loaded.

## Automated Evidence

- 106/106 exact-game Host tests;
- 7/7 SDK tests, strict typecheck and production build;
- contract, boundary, CLI, Python, package, documentation and release-tool
  checks;
- release install/rollback and loaded-identity micro-environment tests;
- independent fresh clone produced byte-identical DLL and PDB with the same
  source digest, SHA and MVID;
- archive extraction exposed the documented `payload/` and self-contained
  tools, and its verifier matched the currently loaded process.

## Live Gates

- second controller rejected with HTTP 409;
- duplicate submit and Receipt poll returned the same terminal result;
- stale action and stale Read tokens were rejected without mutation;
- native-page `run_deck` open/read/return rejected a wrong runtime and restored
  the exact owner on a fresh successor Snapshot;
- a fresh ordinary Journey reached `game_over`: 117 delivered actions, four
  safe stale rejections, 261 Reads, 21 expected stale Reads and zero `unknown`;
- exercised menu, event, map, reward, combat, generated choice, simple card
  selection and combat-hand select/confirm interactions.

## Rollback

The final artifact was replaced with predecessor DLL
`3cfd0815a48caa52f62371976002683581db6d56df3d4e564b39108c88f8750c`
(MVID `32d6e1f5-7178-4576-a4e3-7136a91a1cbf`), cold-loaded as runtime
`4ec0ee4105784a70af2a30b47372dee5`, then rebuilt, reinstalled and cold-loaded
back to the exact RC2 identity. The latest local rollback snapshot was created
at `.local/deployments/2026-08-14T13-07-03-421Z`.

## Non-Claims

- a delivery Receipt is not STS2 business completion;
- no arbitrary game build or Modset support;
- no hidden state, reflection/coordinate mutation, Headless, training, search,
  transient VFX/SFX/history or strategy ownership;
- no evidence transfer from RC1 or from this later documentation commit to an
  unbuilt source revision.
