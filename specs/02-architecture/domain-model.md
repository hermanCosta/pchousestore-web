# Target Domain Model — pcHouseStore Web

**Status:** DRAFT based on ADR-001 through ADR-025 and the consolidated product-owner decision register.  
**Purpose:** Define the conceptual model and domain invariants before implementation and physical DDL approval.

## 1. Domain boundaries

| Module | Responsibility | Main concepts |
|---|---|---|
| Company & Shops | One company profile and branch operations | Company, Shop |
| Shop Access | Shop authentication and shared workstation/session context | ShopAccessCredential, ShopSession |
| Employee Operations | Company-wide employee identity/password and the exact EMPLOYEE, MANAGER and ADMIN profiles across all Shops | Employee, EmployeeCredential |
| People & Customers | Shared company-level customers and contact records | Person, Customer |
| Catalogue | Shared product and service definitions | CatalogItem, Product, Service, ProductCategory |
| Stock | Branch balances, reservations, movements, inter-shop transfers and stock replenishment | ShopInventory, InventoryReservation, InventoryMovement, StockTransfer, StockReturn |
| Parts to Buy | Shop-scoped list combining derived uncovered ServiceOrder demand and manually added parts; Product links or free-text part descriptions with reference URLs | ServicePartRequirement (derived demand), ManualPartToBuy, ManualPartToBuyReference |
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
- Employee accounts are Company-wide and use exactly one profile: `EMPLOYEE`, `MANAGER` or `ADMIN`. `TECHNICIAN` and multi-role assignment/permission-union semantics are retired. `ADMIN` inherits the full `MANAGER` permission set; `EMPLOYEE` performs only actions explicitly granted by each approved feature SPEC. The active Shop still comes only from `ShopSession`.
- `ShopInventory.minimum_quantity` is Shop-specific and drives that Shop's low-stock reporting.
- Before each protected business command, the executing employee supplies their employee code and individual password. The backend resolves the employee and directly validates the credential, active status, Company membership and feature-specific profile permission before executing the operation. Every protected operation requires a fresh challenge; authentication is never cached or reused. No `EmployeeActionAuthorization` token, entity, endpoint or persisted authorization record is part of the target design. Never place `EmployeeId` or employee role in the long-lived ShopSession identity.

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
- Physical stock return is a separate operation for Sales and ServiceOrders. It applies only to explicitly selected, previously consumed product quantities that have actually been physically returned; a refund or ServiceOrder lifecycle status does not automatically create a stock return.
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
- Every `ServiceOrder` belongs to exactly one `Shop`, one customer and exactly one non-null `Device`. It has a globally unique internal ID and a sequential display number unique within its Shop; display numbers are never reused and are allocated atomically. It starts in `CREATED`; booking does not mean repair work has started. An explicit employee-authorized start-work action moves it to `IN_PROGRESS`. Its lifecycle includes `AWAITING_PARTS` when booking/diagnosis (while `CREATED`) or later repair work (`IN_PROGRESS`) reveals a blocking part requirement that is not fully reserved. Receiving/reserving parts never starts work automatically; resumption from `AWAITING_PARTS` is explicit. A required-to-complete product part must be fully reserved before the order can move to `FIXED`. Product/Service lines are locked against addition, edit, removal and repricing after the first stock reservation or first recorded payment, whichever occurs first.
- When a `NOT_FIXED` ServiceOrder is physically handed over, its lifecycle status changes to `PICKED` and its technical outcome `NOT_FIXED` remains independently persisted and visible; `PICKED` represents handover, not payment completion.
- A `FIXED` ServiceOrder may return to `IN_PROGRESS` only while its balance remains outstanding; retain its stock reservations and do not reverse financial or inventory postings. Full settlement consumes the reserved parts and closes this direct-reversal path. Subsequent rework requires a separate corrective workflow.
- Each successfully committed action in the ServiceOrder context appends one immutable, chronological `BusinessNote` attributed to the employee and containing the action, time and concise result/reference. This human-readable timeline complements (and never replaces) the immutable `ServiceOrderEvent` and financial ledger. A Note and its originating action commit atomically; failed/rolled-back commands create no Note.
- Service-order creation, status transitions, reservation and payment require a fresh employee code/password challenge validated directly by the backend. `ServiceOrderEvent` records committed ServiceOrder changes; the event actor is resolved server-side, not supplied by the browser.

