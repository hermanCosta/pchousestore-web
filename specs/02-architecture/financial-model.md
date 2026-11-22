# Financial Model — Ireland deployment

**Status:** Approved operational v1 financial rules, reconciled with employee authorization and refund decisions as of 2026-11-21. Customer-facing deposit wording, legal terms, tax and accounting mappings remain subject to external review. This is an operational data model, not legal, tax or accounting advice.

## 1. Scope and decisions

The model supports one Company with multiple Shops, a persistent Shop session and employee authentication per protected operation. Every payment, deposit, refund, cash movement and close is scoped to the active Shop and attributed to the employee whose fresh code/password challenge is directly validated by the backend for that operation.

| Requirement | Target rule | Status |
|---|---|---|
| Tender methods | `CASH` and `CARD` only in v1; enabled per Shop | DECIDED |
| Service deposits | Receive at service booking; track purpose and disposition | DECIDED |
| Weekly refurbished instalments | Plan plus dated schedule and receipts | DECIDED; fulfillment/legal mode pending |
| Refunds | Partial and total; `MANAGER` and `ADMIN`; original tender only | DECIDED v1 baseline |
| Currency | EUR | DECIDED baseline |
| Cash rounding | Optional per Shop, Ireland €0.05 cash-only rounding | DECIDED baseline |
| Approval policy | Every refund requires a fresh `MANAGER` or `ADMIN` code/password challenge; deposit forfeiture requires `MANAGER`; no second approver | DECIDED v1 baseline |
| VAT feature | Company-level `vat_enabled` flag; initial behavior is OFF (no VAT amount is added by the application) | NO-CHARGE SOFTWARE SETTING DECIDED; legal status/reason still OPEN |
| Reconciliation formula | Ledger-derived per Shop/day; external accounting reconciliation deferred | Operational close defined; accountant mapping deferred |

### Approved financial reconciliation controls (technical review 3 — decision 5)

The Product Owner approved on 2026-11-22 a read-only reconciliation capability, executable on demand and on a scheduled daily basis.

**Required reconciliation checks**

1. **ServiceOrder balance:** compare materialized `balance_due` with the canonical formula: order total less settled `PAYMENT` credits and committed `DepositApplication` credits. Exclude unapplied deposit receipts, refunds, deposit refunds and internal transfer events.
2. **Deposit liability:** calculate `available_held` independently for every deposit from settled receipts, committed applications, settled deposit refunds and valid authorized forfeitures. Verify event-to-source references, currency and materialized held-balance projection; `RELEASED` has no effect.
3. **Payment integrity:** compare each PaymentTransaction amount with its PaymentAllocations; verify allocation sums, tender/change/rounding values, original-transaction links, settlement status and cumulative refund caps. Check that only SETTLED transactions contribute to balances and drawer totals.
4. **Cash and closing:** compare settled cash tender, change, cash-rounding adjustment, cash refunds/deposit refunds and cash movements against the DailyClosing expected-cash calculation. Do not duplicate PaymentTransactions as CashMovements.
5. **Shop transfer:** compare each accepted transfer's immutable financial event header and component lines with the canonical order balance and each per-deposit liability snapshot at acceptance. Verify one event per accepted transfer, exact source references, currency, line/header totals and no duplicate or omitted liability.
6. **Cross-ledger consistency:** verify each financial effect is represented once across transaction, allocation, deposit event/application, ServiceOrder balance projection, cash closing and transfer event. A source reference validates a ledger event; it must not cause the same amount to be summed twice.

**Execution and evidence**

- Run on demand by an authorized Manager/Admin and automatically once per business day after the Shop's DailyClosing window. The scheduled run is read-only and must not delay or mutate a closed DailyClosing.
- A run records its scope (Company/Shop/business date and applicable aggregate IDs), start/completion timestamps, trigger (manual/scheduled), initiating actor or system identity, check set/version, outcome, counts by severity and a durable reference to detailed findings.
- Preserve run history and findings append-only. A rerun creates a new run; it never overwrites an earlier result. Do not persist secrets or raw credential material in findings.
- Outcomes distinguish `PASSED`, `FAILED` and `INCOMPLETE` (for example, a technical failure preventing a complete scan). Findings identify affected records, expected and observed amounts, currency, check code and source references without modifying the source ledger.
- A mismatch blocks only the affected financial aggregate/operation and any transfer that depends on it. Unrelated Shops and aggregates continue operating. A technical `INCOMPLETE` run does not certify the scope as reconciled; sensitive operations in an unverified affected scope must fail closed until a complete check or explicit safe validation is available.
- Reconciliation never auto-corrects balances, inserts compensating events, changes transaction status, rewrites transfer events or deletes history. Resolution must use a separately authorized business operation or controlled data-correction procedure with its own audit trail. Re-run reconciliation afterward.
- This is internal operational reconciliation. External accounting, bank/acquirer settlement and Irish tax/accounting mapping remain separate integrations/reviews and are not claimed by a PASSED result.


## 2. Ireland localization and monetary precision

- Company defaults: `country_code = 'IE'`, `default_currency_code = 'EUR'`, `time_zone = 'Europe/Dublin'`.
- Use C# `decimal` and PostgreSQL `NUMERIC(19,2)` for transaction amounts in EUR. Never use binary floating point for amounts.

