# Support And Compatibility

The current exact source audit targets STS2 `v0.111.0` commit `41cef1ea`, main
assembly SHA-256
`9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`
and MVID `57785517-0b16-42b9-8b36-bad6fb28384b`.

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
