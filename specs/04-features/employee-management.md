# Feature: Employee Management

**Status:** PRODUCT-APPROVED REQUIREMENTS BASELINE — 2026-11-20; DIRECT-CREDENTIAL IMPLEMENTATION IN PROGRESS — 2026-11-21  
**Scope:** Company-wide employee accounts, roles, staff administration and employee credential lifecycle.  
**Authority:** This specification supersedes conflicting employee-role, employee-session and employee-action-authorization statements in older architecture/authentication documents. Implementation reconciliation is in progress; the direct-credential request contract and legacy authorization retirement are now being applied.

## 1. Objective and boundaries

Define how a Company creates and administers employee accounts, assigns profiles, manages credentials and grants access across its Shops.

- A Company owns multiple Shops.
- Employee accounts belong to the Company, not to an individual Shop.
- Every employee profile can operate in every Shop belonging to the same Company.
- There are no per-employee Shop assignments or Shop-specific employee permissions in this baseline.
- The active Shop remains the operational context represented by `ShopSession`.
- `ShopSession`, OIDC cookies and Shop access tokens must never contain an employee identity or employee role.
- Employee credentials are challenged separately from the persistent Shop sign-in.

## 2. Profiles and authority

The supported profiles are exactly:

- `EMPLOYEE`
- `MANAGER`
- `ADMIN`

The former `TECHNICIAN` profile is retired and replaced by `EMPLOYEE`. Do not preserve dual-role assignment or permission-union semantics from older drafts.

### 2.1 EMPLOYEE

- Can access all Shops in the Company.
- Can perform only the operations that the relevant feature specification explicitly permits to `EMPLOYEE`.
- Cannot administer employee accounts.

### 2.2 MANAGER

- Can access all Shops in the Company.
- Can administer employee accounts within the limits in Section 3.
- Cannot create, grant or restore the `ADMIN` profile.
- Retains the full application permission set already assigned to the `MANAGER` profile in approved business-feature specifications. The manager's additional employee-administration authority is defined in this document. Any feature-specific exception must be explicit in that feature SPEC.

### 2.3 ADMIN

- Inherits the full `MANAGER` permission set and has full administrative authority across the Company and all its Shops.
- Can create other `ADMIN` accounts.
- Can manage any other employee or administrator, including changing profiles, editing data, activating/deactivating accounts and resetting passwords.
- Multiple `ADMIN` accounts are allowed.
- An `ADMIN` may edit every field of their own account, including their own password and profile, subject to the invariant that at least one active `ADMIN` must remain in the Company.
- No other profile can grant or restore `ADMIN`.

### 2.4 Company-level safety invariants

- A Company must always have at least one active `ADMIN`.
- The system must reject any operation that would deactivate, demote or otherwise leave the Company without an active `ADMIN`.
- A Company must also retain at least one active user with management authority (`MANAGER` or `ADMIN`). A `MANAGER` cannot deactivate/demote themselves or another manager if that would leave no active manager/admin.
- These invariants must be enforced transactionally by the backend, not only by frontend validation.

## 3. Employee administration permissions

Any authenticated Shop session may search the Company-wide employee directory. The directory includes active and inactive employees and exposes only name, email, profile and status. Search matches name or email case-insensitively, and results are ordered alphabetically by employee name; the UI may filter by active/inactive status. Full individual employee profile details and all account mutations require the profile permissions below.