### Approved monetary precision and rounding policy (technical review 3 — decision 4)

The Product Owner approved on 2026-11-22:

- All persisted monetary amounts denominated in EUR use exactly two fractional decimal places and PostgreSQL `NUMERIC(19,2)`; application code uses C# `decimal`. Never use `float`, `double` or binary floating point for monetary values.
- Intermediate calculations (for example percentage/tax calculations, prorations and aggregate calculations) retain higher decimal precision. Do not round at each arithmetic step or at each intermediate subtotal. Convert to the two-decimal persisted amount only at the defined monetary posting boundary for the relevant operation, using the rounding rule explicitly specified for that operation.
- The unrounded calculation inputs/results needed to explain a posted amount must remain reproducible from immutable price, quantity, discount, tax-code/rate and transaction snapshots. Do not silently recompute historical documents using current configuration.
- The standard persisted transaction amount and customer balance use two decimals. The optional Irish cash-rounding adjustment is a separate signed amount, also persisted to two decimals; it does not mutate the order total, line prices, taxable base, tax calculation or canonical ServiceOrder balance formula.
- Cash rounding remains optional per Shop and applies only to an eligible bill paid entirely in CASH, rounding the final bill to the nearest €0.05 using the Irish cent-ending pattern already specified below. Mixed CASH/CARD tender is not rounded in v1. CARD and other electronic tenders are never rounded. Exact-change handling remains available.
- Keep distinct values reconcilable: unrounded document total, posted amount applied to the customer balance, cash tendered, change returned and signed cash-rounding adjustment. The cash drawer amount must be derived consistently from cash tender/change and the adjustment; never fold the adjustment into a line discount or tax base.
- The specific tax rounding boundary/method and accounting presentation must be confirmed with the Irish accountant before VAT-enabled production documents are finalized. This decision does not choose a tax rounding method or legal/accounting treatment.

- Store `currency_code` on sales, service orders, deposits, payment transactions, refunds and instalment plans so historical documents remain self-describing. Multi-currency settlement is out of the initial scope.
- Initial product-owner instruction: no VAT is to be added/charged by the application while the Company-level `CompanyTaxConfiguration.vat_enabled` flag is `false`. The configuration defaults to `false` and `setup_status = NOT_REVIEWED`. This describes current software behavior only; it does not establish that the business is legally outside VAT or exempt. Before production tax documents are finalized, an Irish accountant must confirm the Company's position and either document the lawful disabled state or configure and enable the applicable VAT setup.
- Before production tax-document finalization is enabled, the Company tax setup must be reviewed and marked READY by an authorized administrator based on Irish accountant advice. If VAT is enabled, tax codes, effective-dated rates, registration details and price display mode must be populated. If VAT is disabled, the reason/legal basis must be recorded after review.
- Tax percentages/rates use a separate higher-precision decimal. The applied tax code, rate, taxable amount and tax amount must be snapshotted on sale/order lines or tax detail records. Do not hard-code VAT percentages; tax codes/rates must be effective-dated configuration validated by an Irish tax/accounting professional.
- Changes to the flag or tax rates apply prospectively from their effective date. Historical Sale/ServiceOrder tax snapshots and issued documents must never be recomputed by current settings.
- Store event timestamps in UTC; derive Shop business dates using `Europe/Dublin` unless a Shop-specific time zone is configured.

### Optional Irish cash rounding

