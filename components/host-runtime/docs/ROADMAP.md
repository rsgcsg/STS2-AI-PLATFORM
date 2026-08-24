# Roadmap

## Current Baseline

The current Platform candidate is intentionally narrower than formal H1.0
qualification. Host Runtime and Connector feature work should stop unless a
generic environment regression or an explicit release-boundary defect is
reproduced.

The routine maintenance loop is:

```text
verify exact tuple
-> audit current candidate
-> run the bounded external-consumer smoke
-> run STPD
-> quarantine environment-invalid episodes
```

The shipped game/Reference Host remains the semantic authority. A regression is
owned by Connector or the game contract when both Hosts fail, by Host Runtime
when Reference passes and a Host candidate fails, and by STPD when both
environments pass.

## Requalification Triggers

Reopen the relevant gates after any:

- game version, executable, game assembly, GodotSharp, or platform change;
- Managed upstream, patch, artifact, or semantic shim change;
- Connector source, artifact, protocol, Modset, or information-policy change;
- reset, isolation, controller, idempotency, unknown, binding, or successor
  lifecycle change;
- previously unseen environment-invalid episode.

## Deferred Qualification

These are future qualification campaigns, not current source/package claims:

- randomized or exhaustive CrossHost differential coverage;
- long soak, broad fault matrix, and recovery stress;
- changed-build impact analysis and requalification;
- cluster, high-core, cross-platform, or resource-density qualification;
- all card, relic, event, selector, and Mod coverage;
- broad Candidate-to-Reference policy-quality and semantic-transfer studies.

Managed Exact v2, Hybrid, and Simulator work is not active. It should reopen
only if measured consumer work exposes a concrete Host bottleneck that cannot be
fixed while the exact game continues to own gameplay semantics.