| Operation | EMPLOYEE | MANAGER | ADMIN |
|---|---:|---:|---:|
| Search the Company employee directory (limited directory fields) | Yes, through the authenticated Shop session | Yes | Yes |
| Read an individual employee's full administrative profile | No | Yes | Yes |
| Create `EMPLOYEE` | No | Yes | Yes |
| Create `MANAGER` | No | Yes | Yes |
| Create `ADMIN` | No | No | Yes |
| Edit employee/manager data | No | Yes | Yes |
| Edit another `ADMIN` | No | No | Yes |
| Edit own account data | Through self-service password change only | Yes, including self-demotion | Yes, all fields |
| Activate/deactivate `EMPLOYEE` | No | Yes | Yes |
| Activate/deactivate `MANAGER` | No | Yes, subject to management invariant | Yes |
| Activate/deactivate another `ADMIN` | No | No | Yes, subject to last-active-ADMIN invariant |
| Reset `EMPLOYEE` or `MANAGER` password | No | Yes | Yes |
| Reset another `ADMIN` password | No | No | Yes |
| Change another account's profile between `EMPLOYEE` and `MANAGER` | No | Yes | Yes |
| Grant/revoke `ADMIN` on another account | No | No | Yes |
| Change own profile from `MANAGER` to `EMPLOYEE` | N/A | Yes, subject to management invariant | N/A |
| Promote own `EMPLOYEE` account to `MANAGER` | No | No | ADMIN may change own profile, subject to last-active-ADMIN invariant |
| Change own `ADMIN` profile | No | No | Yes, subject to last-active-ADMIN invariant |

Additional rules:

- A `MANAGER` may promote an `EMPLOYEE` to `MANAGER`, demote a `MANAGER` to `EMPLOYEE`, edit another manager's data, deactivate another manager and reset another manager's password.
- A `MANAGER` may demote their own account to `EMPLOYEE`; they cannot promote themselves back. Another `MANAGER` or an `ADMIN` must perform that promotion.
- An `ADMIN` may perform all employee-administration operations, including operations against another `ADMIN`. An `ADMIN` may also edit their own account completely.
- Physical deletion is not the normal account lifecycle. Use deactivation and preserve employee records referenced by business transactions, orders, payments or historical records. Any future hard-delete capability requires a separate retention/integrity decision.
- The precise operation-to-profile matrix for business features remains owned by each feature SPEC. This document defines account-administration authority, not the permission matrix for sales, services, payments or inventory.

## 4. Registration and employee identity

The following fields are required when creating an employee:

- Name.
- Email.
- Profile (`EMPLOYEE`, `MANAGER` or `ADMIN`).

Other profile fields are optional unless a feature-specific requirement says otherwise.

Validation and identity rules:

- Duplicate employee names are allowed.
- Email is required and unique within the Company.
- Email uniqueness is case-insensitive; normalize surrounding whitespace and use a canonical comparison form.
- The same email may be used in a different Company.
- The immutable account identifier is the stable internal identity. It must not be editable.
- Name and email may be edited by an actor authorized under Section 3.
- Changing an employee's email does not change or invalidate their password and does not force a password change.
- Changing an employee's profile takes effect immediately and does not change or invalidate their password or force a password change.
- A profile edit must not rewrite employee-name snapshots already recorded in business history.

## 5. Employee credential lifecycle

### 5.1 Credential storage and verification

- Reuse the existing `EmployeeCredential` entity and the existing password-reset capability. Do not create a parallel credential repository or reset workflow.
- Store only a vetted password hash. Never store plaintext or reversible password representations.
- Verify the submitted employee password against the employee's own credential; never identify an employee by password alone.
- Validate that the employee exists, is active, belongs to the Company in the active `ShopSession`, has a valid credential and has the profile permission required for the requested operation.
- The password must never be logged, returned by an API after issuance, or written to business history.

### 5.2 Temporary password generation

On employee creation and administrative password reset, generate the temporary password using this exact pattern:

`<normalized-first-name>@pchouse`

Normalization rules:

- Use only the first name.
- Remove diacritics/accents.
- Remove spaces and special characters.
- Preserve the remaining letters/digits.
- Example: `João Pedro` becomes `Joao@pchouse`.
- Two employees with the same normalized first name may receive the same temporary password.

This pattern is explicitly approved as a temporary credential only. It is predictable and must not be treated as a secure permanent password.

### 5.3 One-time display and mandatory change

- Show the generated temporary password to the authorized `MANAGER` or `ADMIN` only once, immediately after successful creation or reset.
- Do not store the plaintext temporary password for later retrieval.
- If the screen is closed or the password is not copied, the manager/admin must perform another reset to generate and display a new temporary password.
- The employee must change the temporary password before performing protected business operations.
- A password reset by a `MANAGER` or `ADMIN` sets the mandatory-change state again.
- A self-service password change requires the employee's current password and the new password.
- There is no employee self-service password recovery by email. Recovery is performed only by a `MANAGER` or `ADMIN` through the existing reset flow.
- Reactivation alone does not reset the password. Preserve the existing credential and any outstanding mandatory-change requirement.

