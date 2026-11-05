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
- Banco: MySQL
- Arquitetura: monólito modular
- Testes: xUnit, testes de integração, Vitest e React Testing Library

## Estado do projeto

O trabalho começa pela engenharia reversa e especificação. Este pacote não contém implementação de frontend ou backend.

Consulte [specs/README.md](specs/README.md) para navegar pela documentação.

## Princípios

1. Não inventar regras de negócio.
2. Diferenciar comportamento comprovado de hipótese e decisão de redesign.
3. Não alterar o repositório Java de origem.
4. Não reutilizar segredos ou credenciais do legado.
5. Aprovar as especificações antes da implementação de cada funcionalidade.
6. Usar `decimal` para valores monetários no backend e `DECIMAL` no MySQL.
