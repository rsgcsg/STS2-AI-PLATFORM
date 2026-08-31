# Ritsu-First Architecture Evaluation

Status: `DECISION_READY_RITSU_FIRST`
Evidence date: 2026-08-31
Evidence favors: `RITSU_REFERENCE_ONLY_NO_RUNTIME_DEPENDENCY`

This is a counterfactual integration evaluation. It does not adopt a runtime
dependency, change ADR 0004, migrate a production domain, install an artifact,
or transfer any existing Direct runtime evidence.

## Exact inputs

| Input | Identity |
| --- | --- |
| Direct oracle | `refactor/platform/native-foundation-full-run-mainline@32c76156fed2c14f55427ee88590bf1979598d9d` |
| Previous qualification | `research/ritsu-substrate-qualification@4980d268e70c370cc427427d4c71af1dcfe7619e` |
| Evaluation implementation | `research/ritsu-first-architecture-evaluation@0e42cea741ffc3d23f8529fe7faf0d038f422de9` plus artifact-identity reporting `02d712035142e939f853d569c7a7b15d755f69ac` |
| Ritsu stable | `v0.5.18@f224961a9392e010335da092240b90ee8235317f` |
| Ritsu development | `c466809004f8ecd801956fea2bc3fef83a5d7ad5` |
| Ritsu NuGet / assembly SHA-256 | `03856b26c71bd33a09cd7486d84ee1622cd7bd8a20987648d9350040f575fef3` / `0dc899012a089fac64cb35858840d3263258864e713d3fa23b99dd4cd99cf744` |
| STS2 | `v0.111.0 / 41cef1ea` |
| `sts2.dll` SHA-256 / MVID | `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4` / `57785517-0b16-42b9-8b36-bad6fb28384b` |

The previous qualification answered a narrower retrofit question: replacing
selected Direct mechanics with Ritsu wrappers did not delete semantic
integration work. It did not test a Ritsu-first greenfield domain. This packet
does, while preserving the same Platform-owned contracts and hard shell.

## Capability decomposition

The machine-readable decomposition is
[`capability-decomposition.json`](../../tools/ritsu-qualification/ritsu-first/capability-decomposition.json).
It deliberately separates Platform semantic/domain logic, Ritsu mechanical
integration capability, and Platform compatibility policy.

Ritsu stable has real reusable infrastructure in three categories:

- typed lifecycle subscription for covered effect, room, reward, run, purchase,
  and rest events;
- `IPatchMethod` / `ModPatcher` registration, critical-target diagnostics, and
  rollback mechanics;
- typed private-member resolver helpers.

It does not expose a complete Combat, Treasure, or Shop semantic catalog; a
generic vanilla `GameAction` identity/order/cancel lifecycle; PlayerChoice
parent/continuation lineage; a fair-player Connector contract; or external Host
Runtime lifecycle. These are not implementation omissions in the experiment:
they are the exact categories that determine whether Ritsu can become the
Platform integration kernel.

## Experiment A: existing domains rebuilt Ritsu-first

The Ritsu-first implementations live under
[`tools/ritsu-qualification/ritsu-first`](../../tools/ritsu-qualification/ritsu-first).
They do not call the Direct Combat, PlayerChoice, or Treasure providers.

### Combat and PlayerChoice

`RitsuFirstCombatProvider` starts from Ritsu typed card/potion/turn lifecycle
subscriptions, then independently reads STS2 logical hand, phase, potion,
target, and queue synchronizer state to construct the Platform catalog. The
exact root action lease must still attach directly to `GameAction.BeforeExecuted`,
pause/resume, cancellation, and finish. `RitsuFirstPlayerChoiceProvider` must
still read `ActionExecutor.CurrentlyRunningAction` because the Ritsu lifecycle
API exposes effects and contexts, not the native root/continuation relation.

```csharp
// Ritsu can enrich effect observation, but it cannot authorize or identify a root.
RitsuLibFramework.SubscribeLifecycle<CardPlayedEvent>(...);
_action.BeforeExecuted += OnStarted;
_action.BeforeCancelled += OnCancelled;
_action.AfterFinished += OnFinished;
```

The experiment preserves exact catalog semantics and cancellation proof, but it
retains every difficult STS2 semantic touchpoint. Ritsu contributes diagnostics,
not a thinner causal authority layer.

### Treasure

`RitsuFirstTreasureProvider` independently uses Ritsu `PrivateAccess` and an
`IPatchMethod`/required-patcher pair to register `NTreasureRoom.Create` and
`OnChestButtonReleased`. It does not call `NativeTreasureDecisionProvider`.

