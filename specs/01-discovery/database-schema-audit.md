# Database Schema Audit — pc_house_store

**Status:** DISCOVERY — analysis of the uploaded DDL; not an approved target schema  
**Input:** `schema.sql`  
**Database reported by dump:** `pc_house_store`  
**Dump server version:** MySQL 8.0.26  
**Dump date:** 2025-10-03

## 1. Scope and limitations

This audit compares the uploaded schema-only MySQL dump with the Java/JPA entities and workflows inspected in the legacy repository. The dump contains table definitions, indexes, constraints and AUTO_INCREMENT metadata; it contains no `INSERT` statements or row data.

The dump includes `DROP TABLE IF EXISTS` statements. Do not execute it against an existing database: it is evidence for analysis, not a safe migration script.

This document does not infer undocumented business rules. Recommendations for the new schema are marked as design proposals and require product-owner approval where business behavior is involved.

## 2. Executive summary

The DDL provides useful evidence, but it does not consistently match the Java annotations or application workflows. The most important findings are:

1. Several JPA `@OneToOne` relationships are not unique in MySQL and are used by workflows that support multiple related records. The target model should be based on cardinality verified by domain behavior, not copied from annotations.
2. `DEPOSIT.ID_SALE_PAYMENT` has a foreign key to `SALE_PAYMENT.ID_SALE`, not to `SALE_PAYMENT.ID_SALE_PAYMENT`. This is a confirmed schema defect.
3. `SERVICE_ORDER.STATUS` stores `CANCELED`, while the Java enum uses `CANCELLED`. This mismatch can break status persistence.
4. Sale and service-order refunds use different sign conventions. Service-order refund records may also violate a check constraint when change is non-zero.
5. The cash-closing table has two unique constraints, including one on employee, and is missing a declared employee foreign key. The closing DAO checks closure by date only, with no company scope.
6. Monetary columns use `DOUBLE`; the new model should use `DECIMAL` and C# `decimal`.
7. Several Java identifier types are `long` while corresponding MySQL identifiers are `INT`; other identifiers are `BIGINT`. The new schema should choose a consistent key strategy.
8. The schema contains multi-company references, but some legacy queries are not tenant-scoped or scope only part of an `OR` expression.

## 3. DDL inventory

The dump defines 21 tables:

- CASH_IN_REGISTRY
- CASH_OUT_REGISTRY
- COMPANY
- CUSTOMER
- DAILY_CLOSING
- DEPOSIT
- DEVICE
- EMPLOYEE
- FAULT
- PERSON
- PROD_SERV
- REFUND
- REFURB
- REFURB_SALE
- SALE
- SALE_PAYMENT
- SALE_PROD_SERV
- SERVICE_ORDER
- SERVICE_ORDER_FAULT
- SERVICE_ORDER_NOTE
- SERVICE_ORDER_PAYMENT
- SERVICE_ORDER_PROD_SERV


## 4. Confirmed schema/JPA mismatches

| Area | DDL evidence | Java mapping / code | Assessment |
|---|---|---|---|
| Product/service → Company | `PROD_SERV.ID_COMPANY` has a normal non-unique FK | `ProductService.company` is `@OneToOne` | Relationship is structurally many-to-one in the DDL. Verify DAO/workflow; likely change in target model. |
| Service order → Customer, Device, Employee, Company | Foreign-key indexes are non-unique | `ServiceOrder` maps all four as `@OneToOne` | DDL permits multiple orders for the same customer, device, employee and company. JPA cardinality is inconsistent with the physical schema and business use. |
| Sale payment → Sale and Employee | Foreign keys are non-unique | `SalePayment.sale` and `.employee` are `@OneToOne` | Multiple payments per sale/employee are supported by the workflow and allowed by the DDL. Target relationships should be many-to-one from payment to sale/employee. |
| Refund → Company and Employee | Both FK columns are indexed but not unique | `Refund.company` and `.employee` are `@OneToOne` | DDL does not enforce one refund per company/employee. Reassess as many-to-one. |
| Daily closing → Employee | `DAILY_CLOSING.ID_EMPLOYEE` is `BIGINT UNIQUE`, with no FK | `DailyClosing.employee` is `@OneToOne` | Uniqueness matches the annotation, but it allows only one closing per employee ever. The missing FK and integer-size mismatch also need review. |
| Person email | `PERSON.EMAIL` is nullable and has no unique index | `Person.email` declares `unique=true` | Mapping and DDL differ. Decide whether email uniqueness is a business requirement before recreating it. |
| Company email | `COMPANY.EMAIL` is nullable and has no unique index | `Company.email` declares `unique=true` | Mapping and DDL differ. |
| Company password | `COMPANY.PASSWORD` is nullable | `Company.password` declares `nullable=false` | Mapping and DDL differ. The new authentication design should not copy this password column as-is. |
| Fault code | `FAULT.CODE` exists and is unique when non-null | `Fault` model inspected has no `code` property | DDL contains a field not represented in the entity. Decide whether it is retained. |
| Device serial | `DEVICE.SERIAL_NUMBER` is required and unique | `Device` model has the property but no explicit unique annotation | DDL imposes a global uniqueness rule absent from the JPA annotation. Confirm desired uniqueness scope. |
| Customer company | `CUSTOMER.ID_COMPANY` is nullable | `Customer` links to Company without `nullable=false` | Customer rows can exist without company assignment. Confirm target tenant ownership rule. |
| Note → Sale | `SERVICE_ORDER_NOTE.ID_SALE` exists but has no FK; it is `BIGINT` while `SALE.ID_SALE` is `INT` | `OrderNote.sale` is a `ManyToOne` relationship | Missing FK and mismatched identifier size. Also verify that this note table is intended to support both sales and orders. |

