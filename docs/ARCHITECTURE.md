# Architecture

## Product Boundary

STS2 AI Platform provides a real-game Host, fair-player Player Environment,
native-human evidence recording, model-neutral policy execution lifecycle and
strategy-free integration tools. It does not own policy inference, reward,
models, training or research authority.

## Planes

```text
Environment:  Host <-> Connector <-> consumer
Host control: Host Runtime <-> STS2 process lifecycle
Evidence:     Annotator -> immutable bundle -> verify/store/transfer/receive -> consumer
Policy:       external adapter -> Policy Runtime -> Connector
Operations:   typed application services -> CLI / Workbench / one in-game Live UI
```

The planes may share exact environment identity, but not mutation authority or
transport semantics. The Platform is not a strategy, reward, training, or
research service.

## Recording Application Plane

The native recorder correlation kernel owns one process-local recording state.
It starts `Ready`, and a typed `RecordingService` is the only application
boundary used to query status, issue lifecycle commands, or follow ordered
events. `StartNewSession` creates a fresh session, timeline and append store;
`Pause` blocks new witnesses without discarding already accepted native
lifecycle witnesses; `Close` waits for the strict candidate and every tracked
native action to settle, invalidate, cancel or finish, flushes the store, and
permits another isolated session in the same STS2 process.

`RecordingStatus` is a current projection. Its scope section derives from the
active CaptureProfile and store counters, and separately reports recorded,
native-accepted-but-failed-closed, supported-but-not-observed and
declared-out-of-scope outcomes. A native UI attempt that STS2 rejects creates no
HumanDecision and no capture-failure invalidation. The bounded event stream
supports status-first reconnect followed by events after sequence N; a
retention gap requires a new status query. These
operational events are not durable Human Evidence. RunJournal, decisions,
invalidations, the additive native-action ledger, Reads and bundles remain the
durable Evidence Plane. Audit,
pack, verify, store and transfer never run on the game main thread.

## Hard Shell

STS2 owns rules, RNG, native legality, effects and Commit. Connector publishes
only complete finite Host-bound actions and revalidates exact native operands at
delivery time. Reads are state-bound and non-authorizing. Host control is not a
Player Environment action. Annotator observes accepted native-human actions and
cannot execute them. Unknown delivery is never automatically retried.

The Policy Runtime receives the complete ordered Connector catalog and only
Manifest-required advertised Reads. It cannot filter or invent candidates. Its
adapter returns scores plus an index, and Runtime resolves that index locally
against the same Snapshot before acquiring the one Connector controller. Shadow
never acquires a controller; One-Step returns to Human; Auto hands off on an
unsupported surface, abstention, or not-delivered Receipt. Receipt and stable
successor remain distinct evidence. A transport exception after submission or
an `unknown` Receipt taints the run and is never retried. Adapter failure or a
bounded decision timeout returns to Human before controller acquisition.

## Component DAG

```text
STS2 installation and native runtime
  -> Connector component
     -> Player Environment contract, native UI implementation, SDK, and release identity
  -> Host Runtime component
     -> lifecycle, exact-build admission, probes
     -> public Connector SDK + pinned Connector Host release
  -> Annotator component
     -> Host workstation seam + exact Connector witness artifact
     -> native-human witness recording and session evidence
  -> Evidence component
     -> typed V1/V2 verification, content identity, immutable local logistics
  -> Policy Runtime component
     -> model-neutral policy process and Connector consumer lifecycle
     -> append-only Agent-run evidence through Platform Evidence semantics
  -> Workbench application
     -> typed live status, explicit partial fallback, bounded Runtime commands
  -> Platform Live UI
     -> one in-game shell over typed Connector/Runtime/Annotator services
  -> Platform Game Mod
     -> one manifest/DLL packages Connector + Annotator + Live UI source
     -> exact build/install/load identity and rollback only

External consumers -> public Connector SDK / Host Runtime package
STPD              -> public packages + version-pinned Evidence + thin Policy Adapter
SpireAgent        -> consumer integration and policy
```

The Connector gameplay authority is one component-local path. Host Runtime
owns process lifecycle and may consume the public SDK; it must install the
Connector artifact named by the current Platform BOM/release authority, not an
unrelated branch or predecessor release. Annotator may use the explicitly
declared Platform composition seams for exact native witnessing, but it does
not create a second Connector build or action authority. STPD consumes public
packages and verified evidence artifacts, never Platform implementation
internals. STS2 sees one `STS2_PLATFORM` Mod, while Connector, Annotator and UI
retain separate source provenance and authority inside that assembly. Evidence
validates artifact integrity and transport; STPD alone owns
research admission, splits, labels, B0 and training authorization. STPD owns
checkpoint/Qwen/projection/scoring support but no controller or Connector
lifecycle. Workbench and Live UI own presentation and bounded application
commands, never domain authority or direct gameplay submission.

Portable boundary tests require the Host installer to consume a versioned
Platform Connector release and require Annotator to use only the declared Host
workstation seam plus the exact component-local Connector witness artifact.

## Identity

`workspace_revision` identifies an atomic Platform checkout.
`source_revision` is the latest Git commit that changed a component path;
`component_tree_revision` identifies that path's exact Git tree, and
`component_source_digest_sha256` covers its tracked and non-ignored source
bytes. These identities remain stable across unrelated component changes.
Public contract and artifact identities are separate. Artifact SHA/MVID and
exact loaded runtime remain the final byte/runtime authorities.
