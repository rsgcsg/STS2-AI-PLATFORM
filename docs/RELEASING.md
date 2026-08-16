# Releasing

Headless, Connector Host, Player Environment protocol, and Connector SDK are
separate versions.

- Headless `0.x`: launcher, lifecycle, compatibility and evidence tooling.
- Connector Host `1.0.1`: installed Mod artifact.
- Player Environment protocol `1.0.0`: wire contract.
- Connector SDK `1.0.0`: strategy-free client/validator.

A Headless release contains source and scripts only. It never contains STS2 or
Connector binaries. Before tagging:

1. run `npm ci && npm run check` from a fresh clone;
2. verify the pinned Connector Release and checksum are public;
3. run `doctor` on the claimed exact tuple;
4. run H0, H1, and H2 on the final source;
5. review local evidence for non-claims and secrets;
6. update Status, Compatibility, and Evidence;
7. tag only the reviewed commit.

For H1.0, also require:

8. seed/provenance and semantic differential gates;
9. reset/recovery, soak, fault and update-drill evidence;
10. a reproducibly published Connector Host plus matching SDK dependency;
11. explicit Reference/primary-trainer and Training-Ready verdicts;
12. clean shutdown, or a reviewed containment policy that does not hide runtime
    diagnostics.

Game updates do not automatically require a Headless version bump to collect
experimental evidence, but admitting the new tuple is a source and release
change. Runtime evidence never transfers across tags, game bytes, Connector
artifacts, Modsets, or profile modes.
