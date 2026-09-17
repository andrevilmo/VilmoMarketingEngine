from __future__ import annotations

from langgraph.graph import END, START, StateGraph

from agent.app.config import get_settings
from agent.app.nodes import triage as nodes
from agent.app.state import AgentState


def _route_after_govern(state: AgentState) -> str:
    # Conditional branch: adversarial traffic skips retrieval/tools and goes to decide→block
    if state.get("adversarial_detected"):
        return "decide"
    return "retrieve_context"


def _route_after_decide(state: AgentState) -> str:
    settings = get_settings()
    if int(state.get("step_count") or 0) >= settings.max_graph_steps:
        return "emit"
    if state.get("decision") == "block":
        return "emit"
    if state.get("decision") == "request_approval":
        return "approval_gate"
    return "emit"


def build_graph():
    """
    Fluxo principal LangGraph (sequencial + ramificação + paralelização):

      START → ingest → govern
                 ├─(adversarial)→ decide → emit → END
                 └─ retrieve_context → fetch_signals (parallel tools) → decide
                        ├─ request_approval → approval_gate → emit → END
                        └─ inform/recommend/block → emit → END

    Regras determinísticas: governança, gate de aprovação, max_graph_steps.
    Decisões assistidas: classificação de risco / sumário no node decide (LLM_PROVIDER).
    """
    g = StateGraph(AgentState)
    g.add_node("ingest", nodes.node_ingest)
    g.add_node("govern", nodes.node_govern)
    g.add_node("retrieve_context", nodes.node_retrieve_context)
    g.add_node("fetch_signals", nodes.node_fetch_signals)
    g.add_node("decide", nodes.node_decide)
    g.add_node("approval_gate", nodes.node_approval_gate)
    g.add_node("emit", nodes.node_emit)

    g.add_edge(START, "ingest")
    g.add_edge("ingest", "govern")
    g.add_conditional_edges(
        "govern",
        _route_after_govern,
        {"retrieve_context": "retrieve_context", "decide": "decide"},
    )
    g.add_edge("retrieve_context", "fetch_signals")
    g.add_edge("fetch_signals", "decide")
    g.add_conditional_edges(
        "decide",
        _route_after_decide,
        {"approval_gate": "approval_gate", "emit": "emit"},
    )
    g.add_edge("approval_gate", "emit")
    g.add_edge("emit", END)
    return g.compile()


TRIAGE_GRAPH = build_graph()
