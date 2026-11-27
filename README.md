# pcHouseStore Web

Reconstrução web do sistema desktop pcHouseStore, originalmente desenvolvido em Java Swing.

## Objetivo

Modernizar a aplicação como uma solução web com React + TypeScript e ASP.NET Core, preservando regras de negócio confirmadas e redesenhando a experiência de uso.

## Repositórios

- Aplicação web: este repositório, branch de trabalho `phase2/technical-foundation`.
- Java legado: https://github.com/hermanCosta/pchousestoremvn (referência de descoberta; não alterar).

## Stack alvo

- Frontend: React + TypeScript + Vite
- Backend: ASP.NET Core / .NET 10
- Persistência: Entity Framework Core com Npgsql
- Banco: PostgreSQL
- Arquitetura: monólito modular
- Testes: xUnit, testes de integração, Vitest e React Testing Library

## Estado atual

A solução alvo atende uma Company com múltiplas Shops. A sessão persistente representa somente a Shop ativa; não contém identidade nem perfil de funcionário. Perfis Company-wide: `EMPLOYEE`, `MANAGER` e `ADMIN`, sem atribuição de múltiplos perfis. Cada operação protegida definida pela especificação da funcionalidade exige validação direta e recente das credenciais do funcionário no backend. Não existe fluxo-alvo de `EmployeeActionAuthorization`.

A aplicação está sendo preparada para operação na Irlanda, com EUR e VAT configurável inicialmente OFF. Estoque é controlado por Shop, somente para Products, em quantidades inteiras. Finance, ServiceOrders, reservas, devoluções físicas, transferências, refurbished equipment e fechamento diário possuem regras documentadas.

Para peças de serviço, o escopo aprovado é a lista informativa **Parts to Buy**: demanda automática descoberta nas ServiceOrders e lembretes manuais independentes. Não há cadastro de fornecedores, pedidos de compra, alocações de compra, ETA, rastreamento ou recebimento de fornecedor dentro desse fluxo. Compras ocorrem fora do sistema; reposição física é tratada pelo módulo Stock.

A fundação técnica está em desenvolvimento. A existência de uma SPEC aprovada ou de uma fundação parcial não significa que o módulo esteja concluído. Consulte o [Migration Backlog](specs/06-migration/migration-backlog.md) para o estado de implementação e validação.

## Documentação de referência

- [Índice das especificações](specs/README.md)
- [Decisões do Product Owner](specs/02-architecture/product-owner-decisions.md)
- [Architecture Decision Records](specs/02-architecture/architecture-decisions.md)
- [Arquitetura da solução](specs/02-architecture/solution-architecture.md)
- [Modelo de domínio](specs/02-architecture/domain-model.md)
- [Modelo relacional PostgreSQL](specs/02-architecture/database-model.md)
- [Employee Management — fonte de verdade para perfis e credenciais](specs/04-features/employee-management.md)
- [Service Orders](specs/04-features/service-orders.md)
- [Financial Model](specs/02-architecture/financial-model.md)
- [Stock and Refurbishment](specs/02-architecture/stock-and-refurbishment.md)
- [Cash Closing Policy](specs/02-architecture/cash-closing-policy.md)
- [Regression Matrix](specs/05-testing/regression-matrix.md)

Documentos explicitamente marcados como **SUPERSEDED** são mantidos para histórico e descoberta; não são especificações normativas para novas implementações.

## Princípios

1. Não inventar regras de negócio.
2. Diferenciar comportamento comprovado do legado, decisão aprovada e proposta de redesign.
3. Não alterar o repositório Java de origem.
4. Não reutilizar segredos ou credenciais do legado.
5. Aprovar as especificações antes da implementação de cada funcionalidade.
6. Usar `decimal` para valores monetários no backend e `NUMERIC` no PostgreSQL.
7. Não iniciar migrations de produção ou funcionalidades de negócio sem autorização explícita.

## Design e identidade visual

- [Brand identity](specs/03-design/brand-identity.md)
- [Brand assets](specs/03-design/brand-assets/README.md)
- [Design system](specs/03-design/design-system.md)
- [Website-derived business profile](specs/01-discovery/business-profile-from-website.md)

## Relatórios e documentos

- [Reports, receipts and document printing](specs/02-architecture/reports-and-document-printing.md)
- [Legacy report inventory](specs/01-discovery/legacy-report-inventory.md)
- [Document generation lifecycle](specs/02-architecture/document-generation-lifecycle.mmd)

## Segurança e autenticação

- OAuth 2.0 / OpenID Connect autentica a Shop; React usa Authorization Code + PKCE.
- JWTs representam somente o contexto da Shop e nunca contêm identidade/perfil de funcionário.
- Cada operação protegida valida as credenciais do funcionário diretamente no backend; credenciais nunca são persistidas em logs, hashes de requisição ou snapshots de idempotência.
- [API authentication and token security](specs/02-architecture/api-authentication-and-token-security.md)
- [Employee Management](specs/04-features/employee-management.md)

## Histórico de decisões

Os documentos de descoberta e ADRs preservam decisões históricas e sua supersessão. Para regras atuais, use as fontes normativas listadas acima e o índice em [specs/README.md](specs/README.md).
