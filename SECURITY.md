# Security

Report vulnerabilities privately through GitHub Security Advisories. Do not
post credentials, Steam identifiers, saves, unreviewed runtime logs, game
files, or local evidence in public issues.

The gameplay endpoint is expected to remain loopback-only. Headless refuses to
launch beside an existing STS2 process or occupied Connector endpoint, requires
an exact supported game tuple for normal start, and uses Connector's
single-controller lease and request idempotency.

The current route is **not profile-isolated**. It can read/write the active
modded Steam profile and synchronize Steam Cloud before a Mod can intervene.
Use a disposable profile or dedicated account/OS environment for experiments;
do not present `--shared-profile` as an isolation mechanism.

An `unknown` delivery may have mutated the game. Consumers must stop and must
not retry it automatically.
