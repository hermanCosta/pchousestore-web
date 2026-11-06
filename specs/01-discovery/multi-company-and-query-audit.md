# Company Scoping and Query Audit

**Status:** LEGACY QUERY AUDIT — findings below describe the old Company scoping/query behavior. The initial target product is one Company with multiple Shops; see the current Architecture Decision Records.

## Why this matters

The legacy code associates many records with `Company` and stores the active company in `CommonSetting.COMPANY`. This suggests company-scoped data. However, several queries omit the company filter or apply it inconsistently. This document remains a historical audit of legacy query behavior. In the target application, Company is the single legal entity and Shop is the active operational context; ShopSession governs branch-scoped operations.

## Query audit

| Area | Source | Finding | Risk |
|---|---|---|---|
| Product/service search | `dao/ProductServiceDAO.java` — `searchProdServDAO` | `OR` predicates are not grouped before `AND ps.company = :pCompany` | Search may return rows outside the active company |
| Product/service autocomplete | `dao/ProductServiceDAO.java` — `orderSearchProdServDAO` | No company predicate | Cross-company catalog exposure if shared DB is multi-company |
| Refurb search | `dao/RefurbDAO.java` — `searchRefurbDAO` | No company predicate | Cross-company exposure |
| Customer list | `dao/CustomerDAO.java` — `getAllCustomerDAO` | No company predicate | Cross-company customer visibility |
| Customer phone lookup | `dao/CustomerDAO.java` — `searchCustomerByContactNoDAO` | No company predicate; query references `Person.id`, while the entity property is `idPerson` | Cross-company lookup and likely invalid JPQL property path |
| Employee list | `dao/EmployeeDAO.java` — `getAllEmployeeDAO` | No company predicate; Employee has no Company relation | Tenant ownership/authorization model is unclear |
| Employee password lookup | `dao/EmployeeDAO.java` — `getEmployeeByPasswordDAO` | Searches by password only | Ambiguous employee resolution and weak authorization boundary |
| Refund list | `dao/RefundDAO.java` — `getAllRefundDAO` | Query appears malformed: `SELECT FROM Refund s` | Refund listing may fail |
| Daily close lookup | `dao/DailyClosingDAO.java` | Checks date only; DailyClosing has no Company field | Cannot safely distinguish multiple companies closing on the same date |

## Queries already visibly scoped

The following methods include a company predicate in their inspected query:

- `SaleDAO.getAllSaleDAO` and `searchSaleDAO`
- `ServiceOrderDAO.getAllOrderDAO` and `searchOrderDAO`
- `ProductServiceDAO.getAllProductsDAO` and `getMinStockProductDAO`
- `RefurbDAO.getAllRefurbProdDAO`, `getAllCustomRefurbProdDAO`, `getAllRefurbByCategoryDAO`, `searchRefurbByCategoryDAO` and `searchCustomRefurbDAO`
- `CashInRegistryDAO` and `CashOutRegistryDAO` date-range queries

This is not a complete proof that every path is safe; all DAO methods and API use cases must be reviewed.

## Target requirements (superseding legacy Company-membership assumptions)

1. **DECIDED:** one Company with multiple Shop branches. Do not implement CompanyMembership or multi-company tenant selection in the initial product.
2. Authenticate the active Shop at application entry and establish a ShopSession scoped to exactly one Shop.
3. Apply Shop scope to every operational read, update, delete, lookup-by-ID, search, autocomplete, report and background operation.
4. Company-level master data (customers, employees, catalogue, products/services, devices and faults) is shared across Shops by default.
5. Sales, service orders, inventory, payments, cash movements and other operational records must carry Shop context.
6. Do not attribute operations to the last employee who authenticated. Require the executing employee's own credential for each protected committed operation.
7. Create and consume a short-lived, single-use EmployeeActionAuthorization scoped to ShopSession, Shop and action code.
8. Add integration tests for cross-Shop operational reads/mutations, Company-wide employee role assignments and replay of consumed/wrong-shop/wrong-action authorization.
9. Keep the historical DAO query findings above as regression/security cases; re-evaluate the target implementation against Shop scope and shared Company master data.

## Recorded target decision

The earlier multi-company interpretation was corrected by the product owner: the business has one Company and multiple branches. The persistent session belongs to the Shop, while each employee authenticates only for the operation they perform. The proposed target model is documented in [ADR-001 and ADR-002](../02-architecture/architecture-decisions.md), [Target Domain Model](../02-architecture/domain-model.md) and [Relational Database Model](../02-architecture/database-model.md).
