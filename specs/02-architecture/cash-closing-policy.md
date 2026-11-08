# Cash Closing Policy — one daily close per Shop

**Status:** Approved v1 operational cash-reconciliation baseline. This policy defines register reconciliation, not a statutory accounting ledger or tax opinion; accounting/tax mappings and processor settlement reconciliation remain deferred for finance review.

## 1. Operating model

- One physical cash drawer/register is modelled per Shop in the initial release. Multiple tills/registers inside one Shop are out of scope.
- There is at most one `DailyClosing` per `(shop_id, business_date)`. The row is created in `OPEN` state when the Shop's cash day begins and transitions once to `CLOSED`.
- `business_date` is derived using the Shop's configured time zone; the Ireland default is `Europe/Dublin`.
- A cash payment, cash movement or cash refund must belong to the active Shop business date. A card/bank payment can be recorded without affecting physical drawer cash.
- `MANAGER` opens and closes the cash day in the initial permission matrix. `TECHNICIAN` cannot perform cash operations.

## 2. Opening float

At opening, the manager physically counts the cash placed in the drawer and records it as `opening_float`. Record the employee authorization and opening timestamp.

The prior day's `counted_cash` may be suggested as the next day's opening value, but it must be recounted and explicitly confirmed. Do not carry forward the prior day's `expected_cash` automatically.

## 3. Expected physical cash formula

For one Shop and one business date:

```text
expected_cash = opening_float
              + settled_cash_receipts
              + posted_cash_in
              - posted_cash_out
              - settled_cash_refunds
              - settled_cash_deposit_refunds
              - safe_drops
```

Where:

- `settled_cash_receipts` is the sum of settled cash allocations for `PAYMENT` and `DEPOSIT_RECEIPT` transactions, using the amount physically retained: `amount_tendered - change_amount`.
- `posted_cash_in` is manual cash added to the drawer and recorded as a `CashMovement` of type `CASH_IN`.
- `posted_cash_out` is a documented cash expense/payout recorded as `CASH_OUT`.
- `settled_cash_refunds` is the sum of cash tender allocations on settled `REFUND` transactions.
- `settled_cash_deposit_refunds` is the sum of cash tender allocations on settled `DEPOSIT_REFUND` transactions.
- `safe_drops` are amounts removed from the till and placed in a safe/bank bag, recorded as `SAFE_DROP`.

Do not count `PENDING`, `FAILED`, `VOIDED` or `REVERSED` payment transactions. Do not create a second `CashMovement` for a refund already represented by a cash refund allocation; this would double-subtract the same cash.

### Items excluded from physical drawer cash

- Card, online, bank-transfer and store-credit allocations are not physical cash.
- Card processor fees and bank payout delays are reconciled separately from the customer's gross payment.
- `DepositApplication` is a non-cash accounting/application event. The original deposit receipt is counted only once, at the time it was actually received.
- A deposit refund affects the drawer only when the refund allocation is actually paid in cash.
- Applying a deposit to a service balance does not create a new cash receipt.

### Cash rounding

Use the recorded cash tender less change to calculate what physically entered the drawer. Persist the separate `cash_rounding_adjustment` for audit and reconciliation to the customer balance, but do not add it to the drawer formula a second time. Cash rounding must not change the original line prices or tax calculation base.

## 4. Counting and variance

At close, the manager counts the physical cash by denomination and confirms the total. The optional `DailyClosingCashCount` rows store denomination, quantity and subtotal. The system computes:

```text
cash_variance = counted_cash - expected_cash
```

- Negative variance means the drawer is short.
- Positive variance means the drawer is over.
- A non-zero variance requires a reason/note before closing.
- Preserve the expected value, counted value and variance exactly as recorded. Do not alter historical sales/payments or create balancing movements automatically to force zero variance.
- Company/Shop reports show both the variance amount and its reason.

## 5. Electronic tender reconciliation

Card, bank-transfer and online payments are grouped by payment method/provider for the Shop business date. They are shown in a separate electronic-tender section and never added to `expected_cash`.

Processor fees, chargebacks, settlement timing and terminal/bank batch matching are a separate reconciliation process. The initial release may show gross expected tender by method and allow a manager to record the external settlement reference/amount; a full processor integration is not required for the cash-close formula.

## 6. Safe drops and cash movements

`CashMovement` uses a positive amount plus a controlled type/direction:

- `CASH_IN` / `IN`: cash added to the drawer (e.g. documented float top-up).
- `CASH_OUT` / `OUT`: documented petty cash expense or payout.
- `SAFE_DROP` / `OUT`: cash removed from the drawer and transferred to a safe/bank bag.

Every movement requires a reason, business date, employee, single-use authorization and timestamp. The sum is included once in the expected drawer calculation.

## 7. Closing and corrections

- Closing is a one-way transition `OPEN -> CLOSED`; the original closing snapshot is immutable.
- If a mistake is discovered after close, create a `DailyClosingCorrection` referencing the original close. A correction has a signed amount, reason, actor authorization and timestamp. It must not rewrite the original count or variance.
- `MANAGER` is the only role allowed to create/approve a correction in the initial matrix. The report displays original close and later corrections separately.
- A new business date gets a new `DailyClosing` row. It cannot reuse another Shop's drawer or close.

## 8. Numerical example

| Component | Amount |
|---|---:|
| Opening float | €150.00 |
| Settled cash receipts (sales + cash deposits, net of change) | +€425.50 |
| Cash in | +€20.00 |
| Cash out | -€15.00 |
| Cash refunds/deposit refunds | -€12.50 |
| Safe drop | -€100.00 |
| **Expected cash** | **€468.00** |
| Physical counted cash | €465.00 |
| **Variance** | **-€3.00** |

Card receipts of €300.00 and bank transfers of €80.00 are listed in electronic tender totals, not the cash drawer formula.

## 9. Acceptance scenarios

1. Settled cash sale of €20 with €25 tender and €5 change adds €20 to the expected drawer.
2. A settled €50 cash `PARTS_PREORDER` deposit adds €50 once; applying €30 to the service later adds zero cash.
3. A €10 refund allocated to cash subtracts €10; the same refund must not also be posted as a `CASH_OUT` movement.
4. A €10 card refund does not change the physical drawer.
5. A €30 cash-in adds €30; a €12 cash-out and €100 safe drop subtract €112.
6. A €10.22 bill rounded to €10.20 records the actual cash tender; the separate -€0.02 adjustment is not subtracted again.
7. A pending/failed/voided transaction has no effect on expected cash.
8. Closing one Shop does not include transactions or cash movements from another Shop.
9. A closed snapshot cannot be edited; a manager correction is appended and remains separately visible.

## 10. Deferred finance work

The formula, opening-float model, immutable close and correction-only recovery are approved v1 operational rules. Refunds are Manager-only and return to original tender; there is no amount threshold or second Manager approver. Deposits do not expire or forfeit automatically; 90 days without customer response triggers review/contact only. Final customer-facing deposit wording requires Irish consumer-law review. Provider fees/settlement and chargebacks, accounting chart/VAT posting, cash-rounding posting account and future multi-till support remain outside this approval. No drawer may be reopened or overwritten after close; use an append-only correction with reason and authorization.
