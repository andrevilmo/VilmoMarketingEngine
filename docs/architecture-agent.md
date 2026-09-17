# Arquitetura do agente SCTEC

## Classificação
**Sistema híbrido:** workflow determinístico (LangGraph nodes/edges, governança, approval gate) + decisões assistidas no node `decide` (`LLM_PROVIDER`).

## Diagrama

```text
Cliente / n8n webhook
        │
        ▼
   FastAPI /triage
        │
        ▼
┌──────────────────────────────────────────────┐
│ LangGraph state: AgentState (tipado)         │
│  ingest → govern                              │
│     ├─ adversarial ──────────► decide → emit │
│     └─ retrieve_context (RAG)                 │
│           → fetch_signals (parallel tools)    │
│           → decide ─┬─ request_approval       │
│                     │     → approval_gate     │
│                     └─ emit → END             │
└──────────────────────────────────────────────┘
        │                 │
        ▼                 ▼
  Tools HTTP           Knowledge
  Vilmo API            agent/data/knowledge
  (retry/timeout)      + thread memory jsonl
```

## Separação modelo × regras
| Determinístico | Assistido / classificatório |
|---|---|
| sanitize/scan injection | sumário e priorização de findings |
| max_graph_steps | tom da recomendação (quando LLM real) |
| may_execute / approval_gate | — |
| schema Pydantic de saída | — |
