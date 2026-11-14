# Target Domain Model — pcHouseStore Web

**Status:** DRAFT based on ADR-001 through ADR-025 and the consolidated product-owner decision register.  
**Purpose:** Define the conceptual model and domain invariants before implementation and physical DDL approval.

## 1. Domain boundaries

| Module | Responsibility | Main concepts |
|---|---|---|
| Company & Shops | One company profile and branch operations | Company, Shop |
| Shop Access | Shop authentication and shared workstation/session context | ShopAccessCredential, ShopSession |
| Employee Operations | Company-wide staff identity/password, Company-wide MANAGER and TECHNICIAN permissions, and transaction-level identity verification across all Shops | Employee, EmployeeRoleAssignment, EmployeeActionAuthorization |
| People & Customers | Shared company-level customers and contact records | Person, Customer |
| Catalogue | Shared product and service definitions | CatalogItem, Product, Service, ProductCategory |
| Stock | Branch balances, reservations, movements, inter-shop transfers and supplier receipts | ShopInventory, InventoryReservation, InventoryMovement, StockTransfer, StockReturn |
| Supplier Procurement | Queue and manage what needs to be bought for service jobs, reference images, vendor/source links, ETAs, partial receipt and allocation to jobs | Supplier, ServiceProcurementRequest, ServiceProcurementRequestLine, ServiceProcurementRequestImage, ServiceProcurementRequestEvent, SupplierPurchase, SupplierPurchaseLine, ServicePartProcurementAllocation, SupplierPurchaseEvent |
| Refurbishment & Customer Claims | Category-specific specifications, condition grades, inspections, private unit images, individually tracked equipment, 12-month commercial guarantee and complaint/remedy tracking | ProductAttributeDefinition, RefurbishedConditionGrade, RefurbishedUnit, RefurbishedUnitInspection, RefurbishedUnitWarranty, CustomerComplaintCase, CustomerComplaintCaseEvent, CustomerComplaintCaseAttachment, CustomerComplaintResolution |
| Sales | Branch sales, lines and seller attribution | Sale, SaleItem |
| Service Orders | Branch service workflow, device, faults, line items, part requirements and notes | ServiceOrder, ServiceOrderItem, ServiceOrderFault, ServiceOrderEvent, ServicePartRequirement, BusinessNote |
| Finance | Payments, service deposits, weekly refurbished-sale plans, partial/total refunds and tender configuration | PaymentTransaction, PaymentAllocation, PaymentMethod, ServiceDeposit, DepositApplication, InstallmentPlan, InstallmentSchedule, InstallmentSettlement, PaymentRefundLine |
| Cash Management | Branch cash movements, Irish cash rounding and one daily open/close reconciliation per Shop/day | CashMovement, ShopFinancialPolicy, DailyClosing, DailyClosingCashCount, DailyClosingCorrection |

## 2. Conceptual model

### Company and shops

- There is one `Company` representing the legal/business entity.
- A `Company` owns one or more `Shop` records. A `Shop` is a physical/operational store or branch.
- Company-level master data (people, customers, employees, catalogue and categories) is shared by its shops by default. Stock balances and all operational transactions are Shop-scoped.
- The current deployment is provisioned for one Company. Do not add multi-company tenant patterns without a new decision.

### Shop session and employee identity

- The user selects/authenticates a `Shop` at application entry using that shop's access credential.
- A successful login creates a `ShopSession`. The session has Shop context, session identifier, issue/expiry/revocation metadata and optional workstation metadata. It does **not** identify an active employee.
- The shop session may remain active while different employees use the same workstation. It is not transferred to or owned by the last employee who authenticated a transaction.
- `Employee` is a company-level workforce identity with an individual credential verifier (stored only as a secure hash) and active status.
- Each employee has one Company-wide account and password. The same credentials work in every Shop belonging to the Company; no duplicate account is created for each branch.
- `EmployeeRoleAssignment` assigns Company-wide `MANAGER` or `TECHNICIAN`. MANAGER has full access across every Shop. TECHNICIAN may manage customers, Products, Services, categories, sale prices and Fault catalogue entries, create/edit ServiceOrders, and perform the approved service/diagnostic, service-part procurement/receipt, stock availability/reservation and equipment inspection work. Refunds remain Manager-only; other restricted capabilities follow `employee-access-and-roles.md`. The active Shop still comes only from `ShopSession`.
- `ShopInventory.minimum_quantity` is Shop-specific and drives that Shop's low-stock reporting.
- Before each protected business command, the executing employee supplies their employee code and individual password. The server resolves the employee, verifies the employee is active, belongs to the Company that owns the active Shop and has a role allowed for the action, then creates a short-lived, single-use `EmployeeActionAuthorization` linked to the current `ShopSession` and action code.
- The authorization is consumed atomically with the operation. A later operation requires a new employee credential challenge. Never place `EmployeeId` or employee role in the long-lived ShopSession identity.

