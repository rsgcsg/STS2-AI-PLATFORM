# ADR-0002: Model-Neutral Policy Runtime

Status: Accepted for the bounded `0.1.0-rc.1` source candidate.

## Decision

The Platform owns one small model-neutral Policy Runtime between an external
policy adapter and Connector. STPD and future consumers own model loading,
projection and scoring behind a versioned decision-only port. Connector remains
the only Player Environment, Read, action and delivery authority.

The adapter receives one complete Snapshot/catalog plus only the Reads declared
by its exact Policy Manifest. It returns the same candidate digest, one score per
candidate and an optional selected index. Runtime resolves the index against the
unchanged current catalog and is the only policy-side component allowed to hold
the Connector controller.

Human, Shadow, One-Step and Auto are Agent-run lifecycle modes, not policy
identity. Shadow never mutates. Unknown delivery taints the run and is never
retried. Receipt and successor are recorded separately through the Platform
Evidence plane.

## Rejected

- Keeping generic lifecycle inside every STPD `live_xxx` runner.
- Letting adapters return BoundActions or filter the Connector catalog.
- Moving model, Qwen, reward or training semantics into Platform.
- Letting Workbench or Live UI submit gameplay actions directly.

## Evidence Boundary

Source/tests/build do not prove loaded model execution. The current S1 artifact
is absent on this Mac, so Shadow/One-Step/Auto parity remains pending exact
artifact and runtime evidence. Legacy `live_s1` remains a golden regression
until that evidence exists.