### 5.4 Employee password policy

- The minimum length for employee passwords is **6 characters**.
- Accept passwords of at least 128 characters and support password managers and paste.
- The six-character minimum applies to permanent passwords selected by employees as well as temporary credentials.
- Do not require arbitrary periodic password changes. Require a change after account creation/reset and rotate promptly after suspected compromise or an authorized reset.
- Use the established vetted adaptive password hasher. The temporary password pattern remains exactly as defined above and must be changed before protected business operations.

## 6. Employee operation authorization

- Keep OIDC Authorization Code + PKCE and the persistent Shop authentication/session unchanged.
- For every operation classified as employee-protected by its feature SPEC, require the acting employee's own password at the time of that operation.
- This applies to `EMPLOYEE`, `MANAGER` and `ADMIN`; the acting profile determines whether the requested operation is permitted.
- Do not cache employee authentication or reuse it for another operation.
- Do not add employee identity or role claims to the Shop OIDC token, Shop cookie or `ShopSession`.
- Remove the `EmployeeActionAuthorization` one-use token/entity/endpoint mechanism from the target design. Do not issue or consume a separate employee authorization token.
- The backend validates the submitted credential, active status, Company membership and operation permission at the start of processing the protected command. It must not execute the command before validation succeeds.
- Once the operation has passed this authentication and authorization check, it may complete even if the employee is deactivated while the operation is in progress. Deactivation prevents new protected operations; it does not retroactively cancel an already-authorized in-flight operation.
- The exact mapping of business operation to required profile is specified by each feature SPEC. This document does not invent that matrix.

## 6.1 Implemented API request contract

Every employee-protected write/read uses the authenticated Shop bearer token plus an `employeeChallenge` object in the JSON request body. The object contains the acting employee's `employeeCode` and `password`. The backend validates the employee directly against the existing credential at the start of the operation; it does not issue or consume a separate authorization token.

Example:
```json
{
  "firstName": "Alex",
  "lastName": "Murphy",
  "phone": "+353...",
  "email": null,
  "employeeChallenge": {
    "employeeCode": "MGR001",
    "password": "current-employee-password"
  }
}
```

- `POST /api/v1/employees/queries` remains ShopSession-only for directory search.
- `POST /api/v1/employees/{employeeId}/query` reads an individual administrative profile and requires `employeeChallenge`.
- Create, profile update, status change, role change, credential reset and self-password change include `employeeChallenge` in their respective request bodies.
- Shop access-credential rotation/status changes also use direct employee challenge validation; no `X-Employee-Action-Authorization` header or challenge-token endpoint exists.
- Passwords are transient request data only: never log, persist or return them. All protected operations remain atomic with their business writes.

## 7. Business history, administrative history and logs

- When a protected business operation succeeds, its business `notes` history records the acting employee's name as a snapshot of the name at operation time.
- Later edits to the employee's name must not alter existing history entries.
- Failed employee authentication attempts do not create business `notes` entries.
- Employee administration operations (create, edit, activate, deactivate, profile change and password reset) do not create business `notes` entries.
- Do not introduce a dedicated audit trail/log for employee-administration operations.
- Existing general application/operational logs remain subject to their existing rules; never include passwords, hashes or temporary credentials in logs.

## 8. Deactivation and reactivation

- Deactivation immediately prevents the account from authenticating any new protected operation.
- Since the employee does not own a persistent Shop session, deactivation must not revoke or close the shared `ShopSession` used by the entire Shop.
- An in-flight protected operation that already passed the employee credential, active-status, Company-membership and permission checks may complete after deactivation. Operations that have not yet passed those checks must be rejected.
- Reactivation restores eligibility to authenticate using the existing password.
- Reactivation is immediate and requires no additional approval or password reset. Preserve the existing credential and any mandatory-password-change requirement that was already pending.

## 9. Security and abuse controls

