# Security Policy

STS2 Connector is a release candidate. Its Player Environment HTTP service is
loopback-only but does not authenticate hostile local processes. Client
registration and controller leases enforce one-writer integrity; they are not
an OS security boundary. Never expose port `15526` to another machine.

The separate Host shutdown and episode-provenance routes are disabled unless a
supervisor injects a process-local 256-bit token. They also require the current
runtime instance ID.
The token must remain outside shared configuration, capabilities, logs,
evidence, SDKs and MCP. This prevents accidental use; it does not defend
against hostile code running inside the game process or under the same account.

The optional process seed is Host lifecycle metadata, not player information.
It must not enter Snapshot, Read, BoundAction, SDK or MCP. A configured seed is
accepted only for the headless standard-run native Embark seam; a conflicting
native override fails closed.

Report vulnerabilities privately when they could leak hidden game information,
bypass Snapshot/native binding, defeat controller/idempotency checks, turn a
Read into authority, retry unknown delivery, or expose credentials.

Include redacted source, protocol, artifact SHA/MVID/runtime, game and Modset
identity. Do not attach game binaries, credentials, local paths or raw provider
output.

Only a published release and its explicitly listed exact environments are in
support scope. A branch, build, install or old Live run is not a support claim.
Known candidate binaries remain fail-closed unless both the exact game ID and
the exact embedded source revision are explicitly enabled for that one process.
Empty canary values never mean all candidates.
