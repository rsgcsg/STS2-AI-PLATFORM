# Security Policy

STS2 Connector is a release candidate. Its HTTP service is loopback-only but
does not authenticate hostile local processes. Client registration and
controller leases enforce one-writer integrity; they are not an OS security
boundary. Never expose port `15526` to another machine.

Report vulnerabilities privately when they could leak hidden game information,
bypass Snapshot/native binding, defeat controller/idempotency checks, turn a
Read into authority, retry unknown delivery, or expose credentials.

Include redacted source, protocol, artifact SHA/MVID/runtime, game and Modset
identity. Do not attach game binaries, credentials, local paths or raw provider
output.

Only a published release and its explicitly listed exact environments are in
support scope. A branch, build, install or old Live run is not a support claim.
