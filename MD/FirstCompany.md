# First company seed (Cartão CNPJ + A1)

Loaded from the operator attachments: RFB **CNPJ EMPRESA NOVA** (Comprovante de Inscrição e de Situação Cadastral) and the A1 PKCS#12 (file was renamed `.txt`; restored as `.pfx` locally). Machine-readable copy: [seed/first-company.json](./seed/first-company.json).

**Do not commit** the PFX, the cartão PDF, or the certificate password. They stay in gitignored `.secrets/` and `.env`.

## Who this tenant is

The storefront name is the **nome fantasia**. The legal entity (NF-e emitente, A1, marketplaces) is the **razão social** + CNPJ.

| Field | Value |
| --- | --- |
| Nome fantasia (UI / sender trade name) | **VILMO COMERCIO, REPRESENTACOES E INFORMATICA** |
| Razão social (legal / A1 / emitente) | **A. VILMO PINHEIRO CARDOSO TECNOLOGIA LTDA** |
| CNPJ | `68431371000161` (`68.431.371/0001-61`) |
| Branch | Matriz |
| Opened | 2026-08-04 |
| Porte | ME |
| Natureza jurídica | 206-2 Sociedade Empresária Limitada |
| Situação | ATIVA since 2026-08-04 |
| CNAE principal | 47.81-4-00 Comércio varejista de artigos do vestuário e acessórios |
| Address | R VITOR KONDER, 223, SALA 1108, CENTRO, FLORIANÓPOLIS/SC, CEP 88015-400 |
| E-mail (cartão) | andre.vilmo@gmail.com |
| Telefone | (51) 8022-7183 |
| IE / IM | not on the cartão — fill before production NF-e if required in SC |

Secondary CNAEs (seed as `company_cnae`): 46.19-2-00, 47.51-2-01, 47.53-9-00, 47.57-1-00, 47.59-8-99, 47.61-0-03, 47.72-5-00, 47.89-0-04, 62.01-5-01, 62.04-0-00, 66.19-3-02.

Cartão issued 2026-08-07 08:58:35 (Brasília), IN RFB 2.119/2022.

## A1 (local only)

Validated: file is PKCS#12 (e-CNPJ A1). OpenSSL 3 needs the **legacy** provider for the original blob (RC2-40-CBC). A re-exported AES copy can live next to it in `.secrets` for Linux/`vilmo-nfe`.

| A1 field | Value (public cert, not the key) |
| --- | --- |
| Subject CN | `A VILMO PINHEIRO CARDOSO TECNOLOGIA LTDA:68431371000161` |
| Type | RFB e-CNPJ A1 (AC CONSULTI RFB) |
| SAN email | `admin@vilmomkt.com` |
| Valid | 2026-08-13 → 2027-08-13 (UTC) |

Host layout (gitignored):

```
.secrets/certs/68431371000161.pfx
.secrets/certs/68431371000161.pfx.pass
.env   → COMPANY_68431371000161_A1_HOST_PATH / PASSWORD_FILE
```

Container path when compose exists: `/certs/68431371000161.pfx`. Password is encrypted in `company_certificates`, never in appsettings or git.

## Seed behaviour (when coding starts)

Insert this company first. Set `tradeName` for UI, `legalName` + address + CNPJ for NF-e sender and labels. Attach A1 from secrets. Link `admin@vilmomkt.com` as platform super user **and** optional `CompanyAdmin` of this CNPJ.

Readiness: address and CNPJ are complete (`ready_to_list` still needs marketplaces). `ready_to_invoice` needs this A1 **and** IE if SC requires it.