### Service-part requirements and Parts to Buy view

- Each Product line that requires parts has a durable `ServicePartRequirement` linked to its `ServiceOrderItem`; only Product catalogue items can create requirements. Required quantities are positive whole numbers.
- The Shop has a **Parts to Buy** list, independent of individual ServiceOrder screens, with two visibly distinct sources: calculated ServiceOrder demand and manual entries. Keep the sources in separate sections or clearly source-labeled rows; never combine manual quantities into calculated ServiceOrder demand totals. The calculated section is a derived projection of each ServiceOrder requirement's uncovered quantity after active stock reservations and current Shop stock availability are accounted for. The manual section contains persisted reminders for additional parts not planned on a ServiceOrder. Provide one Shop-scoped search across both sections: match Product names for calculated demand and Product-backed manual entries, and match free-text descriptions for manual entries. Update results automatically as the user types, without requiring Enter or a separate submit action. Search is case-insensitive and supports partial matches within Product names and free-text descriptions (for example, `mem` matches `Memória RAM`). When a search returns no matches, display the empty-state message `No parts found`. If both sources contain no items, display `No parts to buy yet` and keep the manual-entry action available. If only one source is empty, retain its section and show a concise `No items` message within that section. Search results retain their source identity and never combine quantities across sources.
- The automatically derived portion groups uncovered quantities by Product and provides a breakdown of the originating ServiceOrders and their individual quantities. Manual entries may reference the same Product or repeat the same free-text description; keep each manual entry independent with its own quantity and links, and do not automatically merge or deduplicate manual entries. Manual entries are Shop-scoped and may reference an existing Product or hold a free-text description for a part not yet in the catalogue. During editing, the Product reference may be changed or the entry may switch between Product-backed and free-text modes. Their quantity defaults to `1` and may be changed to any positive whole number.
- `EMPLOYEE`, `MANAGER` and `ADMIN` may add, edit and remove manual entries within the active Shop. Edits to Product reference, description, quantity and applicable reference URLs are audited with timestamp and acting-employee attribution. Each mutation uses the standard fresh employee code/password challenge; the backend resolves the employee and derives Shop scope from ShopSession. Removal permanently deletes the manual entry and its entry-owned reference URLs; no removal history or restoration feature is required.
- The list is Shop-scoped. Existing Products display all external reference links maintained centrally in the Product catalogue. Free-text manual entries may store multiple external reference URLs directly on the entry; the operator is responsible for entering and removing them. Do not check URL availability or reachability. Switching to a Product-backed entry uses the newly selected Product's catalogue links; switching to free text requires a description and uses links maintained on that manual entry. A manual entry can be permanently deleted; deletion affects only that entry and its entry-owned reference URLs, and never modifies ServiceOrder requirements, events or Notes. These links are informational only. An unavailable or unreachable reference URL must not hide or disable its associated part entry; the user can edit or remove the URL. URLs are simple operator-managed fields. The operator is responsible for entering and removing links; do not add URL availability checks, reachability checks or special URL validation rules. There is no supplier, buyer assignment, priority, purchase status, ETA, tracking or receipt workflow.
- As stock is replenished through the Stock module and reserved against ServiceOrder requirements, the derived portion recalculates automatically. Partial reservations reduce uncovered quantity; the ServiceOrder remains `AWAITING_PARTS` while any blocking requirement is not fully reserved. Manual entries are maintained independently and do not create ServiceOrder requirements or stock reservations. A manual entry remains visible until the operator explicitly deletes it, regardless of stock availability or ServiceOrder reservations. Stock changes recalculate only the automatic ServiceOrder demand. Clearly identify manual entries as reminders and exclude their quantities from calculated ServiceOrder demand totals. Deleting one permanently removes only that manual entry and its entry-owned URLs, without changing ServiceOrder data or history.
- A stock shortage does not create a purchase request. The business purchases parts outside the application using any preferred source. Product external reference links are informational only and do not record supplier or purchase activity. The application records only the stock changes needed to keep inventory and ServiceOrder reservations accurate.
- When every blocking requirement for an order is fully reserved, an employee-authorized `ResumeServiceOrder` command may move it from `AWAITING_PARTS` to `IN_PROGRESS`. Partial reservations are retained but never satisfy the resume guard.
- After repair, `FIXED` retains reservations. While its balance remains outstanding, it may return to `IN_PROGRESS` without releasing reservations or reversing financial/inventory postings. Full settlement consumes reserved parts; later rework requires a separate corrective workflow. Cancelling before consumption releases reservations.

