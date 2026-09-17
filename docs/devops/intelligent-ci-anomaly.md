# DevOps inteligente — pipeline, logs, anomalia e tendência

## Pipeline
Workflow: `.github/workflows/ci.yml`
- Job `agent`: install → **ruff lint** → **pytest** → validação de import (build equivalente)
- Job `dotnet`: restore → **build** → **test**

Deploy não é obrigatório neste projeto avaliativo.

## Análise de logs com IA (duas etapas)

### 1) Lint (ruff)
Log simulado / típico:
```
agent/app/tools/vilmo_api.py:42:9: F841 Local variable `settings` is assigned to but never used
```
**Explicação IA:** variável não usada após refactor do fallback; não quebra runtime, mas indica dívida. Ação: remover ou usar em métrica.

### 2) Testes (pytest)
Log típico de falha intermitente:
```
FAILED agent/tests/test_triage.py::test_main_scenario_structured_output
httpx.ReadTimeout: timed out
```
**Explicação IA:** a tool `get_inventory` esgotou `TOOL_TIMEOUT_SECONDS` ao tentar a API real indisponível; o teste deveria aceitar fallback. Mitigação implementada: captura de erro + fallback mock + retries limitados.

## Anomalia detectada
**Sinal:** aumento de `tool_retry` spans com `error=upstream_503` correlacionados pelo mesmo `correlation_id` prefix pattern em janela de 15 min (dados simulados em `docs/evidencias/failure-trend.csv`).

**Explicação:** a API Vilmo retornou 5xx em rajadas; o agente degradou para mock (`source=mock`) e elevou `risk_level` quando havia erros acumulados em `tool_errors`.

## Estimativa simples de tendência / risco de falha
Série (falhas de tool / 10 execuções):

| Janela | Falhas | Taxa |
|---|---|---|
| T-3 | 1 | 10% |
| T-2 | 2 | 20% |
| T-1 | 4 | 40% |
| T0 (atual) | 5 | 50% |

Regressão linear simples sobre a taxa → tendência de +13 pp por janela.  
**Probabilidade estimada de falha na próxima janela:** ~63% (cap 95%), classificando risco operacional como **alto** até estabilizar a API.

## Conclusão
A falha não está no LangGraph em si, e sim na dependência HTTP. Controles (timeout, retry, fallback, correlação log+audit) permitem diagnosticar e conter impacto na demonstração.
