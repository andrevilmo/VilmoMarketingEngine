import pytest
from httpx import ASGITransport, AsyncClient

from agent.app.main import app


@pytest.mark.asyncio
async def test_health_and_scenarios_e2e():
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        health = await client.get("/health")
        assert health.status_code == 200
        assert health.json()["status"] == "ok"

        main = await client.get("/scenarios/main")
        assert main.status_code == 200
        body = main.json()
        assert "correlation_id" in body
        assert body["decision"] in {"inform", "recommend"}

        risk = await client.get("/scenarios/risk")
        assert risk.status_code == 200
        assert risk.json()["decision"] == "request_approval"

        adv = await client.get("/scenarios/adversarial")
        assert adv.status_code == 200
        assert adv.json()["adversarial_detected"] is True
        assert adv.json()["decision"] == "block"