Ritsu makes this code more uniform at patch-registration and field-access
sites, but Platform still names both exact callbacks, both private lifecycle
fields, the room owner, relic synchronizer, local vote, semantic stages, and
the action catalog. The measured exact native touchpoint reduction is **0**.

## Experiment B: independent greenfield Shop

The shared Shop contract is intentionally implementation-neutral:

```text
Merchant owner + inventory + gold
  -> typed offered entries
     (card, relic, potion, card-removal; price, stock, affordability, capacity)
  -> current stage (room, inventory, resolving)
  -> finite actions (open, purchase, remove_card, close, proceed)
```

It remains a qualification-only Platform record. Exact operands remain local;
neither lane supplies Connector delivery or public authority.

Both lanes pass the same deterministic fixtures for room, inventory, and
resolving states, including sold stock, unaffordable relic, full-potion capacity,
and removal service. The Direct lane uses public `MerchantInventory.AllEntries`,
`MerchantEntry` price/stock/gold facts, potion capacity plus
`Hook.ShouldProcurePotion`, and public `PurchaseCompleted`/`PurchaseFailed`
events. It needs no patch target.

The Ritsu-first lane uses `PrivateAccess` for the same one UI-resolving flag and
`ItemPurchasedEvent` for successful purchase completion, but it still directly
names the same Merchant room, inventory, entry types, stock, price, gold,
potion and native validator facts. It also lacks the public failed-attempt
disposition which Direct `MerchantEntry.PurchaseFailed` already supplies.

| Greenfield measure | Direct | Ritsu-first |
| --- | ---: | ---: |
| Platform-specific provider LOC | 146 | 131 |
| Patch targets | 0 | 0 |
| Private members | 1 | 1 |
| Exact Shop semantic members removed | 0 | 0 |
| Successful commit observation | public `PurchaseCompleted` | `ItemPurchasedEvent` |
| Failed attempt disposition | public `PurchaseFailed` | unavailable |

The small LOC difference is not treated as a win. Both lanes need the same
semantic knowledge, while Direct has the stronger lifecycle result for this
domain without a second loaded Mod or configuration surface.

## Experiment C: version adaptation

This is **source-history evidence**, not an older-game runtime or two-version
compile claim: no legal local older STS2 assembly was available. Ritsu's stable
compat target list covers `0.103.2`, `0.106.1`, `0.107.0`, `0.107.1`, `0.108.0`,
`0.109.0`, `0.110.0`, and `0.111.0` with cumulative build defines.

The `0.111.0` update (`f5b5ad34`) changed six Ritsu files, adding a target
define and updating Ritsu diagnostics/save internals; none were sampled
Platform semantic adapter files. The `0.110.0` update (`921f1bea`) changed 20
Ritsu files and added `Sts2InputCompat`, but that public facade does not cover
the sampled Combat catalog, GameAction lifecycle, ActionExecutor lineage,
Treasure private fields, or Shop model/UI facts.

Therefore Ritsu can absorb future changes only when Platform consumes an
existing public Ritsu facade. The sampled Platform seams remain version-bound
direct knowledge, and Platform must retain exact artifact admission even if
Ritsu reports a supported minimum version.

## Future-domain forecast

| Domain | Ritsu value | Exact reusable facility | Remaining Platform owner |
| --- | --- | --- | --- |
| Shop | LOW | `ItemPurchasedEvent` | inventory, legality, failure disposition, UI/binding |
| Event | LOW | room lifecycle only | option catalog, owner, native effects/selector lineage |
| Rest | MEDIUM | `RestSiteHealedEvent`, `RestSiteSmithedEvent` | available options, selectors, binding, action result |
| Run entry / terminal | MEDIUM | `MainMenuReadyEvent`, `RunStartedEvent`, `RunEndedEvent` | visible catalog, delivery, causal boundary |
| Reward / CardReward / Map | MEDIUM | `RewardTakenEvent`, continue and map-generated events | exact catalog, referents, owner and native validation |
| Multiplayer/native action submission | LOW | ManagedActions for Ritsu-created actions | arbitrary vanilla Human input and Connector executor |
| Headless startup / diagnostics | MEDIUM | game-ready and diagnostics lifecycle | external process/profile/recovery identity |
| Test/debug fixtures | MEDIUM | patch diagnostics, console/debug facilities | fair-player policy and production evidence |
| Future STS2 version upgrades | MEDIUM | version defines and selected compat facades | all direct semantic escape hatches and exact qualification |

`HIGH` is intentionally absent: no sampled facility removes a meaningful
Platform semantic category across multiple domains. Ritsu's generated or
custom-content/UI APIs are not counted because they do not reduce Platform's
fair-player integration work.

## Whole-architecture result

