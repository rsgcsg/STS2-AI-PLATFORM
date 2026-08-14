"""Thin MCP transport for the STS2 Player Environment.

The adapter forwards state-bound snapshots, reads and exact bound actions. It
owns no game rules, source semantics, action authority or completion logic.
"""

import argparse
import asyncio
from datetime import datetime
import time
from urllib.parse import quote
import uuid

import httpx
from mcp.server.fastmcp import FastMCP

mcp = FastMCP("sts2")

_base_url: str = "http://localhost:15526"
_trust_env: bool = True
_http: httpx.AsyncClient | None = None
_control_lock: asyncio.Lock | None = None
_control: dict | None = None
_CONTROL_PROTOCOL = "1.0-rc.2"
_CONTROL_SCHEMA = "sts2.player-environment/control-1"


def _environment_url(path: str) -> str:
    return f"{_base_url}/api/player-environment/{path.lstrip('/')}"


def _get_client() -> httpx.AsyncClient:
    global _http
    if _http is None:
        _http = httpx.AsyncClient(timeout=httpx.Timeout(10), trust_env=_trust_env)
    return _http


async def _environment_get(path: str) -> str:
    response = await _get_client().get(_environment_url(path))
    response.raise_for_status()
    return response.text


async def _environment_request(
    method: str,
    path: str,
    body: dict | None = None,
) -> str:
    response = await _get_client().request(method, _environment_url(path), json=body)
    # Stale, not-applied and unknown are protocol outcomes. Preserve JSON.
    return response.text


def _get_control_lock() -> asyncio.Lock:
    global _control_lock
    if _control_lock is None:
        _control_lock = asyncio.Lock()
    return _control_lock


def _expiry_monotonic(expires_at: str) -> float:
    expires = datetime.fromisoformat(expires_at.replace("Z", "+00:00")).timestamp()
    return time.monotonic() + max(0.0, expires - time.time())


async def _control_request(path: str, body: dict) -> dict:
    response = await _get_client().post(_environment_url(path), json=body)
    try:
        payload = response.json()
    except ValueError as error:
        raise RuntimeError(f"Player Environment control endpoint returned invalid JSON: {error}") from error
    if not response.is_success:
        status = payload.get("status") if isinstance(payload, dict) else None
        detail = payload.get("detail") if isinstance(payload, dict) else None
        raise RuntimeError(
            f"Player Environment control request {path} rejected: "
            f"{status or response.status_code} {detail or ''}".strip()
        )
    if not isinstance(payload, dict):
        raise RuntimeError("Player Environment control response must be a JSON object")
    # The control DTO protects single-writer delivery; it does not authorize
    # game actions or expose business semantics.
    if payload.get("protocol_version") != _CONTROL_PROTOCOL:
        raise RuntimeError("Player Environment control protocol does not match this adapter")
    if payload.get("schema") != _CONTROL_SCHEMA:
        raise RuntimeError("Player Environment control schema does not match this adapter")
    return payload


async def _ensure_controller() -> dict:
    global _control
    async with _get_control_lock():
        if _control is None:
            client_instance_id = f"mcp-player-environment-{uuid.uuid4()}"
            registration = await _control_request(
                "clients/register",
                {
                    "client_instance_id": client_instance_id,
                    "product_id": "sts2mcp-python-player-environment-adapter",
                    "product_name": "STS2 Player Environment MCP Adapter",
                    "product_version": "1.0.0-rc.1",
                },
            )
            _control = {
                "client_instance_id": client_instance_id,
                "client_session_id": registration["client"]["client_session_id"],
                "runtime_instance_id": registration["runtime_instance_id"],
            }

        lease = _control.get("lease")
        if lease is not None and lease["expires_monotonic"] - time.monotonic() > 10:
            return _control
        if lease is not None:
            try:
                renewed = await _control_request(
                    "controller/renew",
                    {
                        "client_session_id": _control["client_session_id"],
                        "controller_lease_id": lease["controller_lease_id"],
                        "controller_generation": lease["controller_generation"],
                    },
                )
                controller = renewed["controller"]
                _control["lease"] = {
                    **controller,
                    "expires_monotonic": _expiry_monotonic(controller["expires_at"]),
                }
                return _control
            except RuntimeError:
                _control.pop("lease", None)

        acquired = await _control_request(
            "controller/acquire",
            {"client_session_id": _control["client_session_id"]},
        )
        controller = acquired["controller"]
        _control["lease"] = {
            **controller,
            "expires_monotonic": _expiry_monotonic(controller["expires_at"]),
        }
        return _control


def _handle_error(error: Exception) -> str:
    if isinstance(error, httpx.ConnectError):
        return "Error: Cannot connect to the STS2 Player Environment Host. Is the game running with the mod enabled?"
    if isinstance(error, httpx.HTTPStatusError):
        return f"Error: HTTP {error.response.status_code} - {error.response.text}"
    return f"Error: {error}"


@mcp.tool()
async def get_sts2_player_environment_capabilities() -> str:
    """Read exact loaded identity and Player Environment capabilities."""
    try:
        return await _environment_get("capabilities")
    except Exception as error:
        return _handle_error(error)


@mcp.tool()
async def observe_sts2_player_environment() -> str:
    """Read current player-visible facts, interaction grammar and bound actions."""
    try:
        return await _environment_get("snapshot")
    except Exception as error:
        return _handle_error(error)


@mcp.tool()
async def read_sts2_player_information(
    read_id: str,
    expected_snapshot_id: str,
) -> str:
    """Read one exact advertised, state-bound player-visible information item."""
    try:
        encoded_read = quote(read_id, safe="")
        encoded_token = quote(expected_snapshot_id, safe="")
        return await _environment_get(
            f"reads/{encoded_read}?expected_snapshot_id={encoded_token}"
        )
    except Exception as error:
        return _handle_error(error)


@mcp.tool()
async def submit_sts2_bound_action(
    request_id: str,
    expected_snapshot_id: str,
    bound_action_id: str,
) -> str:
    """Deliver one exact bound action advertised by the same snapshot.

    Native operands stay inside C. An unknown delivery must never be retried.
    """
    try:
        control = await _ensure_controller()
        lease = control["lease"]
        return await _environment_request(
            "POST",
            "actions",
            {
                "request_id": request_id,
                "expected_snapshot_id": expected_snapshot_id,
                "bound_action_id": bound_action_id,
                "client_session_id": control["client_session_id"],
                "controller_lease_id": lease["controller_lease_id"],
                "controller_generation": lease["controller_generation"],
            },
        )
    except Exception as error:
        return _handle_error(error)


@mcp.tool()
async def get_sts2_action_receipt(request_id: str) -> str:
    """Read the original request receipt; unknown delivery is terminal."""
    try:
        return await _environment_request("GET", f"actions/{quote(request_id, safe='')}")
    except Exception as error:
        return _handle_error(error)


def main() -> None:
    parser = argparse.ArgumentParser(description="STS2 Player Environment MCP adapter")
    parser.add_argument("--port", type=int, default=15526, help="Player Environment HTTP port")
    parser.add_argument("--host", type=str, default="localhost", help="Player Environment HTTP host")
    parser.add_argument(
        "--no-trust-env",
        action="store_true",
        help="Ignore HTTP_PROXY and HTTPS_PROXY",
    )
    args = parser.parse_args()

    global _base_url, _trust_env
    _base_url = f"http://{args.host}:{args.port}"
    _trust_env = not args.no_trust_env
    _get_client()
    mcp.run(transport="stdio")


if __name__ == "__main__":
    main()
