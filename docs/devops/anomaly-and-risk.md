# DevOps inteligente — logs, anomalia e tendência

## Pipeline
Workflow: `.github/workflows/ci.yml`
- Job `agent`: lint (ruff) → pytest → validação de import (build equivalente)
- Job `dotnet`: restore → build → test

## Análise de logs com IA (duas etapas)

### 1) Lint / teste do agente (simulada a partir de execução local)
```text
span=node_fetch_signals status=ok latency_ms=42.1 correlation_id=...
span=node_decide status=ok latency_ms=1.2 correlation_id=...
...... 6 passed in 5.27s
```
**Explicação IA:** execução saudável; latência do fetch domina o tempo total; ausência de `tool_retry` indica que o fallback mock respondeu sem exceção de transporte.

### 2) Build .NET (padrão esperado)
```text
dotnet build → 0 Error(s)
dotnet test → Passed
```
**Explicação IA:** compilação e suíte de domínio estáveis; falhas futuras aqui devem ser tratadas antes do deploy do agente (tools dependem da API).

## Anomalia detectada
Durante o desenvolvimento, o cenário de risco sem aprovação gerava `recommend` em vez de bloquear publish.

- **Sinal:** `decision=recommend` + `sku=SKU-DEMO-002` + mensagem contendo “Publicar”
- **Anomalia:** ação irreversível não condicionada à aprovação humana
- **Correção:** gate `request_approval` + node `approval_gate`

## Estimativa de tendência / risco de falha
Dados simulados de 7 dias (taxa de `tool_retry` por 100 execuções):

| Dia | retries/100 | falhas finais |
|---|---:|---:|
| D-6 | 2 | 0 |
| D-5 | 3 | 0 |
| D-4 | 5 | 1 |
| D-3 | 8 | 1 |
| D-2 | 12 | 2 |
| D-1 | 15 | 2 |
| D0  | 18 | 3 |

**Conclusão:** tendência de alta na taxa de retry (~+linear). Probabilidade simples de falha final no dia seguinte (regressão linear grosseira): ~20–25%.  
**Ação sugerida:** reduzir timeout apenas após melhorar disponibilidade da API; alertar via n8n quando `risk_level` ∈ {high, critical}.

Script auxiliar: `docs/devops/estimate_risk.py`
