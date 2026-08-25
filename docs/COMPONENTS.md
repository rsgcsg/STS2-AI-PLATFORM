# Components

| Component | Path | Owns | Must not own |
|---|---|---|---|
| Connector | `components/connector` | Player Environment, native binding/execution, REST/MCP, SDK | process lifecycle, strategy, annotation |
| Host Runtime | `components/host-runtime` | discovery, isolation, launch/reset/stop, headless/managed experiments, qualification | gameplay legality, research models |
| Human Annotator | `components/annotator` | native-human witness, records, audit/export/bundle, workstation | action authority, research admission |
| Platform Evidence | `components/evidence` | typed verification, content identity, immutable store, transfer/receiver receipts | research eligibility, corpus policy, mutation |
| Policy Runtime | `components/policy-runtime` | policy process boundary, Human/Shadow/One-Step/Auto, controller lifecycle, stale/Receipt/successor and Agent-run evidence | model inference, legality, native operands, candidate filtering |
| Workbench | `apps/workbench` | typed live status, explicit filesystem fallback, bounded Policy Runtime commands | gameplay submission, evidence admission, model loading |
| Platform Live UI | `apps/ingame-ui` | unified in-game Environment/Policy/Human Data/Diagnostics presentation and typed application commands | direct BoundAction submission, legality, recording writes |
| Platform tools | `tools` | composition, component identity, migration/boundary checks | native operands, policy |
| STPD | external repository | ResearchTransition, Dataset Views, representation, Qwen, training/evaluation | Host implementation or legality |

Each imported component retains its focused `AGENTS.md`, tests and operational
documentation. Those files add component-specific constraints but cannot
override the root hard shell.

The Annotator compiles against the exact Connector Release artifact produced at
`components/connector/host/out/STS2_MCP/STS2_MCP.dll`; it never recompiles or
copies a second Connector implementation.

The versioned Host Runtime package is the public programmatic boundary for its
Node driver/CLI and strategy-free Python client. Consumers provide external
candidate artifacts; they do not import a sibling source checkout.

The versioned Evidence Python package is the portable evidence-integrity
boundary. It verifies V1/V2 Human bundles and owns local transfer mechanics;
research consumers remain responsible for their own admission semantics.

The Policy Runtime is a Connector consumer. A Policy Manifest names exact
model, adapter, representation, Reads, support and environment requirements.
The adapter returns only an ordered score vector and selected index; Runtime
resolves that index against the unchanged current Connector catalog. Workbench
and Live UI issue only typed Runtime or Annotator application commands.