### People and customers

- `Person` and `Customer` are shared at Company level by default, so a customer can be served by any branch without duplicating the customer record.
- `Employee` also references a company-level `Person` record.
- The same customer, device or contact information may therefore be visible across shops. Shop-specific privacy/visibility restrictions require a separate rule and authorization design.

### Catalogue and product categories

- `CatalogItem` is the common identity and commercial definition of an item, shared across shops.
- Each item is exactly one kind: `PRODUCT` or `SERVICE`.
- `Product` is the stock-managed subtype; `Service` has no stock balance and cannot generate stock reservations, transfers or movements.
- Product quantities are whole units. No fractional quantity is allowed for products in catalogue lines, inventory, reservations, returns or transfers. Service line quantities may use a separate unit/pricing rule if fractional service units are needed; that does not create stock.
- `ProductCategory` is a configurable hierarchy, not a hard-coded enum. Examples include PC, Notebook, Console and TV.
- `ProductAttributeDefinition` defines category-specific technical fields (e.g. CPU, RAM, SSD, GPU, screen size, resolution, storage capacity). Definitions carry a data type, unit/format, validation and display order.

### Stock balances, reservations and posting

- Quantity-tracked products use one `ShopInventory` record per `(Shop, Product)`, containing integer on-hand and reserved quantities. Available quantity is `on_hand - reserved`.
- Serialized refurbished products do not use a manually edited aggregate quantity. Each physical unit has one `RefurbishedUnit` record and its stock is determined by its current Shop and lifecycle status.
- `InventoryReservation` groups reservation lines for exactly one Sale or ServiceOrder. A reservation line refers either to a quantity-based ShopInventory product or to one serialized refurbished unit, and to the corresponding product line in the Sale/ServiceOrder.
- Reservation is optional and explicit. It reduces availability but does not reduce on-hand quantity and does not create an outbound InventoryMovement.
- Releasing/cancelling a reservation restores availability only. It does not add stock back, because the stock was never deducted.
- For a standard Sale, product quantities are deducted only when the balance reaches zero through settled payment transactions. Active reservations are consumed in the same transaction. Quantity-tracked stock may become negative; the signed deficit is retained and reported.
- Weekly refurbished instalments use `LAYAWAY` in the initial release: the serialized unit remains reserved until full settlement, and handover occurs after settlement. `CREDIT_SALE` (handover before full payment) stays disabled pending Irish regulatory review and a new explicit product decision.
- For a ServiceOrder, moving to `FIXED` retains reservations for its product parts. Product quantities are deducted when settled payments and explicitly applied deposits reduce the balance to zero; this is not delayed until physical `PICKED`/collection. A deposit received at booking does not consume stock and reduces the order balance only when explicitly applied to the order.
- `InventoryMovement` is the immutable stock ledger. Each balance/unit state change and movement insert is atomic and attributed to a single-use employee authorization.

### Cancellation and selected stock returns

- Cancelling an unpaid Sale/ServiceOrder releases active reservations and creates no positive stock movement.
- When a Sale or ServiceOrder is in `REFUNDED` or `CANCELLED`, the operator may select the product lines and quantities physically returned to inventory.
- `StockReturn` and `StockReturnLine` record only physically received, selected products. Receipt starts pending inspection and does not change sellable stock; only an explicit `RESTOCK` decision creates a positive movement. Serialized refurbished units remain quarantined until inspection passes.
- A stock return cannot exceed the product quantity already consumed minus previous returns. A serialized refurbished return must identify the exact original physical unit.
- Financial refund and physical stock return are separate facts. A `PaymentTransaction` of kind `REFUND` does not automatically restock goods.

### Inter-shop transfers

