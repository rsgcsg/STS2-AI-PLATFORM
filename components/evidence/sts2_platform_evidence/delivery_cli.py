"""Public developer entry point for background close-to-delivery reconciliation."""

from __future__ import annotations

import argparse
import json
import os
import signal
import threading
from contextlib import contextmanager
from collections.abc import Iterator
from pathlib import Path

from .collection_tool import CollectionTool, read_json
from .delivery import DeliveryOutbox, reconcile_and_drain
from .delivery_http import HubTransport


@contextmanager
def process_lock(root: Path) -> Iterator[None]:
    """OS lifetime lock; an orphan child blocks a second worker without PID guessing."""
    with (root / "worker.lock").open("a+b") as stream:
        if os.name == "nt":
            import msvcrt
            if stream.tell() == 0:
                stream.write(b"0")
                stream.flush()
            stream.seek(0)
            try:
                msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
            except OSError:
                raise ValueError("a delivery worker already owns this outbox") from None
            try:
                yield
            finally:
                stream.seek(0)
                msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
        else:
            import fcntl
            try:
                fcntl.flock(stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
            except OSError:
                raise ValueError("a delivery worker already owns this outbox") from None
            try:
                yield
            finally:
                fcntl.flock(stream.fileno(), fcntl.LOCK_UN)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="sts2-evidence-delivery")
    parser.add_argument("command", choices=["run", "status"])
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--once", action="store_true")
    args = parser.parse_args(argv)
    config = read_json(args.config)
    if config.get("schema") != "sts2.evidence/delivery-config-1":
        raise ValueError("unsupported delivery config")
    for name in ("recordings_root", "outbox_root", "tool_directory"):
        if not isinstance(config.get(name), str) or not Path(config[name]).is_absolute():
            raise ValueError(f"{name} must be an absolute path")
    outbox = DeliveryOutbox(config["outbox_root"], worker_id=config["worker_id"],
        campaign_id=config["campaign_id"], human_origin_attested=config["human_origin_attested"],
        tool_release_id=config["tool_release_id"])
    if args.command == "status":
        print(json.dumps({"schema": "sts2.evidence/delivery-status-1", "sessions": outbox.status()}, sort_keys=True))
        return 0
    tool = CollectionTool(config["tool_directory"], config["tool_release_id"], dotnet=config.get("dotnet", "dotnet"))
    transport = HubTransport(config["hub_url"], os.environ.get("STPD_HUB_TOKEN", ""),
        Path(config["outbox_root"]) / "archives", allowed_upload_hosts=config["allowed_upload_hosts"],
        allow_loopback_http=config.get("allow_loopback_http", False))
    interval = config.get("poll_seconds", 5)
    if not isinstance(interval, (int, float)) or isinstance(interval, bool) or not 1 <= interval <= 300:
        raise ValueError("poll_seconds must be between 1 and 300")
    stop = threading.Event()
    for event in (signal.SIGINT, signal.SIGTERM):
        signal.signal(event, lambda *_: stop.set())
    with process_lock(outbox.root):
        while not stop.is_set():
            result = reconcile_and_drain(outbox, Path(config["recordings_root"]), tool, transport)
            print(json.dumps({"discovery": result["discovery"],
                              "processed": [{k: row[k] for k in ("id", "status", "attempts", "content_id", "error")}
                                            for row in result["processed"]]}, sort_keys=True), flush=True)
            if args.once:
                break
            stop.wait(interval)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
