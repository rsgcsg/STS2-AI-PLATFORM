# STPD Operational Runtime Seal

Tag `v1.1.0-rc.1` binds source
`e0651024117d22bdeb95142766917103d87c0185`, Player Environment protocol
`1.0.0`, source digest `430c9010...1183`, Host artifact
`c1877f1a...7586` and MVID `64765ea1...5825`.

The clean release build reproduced the same artifact. It was installed and
cold-loaded on macOS arm64 STS2 `v0.111.0/41cef1ea`, assembly
`9cb4f1ad...f12b4`, with only `STS2_MCP` loaded under the explicit exact
process-local canary.

Named runtime gates:

- visible card holders that were not yet clickable produced a settling
  Snapshot with no mutation authority;
- the first stable successor published all three selectable cards plus Skip;
- an independent observation reproduced the same complete catalog;
- two Candidate-trained-policy shipped-Reference episodes reached terminal,
  delivered 390 actions, proved requested seed provenance and returned zero
  unknown;
- one of the two same-seed Candidate/Reference terminal outcomes matched
  exactly.

The prepared release package includes the machine-readable identity record
`STS2-Connector-1.1.0-rc.1-runtime-seal.json`. GitHub Release publication and
anonymous-download verification are distribution gates, not runtime evidence.
This seal does not claim formal H1.0, broad CrossHost parity, arbitrary
content/version/Modset support, long soak or business completion from Receipt.