- A `StockTransfer` moves product stock between two distinct Shops belonging to the same Company.
- On dispatch, source stock decreases and an outbound movement is posted. The goods become `IN_TRANSIT` and are not available at either Shop.
- On receipt, the receiving Shop records actual received quantities and destination stock increases through an inbound movement.
- Partial receipt is supported. A transfer cannot be cancelled without stock effect after dispatch; a reverse/corrective transfer is required.
- Transfer of a serialized refurbished unit identifies that exact `RefurbishedUnit` and has quantity one.
- Receive only verified acceptable quantities, not more than dispatched. Log short/damaged/overage discrepancies with reason, employee and authorization; unaccepted quantities do not enter destination stock. Short/damaged quantities remain outstanding and the transfer stays open. No automatic loss write-off; overages require a separate transfer/document.
- Initial scope has one stock pool per Shop. Multiple shelves/warehouses inside a Shop remain out of scope pending a decision.
- A service order may also be sent to another Shop, separate from `StockTransfer`. The destination Shop assumes operational and financial responsibility; all transactions from transfer onward run through it. Previously settled transactions stay attributed to the source; open balance and held/unapplied deposit liability move to the destination through an audited internal event, not a cash receipt. Device custody, product-part inventory attribution, transfer authorization and event fields still need definition before implementation.

### Refurbished physical units

- A `RefurbishedUnit` is one physical PC, console, TV or other refurbished equipment unit. It references a Product configured for serialized-unit tracking.
- Each unit has an internal asset code, optional manufacturer serial, condition, notes, current Shop/status and its own technical specification values.
- Category-specific attribute definitions make the form adaptable without schema changes for every equipment type. Example: PCs can have CPU/RAM/GPU/SSD attributes, consoles can have generation/storage/accessories, and TVs can have screen size/resolution/panel attributes.
- The sale line references the exact RefurbishedUnit sold. Reservation, transfer and return preserve the same unit identity.

### Sales and service orders

- Every `Sale` belongs to exactly one `Shop`; the customer and catalogue item are shared Company records.
- `SaleItem` snapshots description, quantity and price at transaction time. Product quantity must be an integer. A serialized refurbished item line has quantity one and references the exact `RefurbishedUnit`.
- Every `ServiceOrder` belongs to exactly one `Shop` and one customer. It may have a responsible employee distinct from the employee who created or modified it. Its lifecycle includes `AWAITING_PARTS` when booking/diagnosis (while `CREATED`) or later repair work (`IN_PROGRESS`) reveals a blocking part requirement that is not fully reserved. A required-to-complete product part must be fully reserved before the order can move to `FIXED`.
- Service-order creation, status transitions, reservation, procurement receipt and payment require fresh employee authorization. `ServiceOrderEvent` and `SupplierPurchaseEvent` record committed state and procurement changes.

### Service-part requirements and supplier procurement

- A service order may contain one or more product parts required for diagnosis or repair. Each total demand is represented by a `ServicePartRequirement` linked to its `ServiceOrderItem`; only `PRODUCT` catalogue items can be requirements. Product quantities remain integer-only.
- The Shop has a dedicated **Service Parts Purchase Queue** independent of the ServiceOrder screen. Each `ServiceProcurementRequest` belongs to one ServiceOrder and represents a sourcing round. A ServiceOrder can have multiple requests created at different moments as new parts are identified. Each request contains one or more `ServiceProcurementRequestLine` rows linked to existing requirements.
- Each request line may include several private reference images (part overview, model/serial label, connector, dimensions or damage), notes, part number and compatibility information. Images help the buyer identify the exact part; they do not replace the Product catalogue identity or the durable ServicePartRequirement. Request edits, assignment, priority and image changes are recorded by `ServiceProcurementRequestEvent`.
- The queue is Shop-scoped and provides filters by request status, ServiceOrder ID, customer/device, part, priority, created/needed-by date and assigned buyer. The detail view links back to the ServiceOrder and exposes all request lines, images and sourcing timeline.
- When a required part is available in the active Shop, the employee may reserve it against the requirement. If only part of the required quantity is available, reserve that portion and procure the remainder. Reservations reduce availability but do not reduce physical on-hand stock.
- If a blocking requirement cannot be fully reserved, the service order enters `AWAITING_PARTS` from `CREATED` (for example, immediately after booking/diagnosis and before repair starts) or from `IN_PROGRESS`. The order remains visible in the active work queue and the purchase queue tracks what must be sourced.
- A `SupplierPurchase` represents an external order placed by a Shop. It has a supplier/source, external order reference, purchase/order URL, tracking URL/reference, order date, expected delivery date and status. SupplierPurchase status is independent from ServiceOrder and ServiceProcurementRequest status. `SupplierPurchaseLine` stores the product and whole-unit quantities ordered/received.
- `ServicePartProcurementAllocation` connects purchase lines to request lines, allowing a purchase to cover several service orders and one request line to be split across purchases. The durable requirement is reached through the request line. Unallocated received quantities become ordinary available Shop stock.
- When a delivery is received (fully or partially), an employee records the actual accepted quantity. The system posts a `PURCHASE_RECEIPT_IN` inventory movement for the quantity physically received. Quantities allocated to open service requirements are reserved for those orders in the same transaction; excess quantities remain available.
- A purchase order's ETA, tracking details and status changes are append-only `SupplierPurchaseEvent` entries. An order whose expected delivery date has passed and which is not fully received is displayed as overdue; overdue is a derived display state, not a separately persisted purchase status.
- When every blocking requirement for an order is fully reserved, an employee-authorized `ResumeServiceOrder` command may move it from `AWAITING_PARTS` to `IN_PROGRESS`. This is explicit and audited; receiving a parcel must not silently restart repair work.
- After repair, `FIXED` retains the reservations. Full settlement consumes the reserved parts immediately, independent of the later pickup/collection transition. Cancelling before consumption releases reservations.
- Supplier purchases are operational procurement records for traceability and stock receipt. Supplier invoice settlement/accounts payable, automated marketplace integrations and supplier returns are not included in the initial scope.

