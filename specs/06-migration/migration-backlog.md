# Migration Backlog

**Status:** PHASE 2 — TECHNICAL FOUNDATION

| ID | Workstream | Depends on | Status |
|---|---|---|---|
| DISC-01 | Move docs to repository root and confirm baseline | Repo created | PENDING |
| DISC-02 | Complete Java view/controller/DAO analysis | — | IN_PROGRESS |
| DISC-03 | Audit legacy Company-scoped queries and map target Shop scope | DISC-02 | IN_PROGRESS |
| DISC-04 | Compare JPA mappings with MySQL DDL | Database schema export | PENDING |
| DISC-05 | Map service-order status transition matrix | DISC-02 | IN_PROGRESS |
| DISC-06 | Define payment, refund and daily-close formulas | DISC-02 | CASH CLOSE BASELINE SPECIFIED; CASH/CARD-only tender, Manager-only refunds and deposit v1 terms specified; external processor integration deferred |
| DISC-07 | Inspect report templates and print workflows | DISC-02 | COMPLETED — 15 JRXML / 13 compiled Jasper files inventoried; five active generator methods and refurb label mapped; see legacy-report-inventory.md |
| DISC-08 | Resolve Shop login, employee password challenge and Company-wide role model | DISC-02 | DECISIONS RECORDED; Manager full / Technician restricted matrix specified |
| SPEC-01 | Approve Company/Shop model and authentication boundaries | DISC-03, DISC-04, DISC-08 | DECISIONS RECORDED; MODEL REVIEW PENDING |
| SPEC-02 | Approve design system and navigation | DISC-02 | PENDING |
| BASE-01 | Bootstrap .NET solution and React app | SPEC-01, ADR-033, ADR-034 | DONE — .NET solution/layers, MySQL EF Core context, React/TypeScript/Vite shell, lint/format configuration and CI scaffold created; backend build and frontend build/lint pass |
| AUTH-01 | Shop authentication/session and employee action authorization | SPEC-01 | SPECIFIED — Shop-only principal and one-time employee challenge; implementation pending |
| AUTH-02 | OAuth 2.0 / OIDC authorization server and JWT Bearer API validation | AUTH-01, DATA-02 | SPECIFIED — Authorization Code + PKCE, OpenIddict default, Shop-only claims; implementation and security test pending |
| AUTH-03 | Token/session revocation, refresh-token rotation and signing-key lifecycle | AUTH-02 | SPECIFIED — refresh optional; rotation/reuse detection mandatory if enabled; implementation pending |
| CAT-01 | Customers, employees, product/service/fault catalog | AUTH-01, DISC-04 | PENDING |
| SO-01 | Service-order lifecycle, AWAITING_PARTS state and workflow | CAT-01, DISC-05 | SPECIFIED — procurement workflow defined; state matrix validation pending |
| PROC-01 | Supplier directory, external purchase tracking, ETA/timeline, partial receipt and request-line allocation | SO-01, stock foundation | SPECIFIED — supplier AP/returns excluded |
| PROC-02 | Service Parts Purchase Queue, multiple request rounds per ServiceOrder, line-level reference images and buyer assignment | PROC-01, SO-01, AUTH-01 | SPECIFIED — image size/count specified; MySQL BLOB storage selected; GDPR retention schedule pending; no second purchase approval |
| SALES-01 | Sales and sale payments | CAT-01, DISC-06 | PENDING |
| REF-01 | Refurbishment inventory and sales | CAT-01 | PENDING |
| FIN-01 | Payment methods, service deposits, instalment schedules and partial/total refunds | SO-01, SALES-01, DISC-06 | SPECIFIED — Manager-only original-tender refunds, CASH/CARD-only and deposits policy; processor integration deferred |
| CASH-01 | Shop cash movements and daily closing | FIN-01, DISC-06 | SPECIFIED — opening float, cash formula, variance, immutable close/corrections; finance validation pending |
| REP-01 | Reports, receipts and printing replacement | DISC-07 | SPECIFIED — catalogue, triggers, A4/80 mm layouts, PDF preview and legacy mapping documented; implementation pending |
| REP-02 | Generated-document history, immutable snapshots, template versions and reprint audit | AUTH-01, DATA-02, REP-01 | SPECIFIED — MySQL metadata/snapshots; PDF BLOB persistence deferred |
| REP-03 | Operational report catalogue for stock, procurement, transfers and complaints | DATA-02, REP-01 | DRAFT — minimum report definitions included; final v1 prioritisation pending |
| QA-01 | End-to-end validation and homologation | All retained features | PENDING |
| REL-01 | Production deployment | QA-01 | PENDING |

