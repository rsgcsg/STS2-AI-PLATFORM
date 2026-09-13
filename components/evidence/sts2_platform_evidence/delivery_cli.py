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

from .collection_tool import CollectionTool
from .delivery import DeliveryOutbox, reconcile_and_drain
from .delivery_config import DeliveryConfig, doctor, inspect_outbox
from .delivery_http import HubTransport
from .delivery_summary import inspect_delivery_status


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
    parser.add_argument("command", choices=["run", "status", "doctor", "summarize"])
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--once", action="store_true")
    parser.add_argument("--summary", action="store_true")
    parser.add_argument("--limit", type=int, default=25)
    parser.add_argument("--offset", type=int, default=0)
    parser.add_argument("--delivery-id")
    args = parser.parse_args(argv)
    if args.command in {"doctor", "run"}:
        report = doctor(args.config)
        if args.command == "doctor" or report["status"] != "PASS":
            print(json.dumps(report, sort_keys=True))
            return 0 if report["status"] == "PASS" else 1
    config = DeliveryConfig.load(args.config)
    if args.command == "status":
        value = inspect_delivery_status(config, limit=args.limit, offset=args.offset, delivery_id=args.delivery_id) \
            if args.summary else {"schema": "sts2.evidence/delivery-status-1", "sessions": inspect_outbox(config)}
        print(json.dumps(value, sort_keys=True))
        return 0
    if args.command == "summarize":
        if not (config.outbox_root / "outbox.sqlite3").is_file():
            print(json.dumps({"schema": "sts2.evidence/delivery-summary-rebuild-1", "rebuilt": 0,
                              "unavailable": 0, "total": 0, "next_offset": None}, sort_keys=True))
            return 0
        with process_lock(config.outbox_root):
            outbox = DeliveryOutbox(config.outbox_root, **config.identity)
            print(json.dumps(outbox.rebuild_summaries(limit=args.limit, offset=args.offset), sort_keys=True))
        return 0
    outbox = DeliveryOutbox(config.outbox_root, **config.identity)
    tool = CollectionTool(config.tool_directory, config.tool_release_id, dotnet=config.dotnet)
    transport = HubTransport(config.hub_url, os.environ.get("STPD_HUB_TOKEN", ""),
        config.outbox_root / "archives", allowed_upload_hosts=config.allowed_upload_hosts,
        allow_loopback_http=config.allow_loopback_http)
    stop = threading.Event()
    for event in (signal.SIGINT, signal.SIGTERM):
        signal.signal(event, lambda *_: stop.set())
    with process_lock(outbox.root):
        while not stop.is_set():
            result = reconcile_and_drain(outbox, config.recordings_root, tool, transport)
            print(json.dumps({"discovery": result["discovery"],
                              "processed": [{k: row[k] for k in ("id", "status", "attempts", "content_id", "error")}
                                            for row in result["processed"]]}, sort_keys=True), flush=True)
            if args.once:
                break
            stop.wait(config.poll_seconds)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
