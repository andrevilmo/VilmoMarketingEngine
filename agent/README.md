# Agente de triagem SCTEC (FastAPI + LangGraph)

```bash
# na raiz do repositório
pip install -r agent/requirements.txt
export LLM_PROVIDER=mock
PYTHONPATH=. uvicorn agent.app.main:app --port 8090
PYTHONPATH=. pytest agent/tests -q
```

Endpoints: `/health`, `/triage`, `/scenarios/{main,risk,adversarial}`.
