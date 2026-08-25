# Platform Live UI

The Platform Live UI is one DLL-only in-game shell for Environment, Policy,
Human Data, and Diagnostics. It starts hidden and toggles with `F10`. It calls
typed Connector observation, Policy Runtime, and Annotator recording services;
it does not publish, resolve, or submit gameplay actions itself.

The Runtime loopback defaults to `http://127.0.0.1:15527`. Connector Snapshot
and Read opportunities, recording state, and loaded component identities remain
available when no policy artifact is running; only policy scores/modes/Receipts
are then unavailable.

## Lifecycle

From the Platform root, with STS2 fully closed:

```bash
npm run live-ui:build
npm run live-ui:doctor
npm run live-ui:deploy
```

`live-ui:build` always rebuilds Connector, the Annotator Mod, and the
Annotator identity tool before compiling the UI. The former `--ui-only` mode is
rejected rather than allowing stale dependency DLLs to be packaged silently.

Deployment requires exact installed Connector and Annotator artifacts matching
the build dependencies. It creates a rollback snapshot before replacing files.
After a cold start, `npm run live-ui:verify-loaded` compares the in-process
SHA/MVID/source identity written by the Mod with installed provenance.

```bash
npm run live-ui:rollback
```

`installed` is not `loaded`, and loaded UI identity is not Human or policy-run
evidence. Shadow, One-Step, and Auto remain unavailable until a compatible
Policy Runtime with an exact Policy Manifest and artifact is running.