## 5. Confirmed foreign-key defect in DEPOSIT

The Java entity declares `Deposit.salePayment` using `@JoinColumn(name="ID_SALE_PAYMENT", referencedColumnName="ID_SALE_PAYMENT")`.

The DDL instead declares:

- `DEPOSIT.ID_SALE_PAYMENT` as `INT`;
- an index on `DEPOSIT.ID_SALE_PAYMENT`;
- `FK_DEPOSIT_ID_SALE_PAYMENT` referencing `SALE_PAYMENT(ID_SALE)`.

The intended referenced key according to the Java mapping is `SALE_PAYMENT.ID_SALE_PAYMENT`, whose DDL type is `BIGINT`. The actual DDL points to the sale identifier, not the payment identifier. This can associate a deposit with the wrong logical record and does not implement the JPA mapping.

**Target recommendation:** model deposits against a single explicit financial target, or use separate typed relationships. Do not carry forward the malformed FK. Define one-to-one target ownership as a domain invariant: each deposit must belong to exactly one sale or one service order. The target schema should enforce this with an explicit design (separate tables or validated nullable-FK/check strategy).

## 6. Enum mismatches

### Service-order status

- Java `OrderStatus` includes `CANCELLED`.
- MySQL `SERVICE_ORDER.STATUS` permits `CANCELED` (one L).
- MySQL `SALE.STATUS` permits `CANCELLED` (two Ls).

This is a confirmed mismatch between the service-order enum and its column definition. Standardize the target status vocabulary and provide an explicit mapping if legacy values ever need to be interpreted.

### Payment method

- Java `PayMethod`: CARD, CASH, COMBINE.
- `SALE_PAYMENT.PAY_METHOD`: CARD, CASH.
- `SERVICE_ORDER_PAYMENT.PAY_METHOD`: CARD, CASH, COMBINE.

The inspected `PaymentModal` splits a combined payment into separate CARD and CASH records, rather than persisting a COMBINE record in the combined flow. The extra service-order enum value should be treated as legacy schema capability, not automatically as a required user-facing payment mode.

### Payment type

`SALE_PAYMENT` allows DEPOSIT, SALE, ORDER, REFURB, REFUND and INSTALLMENT. `SERVICE_ORDER_PAYMENT` allows DEPOSIT, SALE, ORDER, REFUND and INSTALLMENT. The target should use explicit transaction purpose types appropriate to each aggregate rather than assume the two lists are interchangeable.

## 7. Financial and refund findings

### Monetary storage

The DDL uses `DOUBLE` for sale totals, service-order totals/due, line totals, payment amounts, deposits, refunds, refurb prices, cash-in/out and daily closing totals. This is unsuitable for exact decimal currency arithmetic.

**Target recommendation:** use `DECIMAL(p,s)` columns and C# `decimal`, choosing precision and scale explicitly (for example, 19,4 only if the product's currency/quantity requirements support that choice). Do not decide precision globally without reviewing unit pricing, tax and rounding requirements.

### Payment rows for combined payments

The inspected `PaymentModal` creates two rows for a combined card/cash payment. In both rows, `AMOUNT_DUE` is set to the full amount due, while `AMOUNT_PAID` and the respective tender components contain the card or cash component.

The daily report code sums `AMOUNT_DUE` over payment rows for gross sales/service totals. If both rows are persisted for one combined payment, this sum can count the same due amount twice. Target design should distinguish transaction amount due from individual tender allocation, or group tender components beneath one payment transaction.

### Refund sign inconsistency