| Plane | Ritsu-first result |
| --- | --- |
| Native Foundation | Keeps all semantic catalogs, validators, action lifecycle, and lineage. Ritsu is useful support plumbing only. |
| Connector | No public Snapshot/Read/BoundAction/Receipt authority is removed; Ritsu must not become a second executor. |
| Annotator | Ritsu effect events can enrich diagnostics, but exact Human/root correlation remains direct `GameAction` lifecycle. |
| Host Runtime | No material role: process/profile/headless/recovery are external to the game-side framework. |
| Tooling | Ritsu has stronger shared patch diagnostics and useful debug/reference material, but debug mutation must remain outside fair-player evidence. |

The scorecard is in
[`measurement-results.json`](../../tools/ritsu-qualification/ritsu-first/measurement-results.json).
It rates Direct higher for greenfield effort, cognitive load, build/headless
integration, runtime configuration, and supply risk. Ritsu rates higher only
for diagnostics; semantic correctness and failure explicitness are equal when
the Direct escape hatches are retained.

## Build, runtime, and reproducibility

The exact-game qualification executable was built against the current local
STS2 references with warnings-as-errors and ran its deterministic conformance
fixtures.

| Evidence | Result |
| --- | --- |
| Node qualification tests | PASS: 16 tests |
| exact-game C# build | PASS: 0 warnings |
| deterministic Shop/Treasure conformance executable | PASS |
| qualification DLL SHA-256 / MVID | `eb1257d5d85cabf039b51064bdbc6b5c565ce767e927d22fb746788bd4f38d3a` / `d42c18bd-d1fe-45ab-961b-fba798202ae4` |
| install / cold-load / visible boot / headless boot | NOT RUN |
| Human evidence | NOT RUN |

Local execution copies proprietary game assemblies only when
`QualificationCopyGameReferences=true`; those output directories are ignored
and never form a package or repository artifact.

The qualification DLL identity above binds this local deterministic build and
conformance execution only. It is a per-build test artifact, not a released or
loadable Platform candidate, and cannot transfer any runtime claim.

No strict Ritsu runtime candidate was installed. Both Mandatory and Selective
fail the architecture/value gate before a runtime test could change the answer:
loading an artifact cannot remove retained exact semantic knowledge, make the
greenfield Shop thinner, or restore failed-attempt proof missing from the
Ritsu event. Direct runtime evidence remains predecessor evidence only and is
not transferred to this experiment.

Reproduce from a legal local STS2 installation:

```bash
npm run check:ritsu-qualification

export STS2_GAME_DIR="/path/to/Slay the Spire 2"
dotnet restore tools/ritsu-qualification/STS2Platform.RitsuQualification.csproj
dotnet build tools/ritsu-qualification/STS2Platform.RitsuQualification.csproj \
  --configuration Release -p:QualificationCopyGameReferences=true
dotnet run --project tools/ritsu-qualification/STS2Platform.RitsuQualification.csproj \
  --configuration Release --no-build -p:QualificationCopyGameReferences=true
```

## Route decision

`EVIDENCE_FAVORS: RITSU_REFERENCE_ONLY_NO_RUNTIME_DEPENDENCY`

Ritsu genuinely simplifies or centralizes patch registration, critical-target
diagnostics, typed private-access syntax, and selected effect/room lifecycle
subscriptions. Platform must still own every game-truth semantic adapter,
native validator, exact binding, PlayerChoice lineage, causal disposition,
Connector authority, Annotator correlation, Host lifecycle, exact game
qualification, and the current Shop facts. Those retained responsibilities are
the dominant integration and maintenance cost.

`RITSU_MANDATORY_INTEGRATION_KERNEL` is not justified: no representative
semantic category is deleted, and it adds build, runtime, configuration and
supply surfaces. `RITSU_SELECTIVE_RUNTIME_INFRASTRUCTURE` is not justified:
the measured benefits are useful conveniences, but not two independently
valuable production categories whose savings exceed duplicate-runtime and
qualification cost. Ritsu remains a strong source/API/reference and can reopen
the decision if it ships a stable, exact generic vanilla-action lifecycle,
PlayerChoice lineage facility, or public facade that eliminates a repeated
Platform seam across real domains.

## Non-claims

- This does not prove Ritsu is unsafe, poor quality, or unsuitable for general
  STS2 mod development.
- This does not prove future Ritsu versions cannot add a decisive integration
  primitive.
- This does not claim an older STS2 compatibility build, Ritsu runtime load,
  gameplay parity, Human validation, latency, memory, or throughput result.
- This does not modify production Shop, Connector, Annotator, Host Runtime, or
  ADR 0004.

`DECISION_READY_RITSU_FIRST`

STOP: Human architecture decision required. No production route has been
adopted and ADR 0004 has not been finalized by this task.
