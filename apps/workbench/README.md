# STS2 Platform Workbench Foundation

This is a dependency-free, read-only filesystem view. It deliberately does not
start STS2, call component APIs, validate domain artifacts, mutate state, or
claim that a service is healthy because a directory exists.

## Start

```bash
node bin/workbench.mjs \
  --environment-root /path/to/environment \
  --annotator-root /path/to/annotator-local \
  --evidence-root /path/to/evidence-store \
  --transfer-root /path/to/transfer \
  --diagnostics-root /path/to/diagnostics
```

The same roots can be configured with `--root name=path` or
`WORKBENCH_<NAME>_ROOT`. The JSON API is `GET /api/status`; the HTML view is
`GET /`. Known status files are small JSON objects. A missing root is `absent`.
A configured root without a known status file, an unreadable root, or malformed
status JSON is `unknown`. No state is inferred as successful from directory
existence alone.

Known status filenames:

| Service | Files checked in order |
|---|---|
| Environment | `runtime-status.json`, `environment-status.json`, `status.json` |
| Annotator | `runtime-status.json`, `recording-status.json`, `status.json` |
| Evidence | `store-status.json`, `status.json` |
| Transfer | `transfer-status.json`, `status.json` |
| Diagnostics | `diagnostics.json`, `status.json` |

## Test

```bash
npm test
```
