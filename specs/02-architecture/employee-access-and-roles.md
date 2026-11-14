# Employee Access and Roles — pcHouseStore Web

**Status:** PRODUCT-APPROVED v1 baseline (updated 2026-11-13) — MANAGER full access and the TECHNICIAN matrix below are approved. The 2026-11-13 product-owner updates supersede earlier Technician restrictions for customer, catalogue, Product, Fault and ServiceOrder work. An employee may hold both roles; effective permissions are the union of assigned roles, so MANAGER grants the full capability set. Refunds remain Manager-only. All operations remain scoped to the active Shop and individually attributed.

## 1. Identity and cross-Shop access

- One Company-wide `Employee` identity and password per person. The same employee account works in every Shop belonging to the Company.
- The Shop is authenticated at application entry. `ShopSession` contains only the active Shop and never stores an employee identity.
- For operations that the approved feature SPEC marks employee-protected, the actor supplies an employee identifier (employee code) and password. The password alone must not be used to identify a person because different people may choose the same password.
- The server verifies the employee password, active status, Company membership and role permission for that action. It issues a short-lived, action-scoped, single-use `EmployeeActionAuthorization`.
- The authorization is consumed atomically with the command. A later employee-protected operation requires another employee challenge; the previous employee is never reused implicitly.
- Customer and shared-catalogue CRUD is Shop-session protected but does not require an individual employee challenge, as approved in CAT-01. Its audit actor is the active `ShopSession`.

## 2. Approved v1 permission matrix

Roles are Company-wide and work across all Shops. MANAGER full access is a confirmed requirement. The TECHNICIAN scope is the approved first-release baseline derived from the service/technical workflows. Permission checks are server-side. The React client may hide unavailable actions for usability, but API authorization is authoritative.

The Product Owner approved the initial action matrix on 2026-11-10 and updated Technician permissions on 2026-11-13. An employee may hold both `MANAGER` and `TECHNICIAN`; effective permissions are the union of assigned roles. Since MANAGER grants full access, it takes precedence wherever the TECHNICIAN column says No. There is no deny-overrides rule.

| Capability | `MANAGER` | `TECHNICIAN` |
|---|---:|---:|
| Access every Shop in the Company | Yes | Yes |
| View company/Shop dashboards and financial reports | Yes | No |
| Manage employees, passwords, roles and Shop credentials | Yes | No |
| Manage Company, Shop and application settings, including VAT configuration | Yes | No |
| Create and edit customer records | Yes | Yes |
| Create and edit Products | Yes | Yes |
| Create and edit Fault catalogue entries | Yes | Yes |
| Manage catalogue, Products, Services, categories and sale prices | Yes | Yes |
| Create and edit sales | Yes | No |
| Apply discounts/price overrides | Yes | No |
| Register payments, deposits and instalments | Yes | No |
| Open/close cash day, cash-in/out, safe drops and closing corrections | Yes | No |
| Create, edit and cancel service orders | Yes | Create and edit service orders; cancellation is Manager-only |
| Read Company customer directory and customer contact details | Yes | Yes, Company scope |
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
| Execute refunds, including refunds related to sales, service deposits or complaint remedies | Yes | No |
| Approve non-refund complaint remedy, replacement or price reduction | Yes | No |
| Close/reopen complaint cases | Yes | Add technical findings; cannot close the case |

`MANAGER` is the full-access role across all Shops. `TECHNICIAN` may create/edit customer records, Products, Services, categories, sale prices and Fault catalogue entries, and create/edit service orders, in addition to the technical/operational work listed above. Refunds remain Manager-only.

### No second purchase approval

Creating, linking, updating or receiving a supplier purchase for service parts does not require a second manager approval. This does not bypass the role matrix: technicians are explicitly allowed to handle operational service-part procurement. Every action still requires the acting employee's own challenge and audit trail. Refunds, deposit forfeitures and complaint financial remedies are Manager-only. The Manager performs the action directly with a fresh password challenge; no second Manager approver is required. Supplier payments/accounts payable remain Manager-only in the initial model.

### Financial operation rule

- Only `MANAGER` can register or execute full/partial refunds, refund service deposits, apply a forfeiture to a deposit, or approve financial complaint remedies. This remains true after the 2026-11-13 Technician permission update.
- Every such action requires the Manager's own fresh employee password challenge and one-use authorization. There is no amount threshold and no second Manager approval in v1.
- `TECHNICIAN` can record technical findings and request manager review but cannot commit a refund, cash operation or deposit forfeiture.

## 3. Employee password challenge and credential operations

- UI collects `employee_code` + password. Employee code is unique within the Company.
- Store only a vetted password hash (Argon2id, bcrypt or the established ASP.NET Core Identity hasher); never plaintext or reversible encryption.
- Transmit the password only over TLS. Do not retain it in browser storage, logs, analytics or audit payloads.
- Do not lock, disable or suspend a Shop or Employee credential because of failed password attempts. Apply request-rate limits at authentication endpoints by source/network and endpoint, with generic authentication errors. Credential-specific failures may be recorded for security monitoring but must not cause a credential-specific waiting period, rejection window or disabled state. Throttling must not persist an account lock state or invalidate a shared ShopSession.
- A Manager may provision, disable and reset a Shop credential, and may provision, disable and reset an Employee credential. Employee reset requires the Manager's fresh action authorization and forces a password change on next use. An employee may change their own password after providing the current password.
- The initial Shop credential and recovery when no Manager can authenticate use a deployment-controlled, identity-verified Company-owner/operator bootstrap procedure outside the public Shop sign-in endpoint. Record and audit each bootstrap/recovery, then require credential rotation before normal use.
- Use a minimum password length of 12 characters, accept at least 128 characters, allow password managers/paste, and do not require arbitrary periodic changes. Rotate promptly after suspected compromise or an authorized reset. Hash using ASP.NET Core Identity's established hasher (or an approved equivalent) and support rehash-on-success when parameters are upgraded.
- The authorization binds `ShopSessionId`, `ShopId`, `EmployeeId`, action code, operation-context hash, authentication time, expiry and consumed time.
- Use a one-use compare-and-set/transaction to prevent replay and concurrent double-consumption.

## 4. Authorization acceptance criteria

1. The same employee code/password works in any Shop belonging to the Company.
2. A `ShopSession` never contains `EmployeeId` or an employee role.
3. Each protected operation records the person who entered the credential for that exact action.
4. Under the approved first-release matrix, a technician can perform permitted service/technical/procurement and catalogue actions, including price edits, but is denied payments, refunds, deposit forfeitures, cash operations, sales, staff administration, settings, stock adjustments and Shop transfers unless that employee also has MANAGER, in which case MANAGER permissions apply.
5. A manager can perform all application actions in every Shop.
6. A previous employee authorization cannot be reused for another action or operation context.
7. A supplier purchase or receipt does not wait for a second manager approval, but still checks the acting role and employee authorization.
8. Repeated failed attempts do not lock or disable a credential; authentication endpoints apply request throttling without account lock state.

See [the consolidated decision register](product-owner-decisions.md).
