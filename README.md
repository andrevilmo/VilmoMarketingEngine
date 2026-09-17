# Vilmo Marketing Engine + Agente SCTEC (M2.2)

**Marketplace Engine multi-empresa (NF-e → estoque → marketplaces)** evoluído com um **agente híbrido LangGraph** de triagem operacional para o projeto avaliativo *IA para Desenvolvedores* (SCTEC).

| Artefato | Link |
|---|---|
| Vídeo de demonstração (YouTube não listado) | _TODO: colar URL após gravar (máx. 12 min)_ |
| Quadro Kanban (GitHub Project) | _TODO: colar URL do Project_ |
| Documentação técnica | [`docs/`](./docs/) |
| Código do agente | [`agent/`](./agent/) |

## 1. Descrição da solução

- **Problema:** operadores precisam diagnosticar estoque, ingestão NF-e e pedidos de publicação com risco controlado, sem executar ações inseguras.
- **Público:** Admin/Company (operações) e avaliadores do módulo SCTEC.
- **Objetivo:** receber uma solicitação, percorrer um fluxo multi-etapas com tools, memória/RAG, governança e produzir `TriageResult` estruturado (JSON/Pydantic).
- **Valor:** reduz tempo de triagem e impede publicações/exfiltrações indevidas.
- **Continuidade:** mantém o engine .NET (API, workers, NF-e, UI). **Evolução M2.2:** serviço `agent/` (LangGraph + FastAPI), `/docs`, CI, low-code n8n e evidências de QA/DevOps.

## 2. Classificação e arquitetura

**Sistema híbrido** — workflow determinístico LangGraph + decisões assistidas (`LLM_PROVIDER`).

Detalhes e diagrama: [docs/architecture-agent.md](./docs/architecture-agent.md)

```text
ingest → govern ─┬─(injection)→ decide → emit
                 └─ RAG → fetch_signals (parallel) → decide ─┬─ approval_gate → emit
                                                            └─ emit
```

## 3. Tool e integração

Tools HTTP em `agent/app/tools/vilmo_api.py`:
- `get_inventory` → `GET /inventory` / `/inventory/{sku}`
- `get_nfe_ingest_logs` → `GET /nfe/ingest-logs`

Validação de erros, **timeout**, **retry limitado** e **fallback** documentado (`source=mock` em demo/CI). Integração low-code: [docs/low-code/](./docs/low-code/).

## 4. Contexto e memória

- **State** tipado (`AgentState`) + path do grafo.
- **RAG lexical** sobre `agent/data/knowledge/` (chunking por parágrafos).
- **Memória de thread** em `agent/data/memory/{thread_id}.jsonl`.

## 5. Segurança e autonomia

- Segredos só via `.env` (ver `.env.example`); nunca no git.
- Scan de prompt injection / redaction; ações proibidas bloqueadas.
- `publish_*` exige `human_approved=true`.
- Cenário adversarial: `GET /scenarios/adversarial` → `decision=block`, sem tools nem vazamento de segredos.

Prompts: [docs/prompts/](./docs/prompts/).

## 6. Instalação e execução

### Pré-requisitos
- .NET 10 SDK (engine)
- Python 3.11+ (agente)
- Docker opcional (Compose em `deploy/`)

### Variáveis de ambiente
Copie `.env.example` → `.env` (já gitignored). Principais do agente:

| Variável | Descrição | Default |
|---|---|---|
| `LLM_PROVIDER` | `mock` ou `openai` | `mock` |
| `LLM_MODEL` | nome do modelo | `gpt-4o-mini` |
| `OPENAI_API_KEY` | chave (nunca commitada) | vazio |
| `VILMO_API_BASE_URL` | base da API | `http://localhost:5080` |
| `VILMO_API_TOKEN` | JWT Bearer | vazio |
| `ALLOW_DESTRUCTIVE_ACTIONS` | false em prod | `false` |

### Agente (demonstração SCTEC)
```bash
python -m venv .venv && source .venv/bin/activate
pip install -r agent/requirements.txt
export LLM_PROVIDER=mock
PYTHONPATH=. uvicorn agent.app.main:app --reload --port 8090
```

Cenários:
```bash
curl -s localhost:8090/scenarios/main | jq .
curl -s localhost:8090/scenarios/risk | jq .
curl -s localhost:8090/scenarios/adversarial | jq .
```

### Testes
```bash
PYTHONPATH=. pytest agent/tests -q
dotnet test tests/Vilmo.Tests/Vilmo.Tests.csproj
```

### Engine Vilmo
Ver `MD/ARCHITECTURE.md` e `deploy/docker-compose.yml` para API/Worker/NFe/Web.

## 7. QA, observabilidade e DevOps

- Review IA + testes: [docs/qa/ai-code-review-and-tests.md](./docs/qa/ai-code-review-and-tests.md)
- Pipeline: [`.github/workflows/ci.yml`](./.github/workflows/ci.yml) (lint, testes, build)
- Anomalia + tendência: [docs/devops/anomaly-and-trend.md](./docs/devops/anomaly-and-trend.md)
- Sinais: logs JSON (`structlog`) + auditoria `AUDIT_LOG_PATH` correlacionados por `correlation_id` (+ `latency_ms`)

## 8. Automação low-code/no-code

Fluxo n8n: webhook → `POST /triage` → alerta ChatOps se risco ≠ low.  
Instruções: [docs/low-code/README.md](./docs/low-code/README.md).

## 9. Cenários de uso

| Cenário | Entrada | Esperado |
|---|---|---|
| Principal | `/scenarios/main` | Triagem com tools + saída estruturada |
| Risco | `/scenarios/risk` | `request_approval`, publish bloqueado |
| Adversarial | `/scenarios/adversarial` | `block`, sem exfiltração |

Detalhes: [docs/evidencias/cenarios.md](./docs/evidencias/cenarios.md).

## 10. Análise crítica, limitações e vídeo

Refinamento documentado: [docs/prompts/refinement-cycle.md](./docs/prompts/refinement-cycle.md).

**Limitações**
- `LLM_PROVIDER=mock` é determinístico (sem custo); OpenAI opcional via env.
- Fallback mock das tools não deve ser usado como fonte de verdade em produção.
- Kanban/vídeo/colaborador professor são passos manuais no GitHub/YouTube/AVA.

**Evoluções**
- Checkpointer Postgres LangGraph; embeddings reais; MCP server dedicado; ChatOps nativo.

Checklist: [docs/checklist-entrega.md](./docs/checklist-entrega.md).

## Estrutura do repositório

```text
agent/           # FastAPI + LangGraph (entrega SCTEC)
docs/            # prompts, qa, evidencias, devops, low-code
src/             # Vilmo.Api / App / Worker / Nfe / Web
tests/           # testes .NET + e2e NF-e
MD/              # arquitetura de produto marketplace
.github/workflows/ci.yml
```

## Fluxo Git sugerido (SCTEC)

`develop` ← `feature/langgraph-agente` (e demais `feature/*`) → PR → `main`.