### Payments, service deposits, instalments and refunds

- `PaymentTransaction` records one financial event and one authoritative amount; `PaymentAllocation` splits that amount across configurable tender methods.
- A service booking may create a `ServiceDeposit` and record its receipt immediately. Deposit purpose distinguishes parts pre-order, booking advance, security deposit and other reasons.
- A deposit remains held until explicitly applied to the ServiceOrder, refunded or forfeited. A security deposit is not automatically revenue or invoice payment. `DepositApplication` is non-cash credit against the order and is separately audited.
- Refurbished sales can have a weekly `InstallmentPlan` with dated `InstallmentSchedule` rows and `InstallmentSettlement` allocations. Layaway is the default; early handover / credit sale remains gated on Irish legal/compliance review.
- Refunds can be partial or total. Each refund is a positive-magnitude `PaymentTransaction` linked to the original settled payment; repeated partial refunds cannot cumulatively exceed the unrefunded amount.
- Optional `PaymentRefundLine` records attribute a refund to specific sale/service lines. Monetary refund remains separate from physical `StockReturn`.
- Procurement purchases do not require a second approval step; the employee who places/records the purchase is identified by the mandatory per-operation password challenge. Other financial approval policies (for example high-value refunds and deposit forfeiture) remain configurable and open.

### Cash management and closing

- `CashMovement` is attributed to one Shop and to the employee authorization used to record it.
- Each `DailyClosing` belongs to exactly one Shop and one business date. Its unique key is `(ShopId, BusinessDate)`.
- Initial localization is Ireland (`IE`), EUR and `Europe/Dublin`. Euro amounts use exact decimal arithmetic with two decimal places. Irish cash rounding is optional per Shop, applies only to the final cash bill total to the nearest €0.05, and is stored as a separate adjustment; electronic tenders and the VAT calculation base are not rounded.

### VAT configuration (Ireland)

- VAT calculation is controlled by a Company-level `CompanyTaxConfiguration.vat_enabled` feature flag. The tax capability is not hard-coded into item types or store-specific sessions.
- The flag is a software configuration, not a statement about whether the business is legally required or entitled to charge VAT. `setup_status=NOT_REVIEWED` remains the initial state; production tax-document finalization requires an accountant-reviewed configuration.
- Tax codes and effective-dated rates are Company data. Sale and ServiceOrder lines snapshot the tax code, rate, taxable amount and tax amount used at the time of the transaction. Historical invoices are never recalculated when the flag or rates change.
- Exact VAT registration status, rates, price-inclusive/exclusive display and treatment by product/service category remain open until reviewed by an Irish tax professional.

## 3. Core invariants to enforce

