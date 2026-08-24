# Runtime-Seal Candidate Evidence - 2026-08-24

This report closes automated and bounded native-human work for Platform BOM
`0.1.0-rc.3`. It does not transfer predecessor evidence or claim durable
qualification.

## Exact Composition

- Connector source `d0b4a3feab7fc707adf8a4e8fc22881564748f92`, version
  `1.2.0-rc.5`, DLL SHA-256
  `8bf01c6818127a9e4febc870b058945742e33e6152d1a946105dd4a76ac3976f`,
  MVID `9476e499-efe8-4167-9614-8c66322677b7`, protocol `1.0.0`.
- Host Runtime source `dce2c26c0accb0bbbe435191ca371aee9725fa48`, version
  `1.1.0-rc.7`, public package SHA-256
  `b77fb7956caabb3b43ac03c0aee78c9918ce852cee819c6ca44cf77671743f20`.
- Annotator source `484f289a805ef18aff85e0ef0444f1f32f296a31`, version
  `0.2.0-rc.2`, source digest
  `bac4d8c64aa3fc529d2b7f9e8c8b8717e85a6dbe7a04f4cd92676026a8b333c8`,
  DLL SHA-256
  `c7517c7b18762fd2572eff6b0143a711b096f619ffe922919a541171b6a83f21`,
  MVID `698e751b-4ad4-41f3-8b8a-3edec677a1d3`.
- STPD consumer source `f75fb140a7a1ed204ad96998df688850c03dc9cb`.
- macOS arm64 STS2 `v0.111.0` / `41cef1ea`; executable SHA-256
  `ec8c10831dbb424c45859907f5ef6a7711f7a6e9a02f386ad13922ba8a7fcbe7`;
  `sts2.dll` SHA-256
  `9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`,
  MVID `57785517-0b16-42b9-8b36-bad6fb28384b`.

## Automated Runtime Gates

- H0 `pass`, runtime `02c791951729461e9f0852b317c3842e`, report SHA-256
  `b429d2bc0630e9e42f1b5249e7de81384898846c52dd4a8e45dee57bc94d201e`:
  public rc7 package, exact Connector-only Modset, interactive Snapshot and
  clean Host shutdown.
- H1 `pass`, runtime `e9c33a6bd43243a5ab2b549667797f1c`, report SHA-256
  `5c324fc38b0510528d76b33147e9cda0bd8da172c161c86f978b6b06195c568e`:
  one menu delivery, duplicate-request idempotency, `stale_snapshot` refusal
  and `character_select` successor.
- H2 `pass`, runtime `268a6d278f8c454b83ca990d40d24d56`, report SHA-256
  `f74a1a1192a656ee81b188b0b27423c9c13a4e6e76ff134f8ccc9243e793bd98`:
  52 deliveries, 47 combat deliveries, main menu, character selection, map,
  combat and reward; `run_deck` and `combat_piles` Reads; eight stale refusals;
  zero unknown, Read, successor, provenance or execution-profile failures.
  Shutdown diagnostics were wholly after native shutdown and matched the
  bounded known Godot signatures; pre-shutdown diagnostics were clean.
- Annotator loaded identity `pass`, runtime
  `a57a3d52437e45f79fa3bb1b38ff0462`, observer Modset fingerprint
  `d919571dd1f424a4671b3970dd5ed9ab293f9431f051b81f809248720d733cb7`.
  Source, build, install and loaded Connector/Annotator identities match.

The local reports remain uncommitted evidence. Their SHA-256 values are bound
in `platform-bom.json`; they are not public release assets.

## Findings And Rollback

- A first final-H0 attempt correctly failed closed because the restored old
  Annotator was also loaded. Staging it produced the required Connector-only
  Modset. This was environment setup, not a Connector defect.
- Earlier rc6 H2 runs preserved integrity but failed coverage because the
  deterministic test consumer selected the first playable card and could spend
  too long blocking in the first combat. Host rc7 now prefers currently
  published targeted plays before stable hand order. It does not calculate
  damage or create legality; final rc7 reached H2 coverage.
- Requesting the optional `shipped_noninteractive_v01110` profile exposed that
  the current Connector does not advertise or attest that profile. It was not
  used for final gates and remains a non-claim.
- Connector rollback snapshot:
  `~/.sts2-connector/backups/2026-08-24T12-24-38-518Z`.
- Annotator rollback snapshot:
  `components/annotator/.local/deployments/2026-08-24T12-53-17.094Z`.

## Native-Human Gate

The owner reported normal native UI operation without an Agent or Connector
controller for session
`session-20260824T125449Z-1104aece077d4b0eb1e4cfb9709a7d16`, using the same
loaded runtime `a57a3d52437e45f79fa3bb1b38ff0462` and exact identities above.
The session admitted 30 ordinary-combat decisions: 10 targeted plays, 12
untargeted plays and 8 end turns across three combat interactions. All 30 had:

- complete interactive pre-frames and finite BoundAction catalogs;
- exact-unique mapping by frozen Host-reference identity;
- different complete interactive successors;
- exact game, Connector, Annotator, protocol, runtime and Modset provenance.

Independent audit passed with 30 valid records, zero invalid records and eight
fail-closed invalidations. Those invalidations were card actions observed while
no complete same-context authoritative pre-frame existed; none entered the
admitted dataset. Deterministic export SHA-256 is
`9433b3b4240dc365cbd24a4af6d28cc5269f02359922bce6940308cc31f5feab`.
The game exited through `NGame.Quit`; Steamworks shutdown completed. Subsequent
Godot resource-leak diagnostics are post-shutdown engine diagnostics, not a
Recorder mutation failure.

## Verdict And Non-Claims

The exact composition passes the bounded runtime-seal candidate gates. Human
origin is owner-attested and cannot be independently machine-proven. This does
not claim lossless capture under rapid input, potion or non-Combat support,
full-game semantic parity, a full journey, long-soak reliability, optional
noninteractive Host support or durable qualification. Winning or losing was
irrelevant to this gate.
