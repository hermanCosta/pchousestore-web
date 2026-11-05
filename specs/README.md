# Specifications

## Document map

| Area | Purpose |
|---|---|
| [Project overview](00-project/overview.md) | Goals, scope and constraints |
| [Legacy architecture](01-discovery/legacy-architecture.md) | Confirmed source structure |
| [Screen inventory](01-discovery/screen-inventory.md) | Swing views and navigation |
| [Domain inventory](01-discovery/domain-inventory.md) | Models, controllers and DAOs |
| [Business rules](01-discovery/business-rules.md) | Verified rules and open questions |
| [Risks and findings](01-discovery/risks-and-findings.md) | Security, data and design findings |
| [Target architecture](02-architecture/solution-architecture.md) | Proposed web architecture |
| [Backend guidelines](02-architecture/backend-guidelines.md) | .NET conventions |
| [Frontend guidelines](02-architecture/frontend-guidelines.md) | React conventions |
| [Design system](03-design/design-system.md) | UX and visual direction |
| [Feature template](04-features/feature-template.md) | Template for each feature SPEC |
| [Testing strategy](05-testing/testing-strategy.md) | Quality strategy |
| [Migration backlog](06-migration/migration-backlog.md) | Phased implementation plan |

## Status vocabulary

- `DISCOVERY`: source analysis in progress.
- `DRAFT`: specification drafted, not approved.
- `APPROVED`: requirements and acceptance criteria reviewed.
- `IN_PROGRESS`: implementation started.
- `VALIDATION`: tests and acceptance checks in progress.
- `DONE`: acceptance criteria satisfied.

## Rule

A feature SPEC must cite Java source paths and methods wherever behavior is claimed to be inherited from the legacy application. Proposed changes must be explicitly marked REDESIGN.
