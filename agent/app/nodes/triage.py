from __future__ import annotations

import asyncio

from agent.app.config import get_settings
from agent.app.governance.policy import may_execute, sanitize_and_scan
from agent.app.memory.rag import retrieve
from agent.app.observability.tracing import timed_span
from agent.app.state import AgentState
from agent.app.tools import vilmo_api


def _path(state: AgentState, node: str) -> list[str]:
    return list(state.get("graph_path") or []) + [node]


async def node_ingest(state: AgentState) -> AgentState:
    with timed_span("node_ingest", state["correlation_id"]):
        return {
            **state,
            "graph_path": _path(state, "ingest"),
            "step_count": int(state.get("step_count") or 0) + 1,
            "tools_used": list(state.get("tools_used") or []),
            "tool_errors": list(state.get("tool_errors") or []),
            "findings": list(state.get("findings") or []),
            "recommended_actions": list(state.get("recommended_actions") or []),
            "blocked_actions": list(state.get("blocked_actions") or []),
        }


async def node_govern(state: AgentState) -> AgentState:
    with timed_span("node_govern", state["correlation_id"]):
        gov = sanitize_and_scan(state["message"])
        return {
            **state,
            "sanitized_message": gov.sanitized_message,
            "adversarial_detected": gov.adversarial_detected,
            "injection_patterns": gov.matched_patterns,
            "blocked_actions": list(
                dict.fromkeys([*(state.get("blocked_actions") or []), *gov.blocked_actions])
            ),
            "graph_path": _path(state, "govern"),
            "step_count": int(state.get("step_count") or 0) + 1,
        }


async def node_retrieve_context(state: AgentState) -> AgentState:
    with timed_span("node_retrieve_context", state["correlation_id"]):
        query = state.get("sanitized_message") or state["message"]
        snippets = retrieve(query)
        return {
            **state,
            "context_snippets": snippets,
            "graph_path": _path(state, "retrieve_context"),
            "step_count": int(state.get("step_count") or 0) + 1,
            "tools_used": list(dict.fromkeys([*(state.get("tools_used") or []), "rag_retrieve"])),
        }


async def node_fetch_signals(state: AgentState) -> AgentState:
    """Simple parallelization: inventory + ingest logs concurrently."""
    with timed_span("node_fetch_signals", state["correlation_id"]):
        errors = list(state.get("tool_errors") or [])
        tools = list(state.get("tools_used") or [])

        inv_task = asyncio.create_task(vilmo_api.get_inventory(state.get("sku")))
        logs_task = asyncio.create_task(vilmo_api.get_nfe_ingest_logs(state.get("nfe_chave")))
        inventory, logs = None, None
        inv_res, logs_res = await asyncio.gather(inv_task, logs_task, return_exceptions=True)

        if isinstance(inv_res, Exception):
            errors.append(f"get_inventory:{inv_res}")
        else:
            inventory = inv_res
            tools.append("get_inventory")

        if isinstance(logs_res, Exception):
            errors.append(f"get_nfe_ingest_logs:{logs_res}")
        else:
            logs = logs_res
            tools.append("get_nfe_ingest_logs")

        return {
            **state,
            "inventory": inventory,
            "ingest_logs": logs,
            "tool_errors": errors,
            "tools_used": list(dict.fromkeys(tools)),
            "graph_path": _path(state, "fetch_signals"),
            "step_count": int(state.get("step_count") or 0) + 1,
        }


