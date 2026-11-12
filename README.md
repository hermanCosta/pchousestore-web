# pcHouseStore Web

Reconstrução web do sistema desktop pcHouseStore, originalmente desenvolvido em Java Swing.

## Objetivo

Modernizar a aplicação como uma solução web com React + TypeScript e ASP.NET Core, preservando regras de negócio confirmadas e redesenhando a experiência de uso.

## Repositório de origem

- Java legado: https://github.com/hermanCosta/pchousestoremvn
- Branch de referência analisada: `main`

## Stack alvo

- Frontend: React + TypeScript + Vite
- Backend: ASP.NET Core / .NET 10
- Persistência: Entity Framework Core
- Banco: PostgreSQL
- Arquitetura: monólito modular
- Testes: xUnit, testes de integração, Vitest e React Testing Library

## Estado do projeto — Fase 2

O baseline v19 consolida uma empresa com várias lojas/filiais e adiciona autenticação OAuth 2.0 / OpenID Connect para a API ASP.NET Core. A sessão persistente é autenticada pela loja, sem identidade de funcionário; cada comando operacional protegido exige a credencial individual de quem o executa. O estoque é controlado por loja, com reservas, transferências, equipamentos recondicionados serializados e recebimentos de compras externas. Para serviços, inclui o estado `AWAITING_PARTS`, uma fila de compras por loja, várias solicitações por ordem de serviço em momentos diferentes, fotos de referência por peça, rastreamento de fornecedor, links de compra e transporte, previsão de entrega, recebimento parcial e reserva dos itens destinados à ordem. Pagamentos, depósitos, parcelamentos, estornos e fechamento diário por loja também estão especificados. A documentação registra as regras aprovadas e as pendências ainda abertas. A fundação técnica está sendo criada na Fase 2, sem fluxos de negócio.

Consulte [specs/README.md](specs/README.md) para navegar pela documentação.

A Fase 1 de descoberta e decisões arquiteturais foi concluída e o PR #1 foi mesclado. A Fase 2 iniciou na branch `phase2/technical-foundation`, com a solução .NET 10, os projetos em camadas, o app React/TypeScript/Vite e a configuração de CI. Nenhuma funcionalidade de negócio foi implementada. Consulte os ADRs 033 e 034 em [architecture-decisions.md](specs/02-architecture/architecture-decisions.md) e o [migration backlog](specs/06-migration/migration-backlog.md).

## Princípios

1. Não inventar regras de negócio.
2. Diferenciar comportamento comprovado de hipótese e decisão de redesign.
3. Não alterar o repositório Java de origem.
4. Não reutilizar segredos ou credenciais do legado.
5. Aprovar as especificações antes da implementação de cada funcionalidade.
6. Usar `decimal` para valores monetários no backend e `NUMERIC` no PostgreSQL.

## Stock and refurbished equipment

See [service parts procurement workflow](specs/02-architecture/service-parts-procurement.md), [service parts purchase queue screen](specs/02-architecture/service-procurement-queue.md), [stock and refurbishment specification](specs/02-architecture/stock-and-refurbishment.md), [financial model — Ireland](specs/02-architecture/financial-model.md), [inventory lifecycle](specs/02-architecture/inventory-lifecycle.mmd), and [stock transfer lifecycle](specs/02-architecture/stock-transfer-lifecycle.mmd), plus [service parts procurement lifecycle](specs/02-architecture/service-parts-procurement-lifecycle.mmd).


## Product-owner decisions incorporated through v15

- One legal Company with multiple Shops. ShopSession authenticates only the active Shop; employees share one Company-wide identity/password across all branches.
- Roles are Company-wide: `MANAGER` has full access; `TECHNICIAN` has restricted service/technical/procurement/inspection access. Every protected operation still requires a fresh employee-code + password challenge.
- Product quantities are whole units. Quantity-tracked on-hand may become negative. Stock is deducted only when a Sale/ServiceOrder balance reaches zero through settled payments; partial payments do not consume stock. ServiceOrder deductions occur while `FIXED`; `PICKED` only records handover.
- VAT is initially disabled at Company level (no VAT amount added by the app while off); legal registration/status, rates and price display remain subject to Irish tax review.
- Every sold refurbished unit receives a 12-month commercial guarantee from documented handover, additional to statutory Irish consumer rights.
- Supplier/service-parts purchases do not require a second manager approval; the acting employee still authenticates each committed operation and is audited.
- Private image uploads are supported for procurement, refurbished units and complaint cases (10 MiB/file, 10 files, 100 MiB per target; secure format validation and private storage).
- Cash closing, refurbished condition grades/complaints and image upload limits now have explicit baseline specifications.


