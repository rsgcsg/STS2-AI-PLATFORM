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
