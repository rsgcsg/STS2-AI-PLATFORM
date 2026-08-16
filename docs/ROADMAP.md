# Roadmap

## H1.0 Core Release Gate

Completed or implemented:

- exact game discovery and fail-closed compatibility;
- official shipped Reference Host and Host-neutral Player Environment use;
- isolated native profiles, verified templates and hard reset generations;
- process-local endpoints, multi-worker orchestration and resource measurement;
- fault injection, recovered runtime identity and endpoint/process cleanup;
- durable semantic traces and canonical decision comparison.

Open:

- game-owned seed plus episode provenance;
- targeted and randomized semantic differential with first divergence;
- clean shutdown or a qualified containment policy for shipped teardown errors;
- hang watchdog, fault matrix, long soak and Cloud/write sentinels;
- game-update invalidation and targeted requalification drill;
- reproducible release of the RC Connector Host and compatible SDK;
- public release packaging and final exact-artifact gates.

## Training-Ready Claim Gate

No backend currently qualifies. The claim requires all of:

- realistic aggregate throughput, currently hypothesized at `>=1000`
  normalized semantic decisions/s;
- 1M+ reset/scale/recovery/qualification evidence;
- a clean Python/Gym/VectorEnv adapter outside Headless and Connector;
- real learning smoke;
- policy evaluation on the highest-confidence Reference Host.

## Host Route Experiments

1. Retain shipped Godot as Reference and differential authority.
2. Complete seed/provenance and differential tooling before promoting a derived
   Host.
3. Keep the current `wuhao21/sts2-cli` revision rejected as primary trainer;
   reuse only failure cases and source seams until its semantic changes can be
   isolated and qualified.
4. Admit managed, simulator or other candidates only through the same normalized
   decision, resource, reset, recovery and differential gates.
5. Run end-to-end learning bottleneck and policy transfer only after one
   candidate clears semantic admission.

The route is deliberately open. H* is whichever measured Host or combination
best satisfies fidelity, throughput, density, reset, reliability and update
maintenance. Architecture symmetry is not a promotion criterion.
