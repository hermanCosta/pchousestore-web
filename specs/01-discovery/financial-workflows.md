# Financial Workflows — Discovery

**Status:** DISCOVERY + TARGET DECISIONS — not an approved accounting specification.

## Enumerations found

- `PayMethod`: CARD, CASH, COMBINE
- `PaymentType`: DEPOSIT, SALE, ORDER, REFUND, REFURB, INSTALLMENT
- `SaleType`: COMMON, REFURB

Sources: `enums/PayMethod.java`, `enums/PaymentType.java`, `enums/SaleType.java`.

## Payment modal behavior

Source: `views/modals/PaymentModal.java`.

- The payment method must be selected.
- The amount due is parsed and must not be negative.
- Cash/card amounts must not be negative.
- Combined tender is accepted when cash + card is at least the requested amount.
- Underpayment is rejected by the modal.
- For cash, change equals cash tendered minus amount due, floored at zero.
- Combined tender is stored as two payment rows: one CARD row and one CASH row.

### Defect candidate: duplicated amount due

Both rows in a COMBINE payment receive the full amountDue. `ReportGenerator` sums `amountDue` per persisted row for gross totals. A combined payment may therefore inflate gross revenue. The new model should represent one payment transaction with one authoritative gross amount and one or more tender components, or otherwise define aggregation rules that cannot double count.

## Deposits

Source: `models/Deposit.java`.

A deposit can reference a sale or service order, the employee, and optionally the corresponding sale/service-order payment. Both parent references are nullable in the entity. Define and enforce whether exactly one parent (sale OR service order) must be set.

## Refunds

Sources: `models/Refund.java`, `dao/RefundDAO.java`, `util/ReportGenerator.java`.

The legacy system writes sale refunds and service-order refunds differently:

- Sale refunds copy positive payment amount values from original sale payments.
- Service-order refunds write negative values.
- Daily report logic treats refund rows as a refund category and subtracts refund totals.

This is inconsistent and can result in incorrect service-order net totals. Define a canonical sign convention and test both cash and card refunds. Prefer storing positive refund amount with an explicit transaction type and applying the sign in ledger/report calculations, or use a consistent signed-ledger model with carefully specified formulas.

The refund entity can reference either Sale or ServiceOrder. Add a rule/constraint ensuring exactly one target is assigned.

## Cash movements and daily closing

Sources: `models/CashInRegistry.java`, `models/CashOutRegistry.java`, `models/DailyClosing.java`, `views/ClosingTillView.java`, `controllers/ClosingTillController.java`, `dao/DailyClosingDAO.java`, `util/ReportGenerator.java`.

Observed inputs to daily reconciliation:

- Sale payment rows
- Service-order payment rows
- Cash-in records
- Cash-out records
- Refunds represented in payment rows

Open reconciliation formula (must be approved):

`expected_cash = opening_cash + cash_sales + cash_service_orders + cash_in - cash_out - cash_refunds - change_paid_out`

`expected_card = card_sales + card_service_orders - card_refunds`

Do not implement this formula until the meaning of each stored cash/card/change field is confirmed. In particular, determine whether cashAmount represents tendered cash or net retained cash, how deposits and installments are classified, and whether change is included in payment or cash movement.

## Closing defects to resolve

- The close-till action is commented out in the inspected `ClosingTillView` listeners.
- `loadPayments` adds sale payment totals to cached cash/card, but does not add service-order payments to those cached totals.
- Cash-in/out entries are loaded into tables but are not added to cached cash/card totals.
- `printDailyClosingTillReport` passes cachedTotalCash for both openCash and closeCash.
- Legacy fact: `DailyClosing` has a unique constraint on closing date alone and no Company association. The target model does not copy this global uniqueness; ADR-005 defines one close per `(ShopId, BusinessDate)`.
- `DailyClosingDAO.isDateAlreadyClosedDAO` checks only the date and has no company scope.

## Required acceptance tests

- Cash-only payment exact amount and overpayment/change.
- Card-only payment.
- Combined card/cash tender with no change and with change.
- Underpayment rejected.
- Multiple payment attempts/instalments if allowed.
- Deposit creation and later settlement.
- Sale refund by cash and card.
- Service-order refund by cash and card.
- Daily close including both sale and service-order payments.
- Cash-in and cash-out impact on expected drawer balance.
- Duplicate close prevention scoped to the correct company and business date.
- Concurrent payment/closing attempts.