- Sale refund creation copies original sale payment amounts as positive values and marks the new rows as `REFUND`.
- Service-order refund creation negates amount due, amount paid, card, cash and change values.
- `SERVICE_ORDER_PAYMENT` check `service_order_payment_chk_1` allows negative values for REFUND, but `service_order_payment_chk_3` still requires `CHANGE_AMOUNT >= 0` unconditionally.
- The refund code negates change, so a refund associated with a payment that had positive change can fail the unconditional check.
- The cash-closing/report code also flips values for rows marked REFUND. For negative service-order refund rows, this can invert the sign a second time. Sale refund rows use positive values, so the two paths are inconsistent.

**Target recommendation:** represent refunds as explicit reversal transactions with a positive refund amount and a direction/type that determines ledger impact. Avoid encoding business meaning through mixed positive/negative conventions in payment columns. Define and test the net cash, net card and net receivable equations before implementation.

### Deposit and refund target integrity

Neither `DEPOSIT` nor `REFUND` has a database check enforcing that exactly one of `ID_SALE` or `ID_SERVICE_ORDER` is populated. Enforce this rule in the target domain and persistence design.

## 8. Daily closing findings

`DAILY_CLOSING` defines:

- unique `CLOSING_DATE`;
- unique `ID_EMPLOYEE`;
- `ID_EMPLOYEE BIGINT NOT NULL`;
- no foreign-key constraint to `EMPLOYEE`;
- `EMPLOYEE.ID_EMPLOYEE` is `INT` in the same dump.

The legacy DAO checks whether a date is already closed by date only, without company scope. The closing view loads sale payments, service-order payments and cash-in/out by the selected date. The closing flow asks for employee reauthorization.

**Legacy constraints requiring target-model interpretation:**

1. The legacy DDL permits only one closing per date globally. **Target decision:** the new model allows one close per Shop/business date, unique on `(ShopId, BusinessDate)` (ADR-005).
2. The legacy unique employee constraint would prevent an employee from closing more than once ever. The target model does not copy that constraint; each close is attributed through the employee authorization for the Shop being closed.
3. Whether a closing is an immutable snapshot or can be reopened/adjusted with an audit trail remains open.
4. Whether cash-in/out, refunds and change are included in the expected drawer balance remains open; the inspected legacy total calculation focuses on sale/service payments.

Do not copy the legacy unique constraints into the target schema.

## 9. Multi-company and query-scope implications

The DDL associates Company with sales, service orders, products/services, refurbished inventory, cash entries and refunds. Employees do not have a company FK. Customers have a nullable company FK. Faults and devices are global in the current schema.

The legacy code has queries that do not consistently filter by company. One inspected product search applies the company condition only to the final `OR` branch because the predicate lacks parentheses; another order search has no company filter. Customer and employee list/search operations also require scope review.

**Legacy-audit note:** the query findings above describe the old Company predicate behavior only. For the target, there is one Company and multiple Shops: resolve Shop context from ShopSession, scope operational records by Shop, and keep shared master data at Company level by default. Employee attribution is established by a fresh per-operation credential authorization, not from the ShopSession.

## 10. Identifier type drift

Examples of Java `long` IDs whose SQL primary keys are `INT` include Company, Person, Customer, Employee, ProductService, Sale and ServiceOrder. `ServiceOrderPayment` is also Java `long` but SQL `INT`. Other child/ledger tables use `BIGINT`.

The dump also has `DAILY_CLOSING.ID_EMPLOYEE BIGINT` while `EMPLOYEE.ID_EMPLOYEE` is `INT`, and `DEPOSIT.ID_SALE_PAYMENT INT` while `SALE_PAYMENT.ID_SALE_PAYMENT` is `BIGINT`.

