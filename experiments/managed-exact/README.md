# Managed Exact Candidate

This directory admits one reproducible, exact-build experiment derived from
[`wuhao21/sts2-cli`](https://github.com/wuhao21/sts2-cli). It is a candidate
Host, not the Reference Host and not a qualified trainer.

The committed authority is:

- `manifest.json`: upstream revision, exact game tuple, required local files,
  semantic shims, and allowed/non-allowed claims;
- `patches/sts2-cli-d11aa88-v01110.patch`: the complete reviewable source
  delta from that immutable MIT-licensed upstream revision;
- repository tests and `tools/managed-exact.mjs`: preparation, identity,
  runtime, Player Environment, binding, reset, and capacity gates.

No game DLL, asset, localization payload, save, or generated candidate is
committed. `prepare` discovers the user's exact installation, copies required
files only into ignored `.local/`, refuses a modified runtime `sts2.dll`,
builds with .NET 9+, and records provenance.

```bash
npm run experiment:managed -- prepare
npm run experiment:managed -- audit --candidate .local/candidates/<candidate>
npm run experiment:managed -- native-gates --candidate .local/candidates/<candidate>
npm run experiment:managed -- pe-probe --candidate .local/candidates/<candidate> --episodes 3 --max-actions 600
npm run experiment:managed -- pe-capacity --candidate .local/candidates/<candidate> --workers 1,2,4,8 --episodes 3 --max-actions 600
```

`probe` and `capacity` exercise the upstream-shaped raw protocol and cannot
support Player Environment claims. `pe-probe` and `pe-capacity` use the strict
Connector SDK contract, snapshot-bound finite actions, Host-local operands,
idempotent request ledger, stale refusal, and unknown-no-retry behavior. They
remain partial until cross-Host differential and coverage gates pass.

Privileged commands such as `enter_room` are scenario controls. They are used
only by named targeted gates and never enter Player Environment actions.
Passing those gates is not a fair-player journey.

When changing the candidate:

1. edit a disposable checkout at the manifest's exact upstream revision;
2. run its C# build and targeted runtime gates;
3. regenerate the complete patch with
   `git diff --binary --no-ext-diff`;
4. run `prepare` into a new directory;
5. verify the prepared source patch, unmodified game SHA, artifact SHA/MVID,
   and runtime identity before collecting evidence.

Never transfer evidence across a changed patch, artifact, game assembly,
runtime instance, or canonical adapter source.