## Sequencing notes

Do not begin domain implementation until the Company/Shop boundaries, employee action authorization, entity relationships and financial invariants are resolved. OAuth/OIDC Shop authentication, JWT Bearer API enforcement, ShopSession revocation, employee action authorization and shared catalogs are expected foundations. Service orders and sales can be implemented independently after catalog/customer foundations. Financial ledger, refunds and daily closing must be designed together to avoid reconciliation defects.

## Domain decisions and data-model work

| ID | Workstream | Depends on | Status |
|---|---|---|---|
| ADR-01 | One Company with multiple Shop branches | Product-owner decision | DECIDED |
| ADR-02 | Shop session; employee credentials per protected operation | Product-owner decision | DECIDED |
| ADR-03 | Product-only inventory with Shop-specific balances | Product-owner decision | DECIDED |
| ADR-04 | Payment transaction + tender allocations | Product-owner decision | DECIDED |
| ADR-05 | One closing per Shop/business date | Product-owner decision | DECIDED |
| ADR-06 | Integer product stock, optional reservations, sale/service posting, selected returns | Product-owner decision | DECIDED; full-settlement trigger decided |
| ADR-07 | Inter-Shop transfer workflow (dispatch/in-transit/receipt) | Product-owner decision | DECIDED |
| ADR-08 | Refurbished categories, technical attributes and serialized units | Product-owner decision | DECIDED |
| ADR-13 | Supplier procurement and parts-waiting service workflow | Product-owner decision | DECIDED — operational procurement tracking; AP/returns excluded |
| ADR-14 | Service parts purchase queue, separate request rounds and reference images | Product-owner decision | DECIDED — initial image limits and MySQL-only BLOB storage decided; GDPR retention schedule pending; no second purchase approval |
| DATA-01 | Review conceptual model and remaining domain gaps | ADR-01..08 | IN REVIEW — v14 incorporates approved role baseline, cash close, refurb grades/complaints and image limits |
| DATA-02 | Approve relational entities, Shop FKs, action authorization and DER | DATA-01 | PENDING |
| DATA-03 | Finalize stock constraints and one-pool-per-Shop baseline | DATA-01 | PAYMENT SETTLEMENT TRIGGER, NEGATIVE STOCK AND SINGLE-POOL/NO-EXPIRY BASELINE DECIDED; return disposition and serialized identity remain to refine |
| DATA-06 | Validate transfer discrepancies, return disposition and refurbished-unit condition/serial rules | ADR-07, ADR-08 | IN REVIEW — A/B/C, required internal asset code, optional manufacturer serial when unavailable, and complaint workflow specified; warranty terms need owner/legal review |
| DATA-04 | Approve closing formula, deposit terms, refund approvals and reopening policies | DISC-06 | IN REVIEW — operational formula, Manager-only refunds and deposit defaults specified; customer contract wording needs review |
| DATA-05 | Produce executable MySQL DDL and EF Core mappings | DATA-02..04 | NOT STARTED |
| DATA-07 | Configure Ireland EUR, cash rounding and effective-dated VAT/tax snapshots | FIN-01, Irish accountant review | BLOCKED ON COMPANY VAT STATUS / ACCOUNTANT REVIEW; application flag OFF |
| DATA-08 | Legal review of weekly refurbished-sale plan and any early-handover credit mode | FIN-01, Irish legal/compliance review | BLOCKED ON REVIEW |
| DATA-09 | Approve supplier purchase/receipt model, procurement-request allocation and ETA audit | ADR-13 | PENDING REVIEW — model drafted |
| DATA-10 | Approve ServiceProcurementRequest rounds, request-line allocations, image metadata/storage and queue screen | ADR-14 | PENDING REVIEW — model and UX draft added |

The current DER and relational model are design artifacts only. Do not generate production migrations until DATA-02 through DATA-06 are reviewed. Stock posting at full balance settlement, negative quantity-tracked balances, one stock pool per Shop and no reservation-expiry automation are decided. v1 payments are CASH/CARD only; card is recorded from an external terminal without processor integration. Refunds are Manager-only and return to original tender. Deposits have no automatic expiry/forfeiture and receive a 90-day no-response review flag. Images are stored in MySQL BLOBs; GDPR retention schedule and backup-capacity validation remain open. VAT flag is OFF until the owner confirms the Company's legal tax position. Accounting/bookkeeper reconciliation and installment renegotiation/late fees are deferred by owner instruction. Refurbished CREDIT_SALE handover-before-full-payment remains blocked pending Irish legal/regulatory review.


## Product-owner decisions — v11 update

