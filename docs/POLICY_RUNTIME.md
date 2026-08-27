# Policy Runtime

Policy Runtime is a small model-neutral Connector consumer, not a policy and not
a second Player Environment. Its normal decision path is:

```text
Connector Snapshot + complete ordered BoundActions
  -> exact Connector capabilities/environment pin admission
  -> materialize only Manifest-required advertised Reads
  -> external decision-only Policy Adapter
  -> score vector + selected index
  -> resolve index against the unchanged current catalog
  -> acquire Connector controller when mutation is required
  -> submit exact bound_action_id
  -> Receipt
  -> distinct non-settling successor
```

## Ownership

- Connector owns player-visible truth, Read authority, complete actions,
  execute-time native validation and delivery Receipt.
- Policy Manifest owns exact policy/artifact/adapter/representation/support
  compatibility claims, including the bounded adapter code digest and
  `requirements.environment` pin. It never contains a current catalog or run
  mode.
- Policy Adapter owns model loading, projection and scoring only. It cannot
  return actions, native operands or a filtered catalog.
- Policy Runtime owns Human/Shadow/One-Step/Auto, controller lifecycle, stale
  whole-bundle refresh, request identity, successor polling and Agent evidence.
- Platform UI and Workbench issue typed mode/tick commands only.

Shadow scores each Snapshot once without a controller. One-Step performs at
most one decision and returns to Human. Auto hands off to Human on unsupported
or incomplete observations, policy abstention, or `not_delivered`. Any unknown
post-submit state taints the run and is never retried. Policy scoring is bounded
to 30 seconds by default; adapter failure or timeout returns to Human before
controller acquisition and never submits a fallback action.

Before the HTTP service or automatic drive starts, the adapter child emits a
strict startup attestation containing its Manifest-pinned id, version, protocol
and complete code digest. Parent Runtime rejects any drift before observation or
controller activity. The child has a bounded 30-second startup window so a
resident model can cold-load before attestation; timeout still closes the child
and fails before the HTTP service starts.

The current STPD S1 Manifest requests no Reads because its frozen training
projection used none. Future policies may request advertised Read kinds without
changing Runtime or Connector authority. Missing required Reads fail closed.

## Exact Environment Admission

Every Policy Manifest pins the Connector environment before the Runtime asks for
a Snapshot or invokes policy scoring. `requirements.environment` requires exact
values for `host_kind`, Connector `version`, `source_revision`, `artifact_sha256`
and `module_version_id`, plus Modset `status`, `fingerprint` and the complete
ordered `loaded_mod_ids` list. A missing, null, reordered, added, removed or
changed value fails closed with a field-specific environment drift reason; no
Snapshot observation, policy scoring, controller acquisition or action submit is
allowed after that failure.

Runtime status exposes the observed `host_kind` and `loaded_mod_ids` alongside
the other exact Connector and Modset identity fields. This is diagnostic
observation only: it does not replace Connector authority or turn the UI into an
action publisher.

Machine contracts live in `contracts/policy-runtime/`; implementation and tests
live in `components/policy-runtime/`. Agent-run evidence is verified and moved
by the existing Platform Evidence plane. Every finalized run manifest binds the
Runtime code digest, canonical Policy Manifest digest and checkpoint SHA-256;
an `environment_admitted` event records the exact Connector/game/Modset identity
before any decision evidence. The sealed directory also carries the canonical
`policy-manifest.json` and a standalone `adapter-attestation.json`; both are
checksummed, linked to the run, and independently revalidated before Evidence
Store promotion. A run that never attested its adapter may describe a failed
startup, but it cannot contain admissible decision evidence.
