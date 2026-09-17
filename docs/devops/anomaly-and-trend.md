# DevOps inteligente — logs, anomalia e tendência

## Pipeline
Workflow: `.github/workflows/ci.yml`
- Job `agent`: lint (ruff) → pytest → validação de import (build equivalente)
- Job `dotnet`: restore → build → test

## Análise de logs com IA (duas etapas)

### Etapa A — Lint (ruff)
Log simulado / típico:
```
agent/app/tools/vilmo_api.py:45:9: F841 Local variable `settings` is assigned to but never used
```
**Explicação IA:** variável residual após refactor do fallback; não quebra runtime, mas indica dívida. Ação: remover ou usar em métrica.

### Etapa B — Testes (pytest)
Log real observado durante implementação:
```
tool_retry attempt=1 path=/inventory error=...
inventory_fallback error=...
6 passed in 5.27s
```
**Explicação IA:** a API Vilmo não estava no ar; a tool aplicou timeout/retry e caiu no fallback mock (`source=mock`). Comportamento esperado em demo; em produção o aumento de `inventory_fallback` é sinal de incidente.

## Anomalia detectada
**Sinal:** aumento da taxa de `tool_retry` / `inventory_fallback` correlacionado pelo `correlation_id` nos spans JSON + auditoria `agent/data/audit.jsonl`.

**Explicação:** latência/indisponibilidade do upstream `VILMO_API_BASE_URL`, não falha do grafo (nodes `fetch_signals` ainda completam via fallback).

## Estimativa simples de tendência / risco de falha
Dados simulados (7 dias) de taxa de fallback de inventory:

| Dia | fallback_rate |
|---|---|
| D-6 | 0.02 |
| D-5 | 0.03 |
| D-4 | 0.04 |
| D-3 | 0.08 |
| D-2 | 0.11 |
| D-1 | 0.15 |
| D0  | 0.18 |

Regressão linear simples (x=0..6): slope ≈ **+0.028/dia**.  
Projeção D+3 ≈ 0.26. **Risco de falha operacional classificado como médio-alto** se a tendência continuar sem recuperação do upstream (limiar de alerta = 0.20).

Script de apoio: `docs/devops/estimate_failure_trend.py`.

## Evidências
- Spans estruturados (`structlog` JSON) com `correlation_id` + `latency_ms`
- Auditoria append-only em `AUDIT_LOG_PATH`
- Este documento + script de tendência
