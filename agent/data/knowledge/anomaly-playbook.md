# Anomalias conhecidas

## Taxa de erro de tool
- Timeout em get_inventory após 5s: aplicar retry limitado (máx 2) e fallback mock apenas em ambiente de demonstração.
- 5xx da API Vilmo: registrar span tool_retry com correlation_id.

## Estoque zerado
- SKU-DEMO-002 frequentemente aparece com on_hand=0 em fixtures; risco alto para publish_listing.

## Latência
- Pipeline de triagem esperado: p95 < 2000ms em modo mock; > 5000ms indica degradação.
