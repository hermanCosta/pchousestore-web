# Product-owner decision update — employee operation authentication

**Status:** DECIDED  
**Recorded:** 2026-11-19  
**Scope:** Supersedes conflicting employee-role and employee-operation authorization rules in earlier draft specifications. This is a requirements update only; it does not authorize implementation by itself.

## 1. Company and Shop context

- The target model remains one `Company` with multiple `Shops`.
- A `ShopSession` authenticates the active Shop and remains the persistent application session.
- Employee accounts are company-wide. An employee may be associated with multiple Shops within the same Company.
- An active employee may authenticate a protected operation in any Shop belonging to that same Company. The active Shop remains the operational context for the operation.
- The employee does not maintain a separate, persistent application login.

## 2. Employee roles and permissions

- The supported employee profiles are `MANAGER` and `EMPLOYEE`.
- Replace the former `TECHNICIAN` profile with `EMPLOYEE`.
- Permissions are determined directly by the employee profile. There are no per-employee permission overrides or additional individual grants in this version.
- `MANAGER` and `EMPLOYEE` both authenticate in the same way. Their authorization differs only according to the permissions assigned to their profile.
- The exact operation-to-profile permission matrix must be defined by the relevant feature specifications; this update does not invent that matrix.

## 3. Protected and unprotected operations

- The active `ShopSession` is required for application operations.
- Operations classified as unprotected (for example, catalogue consultation and service-order viewing, where the feature specification permits it) do not require an employee password.
- Every protected operation requires the acting employee to enter their own password at the time of that operation.
- This applies equally to `MANAGER` and `EMPLOYEE`.
- Employee authentication is not cached or reused for a later operation. No extra confirmation screen displaying the employee name is required.

## 4. Employee credential validation

- Reuse the existing `EmployeeCredential` entity and the existing password-reset flow. Do not introduce a parallel credential store or a new reset workflow as part of this change.
- Validate that the employee exists, is active, has a valid password, and has the profile permission required for the requested operation.
- A deactivated employee cannot authenticate a protected operation.
- Reactivating an employee preserves their existing credential; no password reset is required solely because of reactivation.
- Do not lock or deactivate an employee account because of consecutive incorrect password attempts.
- Failed employee-authentication attempts are not written to the business `notes` history.
- Preserve applicable endpoint/network abuse protections already specified separately; they must not become an employee-account lockout or credential-specific cooldown.

## 5. Operation audit history

- When a protected operation succeeds, its business history (`notes`) records the employee's name.
- Store a snapshot/copy of the employee name as it was at the time of the operation. Later changes to the employee profile must not rewrite historical entries.
- Do not require the employee ID to appear in the human-readable `notes` text.
- Failed authentication attempts do not create `notes` entries.
- No additional pre-execution employee-name confirmation is required.

## 6. Authorization mechanism redesign

- Keep OpenIddict/OIDC for the Shop sign-in/session and Shop-scoped API access.
- Simplify employee authorization: remove the employee `one-use authorization` token/entity/endpoint flow from the target design.
- For each protected operation, the backend validates the submitted employee password against `EmployeeCredential`, checks active status and profile permissions, and executes the operation only after successful validation.
- Do not issue, persist, transmit, consume, or replay an `EmployeeActionAuthorization` token for this employee-password challenge.
- The employee identity is used to attribute the successful operation and its history entry; it is not added to the persistent Shop session or Shop access token.
- The employee password must never be stored, logged, returned, or included in business history.

## 7. Existing design elements to preserve

- Preserve the Company → multiple Shops relationship and the active-Shop session boundary.
- Preserve `EmployeeCredential` and the already implemented password-reset capability.
- Preserve the existing Shop OIDC Authorization Code + PKCE flow and Shop-only access-token identity.
- This update changes employee roles and per-operation employee authorization only. It does not change unrelated Shop token/session policies, password hashing requirements, or the detailed business rules of individual features.

## 8. Required specification reconciliation

Before implementing affected features, reconcile the following documents with this decision update:

- `specs/04-features/authentication-and-shop-context.md`
- `specs/02-architecture/employee-access-and-roles.md`
- `specs/02-architecture/api-authentication-and-token-security.md`
- `specs/02-architecture/authentication-persistence.md`
- `specs/02-architecture/authentication-flow.mmd`
- `specs/02-architecture/architecture-decisions.md`
- `specs/README.md`

Any older statement requiring `TECHNICIAN`, per-operation one-use employee authorization, employee identity in a persistent login/session, or per-employee permission overrides is superseded by this decision update.