1. Exactly one Company is provisioned for the current deployment; every Shop belongs to that Company.
2. Every operational record is scoped to exactly one Shop; child records must remain in the same Shop as their parent.
3. `ShopSession` authenticates only the Shop context; it contains no active employee identity.
4. Each protected committed action requires a fresh employee credential challenge and a single-use authorization.
5. An employee can operate in any Shop belonging to the employee's Company if active and permitted by a Company-wide MANAGER/TECHNICIAN role; the same password works across all Shops.
6. `EmployeeActionAuthorization` is short-lived, action-scoped, bound to operation context and consumed atomically; no replay or reuse.
7. Employee attribution on an operational record must match the consumed authorization; never accept actor identity from the browser.
8. Only `PRODUCT` entries can have stock, reservation, transfer, return or movement records. `SERVICE` entries cannot.
9. Product quantities are integers across stock balances, reservations, stock returns, transfers and product sale/order lines. Fractional product quantities are rejected.
10. For quantity-tracked products, `ShopInventory` is unique on `(ShopId, ProductId)`; `quantity_on_hand` is signed and may be negative, `quantity_reserved` is non-negative, and `available_quantity = quantity_on_hand - quantity_reserved` may be negative. Reservations are created only from positive available stock; negative deficits are visible and auditable.
11. For serialized refurbished products, one `RefurbishedUnit` represents one physical unit; serialized sale/reservation/return/transfer quantity equals one.
12. An active reservation reduces availability but not on-hand. Release restores availability only. Consumption reduces both on-hand and reserved quantity; unreserved payment deduction reduces available/on-hand stock.
13. Sale stock is consumed when settled payments reduce the balance to zero. Service-order `FIXED` preserves reservations; full settlement consumes stock regardless of the later pickup status. Partial payment does not consume stock. Quantity-tracked stock may become negative at settlement.
14. Cancellation before consumption releases reservations without stock-in movement. `REFUNDED`/`CANCELLED` permits explicit selected stock-return lines only for previously consumed quantities.
15. Financial refunds do not imply physical stock return. Each physical return is separately selected, authorized, recorded and ledgered.
16. Stock transfer dispatch decreases source stock; receipt increases destination stock. In-transit stock is unavailable; source and destination belong to the same Company.
17. Inventory balance/unit-state update and corresponding InventoryMovement insert are atomic and idempotent.
18. A PaymentTransaction references exactly one of Sale or ServiceOrder (XOR), from the same Shop.
19. `DailyClosing` is unique on `(ShopId, BusinessDate)` and reconciles only that Shop.
20. A service order may enter `AWAITING_PARTS` from `CREATED` or `IN_PROGRESS` only while at least one blocking `ServicePartRequirement` is not fully reserved.
21. A supplier receipt increases stock only by actual accepted whole units; quantities allocated to service jobs are reserved atomically and cannot be sold elsewhere.
22. Supplier purchase lines and their request-line allocations must reconcile: received allocations cannot exceed line receipts; total allocations cannot exceed the request-line quantity; active request rounds cannot double-count the requirement shortage; unallocated receipt quantities remain available stock.

## 4. Aggregate boundaries and transactional use cases