See [Product-owner decisions and remaining questions](specs/02-architecture/product-owner-decisions.md) for the consolidated decision register.


See [cash closing policy](specs/02-architecture/cash-closing-policy.md), [refurbished condition and complaints](specs/02-architecture/refurbished-condition-and-complaints.md), and [image upload policy](specs/02-architecture/image-upload-policy.md).

## Latest policy specifications (v15)

- [Cash closing policy](specs/02-architecture/cash-closing-policy.md)
- [Employee access and roles](specs/02-architecture/employee-access-and-roles.md)
- [Refurbished grades and customer complaints](specs/02-architecture/refurbished-condition-and-complaints.md)
- [Image upload policy](specs/02-architecture/image-upload-policy.md)
- [Product-owner decisions](specs/02-architecture/product-owner-decisions.md)

## Latest baseline — v16

- v1 payment methods are CASH and CARD only; card is recorded after approval on an external terminal, with processor integration deferred.
- Refunds are partial/total and Manager-only, with original-tender default and cumulative cap.
- Service deposits have no automatic expiry/forfeiture; 90-day inactivity prompts Manager review/contact only.
- Images are stored in PostgreSQL (`BYTEA` full content and thumbnail), not in external storage.
- Refurbished units require a Company-unique internal asset code; manufacturer serial is captured when available.
- See [v15 changelog](CHANGELOG-v15.md), [v16 changelog](CHANGELOG-v16.md) and [product-owner decisions](specs/02-architecture/product-owner-decisions.md).


## PC House brand and website profile (v17)

- [Brand identity and approximate colour tokens](specs/03-design/brand-identity.md)
- [Owner-supplied logo assets and usage guidance](specs/03-design/brand-assets/README.md)
- Primary logo PNG: [transparent background](specs/03-design/brand-assets/originals/pc-house-logo-primary-transparent.png)
- Alternative logo PNG: [white background](specs/03-design/brand-assets/originals/pc-house-logo-light-background.png)
- Preserved alternate: [wide transparent original](specs/03-design/brand-assets/originals/pc-house-logo-alternate-original.png)
- [Website-derived business profile and Shop candidates](specs/01-discovery/business-profile-from-website.md)
- [CSS theme tokens](specs/03-design/pc-house-theme.css)
- See [v16 changelog](CHANGELOG-v16.md) and [v17 changelog](CHANGELOG-v17.md).

## Owner-supplied logo files (v17)

The owner supplied three PNG assets. The 248 × 273 transparent-background mark is the provisional primary logo; the white-background version is an alternative; the 320 × 120 wide-canvas image is retained unchanged for future review. The supplied artwork is not a textual wordmark, so “PC House” should be rendered as UI text beside it where needed. The files are preserved in `specs/03-design/brand-assets/originals/`.


## Reports and printing (v18)

- [Reports, receipts and document printing](specs/02-architecture/reports-and-document-printing.md) defines the customer document catalogue, print triggers, A4/80 mm PDFs, snapshot history and reprints.
- [Legacy report inventory](specs/01-discovery/legacy-report-inventory.md) maps the five connected legacy JasperReports workflows and the refurbished-unit label to their Java methods and UI callers.
- [Document generation lifecycle](specs/02-architecture/document-generation-lifecycle.mmd)
- [Document history ERD (SVG)](specs/02-architecture/er-diagram-documents.svg)
- The legacy templates have been inventoried; the web renderer and React print UI are specified but not implemented. PDF binaries are not stored in PostgreSQL in v1; snapshots and metadata are.

See [v18 changelog](CHANGELOG-v18.md).


## API authentication and token security (v19)

- The API is protected by OAuth 2.0 JWT Bearer access tokens; OpenID Connect authenticates the Shop principal.
- React uses Authorization Code + PKCE (`S256`); OpenIddict is the default self-hosted authorization-server direction.
- Tokens represent the active Shop only. They never carry an employee identity or role. Each protected business operation still requires a fresh employee password challenge and one-use authorization.
- [API authentication and token security](specs/02-architecture/api-authentication-and-token-security.md)
- [Updated OAuth/OIDC authentication flow](specs/02-architecture/authentication-flow.mmd)
- [ADR-032 — OAuth 2.0 / OpenID Connect](specs/02-architecture/architecture-decisions.md)
- [v19 changelog](CHANGELOG-v19.md)
