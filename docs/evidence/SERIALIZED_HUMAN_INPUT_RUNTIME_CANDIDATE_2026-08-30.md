# Serialized Human Input Runtime Candidate - 2026-08-30

## Exact identity

The clean serialized-input candidate was built from Platform workspace
`95acd12f00c0709a8e789d81fc36a559c694029e`. Its Native identity is:

- Platform source `b5389a0b4a1bbed37e6a9718776fdf38f06f50c9` /
  digest `0d01f16685001b0fa02f2ed4ff9cfd0c611fe27bf1258ea63fc715eee442fa58`;
- Connector source `54efe38d6d2f49051e04248072acb548feddfe9a`;
- Annotator source `2a7f7aa4d632c5bd4890df0e82ab8911f41b11d4`;
- unified artifact
  `b805474d3e99a8a2b1d13a00b0b5b92ea6b8cd06b57d6e65935c7870e54194e1` /
  MVID `3ab1e10e-dda6-472a-83a3-c3b7be1c6f40`;
- STS2 `v0.111.0 / 41cef1ea`, assembly
  `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4` /
  MVID `57785517-0b16-42b9-8b36-bad6fb28384b`.

## Build, install and load

Portable checks, exact-game checks and both GitHub CI jobs for PR #3 pass on
the workspace revision above. The artifact was safely installed with rollback
`apps/game-mod/.local/deployments/2026-08-29T14-43-21.403Z` and cold-loaded at
`2026-08-29T14:43:32Z`.

Loaded verification reports:

- runtime instance `8fb991bea8d94298b89f3c7d3cc04c6a`;
- environment `36ec4fa719d62ed5ab99db3c3026a3120bfac5a8dd17a552c47a2148790cae88`;
- exact sole-Platform Modset
  `eb862f2123995708937057b6616bc61a790788e2a6015653c9de9fe2455384c8`;
- Connector protocol `1.0.0`, single controller and execution available;
- Recorder `ready`, with no open session.

## Evidence boundary

This proves source, tests, exact build, install and loaded identity only. It does
not prove Human input admission, canonical rows, first-command Close, blocked
rapid-input UX, after-latency or after-footprint. No predecessor Human evidence
transfers to these bytes. The next and only runtime gate is one short owner
session on this exact artifact.