The Central Bank of Ireland states that rounding in Ireland is voluntary, applies only to cash payments, rounds the total bill (not individual item prices) to the nearest €0.05, leaves 1c/2c coins as legal tender and does not alter VAT calculated on the unrounded amount. Card/electronic transactions are not rounded. Source: [Central Bank of Ireland — Rounding](https://www.centralbank.ie/consumer-hub/notes-and-coins/rounding).

Target application behavior:

1. Each Shop can enable/disable rounding through `ShopFinancialPolicy`.
2. Apply it only to the eligible final bill amount paid entirely in cash; for mixed tender, do not round in the initial implementation until the exact mixed-tender policy is approved.
3. Round the final total to the nearest €0.05 using the Irish pattern: final cent endings 1/2/6/7 round down; 3/4/8/9 round up. An exact 0/5 ending is unchanged.
4. Preserve the unrounded document total, line prices and tax calculation. Persist the signed `cash_rounding_adjustment` separately on the cash payment transaction.
5. Allow exact-change handling when requested; do not round card, bank transfer or other electronic allocations.
6. Keep cash received/change and amount applied to the customer balance separately reconcilable. A rounding adjustment is not a line discount and is not a change to VAT base.

### Approved cash-rounding refund cap (technical review 4 — decision 1)

The Product Owner approved on 2026-11-22 that refunds are capped by the amount actually received/settled through each original tender allocation, not solely by the amount credited to the order.

```text
physical_cash_received = amount_applied + cash_rounding_adjustment
                       = amount_tendered - change_amount

cash_refundable_remaining =
    physical_cash_received
    - sum(PENDING and SETTLED cash refund allocations linked to this original allocation)
```

- The signed adjustment is the rounded cash amount minus the unrounded amount applied to the customer balance. Example: €10.02 applied, -€0.02 cash-rounding adjustment, €10.00 physically received.
- In that example, the maximum cumulative cash refund is €10.00, not €10.02. The €0.02 rounding concession is not money received and cannot be refunded.
- For CARD, the refundable cap is the amount actually approved/settled on the original card allocation, less PENDING and SETTLED refund allocations linked to that allocation. CARD has no cash-rounding adjustment.
- For a transaction split across CASH and CARD, enforce each tender's remaining refundable amount independently; no refund may exceed the original allocation's actual received/settled amount. Mixed-tender cash rounding remains disabled in v1.
- Apply the same original-tender cap to `REFUND` and `DEPOSIT_REFUND`. A refund must reference the original transaction and allocation; alternative tender is not allowed in v1.
- Calculate the cap under the ServiceOrder-first lock order (when ServiceOrder-linked), then lock original transaction/allocation rows in stable ID order. Concurrent refunds must not over-refund any tender allocation.
- The original `amount_applied`, order total, tax base and deposit/order ledger credits are not rewritten by a refund. Refunds remain separate outgoing-funds transactions.

This rule governs the maximum refundable cash/card received. Consumer-law or contractual entitlement may require a different business resolution; the application must not pay more through the original tender than was actually received.

`refundable_remaining(original, allocation) = original_allocation_received_amount - sum(PENDING and SETTLED refund allocations referencing that original allocation)`; the original transaction's overall cap is the sum of its per-allocation remaining caps.
## 3. Configurable tender methods

`PaymentMethod` is a catalogue seeded with exactly two active methods in v1: `CASH` and `CARD`. `ShopPaymentMethod` enables/disables these methods per branch. Other tenders (bank transfer, store credit, cheque, online payment) are out of initial scope.

**Payment processor clarification:** a processor/acquirer is the provider behind a card terminal that authorizes the card payment and later settles funds to the merchant. It is not the tender type. In v1 the employee uses an external card terminal and records `CARD` only after the terminal shows approval. No processor API integration, automated settlement matching or fee reconciliation is implemented. The external terminal's transaction/reference code may be recorded when available.

Every `PaymentAllocation` records method, amount applied, amount tendered, change and optional external terminal reference. Never persist card PAN, CVV, PIN, magnetic-stripe data or reusable payment credentials. A mixed CASH/CARD payment is one `PaymentTransaction` with multiple allocations.

Only a transaction in `SETTLED` state contributes to the paid balance or till. For CARD, the employee may mark the allocation `SETTLED` only after confirming approval on the external terminal. `PENDING`, `FAILED`, `VOIDED` and `REVERSED` do not count as collected money.

## 4. Service deposits at booking

A `ServiceDeposit` is created against a `ServiceOrder`. Its incoming receipt is captured at booking through a `PaymentTransaction` of kind `DEPOSIT_RECEIPT`, with its tender allocations. `DEPOSIT_RECEIPT` and `DEPOSIT_REFUND` transactions target the `ServiceDeposit`; ordinary `PAYMENT` and `REFUND` transactions target exactly one `Sale` or `ServiceOrder`. A `DEPOSIT_REFUND` references the original settled `DEPOSIT_RECEIPT`; a `REFUND` references the original settled `PAYMENT`. Supported purpose values:

- `PARTS_PREORDER`: customer provides funds toward an expected part such as a battery or keyboard.
- `BOOKING_ADVANCE`: advance intended to be credited to the service price later.
- `SECURITY`: refundable amount held as a guarantee; not automatically revenue or invoice settlement.
- `OTHER`: purpose must be described in notes/terms.

The deposit record contains the agreed target amount, currency, purpose, terms snapshot and lifecycle status. One or more receipt transactions can contribute to the deposit.

### Deposit lifecycle

The deposit status is a derived projection, not a manually advanced state machine. Its values are `OPEN`, `PARTIALLY_FUNDED`, `HELD`, `PARTIALLY_DISPOSED` and `CLOSED`. Applications, refunds and forfeitures are immutable ledger facts and may coexist; they are not mutually exclusive status values. The canonical per-deposit liability formula remains authoritative. `valid_until` is null by default; no automatic expiry applies.

- A deposit received at booking is a cash/bank/card movement and appears in the correct Shop cash/financial report.
- It is not automatically applied to `ServiceOrder.balance_due`.
- `DepositApplication` is an explicit, non-cash operation that credits the service order, optionally against a specific `ServiceOrderItem`. It requires fresh direct employee credential validation and feature-specific permission, and cannot exceed the deposit's available held balance.
- `DEPOSIT_REFUND` is a real outgoing payment linked to the original settled deposit receipt and must not exceed the deposit's available refundable held balance. Refund tender allocations return funds through the original tender only. It is distinct from `REFUND`, which applies only to an original settled `PAYMENT`; deposit refunds follow deposit lifecycle and available-held-balance rules.
- No expiry/forfeiture runs automatically. If no customer response has been recorded for 90 days, set an operational `REVIEW_REQUIRED` flag and prompt the Shop to contact the customer; this is not a legal expiry date.
- Before receipt, snapshot the service/part purpose, expected fulfilment date, refund/cancellation terms and customer acknowledgement.
- If the Company cannot supply the agreed service/item, or misses the agreed date and no replacement date is agreed, the unconsumed balance is refundable, subject to applicable consumer rights.
- If the customer changes their mind, apply the terms disclosed and accepted before payment. Any retention/non-refundable portion must be clearly disclosed, fair and legally valid. For a special-order part, only a documented and genuinely non-recoverable supplier cost may be considered where the customer accepted that term in advance. Never automatically forfeit the whole deposit.
- A `SECURITY` deposit is returned when its stated purpose ends, less only documented deductions accepted in the terms and permitted by law.
- `FORFEITED` requires a fresh `MANAGER` code/password challenge, reason, terms snapshot and documented evidence/cost. This is an explicit feature-specific restriction; `ADMIN` cannot forfeit deposits. No second approver is required.
- Deposit amount reconciliation: `available_held = settled_receipts - applied - settled_refunds - approved_forfeitures`.



### Derived deposit status and operational review flag (lifecycle review approved 2026-11-22)

Derive the status from committed, valid financial sources for that specific deposit, in this precedence order:

1. `OPEN`: no settled `DEPOSIT_RECEIPT` exists.
2. `CLOSED`: at least one settled receipt exists and canonical `available_held = 0`; all received value has been applied, refunded or validly forfeited.
3. `PARTIALLY_DISPOSED`: `available_held > 0` and at least one positive committed application, settled deposit refund or valid forfeiture exists.
4. `PARTIALLY_FUNDED`: no disposition exists and settled receipts are greater than zero but below `target_amount`.
5. `HELD`: no disposition exists and settled receipts meet or exceed `target_amount`.

`available_held` is always calculated as settled receipts minus committed applications, settled deposit refunds and valid authorized forfeitures. The status is only a projection/cache and cannot authorize or execute a financial command. It may be recomputed when a valid source record is committed; a later receipt can therefore change the derived classification without rewriting any earlier event. Do not create exclusive `APPLIED`, `REFUNDED` or `FORFEITED` statuses.

Pending deposit-refund reservations reduce command-time spendable capacity but do not alter the posted liability formula or derived status. `RELEASED` is an administrative event with zero financial effect and does not change the status.

The operational `REVIEW_REQUIRED` flag is independent of the derived status. It can coexist with any status, including `CLOSED`, and has no effect on liability, application/refund capacity or Shop transfer. Clearing it is an operational acknowledgement only. The existing 90-day no-customer-response rule may raise the flag; it must never trigger automatic expiry, refund or forfeiture.

### Canonical ServiceDeposit liability reconciliation (technical review 3 — decision 3 approved)

The Product Owner approved on 2026-11-22 the following formula, calculated independently for each deposit:

```text
available_held = settled_receipts
               - applied
               - settled_refunds
               - approved_forfeitures
```

- `settled_receipts`: deposit receipt amounts whose linked `DEPOSIT_RECEIPT` PaymentTransactions are `SETTLED`.
- `applied`: committed DepositApplication amounts linked to this exact deposit.
- `settled_refunds`: `DEPOSIT_REFUND` amounts linked to this exact deposit and backed by settled refund transactions.
- `approved_forfeitures`: amounts in valid, authorized `FORFEITED` events with mandatory reason and required Manager authorization.
- `RELEASED` has zero financial effect. It is an administrative unhold only and must not reduce liability.
- Pending, failed, voided or otherwise unsettled receipt/refund transactions do not affect the formula. An event cannot count an amount unless its required source transaction/application is valid and committed.
- The deposit event ledger is the reconciliation trail. A referenced PaymentTransaction or DepositApplication is evidence for the event, not a second amount to add. Never sum the same financial effect once from the event and again from its referenced transaction/application.
- All components must use the deposit's currency. Deposits are reconciled independently; no pooling, offsetting or reallocation between deposits is allowed.
- The result must be non-negative. Any event/transaction mismatch, duplicate reference, unsupported event, currency mismatch or negative result is a reconciliation failure—not a value to clamp to zero.
- Before deposit receipt/refund/application/forfeiture or transfer acceptance, recalculate under the ServiceOrder-first and deposit-row lock strategy. If the calculated result differs from any materialized held balance, block the command and require explicit reconciliation.
- Materialized deposit balance is a cache/projection only. A read-only reconciliation process reports mismatches; it must not auto-correct or create compensating ledger events.

**Deposit event source integrity (approved 2026-11-22):** Each `RECEIVED`, `APPLIED` or `REFUNDED` event must reference exactly one source record of the matching type: respectively a settled `DEPOSIT_RECEIPT` transaction, a committed `DepositApplication`, or a settled `DEPOSIT_REFUND` transaction. `FORFEITED` and `RELEASED` events have no payment/application reference. A source transaction or application can back only one event. Composite relational constraints must ensure the source belongs to the exact deposit. Event amount must equal its source amount for receipt, application and refund; those amounts are positive. A forfeiture amount is positive, cannot exceed the authoritative available-held amount, and requires a reason plus fresh `MANAGER` credential validation. A release amount is exactly zero. Every event has a server-resolved employee actor and is append-only. Cross-table type, settlement/commitment and amount checks must be enforced atomically, using PostgreSQL constraint triggers or an equivalently strong mechanism where ordinary constraints cannot express them. The event is the audit trail for its referenced source and is never added as a second liability amount.

A ServiceOrder transfer may move only this per-deposit `available_held` value. It must preserve all historical receipt, application, refund, forfeiture and RELEASED records and must not treat a released deposit as extinguished.

### ServiceOrder balance and refund separation (technical review 3 — decision 1 approved)

The Product Owner approved on 2026-11-22 that a `REFUND` or `DEPOSIT_REFUND` is an outgoing-funds event and must not directly mutate or recalculate `ServiceOrder.balance_due`.

- A settled `PAYMENT` credits the ServiceOrder balance once. A `DepositApplication` credits the ServiceOrder balance once and reduces the originating deposit's held liability by the same amount.
- A `REFUND` records the outgoing amount against its original settled `PAYMENT`, subject to the original-tender and refundable-remainder rules. It does not reverse the original payment credit in `balance_due`, create a new customer debt, or change the order total.
- A `DEPOSIT_REFUND` reduces only the originating deposit's remaining held liability and records the outgoing funds against its original settled `DEPOSIT_RECEIPT`. It does not change `balance_due`; the refunded amount must be from the deposit's un-applied refundable balance.
- A refund is not an order-price correction, discount, write-off or balance adjustment. If a future business case requires changing the amount owed, it must use a separate explicit, authorized, reasoned and auditable balance-adjustment operation. Such an operation, its permissions, accounting treatment and persistence schema are not approved or defined by this decision; do not infer or implement one.
- Refund records remain separate from payment credits and from inventory returns. A refund does not automatically restock stock or alter ServiceOrder technical/lifecycle status.

The balance cache must be updated atomically with each qualifying credit/application and protected by the ServiceOrder aggregate lock. The exact reconciliation formula and treatment of any future balance-adjustment events remain part of technical review 3; do not derive `balance_due` by subtracting refund transactions.

### Canonical ServiceOrder balance formula (technical review 3 — decision 2 approved)

The Product Owner approved on 2026-11-22 the following canonical formula:

```text
balance_due = total
            - sum(settled PAYMENT amounts targeting this ServiceOrder)
            - sum(DepositApplication amounts credited to this ServiceOrder)
```

Rules and enforcement:

- `total` is the current approved ServiceOrder total after discounts and applicable tax calculation, using the order's currency and persisted monetary snapshots.
- Count only `PaymentTransaction` rows with kind `PAYMENT`, target `service_order_id`, status `SETTLED`, and the same currency. Use the transaction's applied amount once; do not sum its tender allocations again because those allocations reconcile to that amount.
- Count each committed `DepositApplication` exactly once. Its amount is a non-cash credit to the order and a corresponding reduction of that same deposit's held liability.
- Do not count `DEPOSIT_RECEIPT` as an order payment before an explicit application. Do not subtract `REFUND` or `DEPOSIT_REFUND` transactions from this formula. Do not count transfer-financial-event lines as payments or credits.
- A new settled payment or deposit application must not make the resulting balance negative. Reject an amount greater than the current outstanding balance; any future overpayment/credit-balance workflow requires separate Product Owner approval.
- A refund does not reopen the ServiceOrder balance. A future change to the amount owed must use a separately approved balance-adjustment operation; no such operation is defined by this decision.
- Recalculate and validate the formula under the ServiceOrder aggregate lock. Persist the resulting `balance_due` in the same transaction as the qualifying payment/application and its ledger/event/Note/idempotency effects. If the computed result differs from the materialized value before a new financial command, block the command and require reconciliation; never silently overwrite the discrepancy.
- The materialized `balance_due` is a transactionally maintained cache/projection, not an independent financial source of truth. Provide a read-only reconciliation process that compares it with the formula and reports mismatches. It must not auto-correct balances.

This formula supersedes any ambiguous interpretation of `balance_due` elsewhere in the specs. All features, reports and transfer acceptance must use the same definition.

A parts pre-order deposit is a financial arrangement only. The application does not capture supplier/source, purchase orders, purchase links, ETA, tracking or supplier receipt. Parts are purchased outside the application and physical stock replenishment is recorded independently through the Stock module.

## 5. Weekly instalment plans for refurbished sales

An `InstallmentPlan` is permitted only for a Sale that contains one or more serialized refurbished equipment units. The initial cadence is weekly.

The plan snapshots:

- cash price of the refurbished item(s);
- down payment, if any;
- financed/outstanding amount;
- number of weekly instalments;
- first due date and every scheduled due date;
- amount due per instalment and total amount payable;
- any charge/interest only if explicitly approved and legally validated;
- handover model and customer-agreed terms.

`InstallmentSchedule` has one row per due payment. `InstallmentSettlement` bridges schedule rows to settled `PaymentTransaction` records. It supports one receipt settling several due instalments and a single instalment paid through multiple receipts. Unallocated overpayments remain unapplied credit until a deliberate policy is approved; they are never silently duplicated across instalments.

### Handover and stock policy

The plan must choose exactly one fulfillment mode:

- `LAYAWAY` (default): the refurbished unit remains reserved and unavailable to other sales. Ownership/physical handover is after the full plan amount is settled. Stock is consumed at handover/full settlement.
- `CREDIT_SALE`: the item is handed over before the plan is fully paid. Stock is consumed at handover, while the outstanding plan remains a receivable. This mode is feature-gated and must not be activated until the business obtains Irish legal/regulatory advice and confirms any required authorisation, consumer disclosures and affordability/creditworthiness checks.

The Central Bank of Ireland explains that buying goods now and paying by instalments later is short-term credit even where no interest is charged; it lists in-store credit and BNPL among examples and describes authorisation and affordability/creditworthiness requirements for regulated short-term-credit activity. See [Central Bank of Ireland — Short-term credit](https://www.centralbank.ie/consumer-hub/short-term-credit). The exact legal classification of this retailer's own weekly payment arrangement depends on contract and handover design and must be confirmed by qualified Irish counsel/compliance before enabling `CREDIT_SALE`.

No interest, APR, setup fee, late fee, default charge, repossession, title retention or creditworthiness decision is implemented in v1. A missed weekly due date changes the schedule row to `OVERDUE` only. Payment recording continues to be supported; renegotiation, collections and late-payment enforcement are out of scope by owner instruction.

## 6. Partial and total refunds

Every refund is a separate positive-magnitude `PaymentTransaction` with kind `REFUND` and a reference to the original settled incoming `PAYMENT`. Multiple partial refunds are allowed until the amount remaining refundable reaches zero.

`refundable_remaining(original, allocation) = original_allocation_received_amount - sum(PENDING and SETTLED refund allocations referencing that original allocation)`; the original transaction's overall cap is the sum of its per-allocation remaining caps. PENDING refunds reserve capacity immediately; FAILED and VOIDED refunds release it; REVERSED refunds keep capacity blocked pending explicit reconciliation/correction.

Validation must lock the original transaction and affected original `PaymentAllocation` rows in deterministic ID order, then recalculate capacity after locks. For ServiceOrder-linked refunds, lock the `ServiceOrder` first, following the shared lock order; for other targets, lock the original transaction before allocation rows. A PENDING refund reserves capacity in the same transaction as its header and allocations, so concurrent requests cannot reserve the same amount. A full refund is an amount equal to the remaining refundable capacity. Partial refunds may be item-linked through `PaymentRefundLine`; order-level refunds are allowed with a documented reason.

- Every refund requires a fresh `MANAGER` or `ADMIN` code/password challenge, regardless of amount. The backend directly validates the employee credential, active status, Company membership and permission; no action-authorization token/entity/endpoint is created, persisted or consumed. No second approver is required.
- Refund is allocated back to the original tender only. For a split CASH/CARD receipt, each refund allocation is linked to the original `PaymentAllocation` and cannot exceed that allocation's remaining refundable amount. Alternative tender is disabled in v1.
- For CARD, the authorized `MANAGER` or `ADMIN` performs the refund on the external terminal against the original card transaction, then records the result/reference in the application. Do not store card numbers or security data.
- A refund transaction does not automatically restock inventory. Physical items returned to the Shop are selected separately through `StockReturn`/`StockReturnLine`, with quantity capped at previously consumed less already returned. A monetary refund and physical return are separate records.
- Partial refunds can be linked to one or more SaleItems/ServiceOrderItems and a reason. A total refund is the residual refundable amount after prior settled refunds.

- Refund header integrity: `REFUND` must reference an original settled incoming `PAYMENT`; `DEPOSIT_REFUND` must reference an original settled incoming `DEPOSIT_RECEIPT`. Original and refund must target the same aggregate and use the same currency.
- Allocation integrity: every refund allocation references the exact original `PaymentAllocation` from the transaction named by the refund header, with the same tender and currency. Each refund allocation is positive, and allocation totals must equal the transaction amount. Non-refund allocations must not carry an original-allocation reference.
- Refund capacity is calculated independently for each original allocation. CASH capacity is based on actual physical cash received (`amount_tendered - change_amount)); CARD capacity is based on the approved/settled original allocation amount. Prior linked PENDING and SETTLED refunds reserve/consume capacity; FAILED and VOIDED refunds release it. A REVERSED refund blocks further refunds until explicit reconciliation/correction and does not automatically release capacity.
- Lock the original transaction before calculating refund capacity, then lock affected original allocation rows in deterministic ID order and recalculate after locks. For ServiceOrder refunds, lock the ServiceOrder first according to the shared lock order. No cross-tender substitution, cross-allocation offset or reallocation is allowed.
- Persist the refund transaction and allocations, idempotency record, and required ServiceOrder `ServiceOrderHistory` entry atomically. A pending refund has no cash-drawer effect; only a settled cash refund reduces physical cash.

## 7. Approval and employee accountability

The persistent ShopSession remains the store context only. Every receipt, deposit application, deposit refund/forfeiture, instalment plan acceptance, instalment receipt, refund, adjustment and cash movement requires fresh direct employee credential validation in the backend, including active status, Company membership and feature-specific permission.

The v1 approval policy is role-based, not amount-threshold based: `MANAGER` and `ADMIN` can execute payment refunds and deposit refunds, each after fresh direct backend validation of the employee's code/password, active status, Company membership and feature-specific permission. Deposit forfeiture remains `MANAGER`-only and requires the same fresh direct validation. Do not issue, persist, consume or reuse an `EmployeeActionAuthorization`. No second approver is required. `BusinessActionApproval` remains available for future actions that explicitly need dual control, but it is not inserted into these standard flows. `ADMIN` inherits the Manager permission set except where a feature-specific rule explicitly restricts an action. The actor is Company-wide and may operate in any Shop under the same Company.

## 8. ServiceOrder financial and stock orchestration

FIN-01 owns the financial command and ledger rules. For a financial action associated with a ServiceOrder, the application must use one PostgreSQL transaction for all effects that form that business operation; Stock owns inventory rules and exposes domain operations to the coordinating financial command. Stock operations must participate in the caller's transaction and must never commit independently.

- For a settled `PAYMENT`, or an explicit `DepositApplication`, that reduces a `FIXED` ServiceOrder's `balance_due` to zero, the same transaction must persist the payment/application, update the balance, consume the applicable active reservations through the Stock domain operation, insert the corresponding `InventoryMovement` records, persist the relevant financial/domain events, append exactly one ServiceOrder `ServiceOrderHistory` entry, and persist the committed idempotency result.
- If any step fails, the entire operation rolls back: no payment/application, balance update, stock movement, reservation mutation, event, Note or committed idempotency result may remain.
- A partial payment or an unapplied deposit does not consume stock. A `NOT_FIXED` ServiceOrder payment settlement or deposit application never consumes or releases reservations; its stock disposition remains exclusively part of the Pickup command.
- A financial action that does not trigger stock consumption still persists its financial record/event and required ServiceOrder `ServiceOrderHistory` entry atomically in one transaction.
- Every successfully committed financial action associated with a ServiceOrder—including payment, deposit receipt, deposit application, payment refund and deposit refund—must append an employee-attributed `ServiceOrderHistory` containing the action/result and relevant transaction reference. Resolve the employee from the fresh direct credential validation; never accept the actor identity from the client. Store the resolved employee identity and name snapshot plus timestamp as structured metadata. The financial ledger/event remains authoritative; the Note is an append-only operational history entry. A failed or rolled-back action creates no Note.
- `REFUND` eligibility for a ServiceOrder payment requires an existing settled incoming `PAYMENT`, a positive refundable remainder, technical outcome `NOT_FIXED`, and lifecycle status `NOT_FIXED` or `PICKED`. This condition must be enforced by the backend, not only by UI visibility. A `DEPOSIT_REFUND` is a separate operation governed by the deposit's refundable held balance and lifecycle; it must not be represented as `REFUND` or counted against a payment's refundable remainder.
- Payment settlement and deposit application commands that can be retried must use the shared idempotency persistence rules. A committed replay returns the original result without repeating financial, stock, event or Note effects; a rolled-back attempt leaves no committed record and may be retried.

- **Concurrency baseline (approved 2026-11-21):** ServiceOrder-linked financial commands use PostgreSQL `READ COMMITTED` and explicitly lock the parent `service_order` row first with `SELECT ... FOR UPDATE`. This applies to payment settlement, deposit receipt/application/refund/forfeiture and payment refunds. After the parent lock, lock the affected deposit rows (ascending ID) and existing original payment transaction rows (ascending ID) as applicable, then any reservation rows and ShopInventory rows in the shared order defined in [Relational Database Model](database-model.md), section 4. Re-read authoritative balances/refundable amounts after locks; pre-lock values are not authoritative.
- All financial mutations for the same ServiceOrder must follow the same parent-lock convention, so a cross-Shop transfer cannot race a payment, deposit application or refund. Persist idempotency records in the same transaction as financial, stock, event and Note effects. Use bounded whole-transaction retries only for deadlocks/transient database failures; stale-version and business conflicts are returned without automatic replay. Exact provider implementation and retry limits remain DATA-05 review items.

## 9. Accounting and daily close semantics

Daily closing is one immutable cash reconciliation per Shop and business date. The full operational procedure and formula are specified in [Cash Closing Policy](cash-closing-policy.md). The system creates the `DailyClosing` row in `OPEN` state with a physically counted `opening_float`; it closes once, and any later correction is an append-only record created/approved by `MANAGER` or `ADMIN` after fresh direct credential validation.

```text
expected_cash = opening_float
              + settled_cash_receipts (amount_tendered - change_amount)
              + posted_cash_in
              - posted_cash_out
              - settled_cash_refunds
              - settled_cash_deposit_refunds
              - safe_drops

cash_variance = counted_cash - expected_cash
```

- Settled cash allocations from ordinary payments and service-deposit receipts count when physically received. A later `DepositApplication` is non-cash and is never counted again.
- A cash refund/deposit refund reduces the drawer only when the refund is settled through cash. A card/bank refund is excluded from physical cash. Never post the same cash refund again as a manual CashMovement.
- CARD tender is shown separately from physical cash and does not affect the drawer. External terminal/provider fee, chargeback and payout reconciliation are deferred; the application records the approved card amount and optional external reference only.
- Physical cash uses `amount_tendered - change_amount`. Keep `cash_rounding_adjustment` separately for transaction audit; do not add/subtract it a second time in the drawer formula.
- Opening float may be suggested from the prior day's actual `counted_cash`, but must be recounted and confirmed. Do not automatically reuse the prior expected balance.
- Count actual cash by denomination, calculate `counted_cash`, and persist `cash_variance = counted_cash - expected_cash`. Any non-zero difference requires a reason; never create a balancing transaction automatically.
- A `DailyClosing` is unique on `(shop_id,business_date)`, transitions `OPEN -> CLOSED` once and is immutable after close. Corrections are separate `DailyClosingCorrection` records.

The formula is the v1 operational till-control baseline. Detailed accounting chart mapping, VAT posting and external card settlement/fee reconciliation are deferred and are not a v1 implementation blocker. VAT legal configuration remains separately pending the owner's later accountant confirmation.

## 9. Suggested domain acceptance cases

1. Service booking receives a €50 `PARTS_PREORDER` deposit by card; deposit exists immediately, service balance remains unchanged until application.
2. Apply €30 of that deposit to a replacement battery line; €20 remains held.
3. Refund €20 of remaining deposit to original tender; no service line or inventory is altered.
4. A `SECURITY` deposit is received and later fully refunded; it never becomes revenue or order payment.
5. A refundable/forfeitable deposit is partially applied and the remainder refunded; totals reconcile.
6. A refurbished laptop sale has a down payment and 8 weekly instalments; schedule sums to agreed payable amount and each receipt is allocated once.
7. A weekly instalment is paid in two receipts, then one receipt pays the next two instalments; due/outstanding balances remain correct.
8. LAYAWAY unit stays reserved and cannot be transferred/sold elsewhere; final settlement triggers handover and stock consumption exactly once.
9. CREDIT_SALE handover is rejected while feature gate is off; attempts to enable without required compliance approval fail.
10. Two concurrent partial refunds cannot refund more than original payment amount.
11. A full refund after an earlier partial refund can only refund the residual amount.
12. Cash total €10.22 may be rounded to €10.20 only when the Shop has opted in and the bill is fully cash-paid; line/tax values remain €10.22-derived and the adjustment is stored separately.
13. Card/bank-transfer totals are never rounded. Exact cash payment can bypass rounding.
14. A mixed cash/card transaction is not rounded under the initial implementation rule.
15. Payment refunds and deposit refunds require one fresh `MANAGER` or `ADMIN` code/password challenge at every amount; deposit forfeiture requires a fresh `MANAGER` challenge only. No second approver or configurable threshold applies. Stock replenishment is a separate stock operation and is not linked to supplier procurement records.

## 10. Open and deferred items

- Irish VAT registration/status, tax codes/rates and tax-inclusive/exclusive display remain pending owner confirmation with an Irish accountant. The v1 feature flag is OFF and setup remains `NOT_REVIEWED`.
- Selection/integration of an external card processor/acquirer is deferred. It does not block v1 manual CARD recording through an existing terminal.
- Deposit/customer-facing terms and the exact special-order non-refundable-cost wording require review before use with customers. No automatic expiry or forfeiture is allowed.
- The detailed customer terms for weekly LAYAWAY plans need owner review; delinquency fees, collections and renegotiation are out of scope.
- Commercial refurbished guarantee exclusions are a proposed baseline in `refurbished-condition-and-complaints.md`, pending owner/legal review.
- Accountant/bookkeeper reconciliation mapping is deferred by owner instruction and is not a current implementation blocker.

## VAT configuration and official Irish references

The product-owner requested no VAT charge by the application for now: `vat_enabled=false` and `setup_status=NOT_REVIEWED`. With the flag off, the application does not add a VAT amount. This is not a legal exemption or a conclusion that VAT is not due. Before production tax documents are finalized, the Company's actual position must be reviewed and recorded by an Irish tax professional. The Revenue Commissioners explain that VAT registration obligations depend on whether the business is an accountable person and on relevant turnover/activity thresholds; this is not determined by a software toggle. See [Revenue — Who should register for VAT?](https://www.revenue.ie/en/vat/vat-registration/who-should-register-for-vat/vat-thresholds.aspx). Current VAT rates should be read from the official [Revenue VAT rates database](https://www.revenue.ie/en/vat/vat-rates/search-vat-rates/current-VAT-rates.aspx), not copied into application constants.

## Sources

- CCPC, [Guarantees and Warranties](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/guarantees-and-warranties) — a commercial guarantee does not replace the consumer's statutory rights.
- CCPC, [Your rights when buying goods](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/your-rights-when-buying-goods/) — statutory rights also apply to second-hand goods bought from a business.
- CCPC, [Deposits](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/deposits/) — deposit contracts, refundability and cancellation terms should be disclosed in writing.
- Central Bank of Ireland, [Rounding](https://www.centralbank.ie/consumer-hub/notes-and-coins/rounding) — voluntary €0.05 rounding for cash totals only; card/electronic transactions and unrounded VAT treatment are unaffected.
- Central Bank of Ireland, [Short-term credit](https://www.centralbank.ie/consumer-hub/short-term-credit) — instalment sales may fall within regulated short-term credit depending on the arrangement; review classification and authorisation before enabling early handover.


### Irish VAT configuration reference

The system includes a Company-level enable/disable flag but does not assume the Company is registered, exempt or outside scope. The applicable treatment and effective-dated rates must be reviewed against current [Irish Revenue VAT rates](https://www.revenue.ie/en/vat/vat-rates/search-vat-rates/current-vat-rates.aspx) and the Company's actual circumstances by an Irish tax professional before `setup_status` can be marked `READY`. Do not copy a current rate into constants or use `vat_enabled=false` as a legal conclusion.


See the [consolidated product-owner decision register](product-owner-decisions.md) for confirmed choices and remaining questions.