**Target recommendation:** use one deliberate key-size convention for new entities (typically 64-bit/BIGINT paired with C# `long`, if that matches expected scale), and ensure every FK has exactly the same signed type as the referenced PK.

## 11. Additional constraint and model observations

- `CUSTOMER.ID_PERSON` is not unique. The schema allows a person to be represented by multiple customer records, potentially across companies.
- `EMPLOYEE.USERNAME` is globally unique, while Employee has no Company FK. This affects the future account/company-context design.
- `SALE` has a check for `TOTAL >= 0`, but no equivalent check for `REMAINING >= 0`, despite the Java setter rejecting negative values.
- `CASH_IN_REGISTRY.AMOUNT` is nullable and has no non-negative check. `CASH_OUT_REGISTRY.AMOUNT` is non-null but also has no non-negative check.
- `SALE_PROD_SERV` and `SERVICE_ORDER_PROD_SERV` correctly enforce positive quantity and non-negative line total.
- `REFURB.SERIAL_NUMBER` is unique when present; the column is nullable.
- `DEPOSIT.AMOUNT` and `REFUND.AMOUNT` have non-negative checks.
- `SERVICE_ORDER_NOTE` supports nullable order and sale references without an XOR constraint; the sale reference has no FK.

## 12. Proposed target-domain aggregates (DRAFT)

These are modeling candidates, not final design decisions:

- **Company / Shop** — one legal company with multiple operational branches; Shop is the authenticated operating context.
- **ShopAccessCredential / ShopSession** — branch login credential and persistent Shop-scoped session; no employee identity is stored in the session.
- **Employee / EmployeeRoleAssignment / EmployeeActionAuthorization (target model)** — one Company-wide employee password with MANAGER/TECHNICIAN roles across all Shops and one-time employee credential authorization for each protected operation. `ShopEmployee` is not part of the target model.
- **Person/Customer** — shared company-level contact and customer relationship; avoid relying on email uniqueness unless approved.
- **CatalogItem** — shared company-level product or service definition, with explicit item kind; stock balances belong to ShopInventory.
- **Device** — device details tied to a customer/service-order history; serial-number uniqueness scope to be decided.
- **ServiceOrder** — customer, device, responsible employee, lifecycle/status, faults, line items, notes and financial transactions.
- **Sale** — Shop, customer, seller (from action authorization), lines (catalog items/refurbished units), state and financial transactions.
- **PaymentTransaction / TenderAllocation** — one financial transaction with one or more tender allocations (cash/card), amount due/received/change and reversal linkage.
- **Deposit** — an advance associated with exactly one commercial aggregate and optionally reconciled to a payment transaction.
- **Refund** — an explicit reversal linked to an original sale/order and original payment(s).
- **CashMovement** — Shop cash-in/out with employee action authorization, timestamp, amount and reason.
- **DailyClosing** — the target model is one Shop/date snapshot; the legacy schema facts above remain unchanged.

## 13. Required decisions before target schema approval

1. **Resolved:** one Company with multiple Shop branches; no multi-company membership in the initial product.
2. **Resolved:** persistent ShopSession represents the Shop, not an employee; employee credentials are required per protected operation.
3. Employee credential form, action challenge scope and role/permission matrix. Employee access is Company-wide; no Shop assignment cardinality is required.
4. Person/customer sharing across Shops; this model currently assumes company-wide shared records.
5. Device serial uniqueness and ownership/transfer behavior.
6. **Resolved:** stock only for `PRODUCT`, with separate quantity per Shop; posting/return timing and stock transfer rules remain open.
7. Sale and service-order status lifecycle, including cancellation and refunds.
8. **Resolved:** PaymentTransaction with one or more allocations; partial payments, installments, deposits and change policies remain open.
9. **Resolved for target:** one DailyClosing per Shop/date. Reopen/adjustment and cash calculation remain to be defined.
10. Monetary precision, currency and rounding policy.

## 14. Source references

- Legacy entity mappings: `models/Company.java`, `Person.java`, `Customer.java`, `Employee.java`, `ProductService.java`, `Device.java`, `Sale.java`, `ServiceOrder.java`, `SalePayment.java`, `ServiceOrderPayment.java`, `Deposit.java`, `Refund.java`, `DailyClosing.java`, `OrderNote.java`.
- Legacy workflows: `views/NewSaleView.java`, `views/NewServiceOrderView.java`, `views/modals/PaymentModal.java`, `views/ClosingTillView.java`, `views/PickedOrderView.java`.
- Legacy persistence: `dao/ProductServiceDAO.java`, `CustomerDAO.java`, `EmployeeDAO.java`, `RefundDAO.java`, `DailyClosingDAO.java`, `SalePaymentDAO.java`, `ServiceOrderPaymentDAO.java`.
- Reporting: `util/ReportGenerator.java`.

## Target design disposition (2026-11-04)

The audit above describes the legacy DDL and must remain a record of observed source facts. The target cash-closing policy is now resolved as one `DailyClosing` per `(ShopId, BusinessDate)`. The corrected target decisions are documented separately: one Company with multiple Shops; ShopSession as the persistent operational context; employee credential authorization per protected operation; product-only stock with a separate balance per Shop; PaymentTransaction with PaymentAllocation children; and one DailyClosing per Shop/business date, with each branch reconciled independently. The target model deliberately corrects the legacy payment duplication risk, branch scoping, stock representation employee attribution and Shop/date cash closing. See [Architecture Decision Records](../02-architecture/architecture-decisions.md), [Relational Database Model](../02-architecture/database-model.md) and [DER](../02-architecture/er-diagram.svg).