- [x] Allow signed negative stock balances for quantity-tracked products; retain deficit visibility and integer-only quantities.
- [x] Deduct stock when Sale/ServiceOrder balance reaches zero through settled payments; partial payments do not post stock. ServiceOrder deductions do not wait for pickup/`PICKED`.
- [x] Company-wide employee password credentials work across all Shops.
- [x] Define Company-wide roles `MANAGER` and `TECHNICIAN`; implement action-level policy checks.
- [x] Add Company-level VAT enable/disable flag and effective-dated tax schema; tax legal setup/rates remain an implementation/go-live prerequisite.
- [x] Apply a 12-month commercial guarantee to refurbished units, starting on documented customer handover. Preserve Irish statutory rights.
- [x] Remove second-approval requirement from supplier/service-parts purchases; retain employee action authentication and audit.
- [x] Support multiple private image uploads for procurement request lines and refurbished physical units.


## Product-owner confirmations — v11

- [x] Employee uses an individual password; the same Company-wide employee identity and `MANAGER`/`TECHNICIAN` role assignments work in every Shop.
- [x] Allow negative quantity-tracked stock; deduct stock only when Sale/ServiceOrder balance reaches zero through settled payments.
- [x] Company-level VAT enable/disable flag; tax setup and legal status remain subject to Irish accountant review.
- [x] 12-month commercial guarantee for every sold refurbished unit from recorded handover; statutory consumer rights remain additional.
- [x] Supplier purchase flow does not require a second manager approval.
- [x] Multiple private image uploads are allowed for procurement request lines and refurbished units.


## Product-owner confirmations — v13

- [x] Deduct quantity-tracked Product stock when Sale/ServiceOrder balance reaches zero through settled payments; partial payments do not deduct; negative balances are permitted.
- [x] One Company-wide employee password and Company-wide MANAGER/TECHNICIAN roles work across every Shop; keep the persistent ShopSession employee-free and request employee password per protected operation.
- [x] VAT enable/disable feature flag is required; legal VAT registration/status/rates remain under Irish professional review.
- [x] Every sold refurbished unit has a 12-month commercial guarantee from customer handover, additional to statutory rights.
- [x] Supplier/service-parts purchases require no second manager approval.
- [x] Multiple image uploads are supported for procurement request lines and refurbished units.


## Product-owner decisions — v15

- [x] Enable only CASH and CARD tender methods in v1; record card approval from an external terminal and defer processor API integration.
- [x] Refunds may be partial/total, require Manager authorization at every amount and default to original tender; no second Manager approver.
- [x] Deposits do not expire automatically; 90 days without customer response causes review only, not forfeiture. Cancellation/forfeiture rules are defined with written terms and documented costs.
- [x] MySQL-only image storage: `MEDIUMBLOB` content + bounded `BLOB` thumbnails; no external storage provider/filesystem.
- [x] Manufacturer serial captured when present; every refurbished unit has a Company-unique internal asset code.
- [x] Installment late fees/collections/renegotiation are excluded from v1; overdue status only.
- [ ] Owner/legal review of proposed refurbished commercial guarantee exclusions and customer-facing terms.
- [ ] Confirm VAT registration/status with an Irish accountant; VAT feature flag stays OFF and unreviewed until then.


## Product-owner decisions — v13

- [x] Full-settlement inventory deduction; negative quantity-tracked balances permitted.
- [x] One Company-wide employee identity/password across all Shops; `MANAGER` and `TECHNICIAN` role names are global.
- [x] Company VAT feature flag included; tax setup remains pending Irish professional review.
- [x] 12-month commercial guarantee from handover for sold refurbished units.
- [x] No second approval for supplier/service-parts purchases.
- [x] Multiple private image uploads for procurement lines and refurbished units.
- [ ] Approve the detailed MANAGER/TECHNICIAN action-permission matrix.


## Product-owner decisions — v14 (2026-11-05)

- [x] MANAGER full access; TECHNICIAN restricted service/technical/procurement/inspection scope.
- [x] Daily cash close baseline: opening float, net cash receipts, cash-in/out, cash refunds, safe drops, counted variance and immutable correction events.
- [x] VAT disabled in the application by default; no VAT amount is added while disabled. Legal status remains a go-live gate.
- [x] Refurbished condition grades A/B/C plus separate functional-test status and inspection history.
- [x] Customer complaint case workflow with evidence, technical inspection, Manager resolution and refund/replacement/repair references.
- [x] Image upload baseline: 10 MiB/file, max 10 images and 100 MiB per target; JPEG/PNG/WebP, private storage and OWASP validation controls.
- [x] Initial stock simplification: one stock pool per Shop; reservation-expiry automation out of scope.
