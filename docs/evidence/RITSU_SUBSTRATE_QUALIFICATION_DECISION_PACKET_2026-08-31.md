# Ritsu Substrate Qualification Decision Packet

Status: `DECISION_READY`  
Evidence date: 2026-08-31  
Evidence favors: `RITSU_REFERENCE_ONLY_NO_RUNTIME_DEPENDENCY`

This packet stops at route selection. It does not adopt a production route and
does not replace ADR 0004. Mandatory, Optional, and Reference-only remain the
owner's decision.

## Exact scope

| Item | Exact identity |
|---|---|
| Direct oracle branch | `refactor/platform/native-foundation-full-run-mainline` |
| Direct oracle SHA | `32c76156fed2c14f55427ee88590bf1979598d9d` |
| Qualification branch | `research/ritsu-substrate-qualification` |
| Qualification implementation | `39d93885920c7318dc01c9cca612869ce8552d37` |
| Ritsu stable | `v0.5.18` / `f224961a9392e010335da092240b90ee8235317f` |
| Ritsu development | `c466809004f8ecd801956fea2bc3fef83a5d7ad5` |
| Official NuGet SHA-256 | `03856b26c71bd33a09cd7486d84ee1622cd7bd8a20987648d9350040f575fef3` |
| Ritsu assembly SHA-256 | `0dc899012a089fac64cb35858840d3263258864e713d3fa23b99dd4cd99cf744` |
| STS2 | `v0.111.0 / 41cef1ea` |
| `sts2.dll` SHA-256 | `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4` |
| `sts2.dll` MVID | `57785517-0b16-42b9-8b36-bad6fb28384b` |

The sampled contract is Combat + PlayerChoice + Treasure. Treasure is the
non-combat discriminator because the Direct oracle already contains a bounded,
source/test/build/install/load-evidenced implementation and its private owner
and lifecycle seams directly test the benefit Ritsu claims to provide. Shop is
not needed: the required discriminators already make the route decision-ready.

## Implemented comparison

The qualification bridge compiles against exact STS2 and the official Ritsu
package. Both lanes expose the same Platform-owned records; no duplicate DTO or
public action authority was introduced.

### Direct

- Combat `A(S)` comes from the Platform `NativeCombatDecisionProvider`, using
  STS2 logical hand/potion/turn state and native validators.
- Exact action identity and dispositions come from STS2 `GameAction` lifecycle.
- PlayerChoice parent lineage comes from the current STS2 `ActionExecutor` root.
- Treasure state and `A(S)` remain in `NativeTreasureDecisionProvider`; two
  exact private lifecycle fields and two exact owner/accepted callbacks are
  version-bound.

### Ritsu-backed prototype

- Combat still calls `NativeCombatDecisionProvider.Capture` unchanged.
- Exact action lifecycle still uses `NativeActionLifecycleObserver` unchanged.
- PlayerChoice lineage still calls `NativePlayerChoiceLineage.Capture`
  unchanged.
- Treasure still calls `NativeTreasureDecisionProvider`; Ritsu `PrivateAccess`
  and `ModPatcher` only replace reflection and patch-registration syntax.
- Ritsu Card/Potion/turn/reward events are exposed as diagnostics only. They do
  not carry exact generic root `GameAction` identity, cancel/abort disposition,
  or PlayerChoice root lineage, so they cannot become semantic authority.

## Semantic and authority result

| Gate | Result | Reason |
|---|---|---|
| Same public semantic contract | PASS | Ritsu prototype returns the exact Platform record types. |
| STS2 remains rules/legality/effects owner | PASS | No Ritsu or Platform shadow legality was added. |
| Complete Combat catalog | DIRECT RETAINED | Ritsu has no equivalent catalog. |
| Exact root lifecycle/cancel/abort | DIRECT RETAINED | Ritsu events are effect-level, not generic root lifecycle. |
| PlayerChoice parent/continuation lineage | DIRECT RETAINED | No sampled stable/dev Ritsu API supplies it. |
| Treasure catalog/owner semantics | DIRECT RETAINED | Ritsu only changes access/patch syntax. |
| Connector/Annotator compatibility | PASS BY NO CHANGE | The production contract and evidence semantics were not modified. |

The Ritsu-backed lane can match semantics only by retaining every difficult
Direct semantic seam. This is conformance through Direct reuse, not an
independent Ritsu semantic substrate.

## Native integration deletion ledger

Machine-readable results are in
`tools/ritsu-qualification/measurement-results.json`.

- Whole Platform native-integration categories removed: **0**.
- Treasure exact native touchpoints: **4 Direct, 4 Ritsu-backed** (0% reduction).
- Direct sampled code: **884 LOC**.
- Qualification bridge added: **185 LOC**.
- Ritsu replaces the small Harmony registration helper and typed field-read
  mechanics, but retains both exact method names, both exact private field
  names, the owner callbacks, every catalog rule, and all lifecycle authority.