- Do not lock or deactivate an employee because of repeated incorrect password attempts.
- Failed employee authentication attempts do not enter business `notes`.
- Preserve applicable general endpoint/network rate limits, but do not introduce an employee-account lockout or credential-specific cooldown.
- Never return or log submitted passwords, password hashes, temporary passwords or other credential secrets.
- All employee-management authorization checks and Company/last-admin invariants are enforced server-side.

## 10. Acceptance criteria

1. The Company supports exactly the profiles `EMPLOYEE`, `MANAGER` and `ADMIN`; `TECHNICIAN` is not an active target profile.
2. All three profiles can operate in every Shop belonging to the same Company, subject to feature-specific permissions.
3. No employee is assigned to an individual Shop.
4. Only `MANAGER` and `ADMIN` can administer employees, with the limits in Section 3.
5. Only `ADMIN` can create or grant `ADMIN`; multiple active administrators are supported.
6. The Company cannot be left without an active `ADMIN` or without an active user with management authority.
7. Employee name and email are required; email is unique case-insensitively within the Company; duplicate names are allowed. Email or profile edits do not force password changes.
7a. Any authenticated user can search active and inactive employees by name or email; the limited directory shows name, email, profile and status, supports status filtering and sorts by name.
8. Creation/reset generates the approved temporary password, displays it once, and requires a password change before protected business operations.
9. Employee password recovery is administrative only; self-service change requires the current password.
10. Reactivation preserves the existing credential and any pending password-change requirement.
11. Deactivation blocks new protected operations immediately without revoking the shared ShopSession; an operation already authenticated and authorized may complete.
11a. Reactivation is immediate, preserves credentials and any pending mandatory-password-change state, and requires no additional approval.
12. Each protected business operation validates the acting employee's own password afresh; no employee authentication is cached or reused.
13. No employee action-authorization token/entity/endpoint is part of the target design.
14. Successful protected business operations store an immutable employee-name snapshot in `notes`; failed attempts and staff-administration actions do not create such entries.
15. No employee-administration-specific audit log is introduced.
16. Business feature permission matrices remain explicit in their respective feature SPECs.

## 11. Required technical reconciliation before implementation

- Reconcile the current `Employee` / `EmployeeCredential` persistence mapping and add a mandatory-password-change state without creating a parallel credential store.
- **IMPLEMENTED — 2026-11-21:** Retired `EmployeeActionAuthorization` and `EmployeeAdministrationEventRecord` from API writes, EF entities/configurations/DbSets and the model snapshot. Migration `20261121153000_RemoveEmployeeActionAuthorization` drops both legacy tables and reconciles `TECHNICIAN` to `EMPLOYEE`; it has not yet been applied to a database. Applying this migration permanently deletes any rows currently stored in `employee_administration_event`; the approved design intentionally retains no employee-administration audit history.
- **IMPLEMENTED IN MIGRATION — 2026-11-21:** Replace `TECHNICIAN` with `EMPLOYEE`, preserve `MANAGER` precedence where legacy dual assignments exist, and enforce one active profile per employee with a filtered unique index.
- Apply the approved six-character minimum consistently to employee password validation in the API, UI and tests.
- **IMPLEMENTED — 2026-11-21:** Employee administration and Shop credential operations submit a fresh `employeeChallenge` in each protected request body. Added policy and migration-operation tests. CI build/test and database execution remain pending.
- **IMPLEMENTED — 2026-11-21:** The deployment bootstrap now provisions the initial `ADMIN` profile (`BootstrapAdmin`); the legacy `BootstrapManager` operation name remains as a compatibility alias but also creates an `ADMIN`.
- Keep Shop OIDC/session/token identity unchanged.
- Inspect the legacy Java Swing employee screens, controller, DAO, model, password-reset and role checks. Record source references and distinguish confirmed legacy behavior from this approved redesign, per `AGENTS.md`.

## 12. Explicitly out of scope

- The complete business-operation permission matrix for every feature.
- ShopSession timeout/multi-device policy.
- Changes to Shop credential hashing or Shop OIDC protocol.
- New employee-specific persistent login/session.
- A new employee credential repository or parallel password-reset workflow.
- A dedicated employee-administration audit log.
