from __future__ import annotations

from typing import Any, Literal, TypedDict

from pydantic import BaseModel, Field


RiskLevel = Literal["low", "medium", "high", "critical"]
Decision = Literal["inform", "recommend", "request_approval", "block", "execute"]


class TriageRequest(BaseModel):
    """Structured input for the operational triage agent."""

    message: str = Field(..., min_length=1, max_length=4000)
    company_id: str | None = None
    sku: str | None = None
    nfe_chave: str | None = None
    human_approved: bool = False
    thread_id: str | None = None
    scenario: Literal["main", "risk", "adversarial"] | None = None


class TriageResult(BaseModel):
    """Structured domain output."""

    correlation_id: str
    decision: Decision
    risk_level: RiskLevel
    summary: str
    findings: list[str] = Field(default_factory=list)
    recommended_actions: list[str] = Field(default_factory=list)
    blocked_actions: list[str] = Field(default_factory=list)
    tools_used: list[str] = Field(default_factory=list)
    context_snippets: list[str] = Field(default_factory=list)
    adversarial_detected: bool = False
    requires_human_approval: bool = False
    latency_ms: float = 0.0
    graph_path: list[str] = Field(default_factory=list)


class AgentState(TypedDict, total=False):
    """Shared LangGraph state (typed)."""

    correlation_id: str
    message: str
    sanitized_message: str
    company_id: str | None
    sku: str | None
    nfe_chave: str | None
    human_approved: bool
    scenario: str | None

    adversarial_detected: bool
    injection_patterns: list[str]
    context_snippets: list[str]
    inventory: dict[str, Any] | None
    ingest_logs: list[dict[str, Any]] | None
    tool_errors: list[str]
    tools_used: list[str]

    decision: Decision
    risk_level: RiskLevel
    summary: str
    findings: list[str]
    recommended_actions: list[str]
    blocked_actions: list[str]
    requires_human_approval: bool
    graph_path: list[str]
    step_count: int
    stop: bool
    result: dict[str, Any]
