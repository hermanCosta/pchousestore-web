# Deep-Dive Discovery Findings — Phase 1

**Status:** DISCOVERY — evidence-based, not yet approved as target behavior  
**Legacy repository:** https://github.com/hermanCosta/pchousestoremvn  
**Reference:** branch `main` as inspected during this discovery pass

## 1. Executive summary

The legacy application is not only a point-of-sale and inventory tool. The source shows distinct workflows for sales, service orders, refurbished equipment, payment history, deposits, refunds, cash movements and daily till closing. The web redesign should preserve these capabilities only after the corresponding rules and edge cases are validated.

The source also contains several high-impact risks that should be resolved before implementation:

1. Company scoping is inconsistent in some DAO queries.
2. Several JPA `OneToOne` relationships appear inconsistent with actual usage patterns (for example, one sale with multiple payment rows).
3. Password handling uses Base64 encoding, which is reversible and is not password hashing.
4. Combined payment rows repeat the full `amountDue`, which can inflate gross totals when reports sum payment rows.
5. Service-order refunds and sales refunds use different amount signs, while the report applies similar netting logic.
6. Daily closing UI and report wiring contain inconsistencies that require correction or an explicit redesign decision.

Do not reproduce these defects in the new system. First establish the intended business behavior, then define the corrected behavior in approved feature specifications.

## 2. Evidence inventory

Repository tree inspection found 123 Java source files, 37 classes under the Swing views package (including modal dialogs), 21 controllers, 21 DAOs and 24 model classes. These are file/class counts, not counts of unique business features.

Main code package: `src/main/java/com/pchouse/pchousestoremvn/`

Important inspected files:

- `views/MainMenuView.java`
- `views/NewSaleView.java`
- `views/NewServiceOrderView.java`
- `views/CreatedServiceOrderView.java`
- `views/FixedServiceOrderView.java`
- `views/PickedOrderView.java`
- `views/ClosingTillView.java`
- `views/modals/PaymentModal.java`
- `controllers/SaleController.java`
- `controllers/ServiceOrderController.java`
- `controllers/RefundController.java`
- `dao/SaleDAO.java`
- `dao/ServiceOrderDAO.java`
- `dao/SalePaymentDAO.java`
- `dao/ServiceOrderPaymentDAO.java`
- `dao/RefundDAO.java`
- `dao/CustomerDAO.java`
- `dao/ProductServiceDAO.java`
- `dao/EmployeeDAO.java`
- `dao/DailyClosingDAO.java`
- `dao/CashInRegistryDAO.java`
- `dao/CashOutRegistryDAO.java`
- `dao/RefurbDAO.java`
- `util/ReportGenerator.java`

## 3. Sales workflow — confirmed observations

Sources: `views/NewSaleView.java`, `views/modals/PaymentModal.java`, `controllers/SaleController.java`, `dao/SaleDAO.java`, `models/Sale.java`, `models/SaleProdServ.java`.

- The new-sale form requires first name, last name, contact number and at least one product/service row.
- Email validation permits an empty value in `CommonExtension.checkEmailFormat`.
- An existing customer can be selected; the view compares entered personal data with the saved person and checks contact-number matches before creating a new person/customer.
- The operator is identified by requesting a password and looking up an employee.
- Product/service rows include item, quantity and total. The UI prevents adding the same catalog item twice to the table.
- The new sale is created with `OrderStatus.PICKED` and `SaleType.COMMON` in the inspected `createSale` method.
- Payment is collected through a modal. The sale save path rejects an empty payment result.
- `SaleController.addOrderSale` delegates the sale, line items, payments, optional deposit and note to a single DAO operation.
- A receipt is generated after successful creation.

### Business questions still open

- Is editing a sale allowed after payment or after receipt generation?
- Are price overrides or discounts allowed, and who may authorize them?
- Are partial payments supported outside the deposit/installment flows?
- What is the exact meaning of `remaining` for a sale?
- Can a single sale have multiple payment attempts or multiple payment dates?
- How are cancelled sales represented and reversed?

## 4. Payment modal — confirmed observations and risks

Sources: `views/modals/PaymentModal.java`, `models/SalePayment.java`, `models/ServiceOrderPayment.java`.

Payment methods are `CARD`, `CASH` and `COMBINE`. Payment types include `DEPOSIT`, `SALE`, `ORDER`, `REFUND`, `REFURB` and `INSTALLMENT`.

