# Employee Access and Roles — pcHouseStore Web

**Status:** DRAFT v15 — MANAGER full access is confirmed. The TECHNICIAN matrix below is the recommended first-release interpretation of partial access; it is ready for owner review before fine-grained policy freeze. All operations remain scoped to the active Shop and individually attributed.

## 1. Identity and cross-Shop access

- One Company-wide `Employee` identity and password per person. The same employee account works in every Shop belonging to the Company.
- The Shop is authenticated at application entry. `ShopSession` contains only the active Shop and never stores an employee identity.
- Before every protected committed operation, the actor supplies an employee identifier (employee code) and password. The password alone must not be used to identify a person because different people may choose the same password.
- The server verifies the employee password, active status, Company membership and role permission for the requested action. It issues a short-lived, action-scoped, single-use `EmployeeActionAuthorization`.
- The authorization is consumed atomically with the command. A later operation requires another employee challenge; the previous employee is never reused implicitly.

## 2. Roles and proposed first-release permission matrix

Roles are Company-wide and work across all Shops. MANAGER full access is a confirmed requirement. The TECHNICIAN scope is a recommended first-release baseline derived from the service/technical workflows. Permission checks are server-side. The React client may hide unavailable actions for usability, but API authorization is authoritative.

| Capability | `MANAGER` | `TECHNICIAN` |
|---|---:|---:|
| Access every Shop in the Company | Yes | Yes |
| View company/Shop dashboards and financial reports | Yes | No |
| Manage employees, passwords, roles and Shop credentials | Yes | No |
| Manage Company, Shop and application settings, including VAT configuration | Yes | No |
| Manage catalogue, products, services, categories and sale prices | Yes | No |
| Create and edit sales | Yes | No |
| Apply discounts/price overrides | Yes | No |
| Register payments, deposits and instalments | Yes | No |
| Open/close cash day, cash-in/out, safe drops and closing corrections | Yes | No |
| Create, edit and cancel service orders | Yes | Create/update service work; cancellation is Manager-only |
| Read customer contact details required by a service assigned/available to the technician | Yes | Yes, service-context only |
| Register diagnosis, faults, technical notes and test results | Yes | Yes |
| Move service between `CREATED`, `IN_PROGRESS`, `AWAITING_PARTS` and `FIXED` | Yes | Yes |
| Mark customer handover `PICKED`, cancel or financially refund service | Yes | No |
| View product availability in the active Shop | Yes | Yes |
| Reserve/release parts for a service order | Yes | Yes |
| Create/manage service-part procurement requests and upload reference images | Yes | Yes |
| Create/link/update service-part supplier purchases and record their receipts | Yes | Yes; no second manager approval is required |
| Make supplier payments/accounts-payable postings | Yes, if AP module is enabled | No |
| Adjust stock balances, perform write-offs or inter-Shop transfers | Yes | No |
| Inspect refurbished units, record test results/condition and upload photos | Yes | Yes |
| Open a product complaint and perform technical inspection | Yes | Yes |
| Approve complaint remedy, refund, replacement or price reduction | Yes | No |
| Close/reopen complaint cases | Yes | Add technical findings; cannot close the case |

`MANAGER` is the full-access role across all Shops. The v1 proposed `TECHNICIAN` profile is intentionally a technical/operational role: the technician may diagnose and repair, reserve or request parts, capture purchase/receipt details for service parts, and inspect equipment, but cannot perform financial, commercial-pricing, staff-administration or company-configuration actions.

### No second purchase approval

Creating, linking, updating or receiving a supplier purchase for service parts does not require a second manager approval. This does not bypass the role matrix: technicians are explicitly allowed to handle operational service-part procurement. Every action still requires the acting employee's own challenge and audit trail. Refunds, deposit forfeitures and complaint financial remedies are Manager-only. The Manager performs the action directly with a fresh password challenge; no second Manager approver is required. Supplier payments/accounts payable remain Manager-only in the initial model.

### Financial operation rule

- Only `MANAGER` can register or execute full/partial refunds, refund service deposits, apply a forfeiture to a deposit, or approve financial complaint remedies.
- Every such action requires the Manager's own fresh employee password challenge and one-use authorization. There is no amount threshold and no second Manager approval in v1.
- `TECHNICIAN` can record technical findings and request manager review but cannot commit a refund, cash operation or deposit forfeiture.

## 3. Employee password challenge

- UI collects `employee_code` + password. Employee code is unique within the Company.
- Store only a vetted password hash (Argon2id, bcrypt or the established ASP.NET Core Identity hasher); never plaintext or reversible encryption.
- Transmit the password only over TLS. Do not retain it in browser storage, logs, analytics or audit payloads.
- Rate-limit failed challenges and apply account-level lockout/protection without invalidating the shared ShopSession for all employees.
- The authorization binds `ShopSessionId`, `ShopId`, `EmployeeId`, action code, operation-context hash, authentication time, expiry and consumed time.
- Use a one-use compare-and-set/transaction to prevent replay and concurrent double-consumption.

## 4. Authorization acceptance criteria

1. The same employee code/password works in any Shop belonging to the Company.
2. A `ShopSession` never contains `EmployeeId` or an employee role.
3. Each protected operation records the person who entered the credential for that exact action.
4. Under the proposed first-release matrix, a technician can perform permitted service/technical/procurement actions but is denied payments, refunds, deposit forfeitures, cash operations, sales, price edits, staff administration, settings, stock adjustments and Shop transfers.
5. A manager can perform all application actions in every Shop.
6. A previous employee authorization cannot be reused for another action or operation context.
7. A supplier purchase or receipt does not wait for a second manager approval, but still checks the acting role and employee authorization.

See [the consolidated decision register](product-owner-decisions.md).