async def node_decide(state: AgentState) -> AgentState:
    settings = get_settings()
    with timed_span("node_decide", state["correlation_id"], provider=settings.llm_provider):
        if state.get("adversarial_detected"):
            return {
                **state,
                "decision": "block",
                "risk_level": "critical",
                "summary": (
                    "Entrada adversarial detectada. Regras da aplicação prevalecem; "
                    "nenhuma ação externa executada e segredos não são revelados."
                ),
                "findings": [
                    "Padrões de prompt injection ou exfiltração identificados.",
                    f"Padrões: {', '.join(state.get('injection_patterns') or [])}",
                ],
                "recommended_actions": ["Rejeitar a solicitação", "Registrar incidente de segurança"],
                "requires_human_approval": False,
                "graph_path": _path(state, "decide"),
                "step_count": int(state.get("step_count") or 0) + 1,
            }

        findings: list[str] = []
        inventory = state.get("inventory") or {}
        items = inventory.get("items") or []
        zero = [i for i in items if int(i.get("on_hand") or 0) <= 0]
        if zero:
            findings.append(f"SKUs sem estoque: {', '.join(i.get('sku', '?') for i in zero)}")
        if state.get("tool_errors"):
            findings.append(f"Falhas de tool (com fallback): {'; '.join(state['tool_errors'])}")
        if state.get("context_snippets"):
            findings.append(f"Contexto RAG recuperado: {len(state['context_snippets'])} trecho(s)")

        logs = state.get("ingest_logs") or []
        failed_logs = [x for x in logs if str(x.get("status", "")).lower() in {"failed", "error"}]
        if failed_logs:
            findings.append(f"Ingestões NF-e com falha: {len(failed_logs)}")

        msg = (state.get("sanitized_message") or "").lower()
        wants_publish = "publicar" in msg or "publish" in msg
        high_risk = bool(zero) or bool(failed_logs) or wants_publish

        if wants_publish and not state.get("human_approved"):
            decision = "request_approval"
            risk = "high"
            summary = "Publicação de anúncio solicitada. Ação condicionada à aprovação humana."
            requires = True
            blocked = list(state.get("blocked_actions") or []) + ["publish_without_approval"]
        elif high_risk:
            decision = "recommend"
            risk = "high" if zero or failed_logs else "medium"
            summary = "Triagem concluiu risco elevado; recomendações geradas sem executar ações destrutivas."
            requires = False
            blocked = list(state.get("blocked_actions") or [])
        else:
            decision = "inform"
            risk = "low"
            summary = "Triagem operacional concluída sem anomalias críticas."
            requires = False
            blocked = list(state.get("blocked_actions") or [])

        recommended: list[str] = []
        if zero:
            recommended.append("Revisar saldo e bloquear publicação do SKU zerado")
        if failed_logs:
            recommended.append("Reprocessar ingestão NF-e e inspecionar logs correlacionados")
        if wants_publish:
            recommended.append("Aguardar aprovação humana antes de publish_listing")
        if not recommended:
            recommended.append("Nenhuma ação imediata necessária")

        _ = settings.llm_model  # configured via env; mock path is deterministic

        return {
            **state,
            "decision": decision,
            "risk_level": risk,
            "summary": summary,
            "findings": findings or ["Sem anomalias detectadas nos sinais coletados"],
            "recommended_actions": recommended,
            "blocked_actions": list(dict.fromkeys(blocked)),
            "requires_human_approval": requires,
            "graph_path": _path(state, "decide"),
            "step_count": int(state.get("step_count") or 0) + 1,
        }


async def node_approval_gate(state: AgentState) -> AgentState:
    with timed_span("node_approval_gate", state["correlation_id"]):
        allowed, reason = may_execute(
            "publish_listing",
            human_approved=bool(state.get("human_approved")),
            allow_destructive=get_settings().allow_destructive_actions,
        )
        if not allowed:
            return {
                **state,
                "decision": "request_approval",
                "blocked_actions": list(
                    dict.fromkeys([*(state.get("blocked_actions") or []), "publish_listing"])
                ),
                "summary": f"Gate de aprovação: {reason}",
                "graph_path": _path(state, "approval_gate"),
                "step_count": int(state.get("step_count") or 0) + 1,
            }
        return {
            **state,
            "decision": "execute",
            "summary": "Aprovação humana recebida; execução simulada (não destrutiva).",
            "graph_path": _path(state, "approval_gate"),
            "step_count": int(state.get("step_count") or 0) + 1,
        }


async def node_emit(state: AgentState) -> AgentState:
    with timed_span("node_emit", state["correlation_id"]):
        result = {
            "correlation_id": state["correlation_id"],
            "decision": state.get("decision") or "inform",
            "risk_level": state.get("risk_level") or "low",
            "summary": state.get("summary") or "",
            "findings": state.get("findings") or [],
            "recommended_actions": state.get("recommended_actions") or [],
            "blocked_actions": state.get("blocked_actions") or [],
            "tools_used": state.get("tools_used") or [],
            "context_snippets": state.get("context_snippets") or [],
            "adversarial_detected": bool(state.get("adversarial_detected")),
            "requires_human_approval": bool(state.get("requires_human_approval")),
            "graph_path": _path(state, "emit"),
        }
        return {
            **state,
            "result": result,
            "graph_path": result["graph_path"],
            "step_count": int(state.get("step_count") or 0) + 1,
            "stop": True,
        }