The modal validates that the selected payment method exists, parses the amount due, rejects negative cash/card amounts and refuses to complete when the sum tendered is lower than the amount due. Cash overpayment is represented as change.

For `COMBINE`, the modal creates two records (one card and one cash) for the same parent transaction. Both records receive the full `amountDue`, while `amountPaid` contains the channel-specific amount. `ReportGenerator.generateDailyClosingTillReport` later sums `amountDue` over the payment rows to compute gross values. **This means a combined payment may be counted twice in gross totals.** Confirm intended semantics and write regression tests before replacing this behavior.

The modal's payment rows are separate records, so target relationships must support multiple payments per sale/order where the business allows them.

## 5. Service-order workflow — confirmed observations

Sources: `views/NewServiceOrderView.java`, `views/CreatedServiceOrderView.java`, `views/FixedServiceOrderView.java`, `views/PickedOrderView.java`, `controllers/ServiceOrderController.java`, `dao/ServiceOrderDAO.java`, `dao/ServiceOrderPaymentDAO.java`.

The new service-order form requires:

- Customer first name, last name and contact number.
- Device brand, model and serial number.
- At least one product/service row.
- At least one fault row.

The flow supports customer matching by contact, brand/model/serial autocomplete, adding known faults, adding product/service items, notes and bad-sector count. A newly-created order is assigned an employee, company, device and customer and starts in an in-progress state in the inspected construction path.

`ServiceOrderController` exposes create, update, status update and transfer operations. Creation receives the order plus fault rows, product/service rows, payment rows, an optional deposit and an optional note.

`FixedServiceOrderView.payServiceOrder` asks for operator authorization, opens the payment modal for the order's due amount, persists the resulting payment rows and creates an order note. `ServiceOrderPaymentDAO.addOrderPaymentDAO` persists payment rows and then sets the order status to `PICKED` and its picked timestamp.

`PickedOrderView` exposes a refund action. A refunded service order is represented with `OrderStatus.REFUNDED` and a refund record.

### Atomicity concern

In `CreatedServiceOrderView.updateOrderStatus`, the order status update and the creation of its `OrderNote` are separate controller/DAO operations. A failure in the second operation can leave the status changed without the audit note. The new application should make the state transition and its required note/audit event one transactional use case.

### Business questions still open

- Exact permitted status transition matrix and the roles allowed to perform each transition.
- Meaning and allowed range of `badSector`.
- Whether an order can be reopened after `FIXED`, `NOT_FIXED` or `PICKED`.
- Whether serial numbers are globally unique, unique per company, or simply searchable.
- Rules for deposits, payment installments, additional service items and recalculated balances.
- Meaning of `FINISHED` versus `FIXED` and which user action produces each state.
- Transfer destination, ownership and audit requirements.

## 6. Refurbished-equipment workflow — confirmed observations

Sources: `models/Refurb.java`, `models/RefurbSale.java`, `dao/RefurbDAO.java`, `views/RefurbComputerView.java`, `views/RefurbConsoleView.java`, `views/RefurbMonitorView.java`, `views/RefurbTelevisionView.java`, `views/RefurbCustomView.java`, `views/NewRefurbSaleView.java`.

- Refurbished equipment has category, brand, model, price, quantity, serial number and note fields.
- Optional technical attributes include screen, processor, RAM, storage, GPU, battery health and six custom label/value fields.
- Serial number is declared unique in the model mapping.
- The catalog is company-associated and category-aware.
- Refurbished sale line items link a sale to a refurbished item with quantity and total.

Open questions include inventory decrement/restoration, whether quantity is always one for serialized equipment, whether serial uniqueness is global or company-scoped, and how returns/refunds affect refurb stock.

## 7. Financial and till workflow — confirmed observations and risks

Sources: `views/ClosingTillView.java`, `controllers/ClosingTillController.java`, `dao/DailyClosingDAO.java`, `models/DailyClosing.java`, `models/CashInRegistry.java`, `models/CashOutRegistry.java`, `util/ReportGenerator.java`.

