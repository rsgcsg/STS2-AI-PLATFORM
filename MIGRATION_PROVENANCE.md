# Migration Provenance

This repository was extracted with `git-filter-repo` from
`rsgcsg/SpireAgent` after the Player Environment C1 semantic seal.

- Original repository semantic-seal source:
  `4bc448f1fbfa034232b88587faf9a51ea2a15581`
- History-filtered equivalent commit:
  `b3758fadac8a3dd884d33c69ce371541e588dc25`
- Original branch: `human_equivalent_connector`
- Extraction date: 2026-08-13

The extraction retained Host, Player Environment contract, C-owned tools/docs,
MCP transport and strategy-free TypeScript wire/client history. Standalone
reorganization follows in later commits. SpireAgent strategy, provider,
recording and learning ownership was not migrated.

Historical Live evidence from the monorepo is predecessor evidence. It does not
qualify any artifact built from this repository.
