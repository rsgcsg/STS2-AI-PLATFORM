# Evidence

Evidence is exact and non-transferable. Source, test, build, package, boot,
loaded, mutation, journey, differential, performance, human validation, and
qualification are separate levels. Raw reports remain local under `.local/`
and are not release assets.

## Current Candidate

The current Platform candidate is described by the root
`platform-bom.json` and the Connector `release-manifest.json`. They are the
identity authorities for source revisions, package assets, protocol,
compatibility, and non-claims.

At the current documentation baseline:

| Gate | Meaning | Current claim |
|---|---|---|
| Source | tracked component source and manifests | candidate source |
| Test | portable checks and component suites | bounded automated evidence |
| Build | exact-game compilation or package construction | only the named artifact |
| Package | reproducible public asset and checksum | only the named package |
| Installed | artifact copied after backup | not claimed here |
| Loaded | cold-start process reports exact identity | not claimed here |
| Live mutation | native action and receipt on that identity | not claimed here |
| Journey | bounded successor/terminal path | not claimed here |
| Qualified | formal evidence campaign | not claimed here |

## Invalid Environment Evidence

An episode or data segment is invalid if it contains:

- unknown delivery;
- incomplete action authority;
- settling timeout;
- missing successor;
- stale authority replay instead of re-observation;
- identity change within an episode;
- request, BoundAction, or Receipt mismatch;
- unexpected environment or driver exception.

The external-consumer smoke fails closed on these conditions. It does not replace
Reference differential, human validation, or formal qualification.

## Historical Evidence

Dated reports under [evidence](evidence/) and imported predecessor reports retain
their original identities and boundaries. They are useful for provenance,
comparison, and rollback planning only. They do not qualify the current
Platform source, package, install, load, or Live journey.

## Non-claims

No claim is made for exhaustive semantics, every card/relic/event, arbitrary
Mods, later builds, other platforms, long soak, broad fault matrices,
high-core/cluster operation, policy quality, or formal H1 qualification.
Receipt means native delivery, not business completion.
