# Contributing

Read [AGENTS.md](AGENTS.md), the [new engineer guide](docs/NEW_ENGINEER_GUIDE.md)
and [Development](docs/DEVELOPMENT.md). Use a topic branch and keep contract,
implementation, tests and current documentation in one reviewable slice.

## Ownership

- `host/LiveHost`: visible facts and current input owner.
- `host/NativeUi`: private exact binding and native input delivery.
- `host/PlayerEnvironment`: public Observe/Read/Interact contract.
- `host/Authority`: provenance, one writer and idempotency.
- `sdk/` and `transports/`: strategy-free consumers of the canonical contract.

Do not move game legality or native operands into a transport/client. Do not
add action authority through fixtures, manifests or documentation.

## Validation

```bash
npm run bootstrap
npm run check
npm run connector -- test
git diff --check
```

The exact-game Host test/build requires a local STS2 installation and cannot be
replaced by public CI fixtures. Report source, test, build, installed, loaded
and Live evidence separately.

Never commit secrets, game DLLs, installed artifacts, `.local/`, local logs or
run data.