- Cash-in and cash-out records capture amount, note, employee, company and transaction timestamp.
- The closing view loads sale payments, service-order payments, cash-in entries and cash-out entries for a selected date.
- Payment reports group payment rows by parent transaction, payment type and timestamp.
- The closing model stores cash total, card total, transaction count, notes, closing date, closing timestamp and employee.
- A unique constraint exists on `CLOSING_DATE` alone; the model has no company reference.
- The close-till button and its listener are commented out in the inspected UI, although private save/summary methods remain.
- `printDailyClosingTillReport` passes `cachedTotalCash` into both the `openCash` and `closeCash` parameters.
- In `ClosingTillView.loadPayments`, the running cash/card totals are incremented for sale-payment groups, but not for service-order payment groups. Cash-in and cash-out rows are loaded into their own tables but are not included in those cached totals.
- The report generator separately calculates net cash/card from sale and service-order payment lists, but the UI passes the same cash-only cached value as both opening and closing values.

These findings mean the current closing totals should not be treated as authoritative until the intended cash reconciliation formula is agreed and tested.

## 8. Refund behavior — important inconsistency

Source: `dao/RefundDAO.java` and `util/ReportGenerator.java`.

- Service-order refund creation writes refund payment rows with negative amount-due, amount-paid, card, cash and change values.
- Sale refund creation copies the original payment amounts as positive values.
- The report generator uses refund-specific logic and subtracts refund totals from gross totals. For service-order refunds, the stored negative values can therefore be subtracted as negative numbers, increasing the net total instead of reducing it.
- In the service-order refund path, the DAO calls `note.setSale(refund.getSale())`; the normal calling view constructs the note with a `ServiceOrder`, so the service-order association may already be present. Confirm the exact constructor call and persisted association with an integration test rather than treating this as a confirmed defect.
- The service-order refund path also updates `sale` status if a sale reference exists, then updates the service-order status if present; the refund object should be constrained to exactly one refundable target.

Treat the sign convention as a defect candidate to resolve, not behavior to preserve.

## 9. Code-level query concerns

Sources: `dao/ProductServiceDAO.java`, `dao/RefurbDAO.java`, `dao/CustomerDAO.java`, `dao/EmployeeDAO.java`, `dao/RefundDAO.java`.

- `ProductServiceDAO.searchProdServDAO` combines `OR` predicates with a company predicate without parentheses. Because `AND` binds more tightly than `OR`, the company predicate may apply only to the final search condition rather than every alternative.
- `ProductServiceDAO.orderSearchProdServDAO` contains no company predicate.
- `RefurbDAO.searchRefurbDAO` contains no company predicate, unlike its category-specific search methods.
- `CustomerDAO.getAllCustomerDAO` and `searchCustomerByContactNoDAO` do not filter by company, despite `Customer` having a company relationship.
- `CustomerDAO.searchCustomerByContactNoDAO` refers to `c.person.id` and `p.id`, while the `Person` entity field/property is named `idPerson`; validate this JPQL because the property path appears invalid.
- `EmployeeDAO.getAllEmployeeDAO` and `getEmployeeByPasswordDAO` do not apply company scope. The Employee model itself has no Company field.
- `RefundDAO.getAllRefundDAO` contains JPQL text `SELECT FROM Refund s`, which appears malformed (the selected alias is missing); verify runtime behavior and add a query test.

The target API must enforce company authorization on the server and must never rely on the frontend sending a company ID as proof of scope.

## 10. Security findings

Source: `common/CommonExtension.java`, `views/LoginView.java`, `dao/CompanyDAO.java`, `dao/EmployeeDAO.java`.

- `CommonExtension.encryptPassword` Base64-encodes the password. Base64 is reversible encoding, not a password hash.
- `LoginView` prints the username and encoded password during login attempts.
- `CompanyDAO` authenticates with company name and password equality.
- `EmployeeDAO.getEmployeeByPasswordDAO` searches by password alone.
- The application uses password prompts as a second confirmation for certain operations, but the new system should use authenticated identity, permissions and step-up confirmation where justified.

Do not port the legacy password storage or verification logic. Use a vetted password-hashing mechanism and ensure logs never include credentials or their encoded/hash representations.

## 11. Required next discovery actions

1. Inspect remaining DAO methods and verify every company-scoped query.
2. Inspect all status-changing buttons and build a status-transition matrix.
3. Inspect `DepositModal`, `RefundedSaleView`, `RefundedOrderView`, payment history and cash-in/out forms.
4. Compare JPA mappings against database DDL and actual indexes/unique constraints. Do not infer the physical schema from annotations alone.
5. Validate the intended daily closing formula with examples covering sale, service-order payments, cash/card/combined payments, refunds, cash-in and cash-out.
6. Review all report templates and receipt output requirements.
7. Produce approved feature SPECs only after these open questions are resolved.
