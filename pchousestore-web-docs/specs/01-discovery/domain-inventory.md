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
