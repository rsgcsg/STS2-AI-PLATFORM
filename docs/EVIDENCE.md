# Evidence Policy

Evidence levels are intentionally separate:

```text
source -> automated test -> exact-game build -> installed -> cold-loaded
-> native-human accepted action -> complete record -> bounded journey
-> downstream import -> qualification
```

A higher level requires its own exact evidence and cannot be inferred from a
lower one. Fixture tests prove validation behavior only. A loaded Mod proves no
action was observed. One record proves no unseen family. Winning or losing a run
does not prove recorder correctness.

Runtime evidence must bind game SHA/MVID, both source revisions/digests,
artifact SHA/MVID, Player Environment protocol, runtime instance, environment
fingerprint, and full Modset fingerprint. Raw evidence remains local; commit only
small, reviewed, non-sensitive summaries if a future closeout requires them.
