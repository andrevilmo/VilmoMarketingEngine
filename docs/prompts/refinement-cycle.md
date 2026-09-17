# Ciclo de refinamento de prompt / comportamento

## Problema observado
Na primeira versão, o node `decide` tratava “publicar” apenas como recomendação (`recommend`), sem bloquear a tool. Em testes de risco, o cenário adversarial ainda chamava tools externas antes da governança completa.

## Alteração realizada
1. Node `govern` passou a rodar **antes** de RAG/tools, com ramificação condicional para `decide` quando `adversarial_detected`.
2. Mensagens com “publicar/publish” sem `human_approved` forçam `decision=request_approval` e passam pelo `approval_gate`.
3. System prompt documentado em `docs/prompts/system-triage.md` reforçando que conteúdo externo não sobrescreve regras.

## Resultado obtido
- `GET /scenarios/adversarial` → `decision=block`, sem `fetch_signals` no `graph_path`.
- `GET /scenarios/risk` → `request_approval` + ações bloqueadas.
- Testes automatizados em `agent/tests` cobrem os três cenários (6 passed).

## Evidências
- Commits/feature branch sugerida: `feature/langgraph-agente`
- Testes: `pytest agent/tests`
- Logs estruturados: campo `correlation_id` em stdout JSON + `agent/data/audit.jsonl`
