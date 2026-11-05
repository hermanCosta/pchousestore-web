# Migration Backlog

**Status:** DISCOVERY

| ID | Workstream | Depends on | Status |
|---|---|---|---|
| DISC-01 | Complete Java view/controller/DAO analysis | — | IN_PROGRESS |
| DISC-02 | Entity relationship and schema verification | — | IN_PROGRESS |
| DISC-03 | Document status/payment/cash rules | DISC-01 | PENDING |
| SPEC-01 | Approve target architecture and auth model | DISC-01, DISC-02 | PENDING |
| SPEC-02 | Approve design system and navigation | DISC-01 | PENDING |
| BASE-01 | Bootstrap .NET solution and React app | SPEC-01 | PENDING |
| AUTH-01 | Authentication, company context and authorization | SPEC-01 | PENDING |
| CAT-01 | Customer/person/employee/catalog capabilities | DISC-02 | PENDING |
| SO-01 | Service order lifecycle | DISC-03, CAT-01 | PENDING |
| SALES-01 | Sales and sale payments | DISC-03, CAT-01 | PENDING |
| REF-01 | Refurbishment and refurbished sales | DISC-03, CAT-01 | PENDING |
| FIN-01 | Deposits, refunds and payments | DISC-03, SO-01, SALES-01 | PENDING |
| CASH-01 | Cash in/out and daily closing | FIN-01 | PENDING |
| REP-01 | Reports, receipts and printing replacement | DISC-01 | PENDING |
| QA-01 | End-to-end validation and homologation | All retained features | PENDING |
| REL-01 | Production deployment | QA-01 | PENDING |

## Sequencing notes

Discovery must identify the actual business dependencies before committing to implementation order. Authentication/company context and shared catalogs are expected foundations. Service orders and sales should be specified before cash closing because they create financial events.
