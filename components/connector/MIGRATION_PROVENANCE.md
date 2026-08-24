# Migration Provenance

This repository is the responsibility split of the Player Environment C1
source formerly maintained in `rsgcsg/SpireAgent`.

- Original repository semantic-seal source:
  `4bc448f1fbfa034232b88587faf9a51ea2a15581`
- Original branch: `human_equivalent_connector`
- Extraction date: 2026-08-13
- First standalone snapshot: `a91e3e72e37a896945a9f0c4f0b667ce28423e6e`

The first public snapshot was created as a responsibility-preserving tree
snapshot, not as a published `git-filter-repo` lineage. Earlier documents named
`b3758fadac8a3dd884d33c69ce371541e588dc25` as a filtered equivalent, but that
object is unavailable from both public repositories and retained local object
databases. It is therefore not part of the verifiable provenance chain.

`a91e3e7` also contained interrupted writes at the ends of multiple text files.
The repair commit reconstructs those tails from `SpireAgent@4bc448f`, preserves
standalone C1.0-rc.2 changes before each unique source anchor, and validates the
result with exact-game Host tests plus portable SDK, contract, boundary and
packaging checks. Historical blame before the split remains in SpireAgent;
current C source authority begins in this repository.

The split retains Host, Player Environment contract, C-owned tools/docs, MCP
transport and strategy-free TypeScript wire/client responsibility. SpireAgent
strategy, provider, recording and evaluation ownership was not migrated.

Historical Live evidence from the monorepo is predecessor evidence. It does not
qualify any artifact built from this repository.
