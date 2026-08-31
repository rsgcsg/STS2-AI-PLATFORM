# Serialized Human Input Runtime Candidate - 2026-08-30

## Exact identity

The clean serialized-input candidate was rebuilt from Platform workspace
`cbabfac0c27f621f3990961318b2bf48d9b2fa5f`. The Host/deployment integration
change does not alter the Native bytes. Its Native identity is:

- Platform source `b5389a0b4a1bbed37e6a9718776fdf38f06f50c9` /
  digest `0d01f16685001b0fa02f2ed4ff9cfd0c611fe27bf1258ea63fc715eee442fa58`;
- Connector source `54efe38d6d2f49051e04248072acb548feddfe9a`;
- Annotator source `2a7f7aa4d632c5bd4890df0e82ab8911f41b11d4`;
- unified artifact
  `b805474d3e99a8a2b1d13a00b0b5b92ea6b8cd06b57d6e65935c7870e54194e1` /
  MVID `3ab1e10e-dda6-472a-83a3-c3b7be1c6f40`;
- STS2 `v0.111.0 / 41cef1ea`, assembly
  `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4` /
  MVID `57785517-0b16-42b9-8b36-bad6fb28384b`.

## Build, install and load

The initial artifact was safely installed and cold-loaded at
`2026-08-29T14:43:32Z`. Automated Host probing then exposed a stale split-repo
assumption: Host admission recognized only `STS2_MCP.identity` and
`exact_player_environment_only`, not the exact unified Platform identity and
Modset. Workspace `cbabfac...` fixes that owning integration layer. Deployment
now writes and checksum-verifies `STS2_PLATFORM.identity`, retires the stale
predecessor sidecar and preserves rollback
`apps/game-mod/.local/deployments/2026-08-29T15-06-58.805Z`.

After all automated probes and the identity repair, the same Native bytes were
cold-loaded again at `2026-08-29T15:16:49Z`. Final loaded verification reports:

- runtime instance `ba5d974f4f9c498a8812f53644be351a`;
- environment `36ec4fa719d62ed5ab99db3c3026a3120bfac5a8dd17a552c47a2148790cae88`;
- exact sole-Platform Modset
  `eb862f2123995708937057b6616bc61a790788e2a6015653c9de9fe2455384c8`;
- Connector protocol `1.0.0`, single controller and execution available;
- Recorder `ready`, with no open session, in process `23982`.

## Automated Host and differential evidence

An isolated shipped profile was created by STS2 itself with settings schema 8;
the shared player profile filesystem sentinel remained byte-identical. All
automated runs used exact STS2 `v0.111.0 / 41cef1ea` and local, uncommitted
evidence directories.

- Two independent shipped runtimes with the same artifact, profile template and
  seed have a complete semantic match for the first 9 actions.
- Extending that same scenario to 12 rapid combat actions produces a real
  semantic mismatch before End Turn: both sides select and deliver the same
  actions, but the enemy is at 45 HP in one runtime and 39 HP in the other.
  Both runs independently pass integrity with zero unknown delivery, Read,
  successor or provenance failure. This is native effect-timing divergence,
  not an identity or policy-selection mismatch.
- Managed Exact was rebuilt from upstream `d11aa883...` plus audited patch
  `8ced088b...`, producing artifact `8dc622b0... / 7228541c...`. A two-episode
  bounded Player Environment run delivers 80/80 actions and 158 Reads; repeated
  seed reset, stale rejection and idempotent receipt replay all pass. Its
  qualification profile measures 293.083 decisions/s on this machine.
- Reference versus Managed Exact also diverges in the rapid combat prefix:
  Reference observes the pre-effect enemy at 42 HP while Managed has committed
  Bash damage and Vulnerable at 34 HP. This directly rejects treating replay
  from run start as an arbitrary-boundary restore.

The 9-action match proves a bounded differential tool. The two longer
counterexamples prove that same seed plus same chosen actions is not an exact
checkpoint/restore primitive. Twin runtime remains differential-only; it is
not the primary Human collector or a source of canonical S'.

## Evidence boundary

This proves source, tests, exact build, install and loaded identity only. It does
not prove Human input admission, canonical rows, first-command Close, blocked
rapid-input UX, after-latency or after-footprint. No predecessor Human evidence
transfers to these bytes. The automated runs also do not measure foreground
Human input latency. The next and only runtime gate is one short owner session
after a fresh cold load of this exact artifact.
