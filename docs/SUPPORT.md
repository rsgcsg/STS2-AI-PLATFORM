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

The HTTP listener is loopback-only. Local controller leases coordinate one
writer but do not defend against a hostile process on the same OS account.

Candidate testing requires exact process-local game and source-revision opt-ins.
Those opt-ins authorize only that process for canary evidence; no candidate
artifact or Windows run inherits the macOS/v1.0.0 support seal.
