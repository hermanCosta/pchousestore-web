# Domain Inventory — Initial

**Status:** DISCOVERY

## Model classes observed (24)

- CashInRegistry
- CashOutRegistry
- Company
- Customer
- DailyClosing
- Deposit
- Device
- Employee
- Fault
- OrderNote
- PaymentSummary
- Person
- ProductService
- Refund
- Refurb
- RefurbSale
- Sale
- SalePayment
- SalePaymentSummary
- SaleProdServ
- ServiceOrder
- ServiceOrderFault
- ServiceOrderPayment
- ServiceOrderProdServ

## Controllers observed (21)

- CashInRegistryController
- CashOutRegistryController
- ClosingTillController
- CompanyController
- CustomerController
- DepositController
- DeviceController
- EmployeeController
- FaultController
- OrderNoteController
- PersonController
- ProductServiceController
- RefundController
- RefurbController
- SaleController
- SalePaymentController
- SaleProdServController
- ServiceOrderController
- ServiceOrderFaultController
- ServiceOrderPaymentController
- ServiceOrderProdServController

## DAOs observed (21)

- CashInRegistryDAO
- CashOutRegistryDAO
- CompanyDAO
- CustomerDAO
- DailyClosingDAO
- DepositDAO
- DeviceDAO
- EmployeeDAO
- FaultDAO
- OrderNoteDAO
- OrderProdServDAO
- PersonDAO
- ProductServiceDAO
- RefundDAO
- RefurbDAO
- SaleDAO
- SalePaymentDAO
- SaleProdServDAO
- ServiceOrderDAO
- ServiceOrderFaultDAO
- ServiceOrderPaymentDAO

## Relationship samples verified

### Customer

Source: `models/Customer.java`

- Customer references Person using ManyToOne.
- Customer references Company using ManyToOne.

### Sale

Source: `models/Sale.java`

- Sale references Customer, Employee and Company using ManyToOne.
- Sale stores total and remaining as Java double/Double.
- Sale status and sale type are persisted as enum strings.

### ServiceOrder

Source: `models/ServiceOrder.java`

- ServiceOrder references Customer, Device, Employee and Company using OneToOne.
- It stores total and due as Java double.
- It stores created, finished and picked timestamps.
- It includes status, badSector and note fields.

These are Java annotations as currently written. Do not assume they represent the intended cardinality. Compare with actual table constraints, DAO usage and workflows before defining EF Core relationships.

## Enum evidence

`enums/OrderStatus.java` contains:

- IN_PROGRESS
- FIXED
- NOT_FIXED
- FINISHED
- PICKED
- REFUNDED
- CANCELLED
- CREATED

Other enums found: PayMethod, PaymentType and SaleType. Their complete values and usage must be inventoried.

## Target-model mapping (DRAFT)

The legacy entities will not be copied one-to-one into the new schema. Current target mappings include:

- `Company`, `Employee`, `Person` and authentication become one `Company`, multiple `Shop` branches, company-level `Employee` with password hash, `EmployeeRoleAssignment` (Company-wide MANAGER/TECHNICIAN roles), `ShopAccessCredential`, `ShopSession` and one-time `EmployeeActionAuthorization`. The persistent session is Shop-scoped, not employee-scoped.
- `ProductService` becomes a common `CatalogItem` plus a `Product` or `Service` subtype. Only `Product` can be referenced by `InventoryMovement`.
- `Sale`, `SaleProdServ`, `ServiceOrder` and `ServiceOrderProdServ` become `Sale`, `SaleItem`, `ServiceOrder` and `ServiceOrderItem` with immutable line-description/price snapshots.
- `SalePayment` and `ServiceOrderPayment` converge into `PaymentTransaction` plus `PaymentAllocation`; deposit/refund semantics become transaction kinds.
- `DailyClosing`, `CashInRegistry` and `CashOutRegistry` become `DailyClosing` and shop-scoped `CashMovement`. The selected target behavior is one independent close per Shop/business date; each close aggregates only that Shop’s cash/payment activity.
- `Refurb` becomes a proposed `RefurbishedProduct` extension of `Product`; this remains subject to validating quantity and serial-number behavior.

See the [target domain model](../02-architecture/domain-model.md) and [DER](../02-architecture/er-diagram.svg).
