# Releasing

Host Runtime, Connector, Player Environment protocol, and Connector SDK are
independently versioned. Current values come from the root BOM and component
release manifests; this document is not a version registry.

The Host Runtime version is not a formal H1 qualification claim. Release assets
contain source and machine-readable identity/evidence metadata only; they never
contain STS2, generated Managed binaries, localization, saves, or profiles.

## Release Gate

1. clean source, deterministic checks, and repository boundary checks;
2. exact candidate audit including game and Host identities;
3. current-tag Python `reset/observe/read/step` smoke;
4. stale, duplicate, unknown-no-retry, and reset old-authority gates;
5. ambiguous-loss process replacement/recovery gate;
6. one current-artifact Candidate-to-Reference comparison;
7. planned-worker identity, endpoint, and process smoke;
8. reviewed exact identities, deferred qualification, and non-claims;
9. annotated tag, public release, runtime seal, and anonymous download check.

Long soak, exhaustive semantics, and changed-build campaigns are deferred
formal qualification, not hidden release gates.

## Release Contents

The Git tag is the source authority. Release metadata records:

- Host Runtime tag and source digest;
- exact game tuple;
- Managed upstream, patch, and artifact SHA/MVID when applicable;
- Connector release/source/artifact SHA/MVID and protocol/SDK;
- named operational gates and report summaries;
- deferred qualification, non-claims, and requalification triggers.

Every installation must consume the exact package and Connector artifact named by
the release metadata, or rebuild the Managed Host locally from the exact game
and reviewable patch. Hash mismatch fails closed.

## Requalification

Runtime evidence never transfers across Host tags, game bytes, Managed
patch/artifact, Connector artifacts/protocol, Modsets, or information policy.
Run only the gates affected by orchestration-only changes; reopen semantic and
Reference gates for any gameplay-facing change.
