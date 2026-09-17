# QA com apoio de IA

## Code review (alteração real)
Arquivo revisado: `agent/app/tools/vilmo_api.py` (cliente HTTP das tools).

### Achados da IA (resumo)
| Severidade | Achado | Ação |
|---|---|---|
| Alta | Retries sem jitter poderiam martelar API sob 5xx | Mantido backoff linear curto (0.15*attempt) + limite `tool_max_retries` |
| Alta | Token em header sem validar ausência de log do valor | Confirmado: apenas `tool_retry` com `error` string, sem Authorization |
| Média | Fallback mock em produção poderia mascarar outage | Documentado: fallback apenas para demo/CI; evidência em `source=mock` no payload |
| Baixa | Path `/inventory/{sku}` vs lista | Tratado nos testes com SKU explícito |

## Testes gerados/refinados com IA
Prioridade por **risco**: publicação com estoque zerado e prompt injection (impacto de segurança/financeiro).

| Tipo | Arquivo | Por que prioritário |
|---|---|---|
| Integração (grafo) | `agent/tests/test_triage.py` | Cobre decisão + governança end-to-end do LangGraph |
| E2E (API) | `agent/tests/test_api_e2e.py` | Exercita HTTP FastAPI dos 3 cenários públicos |
| Domínio .NET (existente) | `tests/Vilmo.Tests/*` | Regressão do backend consumido pelas tools |

Cenário prioritário justificado: **prompt injection** — risco de exfiltração de segredos e bypass de autonomia; coberto por `test_adversarial_prompt_injection_blocked`.
