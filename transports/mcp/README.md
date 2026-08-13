# Player Environment MCP Adapter

This optional adapter is a thin transport over `/api/player-environment`. It
owns no STS2 legality, action authority, strategy or completion rule.

## Tools

| Tool | Contract |
|---|---|
| `get_sts2_player_environment_capabilities()` | exact loaded Host/game/Modset identity |
| `observe_sts2_player_environment()` | player-visible facts and current finite bound actions |
| `read_sts2_player_information(read_id, expected_snapshot_id)` | execute one advertised, state-bound read opportunity |
| `submit_sts2_bound_action(...)` | deliver one exact advertised bound action |
| `get_sts2_action_receipt(request_id)` | read the original delivery result |

`unknown` delivery is terminal and must not be retried. No tool accepts a game
method, node path, coordinate, index, arbitrary reflection target or hidden
information request. The action tool accepts only request ID, current state
snapshot and opaque bound-action ID; exact native operands remain inside C.

## Run

```bash
uv run --directory /absolute/path/to/STS2-Connector/transports/mcp python server.py
```

Example MCP configuration:

```json
{
  "mcpServers": {
    "sts2": {
      "command": "uv",
      "args": [
        "run",
        "--directory",
        "/absolute/path/to/STS2-Connector/transports/mcp",
        "python",
        "server.py"
      ]
    }
  }
}
```

Validate with:

```bash
uv lock --check --directory /absolute/path/to/STS2-Connector/transports/mcp
uv run --directory /absolute/path/to/STS2-Connector/transports/mcp python -m py_compile server.py
```

Import success proves transpor