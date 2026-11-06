# Financial Model — Ireland deployment

**Status:** DRAFT · v15 incorporates CASH/CARD-only tender, Manager-only refunds, deposit terms, MySQL image storage policy and deferred processor integration. This is an operational data model, not legal, tax or accounting advice.

## 1. Scope and decisions

The model supports one Company with multiple Shops, a persistent Shop session and employee authentication per protected operation. Every payment, deposit, refund, cash movement and close is scoped to the active Shop and attributed to the employee authorization that performed it.

| Requirement | Target rule | Status |
|---|---|---|
| Tender methods | `CASH` and `CARD` only in v1; enabled per Shop | DECIDED |
| Service deposits | Receive at service booking; track purpose and disposition | DECIDED |
| Weekly refurbished instalments | Plan plus dated schedule and receipts | DECIDED; fulfillment/legal mode pending |
| Refunds | Partial and total; Manager-only; original tender by default | DECIDED v1 baseline |
| Currency | EUR | DECIDED baseline |
| Cash rounding | Optional per Shop, Ireland €0.05 cash-only rounding | DECIDED baseline |
| Approval policy | Every refund and deposit forfeiture requires a fresh Manager authorization; no second Manager approver; procurement has no second approval | DECIDED v1 baseline |
| VAT feature | Company-level `vat_enabled` flag; initial behavior is OFF (no VAT amount is added by the application) | NO-CHARGE SOFTWARE SETTING DECIDED; legal status/reason still OPEN |
| Reconciliation formula | Ledger-derived per Shop/day; external accounting reconciliation deferred | Operational close defined; accountant mapping deferred |

## 2. Ireland localization and monetary precision

- Company defaults: `country_code = 'IE'`, `default_currency_code = 'EUR'`, `time_zone = 'Europe/Dublin'`.
- Use C# `decimal` and MySQL `DECIMAL(19,2)` for transaction amounts in EUR. Never use binary floating point for amounts.
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

## 3. Configurable tender methods

`PaymentMethod` is a catalogue seeded with exactly two active methods in v1: `CASH` and `CARD`. `ShopPaymentMethod` enables/disables these methods per branch. Other tenders (bank transfer, store credit, cheque, online payment) are out of initial scope.

**Payment processor clarification:** a processor/acquirer is the provider behind a card terminal that authorizes the card payment and later settles funds to the merchant. It is not the tender type. In v1 the employee uses an external card terminal and records `CARD` only after the terminal shows approval. No processor API integration, automated settlement matching or fee reconciliation is implemented. The external terminal's transaction/reference code may be recorded when available.

Every `PaymentAllocation` records method, amount applied, amount tendered, change and optional external terminal reference. Never persist card PAN, CVV, PIN, magnetic-stripe data or reusable payment credentials. A mixed CASH/CARD payment is one `PaymentTransaction` with multiple allocations.

Only a transaction in `SETTLED` state contributes to the paid balance or till. For CARD, the employee may mark the allocation `SETTLED` only after confirming approval on the external terminal. `PENDING`, `FAILED`, `VOIDED` and `REVERSED` do not count as collected money.

## 4. Service deposits at booking

A `ServiceDeposit` is created against a `ServiceOrder`. The receipt is captured at booking through a `PaymentTransaction` of kind `DEPOSIT_RECEIPT`, with its tender allocations. Supported purpose values:

- `PARTS_PREORDER`: customer provides funds toward an expected part such as a battery or keyboard.
- `BOOKING_ADVANCE`: advance intended to be credited to the service price later.
- `SECURITY`: refundable amount held as a guarantee; not automatically revenue or invoice settlement.
- `OTHER`: purpose must be described in notes/terms.

The deposit record contains the agreed target amount, currency, purpose, terms snapshot and lifecycle status. One or more receipt transactions can contribute to the deposit.

### Deposit lifecycle

`OPEN -> PARTIALLY_FUNDED -> HELD -> PARTIALLY_APPLIED -> APPLIED`

Alternative terminal outcomes include `REFUNDED` and `FORFEITED`; a partially refunded deposit may remain open for its residual held amount. `valid_until` is null by default; no automatic expiry applies.

