# Human Evidence V2 Read-Rich Combat Closeout

Date: 2026-08-25

## Verdict

The exact V2 Native artifact has a verified ordinary-combat Human Evidence path:

```text
native human play
-> Decision V2 with complete BoundAction set and required Reads
-> exact accepted action and interactive successor
-> audited portable bundle
-> typed immutable Store/Transfer/Receiver
-> independent STPD projection
```

Generated-card choice is implemented and deterministic-tested but did not
naturally occur: `not exercised`. This closeout does not authorize training or
qualify unexercised families.

## Exact Runtime

- Game: `v0.111.0`, commit `41cef1ea`, assembly SHA
  `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`,
  MVID `57785517-0b16-42b9-8b36-bad6fb28384b`.
- Connector: source `09e5c2363fff64b2e8cfa07d16ec7cf22d8ad3b5`,
  artifact SHA `6c66cbf06757dca3e7d910467fb56b89376f109c6d7d0ba47d2bc0260ab4a3d2`,
  MVID `11f1da35-558f-4346-8ec9-a493fea95cb8`, protocol `1.0.0`.
- Annotator: source `09e5c2363fff64b2e8cfa07d16ec7cf22d8ad3b5`,
  artifact SHA `4e911dd616afa55753a5320a42c11ce0cebe9d4d10fefd66c8f8a431884cf105`,
  MVID `692e9dd9-4bc7-4203-ae12-511ef95a5331`.
- Runtime instance `abb6b2d81b3d44f1876e88d89324f155`; environment
  fingerprint `cd6b35c554208a865023dd71bc46047cf7d9619aabec04d5f2e67b6c5633f2bd`;
  exact observer Modset fingerprint
  `be4b23c7906cc6589dc728df92057427a584fa4d660080e0ab84e82a27caccc8`.

## Native-Human Evidence

Session `session-20260824T153454Z-3005d3b9cea9425ab0c615f0bd961a39`
passed independent audit with 30 admitted records and five fail-closed unstable
pre-frames. Admitted actions were 7 targeted plays, 16 untargeted plays, and 7
end turns. All 30 successors were interactive. Every admitted record contains
materialized `run_deck` and `combat_piles` in both S and S': 60 results per Read
kind, 120 total, with zero Read failures.

Human origin is owner-attested, not machine-proven. Invalidations were not
rewritten or admitted.

## Portable Evidence Path

- Bundle schema `sts2.human-annotator/session-bundle-2`, content ID
  `b92778bed35ab129920cc8a23071058812ca3637ae693979197f0c85ec044b01`.
- CaptureProfile SHA
  `ae8e5b9b1402176fc43bb366e2e79cf797a9375dce73bb58aedd16a657b1cb21`;
  export SHA `2b8cc6ed08a3cdf33f585ae6ae7b9e34cf054337d01907d37b1810e2166332d6`;
  checksums SHA `79727a7d3210912a88e118785bd4c135b82b2c5bc8d93d002c14e73f5b57ec52`.
- Transfer manifest SHA
  `742f333c3f1ad83545d6574361a41ff20cefc77e7a79871b16f25decdd6b7169`;
  first receive `promoted`, identical retry `reused`, zero findings.
- STPD source `25b5306225bd2c4ea39ddc678e916dd483d5b37e` imported
  30/30 with zero rejection and retained both Read kinds in state and successor.
- Workbench HTTP smoke returned 200 and reported Environment, Annotator,
  Evidence, Transfer, and Diagnostics available without acquiring authority.

## Corrective Work And Boundary

Post-load source fixed three non-Native integration defects: Annotator CLI paths
now resolve from caller CWD (`144a9dc...`; current component revision
`793597fa...` also contains closeout docs), V2 CaptureProfile verification
matches the C# producer's ordered compact JSON (`a5f733d...`), and Workbench
recognizes official receiver receipts/store status (`5154be6...`). These commits
are tested source evidence only; they do not change or inherit the loaded Native
artifact.

V1 verification remains covered. This V2 object is not part of the frozen V1
corpus and has no training authorization, broad selector qualification, full-run
journey claim, long-soak claim, or durable qualification.
