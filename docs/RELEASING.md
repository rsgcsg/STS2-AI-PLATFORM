# Releasing

Headless, Connector Host, Player Environment protocol and Connector SDK are
independently versioned.

- Headless `1.0.0`: exact STPD v0 operational baseline;
- Connector `1.1.0-rc.1`: exact shipped-Reference Host prerelease;
- protocol/SDK `1.0.0/1.0.0`: Host-neutral gameplay contract/client.

The Headless version is not a formal H1 qualification claim. Release assets
contain source and a machine-readable runtime seal only; they never contain
STS2, generated Managed binaries, localization, saves or profiles.

## Release Gate

1. clean source, deterministic checks and repository boundary checks;
2. exact candidate audit including upstream, patch, game and Host artifact;
3. current-tag Python `reset/observe/read/step` smoke;
4. stale/duplicate/unknown-no-retry and reset old-authority gates;
5. ambiguous-loss process replacement/recovery gate;
6. one current-artifact Candidate-to-shipped-Reference comparison;
7. planned-worker identity/endpoint/process smoke;
8. reviewed exact identities, deferred qualification and non-claims;
9. annotated tag, public release, runtime seal and anonymous download check.

Long soak, exhaustive semantics and changed-build campaigns are deferred formal
qualification, not hidden release gates.

## Release Contents

The Git tag is the source authority. The release notes/runtime seal record:

- Headless tag and source digest;
- exact game tuple;
- Managed upstream/patch/artifact SHA/MVID;
- Connector release/source/artifact SHA/MVID and protocol/SDK;
- named operational gates and report summaries;
- deferred qualification, non-claims and requalification triggers.

Every installation rebuilds the Managed Host locally from the exact game and
reviewable patch. Hash mismatch fails closed.

## Requalification

Runtime evidence never transfers across Headless tags, game bytes, Managed
patch/artifact, Connector artifacts/protocol, Modsets or information policy.
Run only the gates affected by orchestration-only changes; reopen semantic and
Reference gates for any gameplay-facing change.