- A deposit received at booking is a cash/bank/card movement and appears in the correct Shop cash/financial report.
- It is not automatically applied to `ServiceOrder.balance_due`.
- `DepositApplication` is an explicit, non-cash operation that credits the service order, optionally against a specific `ServiceOrderItem`. It must be employee-authorized and cannot exceed the deposit's available held balance.
- `DEPOSIT_REFUND` is a real outgoing payment linked to the original deposit receipt. Refund tender allocations identify how money was returned.
- No expiry/forfeiture runs automatically. If no customer response has been recorded for 90 days, set an operational `REVIEW_REQUIRED` flag and prompt the Shop to contact the customer; this is not a legal expiry date.
- Before receipt, snapshot the service/part purpose, expected fulfilment date, refund/cancellation terms and customer acknowledgement.
- If the Company cannot supply the agreed service/item, or misses the agreed date and no replacement date is agreed, the unconsumed balance is refundable, subject to applicable consumer rights.
- If the customer changes their mind, apply the terms disclosed and accepted before payment. Any retention/non-refundable portion must be clearly disclosed, fair and legally valid. For a special-order part, only a documented and genuinely non-recoverable supplier cost may be considered where the customer accepted that term in advance. Never automatically forfeit the whole deposit.
- A `SECURITY` deposit is returned when its stated purpose ends, less only documented deductions accepted in the terms and permitted by law.
- `FORFEITED` requires a fresh Manager authorization, reason, terms snapshot and documented evidence/cost. No second Manager approval is required; TECHNICIAN cannot forfeit.
- Deposit amount reconciliation: `available_held = settled_receipts - applied - settled_refunds - approved_forfeitures`.

The system must not conflate a parts pre-order deposit with supplier procurement. Operational supplier purchase tracking (supplier/source, order link, ETA, tracking and receipt of actual inventory) is specified in [Service Parts Procurement Workflow](service-parts-procurement.md). Supplier invoice settlement, accounts payable, supplier returns and procurement tax postings remain outside this initial scope.

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

`refundable_remaining(original) = original_settled_amount - sum(settled_refunds_referencing_original)`

Validation must lock or use optimistic concurrency on the original transaction so concurrent refund requests cannot over-refund it. A full refund is simply a refund whose amount equals the remaining refundable amount. Partial refunds may be item-linked through `PaymentRefundLine`; order-level refunds are allowed with a documented reason.

- Every refund requires a fresh Manager password challenge and one-use action authorization, regardless of amount. No second Manager approver is required. TECHNICIAN cannot execute refunds.
- Refund is allocated back to the original tender by default. For a split CASH/CARD receipt, each refund allocation is linked to the original `PaymentAllocation` and cannot exceed that allocation's remaining refundable amount. Alternative tender is disabled in v1.
- For CARD, the Manager performs the refund on the external terminal against the original card transaction, then records the result/reference in the application. Do not store card numbers or security data.
- A refund transaction does not automatically restock inventory. Physical items returned to the Shop are selected separately through `StockReturn`/`StockReturnLine`, with quantity capped at previously consumed less already returned. A monetary refund and physical return are separate records.
- Partial refunds can be linked to one or more SaleItems/ServiceOrderItems and a reason. A total refund is the residual refundable amount after prior settled refunds.

## 7. Approval and employee accountability

The persistent ShopSession remains the store context only. Every receipt, deposit application, deposit refund/forfeiture, instalment plan acceptance, instalment receipt, refund, adjustment and cash movement requires a fresh employee credential challenge.

The v1 refund policy is role-based, not amount-threshold based: only a `MANAGER` can execute a refund or deposit forfeiture, using one fresh employee authorization. No second Manager approval is required. `BusinessActionApproval` remains available for future actions that explicitly need dual control, but it is not inserted into the standard refund/deposit-forfeiture flow. The actor is Company-wide and may operate in any Shop under the same Company.

## 8. Accounting and daily close semantics

Daily closing is one immutable cash reconciliation per Shop and business date. The full operational procedure and formula are specified in [Cash Closing Policy](cash-closing-policy.md). The system creates the `DailyClosing` row in `OPEN` state with a physically counted `opening_float`; it closes once, and any later correction is an append-only manager-authorized record.

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
15. Procurement purchase creation/linkage/receipt succeeds without a separate approval record, but still requires the acting employee password challenge and event audit. Financial approvals for refunds/deposit forfeiture remain policy-configured and are independently tested.

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