- This misses both default mandatory signals: fewer than two whole categories
  removed and far below roughly 30% touchpoint reduction.

Ritsu's runtime DLL is 8,604,160 bytes, versus 1,786,880 bytes for the exact
Direct Platform candidate (4.815x the Direct DLL size as an added dependency).
The upstream source sampled here contains 1,281 C# files / 301,133 LOC, 201
`IPatchMethod` files, and 672 typed target declarations. Those numbers are not
treated as defects; they quantify the maintenance and supply surface accepted
for the limited syntax benefit.

## Build, runtime, headless, and performance

The exact-package qualification bridge builds successfully with zero warnings.
The bounded local build took 1.45 seconds and produced a 68,608-byte bridge;
this is a build observation, not a runtime benchmark distribution.

Direct predecessor evidence is bound to:

- artifact SHA-256 `3bc44ddb3c339353a5afcb1acb079e58edad019c4959ffaa10369da783ac3c1b`;
- MVID `708ecfab-b370-4575-83d0-39c1700bc8b6`;
- visible runtime `955e5b0232ff47eda43068e41e13ec99`;
- exact STS2/sole `STS2_PLATFORM` Modset;
- visible and shipped-headless boot PASS.

No Ritsu-backed artifact was installed or loaded. The task requires strict
runtime A/B only if source/test leaves Mandatory or Optional viable. It does
not: exact lifecycle/lineage and meaningful integration deletion fail before
runtime. Creating a new runtime candidate could not change those facts and
would violate the decision-readiness stop rule. Consequently:

- Direct runtime/headless claims remain predecessor exact-artifact evidence;
- Ritsu has source/test/build/package evidence only;
- no Ritsu loaded, gameplay, Human, semantic-latency, memory, or throughput
  claim is made;
- no runtime performance regression is claimed or excluded.

## Config and failure semantics

Ritsu 0.5.18 defaults its debug compatibility master, three compatibility
sub-flags, debug log viewer, update checks, and Workshop update checks on. A
strict candidate profile was specified and fingerprinted as
`98af0c838deb869eabe76e512ba6ac5a40574ebb2190f2eb6dffe07184c7a4b9`;
it was not installed or loaded.

Observed failure properties:

- missing/wrong Ritsu package fails package restore/build due to the exact
  `0.5.18` reference;
- missing Treasure patch targets flow through Ritsu's required patcher and
  disable the qualification provider explicitly;
- missing exact lifecycle/catalog/lineage cannot silently fall back to Ritsu:
  the Direct provider remains the only implementation;
- game identity admission must remain Platform-owned because Ritsu's manifest
  declares a minimum game version, not Platform's exact compatibility policy;
- the ordinary .NET consumer needed explicit Godot generator properties due to
  Ritsu's transitive build assets, a reproducible package-integration cost.

The strict profile is reproducible, but introducing it would add a second
runtime configuration/fingerprint surface without replacing Platform's exact
compatibility authority.

## Stable, development, and fork supply

- Stable `v0.5.18` is an MIT-licensed published GitHub/NuGet release with exact
  package hashes and good basic release availability.
- Development differs from stable only in unrelated debug-action serialization
  and localization for the sampled contract. It adds no required semantic API.
- Pinning stable is operationally possible but buys no qualifying semantic
  deletion.
- Pinning development adds supply risk without sampled benefit.
- A Platform fork would assume a broad 301k-LOC maintenance surface while still
  retaining Direct semantic providers.
- Vendoring a bounded helper subset would preserve the same exact targets and
  is unnecessary while Harmony/reflection usage remains small and explicit.

## Route matrix

| Route | Correctness | Real deletion | Runtime/supply burden | Decision evidence |
|---|---|---|---|---|
| Mandatory | Can match only by retaining Direct | Fails: 0 categories, 0% exact-touchpoint reduction | Adds package/config/broad patch surface | Not justified |
| Optional | Can match only by retaining Direct | No domain with independent net benefit | Adds dual-backend tests and drift | Not justified as an ambiguity hedge |
| Reference-only | Preserves proven Direct authority | Keeps useful API/source patterns as references | No production dependency/config surface | Evidence favored |

## Decision and non-claims

`EVIDENCE_FAVORS: RITSU_REFERENCE_ONLY_NO_RUNTIME_DEPENDENCY`

Remaining uncertainty is preference/risk tolerance, not a missing engineering
discriminator likely to reverse the result. Ritsu could later add a stable,
exact generic GameAction lifecycle, PlayerChoice root lineage, and reusable
semantic catalogs; such a release would be new evidence and may reopen the
decision. Current evidence does not prove Ritsu is unsafe or low quality for
general mod development. It proves only that it is not a cost-effective runtime
substrate for the Platform's current semantic contract.

No production code, ADR verdict, domain migration, release, artifact, or loaded
runtime was changed by this qualification.
