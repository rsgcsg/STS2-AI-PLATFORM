# Roadmap

## Current Baseline

`v1.0.1` is the exact Managed Exact patch baseline for STPD v0 operational use.
`v1.0.0` remains immutable predecessor evidence for its distinct Host artifact.
This is intentionally narrower than formal H1.0 qualification. Headless and
Connector feature work stops unless a generic environment regression is
reproduced.

The routine maintenance loop is:

```text
verify exact tuple
-> audit frozen candidate
-> run cheap Python environment smoke
-> run STPD
-> quarantine environment-invalid episodes
```

Reference remains the semantic authority. A regression is owned by Connector
or the game contract when both Reference and Managed fail, by Headless when
Reference passes and Managed fails, and by STPD when both environments pass.

## Requalification Triggers

Reopen the relevant gates after any:

- game version, executable, `sts2.dll`, GodotSharp or platform change;
- Managed upstream, patch, artifact or semantic shim change;
- Connector source, artifact, protocol, Modset or information-policy change;
- reset, isolation, controller, idempotency, unknown, binding or successor
  lifecycle change;
- previously unseen environment-invalid episode.

## Deferred Formal Qualification

These are future qualification campaigns, not blockers for STPD v0:

- randomized/high-risk and exhaustive CrossHost differential coverage;
- 72-hour/10-million-decision soak and broad fault matrix;
- real changed-build impact analysis and requalification;
- cluster/high-core/cross-platform/resource-density qualification;
- all card, relic, event, selector and Mod coverage;
- broad Candidate-to-Reference policy-quality and semantic-transfer studies.

Managed Exact v2, Hybrid and Simulator work is not active. It should reopen
only if measured STPD work exposes a concrete Host bottleneck that cannot be
fixed while the exact game continues to own gameplay semantics.
