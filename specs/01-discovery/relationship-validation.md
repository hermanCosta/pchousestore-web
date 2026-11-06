# Relationship Validation Matrix

**Status:** DISCOVERY — mappings below are what the Java annotations currently say, not final target cardinalities. The target organizational model is one Company with multiple Shops; operational records are Shop-scoped and employee identity is captured per protected action.

| Entity | Current Java mapping | Candidate interpretation to validate |
|---|---|---|
| Customer → Person | ManyToOne | Confirm if a person can have more than one customer account |
| Customer → Company | ManyToOne | Likely many customers belong to one company |
| Employee → Person | ManyToOne | Confirm uniqueness of person-to-employee |
| ProductService → Company | OneToOne | Likely ManyToOne: one company has many catalog items |
| Sale → Customer/Employee/Company | ManyToOne | Conceptually likely many transactions per customer, employee and company |
| ServiceOrder → Customer/Device/Employee/Company | OneToOne | Likely many orders per customer/device/employee/company; verify constraints |
| SalePayment → Employee/Sale | OneToOne | Payment history and combined tender suggest many payment rows per sale and many rows per employee |
| ServiceOrderPayment → Employee | OneToOne | Likely many payment rows per employee |
| ServiceOrderPayment → ServiceOrder | ManyToOne | Already allows several payments per order |
| DailyClosing → Employee | OneToOne | Likely one employee can close many business dates |
| Refund → Company/Employee | OneToOne | Likely many refunds per company and employee |
| Refund → Sale/ServiceOrder | ManyToOne | Verify that one refund target is exclusive and whether partial refunds are possible |
| Deposit → Sale/ServiceOrder/Employee/payment | ManyToOne | Enforce exactly one parent sale or service order |
| OrderNote → Sale/ServiceOrder/Employee | ManyToOne | Enforce exactly one parent transaction |
| SaleProdServ → Sale/ProductService | ManyToOne | Associative line-item pattern |
| ServiceOrderProdServ → ServiceOrder/ProductService | ManyToOne | Associative line-item pattern |
| ServiceOrderFault → ServiceOrder/Fault | ManyToOne | Associative fault pattern |
| Refurb → Company | ManyToOne | One company has many refurbished items |
| RefurbSale → Sale/Refurb | ManyToOne | Associative sale line-item pattern |

## Validation source material

Compare the table above with:

- Actual MySQL DDL (foreign keys, indexes, unique constraints and nullability).
- Every DAO query and persistence operation.
- Screens supporting repeated payments and histories.
- **RESOLVED FOR TARGET:** one Company has multiple Shops. Employee identity and MANAGER/TECHNICIAN roles are Company-wide; no Employee-to-Shop membership/assignment exists in the target model. Validate Shop foreign keys and one-time employee action authorization in the active Shop context.
- Required delete/archive behavior.

Do not configure EF Core relationships based only on the existing annotations. A JPA `OneToOne` can imply a unique FK, which would conflict with workflows that persist multiple rows against the same parent.

## Target modeling principles

- Use explicit foreign keys and intentional delete behaviors.
- Use database uniqueness only when the business rule is confirmed.
- Model line items and payment events as first-class records.
- Use decimal with explicit precision/scale for all money fields.
- Add concurrency controls for stock and financial state changes.
- Avoid cascade deletion of financial history unless an approved retention rule requires it.

## Target decision impact

The target does not inherit the legacy JPA cardinalities directly. It uses one Company with multiple Shops, shared Company-level Employee and Customer records, EmployeeRoleAssignment for Company-wide MANAGER/TECHNICIAN roles, ShopSession for the persistent branch context, EmployeeActionAuthorization for transaction-level employee attribution, PaymentTransaction + PaymentAllocation for financial events, and a Shop/date DailyClosing with unique `(ShopId, BusinessDate)`. See [Architecture Decision Records](../02-architecture/architecture-decisions.md) and [Relational Database Model](../02-architecture/database-model.md).
