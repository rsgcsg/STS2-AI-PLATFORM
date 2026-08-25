# Support And Compatibility

The exact Host authority inventory is
`contracts/host-compatibility.json`. The sealed scope remains macOS arm64 STS2
`v0.111.0/41cef1ea`, assembly SHA-256 `9cb4f1ad...`, MVID `57785517...`,
runtime hash `1010476334`, with the sealed Connector `v1.0.0` artifact tuple.
The current Windows x64 assembly (`0861bfa1...`, MVID `73b63ee0...`, runtime
hash `222455745`) is a known candidate only.

Source audit and a manifest entry are not runtime support evidence. Capabilities report the
loaded game, Host, runtime and Modset identity. Unknown game identity, changed
ABI, ambiguous owner, unsupported Mod interaction or incomplete finite action
projection fails closed.

The intended first runtime scope is ordinary single-player vanilla plus this
one Player Environment Host. Other Mods may be observed and reported, but they
do not automatically inherit mutation support. Support is granted only to an
explicit exact environment with matching Live evidence.

For bounded observer experiments, one process-local canary may identify an exact
full Modset fingerprint when every extra loaded Mod declares
`affects_gameplay=false`. The fingerprint must be discovered from the loaded
runtime and explicitly replayed on a later cold start. The exact canary admits
Connector input delivery for that process so the Platform's non-gameplay
Annotator and Live UI can coexist with a Policy Runtime. It does not grant
generic Mod compatibility, durable qualification, or admission to any
gameplay-affecting Mod. Current UI actionability and execute-time native
revalidation remain independently required for every delivered action.

The HTTP listener is loopback-only. Local controller leases coordinate one
writer but do not defend against a hostile process on the same OS account.

Candidate testing requires exact process-local game and source-revision opt-ins.
Those opt-ins authorize only that process for canary evidence. The exact
`v1.1.0-rc.1/e065102` macOS artifact has an STPD operational runtime seal,
but remains canary authority rather than general support. No other candidate
artifact or Windows run inherits either the stable or STPD baseline seal.

The clean `main/c9d7af5` Windows artifact `2050ae23...` has a separate named
current-source candidate admission: reproducible primary-checkout build, exact
cold-load, H0, H1 control, and fresh-profile bounded H2 evidence. That evidence
is specific to the recorded Windows x64 game, Host, Modset, profile, and
process-local canary identities. It grants neither formal H1.0 nor general
Windows support. See the
[dated admission](evidence/WINDOWS_CURRENT_SOURCE_RUNTIME_ADMISSION_2026-08-22.md).
