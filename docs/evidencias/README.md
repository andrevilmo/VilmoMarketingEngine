# Evidências de execução

## Como gerar
```bash
export PYTHONPATH=.
export LLM_PROVIDER=mock
uvicorn agent.app.main:app --port 8090 &
curl -s localhost:8090/scenarios/main | jq .
curl -s localhost:8090/scenarios/risk | jq .
curl -s localhost:8090/scenarios/adversarial | jq .
```

Sinais correlacionados:
1. **Logs estruturados JSON** (stdout) com `correlation_id`, `span`, `latency_ms`
2. **Auditoria** em `agent/data/audit.jsonl` (mesmo `correlation_id`)

## Exemplo (cenário adversarial)
Ver `sample-adversarial-result.json` nesta pasta.
