from __future__ import annotations

import json
import time
import uuid
from contextlib import contextmanager
from pathlib import Path
from typing import Any, Iterator

import structlog

from agent.app.config import get_settings

structlog.configure(
    processors=[
        structlog.processors.TimeStamper(fmt="iso"),
        structlog.processors.add_log_level,
        structlog.processors.StackInfoRenderer(),
        structlog.processors.format_exc_info,
        structlog.processors.JSONRenderer(),
    ],
    wrapper_class=structlog.make_filtering_bound_logger(20),
    context_class=dict,
    logger_factory=structlog.PrintLoggerFactory(),
    cache_logger_on_first_use=True,
)

log = structlog.get_logger()


def new_correlation_id() -> str:
    return str(uuid.uuid4())


@contextmanager
def timed_span(name: str, correlation_id: str, **fields: Any) -> Iterator[dict[str, Any]]:
    """Emit structured log + audit row with latency (two correlated signals)."""
    start = time.perf_counter()
    span: dict[str, Any] = {
        "span": name,
        "correlation_id": correlation_id,
        **fields,
    }
    try:
        yield span
        span["status"] = "ok"
    except Exception as exc:  # noqa: BLE001
        span["status"] = "error"
        span["error"] = str(exc)
        raise
    finally:
        span["latency_ms"] = round((time.perf_counter() - start) * 1000, 2)
        log.info("span", **span)
        write_audit(span)


def write_audit(event: dict[str, Any]) -> None:
    settings = get_settings()
    path = Path(settings.audit_log_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a", encoding="utf-8") as fh:
        fh.write(json.dumps(event, ensure_ascii=False) + "\n")
