# Observabilidade

## Dois sinais correlacionados
| Sinal | Onde | Campos-chave |
|---|---|---|
| Logs estruturados | stdout JSON (structlog) | `span`, `correlation_id`, `latency_ms`, `status` |
| Auditoria | `agent/data/audit.jsonl` | mesmo `correlation_id` + payload do span |

## Investigação de uma execução
1. Obter `correlation_id` da resposta HTTP `/triage`
2. `rg <correlation_id> agent/data/audit.jsonl`
3. Ordenar spans: `http_triage` → nodes → latências e erros

## Resiliência
- Timeout configurável (`TOOL_TIMEOUT_SECONDS`)
- Retry limitado (`TOOL_MAX_RETRIES`)
- Fallback mock documentado quando API indisponível (campo `source=mock`)