| Aggregate root | Child records | Atomic operations |
|---|---|---|
| Company | Shop, shared master data | Provision Company; create/deactivate Shop |
| Shop | ShopAccessCredential, ShopSession | Authenticate Shop; create/revoke ShopSession |
| EmployeeActionAuthorization | One protected business command | Verify employee, consume authorization and persist all command records atomically |
| CatalogItem | Product or Service subtype | Create/update shared catalogue and subtype |
| ProductCategory | ProductAttributeDefinition | Configure category and technical specification schema |
| ShopInventory | InventoryMovement, active reservation counter | Reserve/release/consume/adjust stock atomically |
| InventoryReservation | InventoryReservationLine | Create, release or consume reservation lines with balance counters |
| StockTransfer | StockTransferLine and transfer-linked movements | Dispatch at source; receive at destination; handle partial receipt |
| StockReturn | StockReturnLine and return-linked movements | Record selected physical returns; separate receipt, inspection, quarantine/disposal and stock credit |
| RefurbishedUnit | RefurbishedUnitAttributeValue, RefurbishedUnitImage, RefurbishedUnitInspection, RefurbishedUnitWarranty, movements/reservations | Register one device; inspect and grade A/B/C; upload images; reserve, transfer, sell or return the exact unit; issue 12-month commercial guarantee on handover |
| CustomerComplaintCase | CustomerComplaintCaseEvent, CustomerComplaintCaseAttachment, CustomerComplaintResolution | Register and track statutory/commercial/service complaints; preserve evidence, inspection, communications and remedy trail |
| DailyClosing | DailyClosingCashCount, DailyClosingCorrection | Open with counted opening float; calculate expected drawer; count and close once per Shop/date; preserve later corrections separately |
| Sale | SaleItem, PaymentTransaction, InventoryReservation, StockReturn | Settle sale and consume stock atomically; release on cancellation; record selected return |
| ServiceOrder | ServiceOrderItem, ServiceOrderEvent, PaymentTransaction, InventoryReservation, StockReturn | Mark FIXED without consumption; consume product stock at full settlement (before any later pickup status); release on cancellation; record selected return |
| ServiceProcurementRequest | ServiceProcurementRequestLine, ServiceProcurementRequestImage and ServiceProcurementRequestEvent | Queue service-related purchase needs in separate rounds; link every request to one ServiceOrder; retain images, notes and audit history for buyers |
| ServiceProcurementRequestLine | ServicePartRequirement, ServiceProcurementRequestImage and ServicePartProcurementAllocation | Track requested quantity, reference images, part identifiers and supplier-order allocations |
| SupplierPurchase | SupplierPurchaseLine, SupplierPurchaseEvent, ServicePartProcurementAllocation and receipt-linked InventoryMovement rows | Record external source/order/ETA; link one or more queue items; log changes; receive actual quantity and reserve allocations atomically |
| ServicePartRequirement | InventoryReservationLine and one or more ServiceProcurementRequestLine records | Reserve available stock, measure total outstanding need and keep the order awaiting parts until blocking requirements are covered |
| PaymentTransaction | PaymentAllocation | Persist one payment/refund with allocations and enforce aggregate invariants |
| DailyClosing | Closing snapshots | Reconcile one Shop/business date |

## 5. Open decisions

**Decided:** one Company with multiple Shops; Shop-only persistent session; Company-wide employee account/password and MANAGER/TECHNICIAN roles; fresh employee code + password challenge per protected operation; integer Product stock with negative balances allowed; stock deduction at full balance settlement; optional reservations and selected physical returns; inter-Shop transfers; configurable refurb categories, serialized units, one-year commercial guarantee and image uploads; Company VAT enable/disable flag; no second approval for supplier purchases.

**Still open:**

- v1 tender methods are CASH/CARD only; card processor integration is deferred. Manager-only refunds use original tender. Deposit terms and 90-day review/no-auto-forfeit policy are documented in `financial-model.md`.
- Company's legal VAT registration/status and correct tax treatment in Ireland. The software starts with VAT disabled/no VAT amount added, but this is not a legal exemption; review is a production go-live gate.
- Refurbished 12-month commercial guarantee duration is decided; proposed coverage/exclusions are documented and remain subject to owner/legal review. A/B/C condition, inspection and complaint workflow are specified in [Refurbished Condition Grades and Customer Complaint Workflow](refurbished-condition-and-complaints.md).
- Required serial-number/serial-uniqueness rules by category and acquisition/source details for refurbished units.
- Whether supplier AP/settlement, supplier returns or marketplace integrations should be added beyond the initial operational purchase tracker.
- Whether any future Shop requires more than one physical till. Initial baseline is one cash register per Shop/day.
- Credit-sale handover before full instalment settlement remains disabled pending Irish legal/regulatory review; initial mode is LAYAWAY.
- Complete Sale/ServiceOrder transition matrix and production DDL/migrations remain to be reviewed. Images are stored only in PostgreSQL; exact GDPR retention/purge schedule remains pending.

**Out of initial scope by product-owner instruction:** multiple stockrooms/locations within a Shop and reservation-expiry automation. Initial design uses one stock pool per Shop and reservations remain until explicitly consumed or released. Partial sourcing/reservation remains supported where service requirements are fulfilled in batches.


See the [consolidated product-owner decision register](product-owner-decisions.md) for confirmed choices and remaining questions.

## Documents and reporting

The Documents module owns document metadata/history and reprint audit, while business modules own the underlying sales, service, payment, refund, deposit, warranty and closing facts. `GeneratedDocument` stores the immutable render snapshot and template/layout version; `DocumentReprintEvent` records every reprint. Report generation is read-only with respect to business transactions: rendering or reprinting cannot post payment, inventory, refund or cash movements.

The target catalogue and trigger rules are defined in [Reports, Receipts and Document Printing](reports-and-document-printing.md). Operational reports query persisted business ledgers; customer-facing documents use the source transaction's immutable snapshot.