## Target payment model — decision recorded

The new system will use `transaction` semantics. `PaymentTransaction` is the authoritative financial event and stores its amount once. One or more `PaymentAllocation` children identify the tender components, initially CASH and CARD. A cash/card combined payment is one transaction with two allocations; it must not create two copies of the gross amount.

Proposed invariants:

- Each transaction targets exactly one Sale or ServiceOrder.
- `PAYMENT`, `DEPOSIT` and `REFUND` are transaction kinds; all stored amounts are positive magnitudes. Reports apply positive/negative signs from the kind, not from inconsistent stored signs.
- `sum(PaymentAllocation.amount_applied)` equals the transaction amount. For a cash allocation, `amount_tendered = amount_applied + change_amount`; card change is zero.
- Multiple transactions can target one Sale/ServiceOrder, supporting successive payments. A refund references its original transaction and is capped by the remaining refundable amount.

These are target-design rules, not legacy behavior. The original deposit settlement/refund workflow remains under redesign; see [Financial Model](../02-architecture/financial-model.md).

## Target daily closing — decision recorded

The target decision is one independent closing for each `(ShopId, BusinessDate)`. A closing includes only that Shop’s sales, payment allocations, refunds and cash movements for its business date. Different Shops may close the same date independently; a Shop may not close the same date twice. The responsible employee is the person who executed the close and authenticated for that action, and both the employee and authorization must belong to the Shop being closed. Company-level reporting may aggregate completed Shop closings, but the close itself is never consolidated across branches.

The actual reconciliation formula is still OPEN. In particular, opening cash, treatment of change, cash movements, refund allocations, pending transactions and the business-day time zone must be defined and covered by examples before implementation.

## Stock decisions affecting finance and operations (target redesign)

Only `PRODUCT` catalogue entries are stock-managed; `SERVICE` entries have no stock balance. Product quantities are whole units. Reservations are explicit and optional; release/cancellation restores availability without changing on-hand stock. Sale stock is deducted on settlement. Service-order product parts remain reserved at `FIXED` and are deducted when settled payments and explicitly applied deposits reduce the order balance to zero; physical `PICKED`/collection is a later independent event. When a Sale/ServiceOrder is `REFUNDED` or `CANCELLED`, only explicitly selected product lines/quantities physically returned to the Shop are restocked. A monetary refund does not automatically create a stock return. See [Stock, Reservations, Transfers and Refurbished Equipment](../02-architecture/stock-and-refurbishment.md).

A transfer between Shops has source dispatch, `IN_TRANSIT` and destination receipt phases. Source stock decreases at dispatch; destination stock increases only on actual receipt. Product availability at either endpoint excludes stock already reserved or in transit. Refurbished equipment is serialized by physical unit; its technical specifications are driven by configurable category attribute definitions.


## Product-owner finance decisions (2026-11-05)

- **Service deposits:** deposits are required at service booking in scenarios such as ordering a battery/keyboard or holding a security guarantee. Record the receipt at booking as a distinct `DEPOSIT_RECEIPT` PaymentTransaction. A deposit is held until explicitly applied to the service invoice, refunded or forfeited; it is not automatically treated as invoice payment.
- **Refurbished-sale instalments:** weekly instalment schedules are required for refurbished equipment sales. Each schedule entry has a due date and amount; actual receipts are separate PaymentTransactions allocated to one or more schedule entries.
- **Refunds:** both partial and total refunds are supported. Each refund references the original settled transaction; cumulative refund amount is capped at the remaining refundable amount.
- **Ireland localization:** initial deployment uses EUR, `Europe/Dublin`, decimal amounts with two places, and optional cash rounding to nearest €0.05 on eligible cash totals only. The official Central Bank of Ireland states rounding is voluntary, cash-only, applies to the total bill, does not alter individual prices or VAT, and does not apply to card payments: https://www.centralbank.ie/consumer-hub/notes-and-coins/rounding.
- **Regulatory gate:** weekly instalments can constitute short-term credit when goods are provided now and paid later, even when interest-free. Before enabling customer handover before full payment, the business must obtain Irish regulatory/legal advice on classification, authorisation and consumer-protection requirements. Central Bank information: https://www.centralbank.ie/consumer-hub/short-term-credit.
