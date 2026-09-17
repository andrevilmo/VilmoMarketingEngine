# Automação low-code / no-code (n8n)

## Objetivo
Orquestrar um alerta SRE sem reimplementar o agente. A lógica permanece no serviço LangGraph; o n8n só dispara e notifica.

## Fluxo
1. **Gatilho:** Webhook `POST /webhook/vilmo-triage-alert`
2. **Integração:** `HTTP Request` → `POST {VILMO_AGENT_URL}/triage`
3. **Saída observável:** se `risk_level != low`, envia mensagem ao ChatOps (`CHATOPS_WEBHOOK_URL`) e responde o webhook com o JSON do agente.

Arquivo importável: [n8n-vilmo-triage-alert.json](./n8n-vilmo-triage-alert.json)

## Reprodução
1. Suba o agente: `uvicorn agent.app.main:app --port 8090` (na raiz do repo, `PYTHONPATH=.`)
2. Importe o JSON no n8n (Community ou cloud)
3. Defina variáveis `VILMO_AGENT_URL=http://host.docker.internal:8090` e `CHATOPS_WEBHOOK_URL` (Discord/Slack/GitHub)
4. Ative o workflow e envie:

```bash
curl -X POST "$N8N_WEBHOOK_URL" \
  -H 'content-type: application/json' \
  -d '{"message":"Publicar SKU-DEMO-002","sku":"SKU-DEMO-002"}'
```

5. Evidência esperada: resposta JSON com `decision=request_approval` e alerta no canal ChatOps.