### Payments, service deposits, instalments and refunds

- `PaymentTransaction` records one financial event and one authoritative amount; `PaymentAllocation` splits that amount across configurable tender methods.
- A service booking may create a `ServiceDeposit` and record its receipt immediately. Deposit purpose distinguishes parts pre-order, booking advance, security deposit and other reasons.
- A deposit remains held until explicitly applied to the ServiceOrder, refunded or forfeited. A security deposit is not automatically revenue or invoice payment. `DepositApplication` is non-cash credit against the order and is separately audited.
- Refurbished sales can have a weekly `InstallmentPlan` with dated `InstallmentSchedule` rows and `InstallmentSettlement` allocations. Layaway is the default; early handover / credit sale remains gated on Irish legal/compliance review.
- Refunds can be partial or total. Each refund is a positive-magnitude `PaymentTransaction` linked to the original settled payment; repeated partial refunds cannot cumulatively exceed the unrefunded amount.
- Optional `PaymentRefundLine` records attribute a refund to specific sale/service lines. Monetary refund remains separate from physical `StockReturn`.


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
4. Each protected committed action requires a fresh employee code/password challenge; the backend validates active status, Company membership and feature-specific profile permission before execution. No employee action-authorization token/entity/endpoint is used.
5. An employee can operate in any Shop belonging to the employee's Company if active and permitted by their single Company-wide `EMPLOYEE`, `MANAGER` or `ADMIN` profile; the same password works across all Shops. `ADMIN` inherits `MANAGER` permissions.
6. Employee attribution on an operational record is resolved by the backend from the validated challenge; never accept actor identity from the browser.
8. Only `PRODUCT` entries can have stock, reservation, transfer, return or movement records. `SERVICE` entries cannot.
9. Product quantities are integers across stock balances, reservations, stock returns, transfers and product sale/order lines. Fractional product quantities are rejected.
10. For quantity-tracked products, `ShopInventory` is unique on `(ShopId, ProductId)`; `quantity_on_hand` is signed and may be negative, `quantity_reserved` is non-negative, and `available_quantity = quantity_on_hand - quantity_reserved` may be negative. Reservations are created only from positive available stock; negative deficits are visible and auditable.
11. For serialized refurbished products, one `RefurbishedUnit` represents one physical unit; serialized sale/reservation/return/transfer quantity equals one.
12. An active reservation reduces availability but not on-hand. Release restores availability only. Consumption reduces both on-hand and reserved quantity; unreserved payment deduction reduces available/on-hand stock.
13. Sale stock is consumed when settled payments reduce the balance to zero. Service-order `FIXED` preserves reservations; full settlement consumes stock regardless of the later pickup status. Partial payment does not consume stock. Quantity-tracked stock may become negative at settlement.
14. Cancellation before consumption releases reservations without stock-in movement. Physical stock returns are separate from refunds and lifecycle statuses; only explicitly selected, previously consumed quantities that are physically received may enter the stock-return workflow.
15. Financial refunds do not imply physical stock return. Each physical return is separately selected, authorized, recorded and ledgered.
16. Stock transfer dispatch decreases source stock; receipt increases destination stock. In-transit stock is unavailable; source and destination belong to the same Company.
17. Inventory balance/unit-state update and corresponding InventoryMovement insert are atomic and idempotent.
18. A PaymentTransaction references exactly one of Sale or ServiceOrder (XOR), from the same Shop.
19. `DailyClosing` is unique on `(ShopId, BusinessDate)` and reconciles only that Shop.
20. A service order may enter `AWAITING_PARTS` from `CREATED` or `IN_PROGRESS` only while at least one blocking `ServicePartRequirement` is not fully reserved.
21. A stock entry increases Shop availability; any reservation against service requirements is applied atomically and reflected in the Parts to Buy view.
22. The Parts to Buy list combines derived uncovered Product-part requirements with persisted Shop-scoped manual entries. Manual entries may reference a Product or use a free-text part description; multiple informational URLs are available from the Product catalogue or directly on a free-text entry. Neither source creates or tracks external purchase records.

