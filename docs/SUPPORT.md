# Support And Compatibility

The current exact source audit targets STS2 `v0.110.1` commit `db5d3552`, main
assembly SHA-256
`7c446efabf80614c429b5088e87101423aa5bb4c04fc3e73393261f6e6d404fd`
and MVID `c0f649b8-8d57-4a9c-8b07-21aece97dca0`.

That audit is not itself runtime support evidence. Capabilities report the
loaded game, Host, runtime and Modset identity. Unknown game identity, changed
ABI, ambiguous owner, unsupported Mod interaction or incomplete finite action
projection fails closed.

The intended first runtime scope is ordinary single-player vanilla plus this
one Player Environment Host. Other Mods may be observed and reported, but they
do not automatically inherit mutation support. Support is granted only to an
explicit exact environment with matching Live evidence.

The HTTP listener is loopback-only. Local controller leases coordinate one
writer but do not defend against a hostile process on the same OS account.
