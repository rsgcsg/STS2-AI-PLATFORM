# Operations

## Safe Lifecycle

1. fully close STS2;
2. build and deploy a clean exact Connector commit;
3. build and deploy a clean exact Annotator commit;
4. cold-load once to discover the complete observer Modset fingerprint;
5. quit, run `npm run admit:modset`, and cold-load again;
6. run `npm run verify:loaded` before human actions;
7. play through the shipped UI without an external Connector controller;
8. quit and run audit/export;
9. use `npm run rollback` only while the game is fully closed.

`npm run doctor` is read-only. Build, install, loaded identity, and recorded
native-human decisions are separate states.

## Failure Triage

- `exact_observer_modset_canary_missing`: cold-load fingerprint not pinned;
- `external_controller_active`: another process owns Connector mutation;
- `pre_frame_not_complete_interactive`: no authoritative decision frame;
- `mapping_zero` / `mapping_ambiguous`: do not recover by name or coordinates;
- `stable_successor_timeout`: the transition boundary was not observed;
- `runtime_identity_changed`: preserve the invalidation and start a new session;
- `unsupported_native_action_type`: outside the current explicit slice.

Inspect `.local/runtime-status.json`, the session invalidations, Connector logs,
and the STS2 log together. Do not edit a raw record to force admission.
