# Annotator collection and incident response

Distribute one versioned Platform package with its BOM, supported exact game
identity, component source/contract digests and assembly SHA/MVID. The collector
starts only after the installed and loaded identities agree. Record each
candidate/cohort separately; a new DLL or source revision receives no Human
qualification from its predecessor. Retain the previous deployment for rollback.

## Local capture and handoff

Recording and diagnostics stay local by default. For distributed collection,
configure `recording_root` to a persistent user-owned directory and retain
verified bundles outside temporary engineering worktrees before cleanup. The existing Platform launcher
opens Recorder; enable recording before a fresh native run. Keep the full session
through natural terminal and close Recorder normally. Do not pause Recorder,
reload/resume the run or use the console inside the uninterrupted Full-Run qualification gate.
Those paths may have separate bounded tests. Close must finish successfully;
a journal close entry alone is insufficient for close-capability-1 sessions.

Run the owning audit before collection. Preserve the entire original directory,
including failed/unresolved decisions, diagnostics, journal, content objects and
performance profile. Do not delete bad rows to create an all-valid corpus.

```bash
npm run annotator:audit -- /absolute/session-directory
npm run annotator:pack-session -- /absolute/session-directory \
  --worker collector-id --campaign candidate-id --attest-human-origin \
  --output /absolute/new-bundle-directory
npm run evidence -- verify-human-bundle /absolute/new-bundle-directory
```

Use `--attest-human-origin` only with the actual operator's attestation. It is an
explicit non-machine-verifiable statement, not something inferred from audit.
Packing requires a clean producer checkout and a closed, structurally valid
session. A zero-canonical session containing valid failed-decision evidence can
still be bundled. A truncated/corrupt/unclosed session cannot be made verified:
retain its raw bytes and failed audit for explicitly identified incident transfer.

Bundle 3 is the current canonical-first format. Existing record-2 consumers use
explicit `export-compatibility` / `pack-session-compatibility` via Annotator CLI;
that projection is not a complete Full-Run dataset. Pin the consumer contract.
A new STPD canonical-3 adapter, dataset eligibility policy and training/evaluation
checks belong to STPD and require a separate release and admission decision.

## Reports and privacy

A bug report includes package/component/game identities, runtime/session IDs,
expected input and observed disposition, approximate sequence/time, audit errors,
reproduction steps, and whether console/reload/modset changes occurred. Include
bundle content ID and verifier version when available. The issue template asks
for this metadata; do not put raw sessions, saves, user paths or credentials in
public issues. An operator may choose a private issue attachment or configured
Evidence receiver. There is no automatic upload or telemetry in this change.
The existing typed Evidence transfer verifies checksums and quarantines rejected
content before atomic promotion. Delivery success is not research admission.

## Correctness incident and repair

1. Preserve the original artifact and evidence hashes. Reproduce against exact
   source/native identity; determine the first incorrect fact and owning layer.
2. Quarantine the affected cohort or affected decision families for downstream
   admission. Record known affected versions and explicit uncertainty. Do not
   mutate archived evidence or label every historical session invalid by guess.
3. Implement the owning fix on a new short-lived branch with the lowest-cost
   faithful regression, full component/root checks and exact-game gates. No
   later-frame workaround or automatic retry of unknown gameplay delivery.
4. Build one new exact package, cold-load, verify and run the bounded regression
   Human gate. Re-run Full-Run qualification when the shared causal path changed.
5. Publish the new version and incident disposition; consumers explicitly update
   their pin. A schema-specific re-audit can produce a new report referencing the
   original hashes, never rewrite the original recording or its old report.

After merge, verify the normal merge preserves path-scoped component identity
and the tested tree. Record exact merge/CI/package/load receipts before claiming
release readiness. Future defects use the same route; merge is not a claim of
exhaustive native surface coverage, crash recovery or scientific validity.
