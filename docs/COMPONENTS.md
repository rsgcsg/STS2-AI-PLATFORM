# Components

| Component | Path | Owns | Must not own |
|---|---|---|---|
| Connector | `components/connector` | Player Environment, native binding/execution, REST/MCP, SDK | process lifecycle, strategy, annotation |
| Host Runtime | `components/host-runtime` | discovery, isolation, launch/reset/stop, headless/managed experiments, qualification | gameplay legality, research models |
| Human Annotator | `components/annotator` | native-human witness, records, audit/export/bundle, workstation | action authority, research admission |
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