## 4. Aggregate boundaries and transactional use cases

| Aggregate root | Child records | Atomic operations |
|---|---|---|
| Company | Shop, shared master data | Provision Company; create/deactivate Shop |
| Shop | ShopAccessCredential, ShopSession | Authenticate Shop; create/revoke ShopSession |
| Employee credential challenge | One protected business command | Resolve employee and validate credential, active status, Company membership and feature-specific permission before persisting the command |
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
| ServicePartRequirement | ServiceOrderItem and InventoryReservationLine | Track required Product quantities, calculate uncovered demand for the Parts to Buy list and keep the order awaiting parts until blocking requirements are fully reserved |
| ManualPartToBuy | Shop, optional Product and ManualPartToBuyReference | Maintain an additional Shop-scoped part reminder independent of ServiceOrders; Product-backed entries reuse catalogue references, while free-text entries own multiple informational URLs. Removal permanently deletes the manual entry and its entry-owned reference URLs; no removal history or restoration feature is required |
| PaymentTransaction | PaymentAllocation | Persist one payment/refund with allocations and enforce aggregate invariants |
| DailyClosing | Closing snapshots | Reconcile one Shop/business date |

## 5. Open decisions

**Decided:** one Company with multiple Shops; Shop-only persistent session; Company-wide employee account/password and exactly EMPLOYEE/MANAGER/ADMIN profiles (ADMIN inherits MANAGER; no multi-role union); fresh employee code + password validation per protected operation without EmployeeActionAuthorization; integer Product stock with negative balances allowed; stock deduction at full balance settlement; optional reservations and selected physical returns; inter-Shop transfers; configurable refurb categories, serialized units, one-year commercial guarantee and image uploads; Company VAT enable/disable flag; .

**Still open:**

- v1 tender methods are CASH/CARD only; card processor integration is deferred. Manager-only refunds use original tender. Deposit terms and 90-day review/no-auto-forfeit policy are documented in `financial-model.md`.
- Company's legal VAT registration/status and correct tax treatment in Ireland. The software starts with VAT disabled/no VAT amount added, but this is not a legal exemption; review is a production go-live gate.
- Refurbished 12-month commercial guarantee duration is decided; proposed coverage/exclusions are documented and remain subject to owner/legal review. A/B/C condition, inspection and complaint workflow are specified in [Refurbished Condition Grades and Customer Complaint Workflow](refurbished-condition-and-complaints.md).
- Required serial-number/serial-uniqueness rules by category and acquisition/source details for refurbished units.

- Whether any future Shop requires more than one physical till. Initial baseline is one cash register per Shop/day.
- Credit-sale handover before full instalment settlement remains disabled pending Irish legal/regulatory review; initial mode is LAYAWAY.
- Complete Sale/ServiceOrder transition matrix and production DDL/migrations remain to be reviewed. Images are stored only in PostgreSQL; exact GDPR retention/purge schedule remains pending.

**Out of initial scope by product-owner instruction:** multiple stockrooms/locations within a Shop and reservation-expiry automation. Initial design uses one stock pool per Shop and reservations remain until explicitly consumed or released. Partial sourcing/reservation remains supported where service requirements are fulfilled in batches.


See the [consolidated product-owner decision register](product-owner-decisions.md) for confirmed choices and remaining questions.

## Documents and reporting

The Documents module owns document metadata/history and reprint audit, while business modules own the underlying sales, service, payment, refund, deposit, warranty and closing facts. `GeneratedDocument` stores the immutable render snapshot and template/layout version; `DocumentReprintEvent` records every reprint. Report generation is read-only with respect to business transactions: rendering or reprinting cannot post payment, inventory, refund or cash movements.

The target catalogue and trigger rules are defined in [Reports, Receipts and Document Printing](reports-and-document-printing.md). Operational reports query persisted business ledgers; customer-facing documents use the source transaction's immutable snapshot.

