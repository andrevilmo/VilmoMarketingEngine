# Plano de crescimento de vendas no Mercado Livre

Versão em português do plano comercial. Texto em inglês: [ML_SALES_GROWTH.md](./ML_SALES_GROWTH.md).

O caminho para vender mais **nesse protetor de dedos** é **Product Ads no Mercado Livre**. Google entra primeiro como pesquisa de palavras, não como substituto do anúncio dentro da plataforma.

---

## Anúncio de referência

| Campo | Valor |
| --- | --- |
| Anúncio | [Protetor de dedos aço inoxidável — corte legumes / faca seguro](https://www.mercadolivre.com.br/protetor-dedos-aco-inoxidavel-corte-legumes-faca-seguro/up/MLBU1969589861?pdp_filters=item_id%3AMLB3191936628&wid=MLB3191936628) |
| User Product (família de catálogo) | `MLBU1969589861` |
| Oferta do vendedor (item) | `MLB3191936628` |
| Tipo | Gadget barato de cozinha, muitos anúncios iguais, página de catálogo |
| Rastro de Ads | O link já traz `matt_tool=38524122` (rastreio de clique do Mercado Ads). Trate este SKU como **já no ecossistema de Ads**, não como página em branco. |

O produto é um **acessório genérico de segurança**. O comprador escolhe por **preço, frete e reputação** numa página de catálogo compartilhada, não por história de marca. Ads **amplifica a oferta atual**; não cria demanda nova.

---

## Ordem (não pule)

1. **Conta antes de gastar.** Abra o **Simulador de custos** nesse anúncio. Em item abaixo de ~R$ 79, a **tarifa por unidade** (logística / item barato) muitas vezes pesa mais que a comissão. Se o líquido depois de custo + embalagem + comissão + tarifa + frete que você absorve for ≤ 0, **não anuncie**. Nesse ticket, **Clássico** costuma preservar mais margem que Premium.

2. **Anúncio em ordem.** Product Ads **não tem criativo separado** nem palavra-chave negativa. O que aparece é título + foto 1 + preço + frete. Preencha a ficha 100%, foto de fundo branco, ME2 ativo, estoque sem zerar. **Não clone** outro anúncio do mesmo produto de catálogo.

3. **Product Ads** (Vendas → Publicidade). Campanha **automática**, só SKUs com margem parecida. **ROAS Objetivo acima do ponto de equilíbrio** (ROAS de equilíbrio = 1 / margem de contribuição). Deixe **14–28 dias**. A métrica que decide escala é o **TACOS** (gasto em Ads ÷ faturamento total do SKU): ROAS bonito com unidades totais estáveis significa que você comprou a venda que já viria orgânica. **Brand Ads** e Display da Minha Página não valem para esse genérico.

4. **Google como pesquisa.** [Trends](https://trends.google.com.br/trends/explore?geo=BR&q=protetor%20de%20dedos,guarda%20dedos,protetor%20corte) e [Planejador de palavras-chave](https://ads.google.com/home/lib_zulu/keyword-planner/) + autocomplete do ML: coloque no título/ficha o que o comprador busca (`protetor de dedos`, `guarda dedos`, `corte legumes`). Shopping / Performance Max precisa de **loja sua**. Anúncio de Pesquisa apontando para o ML é permitido, mas fraco (sem pixel, um anúncio por domínio, CPC alto vs ticket de ~R$ 15).

5. **Suba o ticket.** Kit ou pack (2 unidades, ou protetor + descascador + tábua) quase sempre **anuncia melhor** que o item de R$ 12. Coloque o orçamento de Ads no kit; deixe o catálogo como cauda longa. Depois replique em Shopee/Magalu. Não espalhe verba pequena em Meta + Google + Display.

---

## Meta em 90 dias

Unidades **totais** sobem (não só as atribuídas ao Ads), ROAS do Ads ≥ equilíbrio, TACOS não explode, reputação não piora.

Automação disso no Vilmo (API de Product Ads) só faz sentido **depois** que esse ciclo no painel do vendedor já for lucrativo. Qualquer integração futura passa pelo gateway + adapter de marketplace, não por chamada direta do ERP / WMS / PWA. Ver [MarketPlaceEngine.MD](./MarketPlaceEngine.MD).
