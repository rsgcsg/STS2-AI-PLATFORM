# Player Environment Connector

This is the current game-side contract. Read [Protocol](PROTOCOL.md) and
[Coverage](COVERAGE.md). The Connector exposes player-visible facts, state-bound
reads and exact current native choices. It delivers native input and returns a
receipt/successor; it does not become a second STS2 rules engine.
