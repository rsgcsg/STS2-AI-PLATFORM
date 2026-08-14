# Development

## Start Here

Read [Architecture](ARCHITECTURE.md), [Compatibility](COMPATIBILITY.md), and
[Evidence](EVIDENCE.md), then run:

```bash
npm ci
npm run check
npm run doctor
```

Public CI is game-independent and must not require proprietary files. Exact
game tests are local runtime evidence and must record source, game, Connector,
Modset, process, profile, and verdict identities separately.

## Ownership

- `src/game-installation.mjs`: Steam discovery and disk identity;
- `src/compatibility.mjs`: exact normal-start support tuple;
- `src/headless-host.mjs`: long-lived lifecycle and process ownership;
- `src/project-identity.mjs`: Headless revision and deterministic source digest;
- `src/runtime-probe.mjs`: H0/H1 harness;
- `src/journey-probe.mjs`: bounded H2 test consumer;
- `src/connector-release.mjs`: pinned release setup/rollback integration;
- `tools/headless.mjs`: CLI transport only.

Do not put gameplay strategy, native operands, card/event rules, reward shaping,
or simulator behavior in these modules. Connector remains the sole gameplay
contract and execution authority.

## Change Rules

- Add game-independent unit tests for parsers, gates, verdicts, and lifecycle
  safety.
- Bind any runtime claim to an exact artifact and game tuple.
- A new game version starts as explicit experimental evidence and remains
  unsupported until reviewed gates pass.
- Any stub, patch, reflection seam, or scheduler change must name the exact
  blocker, changed behavior, drift risk, differential test, and removal rule.
- Never commit `.local`, saves, logs, game files, credentials, or decompiled
  sources.
