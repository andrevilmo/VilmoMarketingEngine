from __future__ import annotations

import asyncio
from typing import Any

import httpx

from agent.app.config import get_settings
from agent.app.observability.tracing import log


class VilmoToolError(Exception):
    pass


async def _request(method: str, path: str, **kwargs: Any) -> Any:
    settings = get_settings()
    headers: dict[str, str] = {"Accept": "application/json"}
    if settings.vilmo_api_token:
        headers["Authorization"] = f"Bearer {settings.vilmo_api_token}"
    if settings.vilmo_company_id:
        headers["X-Company-Id"] = settings.vilmo_company_id

    last_err: Exception | None = None
    for attempt in range(1, settings.tool_max_retries + 2):
        try:
            async with httpx.AsyncClient(
                base_url=settings.vilmo_api_base_url.rstrip("/"),
                timeout=settings.tool_timeout_seconds,
            ) as client:
                resp = await client.request(method, path, headers=headers, **kwargs)
                if resp.status_code >= 500:
                    raise VilmoToolError(f"upstream_{resp.status_code}")
                if resp.status_code == 404:
                    return None
                if resp.status_code >= 400:
                    raise VilmoToolError(f"client_{resp.status_code}:{resp.text[:200]}")
                if resp.headers.get("content-type", "").startswith("application/json"):
                    return resp.json()
                return {"raw": resp.text}
        except (httpx.TimeoutException, httpx.TransportError, VilmoToolError) as exc:
            last_err = exc
            log.warning(
                "tool_retry",
                attempt=attempt,
                path=path,
                error=str(exc),
            )
            await asyncio.sleep(0.15 * attempt)
    raise VilmoToolError(str(last_err) if last_err else "unknown_tool_error")


async def get_inventory(sku: str | None = None) -> dict[str, Any]:
    """Tool: read company inventory (or mock fallback)."""
    settings = get_settings()
    try:
        if sku:
            data = await _request("GET", f"/inventory/{sku}")
            if data is not None:
                return {"source": "api", "items": [data] if not isinstance(data, list) else data}
        data = await _request("GET", "/inventory")
        if data is not None:
            return {"source": "api", "items": data if isinstance(data, list) else [data]}
    except VilmoToolError as exc:
        log.warning("inventory_fallback", error=str(exc))

    # Deterministic mock for demos / offline CI
    items = [
        {"sku": "SKU-DEMO-001", "on_hand": 12, "warehouse": "MAIN"},
        {"sku": "SKU-DEMO-002", "on_hand": 0, "warehouse": "MAIN"},
    ]
    if sku:
        items = [i for i in items if i["sku"] == sku] or [
            {"sku": sku, "on_hand": 3, "warehouse": "MAIN"}
        ]
    return {
        "source": "mock",
        "items": items,
        "note": f"fallback; base={settings.vilmo_api_base_url}",
    }


async def get_nfe_ingest_logs(chave: str | None = None, limit: int = 10) -> list[dict[str, Any]]:
    """Tool: fetch NF-e ingest progress logs."""
    try:
        params: dict[str, Any] = {"limit": limit}
        if chave:
            params["chave"] = chave
        data = await _request("GET", "/nfe/ingest-logs", params=params)
        if isinstance(data, list):
            return data
        if isinstance(data, dict) and "items" in data:
            return list(data["items"])
    except VilmoToolError as exc:
        log.warning("ingest_logs_fallback", error=str(exc))

    return [
        {
            "chave": chave or "35260168431371000161550010000000011000000010",
            "status": "Completed",
            "message": "mock ingest ok",
            "source": "mock",
        }
    ]


async def health_check() -> dict[str, Any]:
    try:
        data = await _request("GET", "/health")
        return {"ok": True, "body": data}
    except VilmoToolError as exc:
        return {"ok": False, "error": str(exc), "fallback": True}
