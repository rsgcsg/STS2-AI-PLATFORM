# Operations

## Safe Lifecycle

1. fully close STS2;
2. from the Platform root, build and deploy the unified `STS2_PLATFORM` Mod;
3. cold-load and run the root `npm run verify:loaded` identity gate;
4. press `K`, open Human Data and choose New Session;
5. play through the shipped UI without an external Connector controller;
6. Pause/Resume as needed, then Close to flush the session safely;
7. optionally choose New Session again in the same process to create an isolated
   second timeline/store;
8. quit and run audit, then pack each session against an exact collection profile;
9. use root `npm run rollback` only while the game is fully closed.

`npm run doctor` is read-only. Build, install, loaded identity, and recorded
native-human decisions are separate states.

The current CLI can verify and relaunch the predecessor macOS schema-1
deployment/canary without redeploying it. This compatibility path is
macOS-arm64-only and derives missing platform/release/Connector-build fields
only after the current game assembly and installed Connector artifact exactly
match the recorded identities. It is reported as
`legacy_macos_v1_derived_exact`; any drift still fails closed.

## Platform discovery and cold launch

The Platform Host Runtime component owns Steam discovery and runtime process
inspection on Windows. `STS2_GAME_DIR` remains the explicit override on
both Windows and macOS. A Windows launch requires the exact Connector
`candidate_exact` game ID plus its source-revision canary; a macOS launch keeps
the existing `supported_exact` source-revision canary. The admitted Modset
fingerprint is added only after the first cold load and `npm run admit:modset`.

Deployment archives any prior runtime status and canary in its rollback snapshot.
Rollback archives the current status/canary before restoring the previous files,
so stale loaded-state evidence cannot silently authorize a new process.

## Failure Triage

- `exact_observer_modset_canary_missing`: cold-load fingerprint not pinned;
- `external_controller_active`: another process owns Connector mutation;
- `pre_frame_not_complete_interactive`: no authoritative decision frame;
- `mapping_zero` / `mapping_ambiguous`: do not recover by name or coordinates;
- `stable_successor_timeout`: the transition boundary was not observed;
- `runtime_identity_changed`: preserve the invalidation and start a new session;
- `runtime_process_executable_mismatch`: the status PID is not the discovered
  exact game executable;
- `unsupported_native_action_type`: outside the current explicit slice.

Inspect `.local/runtime-status.json`, the session invalidations, Connector logs,
and the STS2 log together. Do not edit a raw record to force admission.

## Multi-worker Handoff

Each worker receives a pseudonymous worker ID and the same versioned collection
profile/campaign. After audit, create one immutable bundle with
`npm run pack-session`; transfer the whole directory, not a loose export. Storage
may be a local directory, NAS, Drive or S3-compatible object store, but STPD
registry entries use portable bundle-relative paths and own corpus semantics.

Never combine sessions by concatenating JSONL. STPD replays strict admission per
session, rejects identity drift and collisions, assigns whole runs to splits, and
freezes a new immutable corpus snapshot.

## Experimental Connector-only live policy profile

The Human recorder's two-observer Modset is observation-only by design. A real
Connector-controlled policy must instead cold-load the Host's existing
`exact_player_environment_only` authority profile. On Windows, with STS2 closed:

```powershell
npm run prepare:live-connector
npm run launch:live-connector
```

The preparation command makes a checksummed local settings backup, keeps the
Annotator artifact installed but disables it, rejects every enabled third-party
Mod and enables only `STS2_MCP`. The launch reuses Headless game discovery and
the exact Connector game/source canaries. This does not qualify a model or turn
live evidence into Human Annotator evidence.
