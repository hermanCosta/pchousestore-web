# Feature: Shop authentication and API access

**Status:** APPROVED — product and technical design validated 2026-11-10; authentication foundation implemented; production and integration validation pending
**Owner:** Product owner / technical lead  
**Decision basis:** ADR-002, ADR-032, ADR-034 and [API Authentication and Token Security](../02-architecture/api-authentication-and-token-security.md)

## 1. Objective and scope

Define Shop sign-in, OAuth/OIDC token issuance, API access validation and the boundary between the persistent Shop session and employee authorization for protected operations.

This SPEC covers the cross-cutting authentication contract and foundation only. It does not implement sales, service, inventory, payment, employee administration or other business workflows. Their protected commands must consume an employee action authorization as specified here, but each workflow and its action permissions require its own approved SPEC.

## 2. Legacy references

| Type | Path | Class/method | Evidence |
|---|---|---|---|
| Swing view | `src/main/java/com/pchouse/pchousestoremvn/views/LoginView.java` | `initialize`, `onSignIn` | CONFIRMED — asks for username/password, uppercases the username, calls `CompanyController.getCompany`; logs username, password length and encoded password. |
| Controller | `src/main/java/com/pchouse/pchousestoremvn/controllers/CompanyController.java` | `getCompany` | CONFIRMED — delegates company sign-in to `CompanyDAO.getCompanyDAO`. |
| DAO | `src/main/java/com/pchouse/pchousestoremvn/dao/CompanyDAO.java` | `getAllCompaniesDAO`, `getCompanyDAO` | CONFIRMED — reads Company records; `getCompanyDAO` matches Company name and legacy password. |
| Model / persistence mapping | `src/main/java/com/pchouse/pchousestoremvn/models/Company.java` | `name`, `address`, `contactOne`, `contactTwo`, `email`, `password`, `@Column` mappings | CONFIRMED — name/address/contactOne/password required; contactTwo/email optional. Company email has a JPA unique annotation that conflicts with the inspected DDL. The target does not retain the Company password. |
| Shared authentication helper | `src/main/java/com/pchouse/pchousestoremvn/common/CommonExtension.java` | `encryptPassword`, `requestUserPassword` | CONFIRMED — Base64-encodes UTF-8 password bytes; `requestUserPassword` prompts for a password. Base64 is reversible encoding and is not a password hash. |
| DAO | `src/main/java/com/pchouse/pchousestoremvn/dao/EmployeeDAO.java` | `getEmployeeByPasswordDAO` | CONFIRMED — finds an Employee by password alone. |
| Model / persistence mapping | `src/main/java/com/pchouse/pchousestoremvn/models/Employee.java` | `password`, `accessLevel` | CONFIRMED — has password and access-level fields; the meaning of each access level is not established by this mapping. |
| Model / persistence mapping | `src/main/java/com/pchouse/pchousestoremvn/models/Person.java` | `firstName`, `lastName`, `contactNo`, `email` | CONFIRMED — first name, last name and contact number are required; email is optional. The JPA email uniqueness annotation conflicts with the inspected legacy DDL. |
| DAO | `src/main/java/com/pchouse/pchousestoremvn/dao/EmployeeDAO.java` | `addEmployeeDAO`, `updateEmployeeDAO`, `getEmployeeDAO(Person)`, `getEmployeeByPasswordDAO` | CONFIRMED — Employee is linked to Person for lookup/update; the legacy password-only lookup is insecure and will not be reproduced. |
| Enum/configuration | Authentication-specific | — | CONFIRMED — no legacy OIDC/API token configuration or authentication enum was identified in the inspected sources; `Employee.accessLevel` is a `String`. |

Source paths refer to the legacy repository `hermanCosta/pchousestoremvn`, branch `main`.

## 3. Existing behavior (CONFIRMED)

- The desktop login authenticates a Company by name and password and opens `MainMenuView` with the returned Company.
- The login and helper use Base64 password encoding. `LoginView` prints credential-related debug data; the DAO logs login attempts and failures.
- The employee DAO lookup shown above uses only a password predicate. The Employee mapping has a free-form `accessLevel` field.
- Discovery records that some sale and service-order actions prompt for an employee password. The exact protected-action inventory and Java callers must be mapped in the relevant feature SPECs before implementing those actions.
- Legacy authentication behavior is evidence for migration analysis only. It is not an approved security design and its credentials must not be reused as password hashes.

## 4. Business and security rules

| ID | Rule | Evidence | Status |
|---|---|---|---|
| AUTH-01 | The persistent authenticated context is one active Shop session and contains no employee identity or employee role. | ADR-002, ADR-032 | DECIDED |
| AUTH-02 | A protected committed business operation requires a fresh employee password challenge and a short-lived, one-use `EmployeeActionAuthorization`; consume it atomically with the business write. | ADR-002; security discovery | DECIDED; action-to-role policy is separate |
| AUTH-03 | Shop sign-in uses the stable Shop code and password through OIDC Authorization Code with PKCE `S256`; the React SPA is a public client with no client secret. | Product-owner confirmation 2026-11-07; ADR-032 | DECIDED |
| AUTH-04 | The authorization server is OpenIddict with EF Core persistence; the API validates JWT Bearer access tokens for `/api/v1/**`. | ADR-032 | DECIDED; package/provider compatibility and integration validation required |
| AUTH-05 | Access tokens represent only the Shop context and carry server-issued `company_id`, `shop_id` and `shop_session_id`; they contain no employee claim or employee role. | ADR-032 | DECIDED |
| AUTH-06 | The API validates signature, issuer, audience, lifetime, token identifier, client and `shop.api` scope, then verifies the referenced ShopSession is active. | ADR-032 | DECIDED |
| AUTH-07 | Access-token lifetime defaults to 10 minutes and is configurable. Refresh tokens are disabled for the v1 SPA. | ADR-032, ADR-034 | DECIDED |
| AUTH-10 | ShopSession idle/absolute lifetime and multi-device policy remain configurable/deferred and must be explicitly decided before production. | Product-owner confirmation 2026-11-07 | DEFERRED TO PRE-PRODUCTION |
| AUTH-11 | Company-wide employees may hold both roles; effective permissions are the union of assigned roles, and MANAGER grants full access. | Product-owner approval 2026-11-10 | DECIDED |
| AUTH-12 | Managers provision, disable and reset Shop/Employee credentials; employee reset forces a password change; bootstrap and no-Manager recovery use an identity-verified deployment-controlled owner/operator procedure. | Product-owner approval 2026-11-10 | DECIDED — operational details recorded in employee-access-and-roles.md |
| AUTH-13 | Failed attempts never lock or disable a Shop/Employee credential. Limit each authentication endpoint to 10 requests per minute per source IP, counting success and failure; excess requests receive HTTP 429 for the remainder of the one-minute window. | Product-owner approval 2026-11-10 | DECIDED |
| AUTH-14 | Password baseline: minimum 12 characters, support at least 128 characters and paste/password managers; no arbitrary periodic expiry; rotate after compromise or authorized reset; use vetted adaptive hashing. | Product-owner approval 2026-11-10 | DECIDED |
| AUTH-08 | Never issue or validate home-grown JWTs; never use an ID token as an API access token; never implement Implicit or Resource Owner Password Credentials grants. | ADR-032 | DECIDED |
| AUTH-09 | Use established password hashing for new Shop and Employee credentials. Do not migrate Base64 or plaintext legacy password values as hashes. | Security discovery | DECIDED |

## 5. Proposed redesign (REDESIGN)

1. React starts an OIDC Authorization Code + PKCE flow with `state`, `nonce` and `S256`.
2. The authorization server presents the Shop sign-in UI, validates the stable Shop code and its password, and establishes a Shop-only server-side session and secure OIDC cookie.
3. OpenIddict issues an authorization code to an exactly registered redirect URI. React redeems it and retains tokens in memory only.
4. React sends only the OAuth access token to the versioned API. The API requires the `shop.api` scope and an active ShopSession for `/api/v1/**`.
5. Shop logout revokes the ShopSession and the related OpenIddict authorization state. Refresh tokens are not issued in v1.
6. When a protected business feature is approved and implemented, its UI requests the acting employee's credentials. The API validates the employee and permission for that action, creates a one-use authorization and consumes it in the same transaction as the business write. If both roles are assigned, effective permissions are their union; MANAGER therefore grants full access.
7. Managers administer Shop and Employee credentials. Employee resets require a Manager challenge and force a password change at next use. Initial Shop setup and recovery when no Manager can authenticate are performed through an identity-verified deployment-controlled owner/operator procedure, never a public password-reset endpoint.
8. Authentication endpoints limit request rates by source/network and endpoint. Repeated failures never create credential-specific lockout, waiting or rejection windows, disable a credential or invalidate the shared Shop session.

For Shop and Employee administration, the UI requests Manager credentials in a modal when the user submits a protected change. It clears the credentials after the one operation. Loading/searching the employee directory requires the active Shop session but no employee credential challenge. Changing one's own employee password continues to request that employee's current password.

The authorization server's cookie is confined to the OIDC sign-in session. It is not an implicit authentication fallback for `/api/v1/**`.

## 6. Data requirements

Logical records required by the approved architecture:

- `ShopAccessCredential`, separate from Employee credentials and stored using a vetted password hasher.
- `ShopSession`, containing Shop/Company context, creation and expiry times, and revocation state; never an Employee identity.
- OpenIddict application, authorization and token persistence using the selected EF Core context/provider.
- `EmployeeActionAuthorization`, scoped to ShopSession, Shop and action code, short-lived, single-use, and consumed atomically with the protected business transaction.

Logical ShopSession and employee-action relationships are approved by DATA-02. Physical keys, indexes, concurrency strategy, session uniqueness and full EF mappings must be implemented/reviewed under DATA-05. Do not generate production migrations from this SPEC draft.

Authentication persistence uses Company, Shop, Person and Employee identity/profile rows. Company and Person retain the confirmed legacy name/contact fields while omitting reversible legacy passwords; Shop sign-in and employee challenges use their separate modern credential records. `Employee.person_id` is optional because the legacy `@ManyToOne` does not require a Person. See [Authentication and Signing-Key Persistence](../02-architecture/authentication-persistence.md) for the PostgreSQL column-level mapping and remaining open profile details.

## 7. API and protocol contracts

The Product Owner approved completing these contracts on 2026-11-10. The routes and DTOs below are the concrete technical proposal for review; they do not authorize production deployment before the remaining security and persistence gates are satisfied.

- OIDC endpoints use OpenIddict-standard protocol endpoints (authorization, token, metadata/JWKS and end-session) and Authorization Code + PKCE; do not add a custom password-to-token endpoint.
- All application endpoints under `/api/v1/**` require a validated bearer token, active ShopSession and `shop.api` scope by default.
- Public endpoints are limited to liveness/readiness checks and the OIDC protocol endpoints that must be public.
- API contracts use DTOs; persistence entities are not serialized.
- OpenAPI documents the OAuth2 Authorization Code flow and `shop.api` scope.

### Employee action authorization

`POST /api/v1/employee-action-authorizations` authenticates an individual employee for one committed operation. The caller must already have a valid Shop bearer token.

Request DTO:

```json
{
  "employeeCode": "EMP-001",
  "password": "entered-once",
  "actionCode": "SHOP_ACCESS_CREDENTIAL_ROTATE",
  "resourceType": "Shop",
  "resourceId": "42",
  "targetShopId": 42,
  "operationContext": { "credentialVersion": 3 }
}
```

`targetShopId` is optional and must belong to the Company in the active ShopSession. `operationContext` is optional; when supplied, the server canonicalizes and hashes it. A protected command must present the same context and matching hash. The password is used only to verify the employee and is never persisted or returned.

Success response (`201 Created`):

```json
{
  "authorizationToken": "<opaque-base64url-token>",
  "actionCode": "SHOP_ACCESS_CREDENTIAL_ROTATE",
  "resourceType": "Shop",
  "resourceId": "42",
  "expiresAt": "<UTC timestamp>"
}
```

The raw random 256-bit token is returned once; only its SHA-256 hash is stored. Default validity is two minutes (configurable). The record binds the active ShopSession, active Shop, Company, Employee, action, resource/target Shop and optional operation-context hash.

Protected mutation DTOs carry the token in `X-Employee-Action-Authorization`. The server verifies action/resource/context match, locks and revalidates the ShopSession, then consumes the token using a conditional update in the same transaction as the business write. Exactly one affected row is required; replay, expiry, wrong action, wrong resource or wrong Shop is rejected.

### Shop access credential management

- `PUT /api/v1/shops/{shopId}/access-credential` rotates the target Shop password. Request DTO: `{ "newPassword": "...", "expectedCredentialVersion": 3 }`. It requires `X-Employee-Action-Authorization` bound to `SHOP_ACCESS_CREDENTIAL_ROTATE` and that Shop resource. The target Shop must belong to the Company in the active ShopSession and the employee must have effective MANAGER permission.
- `PATCH /api/v1/shops/{shopId}/access-credential` enables/disables the credential. Request DTO: `{ "isActive": false, "expectedCredentialVersion": 4 }`; `reason` is optional and the audit record receives a system description when omitted. It requires a fresh MANAGER authorization bound to `SHOP_ACCESS_CREDENTIAL_CHANGE_STATUS` and target Shop. Disabling the Shop currently in use is rejected; emergency recovery uses the separate audited bootstrap process.
- Both commands use an optimistic `expectedCredentialVersion`; stale versions return `409 credential_version_conflict`. A successful credential change increments the version and revokes all active sessions for the target Shop in the same transaction. Responses are `204 No Content`.
- Initial provisioning and no-Manager recovery have no public HTTP endpoint. They use the deployment-controlled, identity-verified bootstrap procedure recorded in the decision register.

### Error contract

All errors use `application/problem+json` with stable `code`, `status`, `title`, `detail` and `traceId` fields. Do not include submitted passwords, hashes, authorization tokens or signing-key data in error bodies/logs.

| HTTP | Codes | Meaning |
|---|---|---|
| `400` | `invalid_request`, `password_policy_failed` | Malformed DTO or password fails the approved baseline |
| `401` | `invalid_shop_token`, `shop_session_revoked`, `invalid_employee_credentials` | Authentication failed; employee credential errors are generic and do not identify the missing/disabled account |
| `403` | `action_not_permitted`, `target_shop_outside_company` | Employee role or active Company boundary rejects the operation |
| `404` | `shop_not_found` | Target Shop does not exist in the active Company |
| `409` | `authorization_replayed`, `operation_context_mismatch`, `credential_version_conflict`, `cannot_disable_active_shop` | State changed, authorization mismatched or command conflicts |
| `410` | `authorization_expired` | One-use authorization is expired |
| `429` | `request_rate_limited` | Source/network + endpoint limit exceeded; includes `Retry-After`; does not lock the credential |

Approved request-rate limit: at most 10 requests per minute per source IP and authentication endpoint, counting successes and failures. Exceeding the limit produces `429` for that source/endpoint window only. This never changes credential or ShopSession state. Behind a reverse proxy, trust forwarded client IP headers only from explicitly configured proxies/networks.

## 8. UI flows and states

- Shop sign-in redirects to the authorization server; React does not collect a password to exchange it at the OAuth token endpoint.
- Shop logout clears in-memory SPA tokens, revokes the ShopSession and server cookie, and returns through the registered OIDC post-logout callback with its state restored.
- A protected business action will request the individual employee credential for that action. No employee identity remains cached for the next action.
- Login failure, expired/revoked session, denied authorization, loading and retry states must be specified and tested with the first approved authentication UI implementation.

## 9. Acceptance criteria

- [ ] AC-01: The SPA completes OIDC Authorization Code + PKCE (`S256`) without a browser client secret or password grant.
- [ ] AC-02: A valid API access token is an asymmetric-signed JWS JWT with the configured issuer, API audience, lifetime, client and `shop.api` scope validated.
- [ ] AC-03: The access token identifies the active Shop and session and contains no employee identity or role.
- [ ] AC-04: API requests with a missing/invalid token, wrong audience/issuer/scope, or revoked/expired ShopSession are rejected.
- [ ] AC-05: Shop logout revokes the ShopSession; an otherwise unexpired access token for that session no longer authorizes API access.
- [ ] AC-06: Access and ID tokens are kept in memory only; the ID token is never sent to the API.
- [ ] AC-07: No custom JWT issuance, password grant, Implicit grant or browser client secret exists.
- [ ] AC-08: Employee action authorization cannot be replayed and is consumed atomically when a protected feature is implemented.
- [ ] AC-09: Authentication and token/session security events are logged without credentials, hashes, tokens or signing-key material.
- [ ] AC-10: Repeated failures cannot lock or disable a Shop/Employee credential or create a credential-specific rejection window; request-rate limits are enforced by source/network and endpoint.

## 10. Test scenarios

- Authorization integration tests: successful PKCE exchange; bad/missing verifier; state/nonce and exact redirect URI handling; no refresh token issuance.
- API integration tests: issuer/audience/signature/scope/lifetime validation; no employee claims; revoked session rejection; public health endpoint behavior.
- Persistence/concurrency tests: ShopSession revocation visibility; one-use employee authorization rejects replay and concurrent double consumption.
- Frontend tests: memory-only token handling, redirect/callback failures, logout and ensuring an ID token is not attached to API requests.
- Credential tests: manager provisioning/reset, forced change after reset, employee self-change with current password, bootstrap/recovery audit, generic errors and source/network rate limiting without credential lockout.
- Contract tests: one-use authorization bind/consume/replay, canonical operation-context mismatch, cross-Company target rejection, Shop credential rotate/disable version conflict, session revocation on credential change and RFC 9457 error body shape.
- Use PostgreSQL persistence integration coverage for OpenIddict and session state before declaring the feature complete.

## 10.1 Implementation record

- **IMPLEMENTED:** OpenIddict authorization-code server with S256-only PKCE, asymmetric development signing, unencrypted JWS access tokens, local token validation, public SPA client registration, OIDC login form, antiforgery validation, HttpOnly/Secure OIDC cookie, vetted ASP.NET Core password hashing verification, ShopSession issuance/revocation, IP-and-endpoint-partitioned 10/minute authentication rate limiting, and a bearer-protected session DTO endpoint.
- **IMPLEMENTED:** React OIDC client uses Authorization Code + PKCE; user tokens are held in memory, while short-lived protocol state is retained in session storage across the full-page redirect. It sends only the access token to `/api/v1/session`; that Shop-only DTO now includes Shop name, address and phone for the persistent active-Shop profile display.
- **IMPLEMENTED:** API fallback authorization requires bearer authentication and `shop.api`; `/api/v1/**` checks the active ShopSession, Shop and Company on each request.
- **IMPLEMENTED FOUNDATION:** `POST /api/v1/employee-action-authorizations` verifies an active Company employee with ASP.NET Core Identity hashing, checks MANAGER for Shop credential and employee-administration actions, validates the target resource, canonicalizes/hashes optional operation context, applies the authentication rate limit, and persists only a hash of a random 256-bit token with a two-minute default expiry. Employee first-use credential changes are enforced.
- **IMPLEMENTED:** `PUT /api/v1/shops/{shopId}/access-credential` consumes a matching employee authorization with a conditional update in the same PostgreSQL transaction as the password-hash/version update, audit event and target-Shop session revocation.
- **IMPLEMENTED:** `PATCH /api/v1/shops/{shopId}/access-credential` consumes a matching Manager authorization in the same PostgreSQL transaction as the status/version update, audit event and target-Shop session revocation. It rejects attempts to disable the active Shop session.
- **IMPLEMENTED UI:** The Shop session screen offers password rotation and credential enable/disable for the signed-in Shop. It requests a fresh employee challenge per operation, holds the one-use authorization only in the request flow, clears the employee password, and asks the user to sign in again after success. Credential version is remembered per Shop in browser session storage; it can be edited after a concurrent update conflict.
- **PARTIAL:** Logout revokes the ShopSession, which immediately blocks API use. The session is not yet linked to a specific OpenIddict authorization record, so revocation of that related authorization grant remains outstanding.
- **IMPLEMENTED FOUNDATION:** Company-scoped employee registration/read/update, role/status administration, credential reset and self-service password change are available through the Manager API/UI. The `20261113004053_EmployeeAdministration` and `20261113010857_DeploymentOperatorEmployeeBootstrap` migrations are applied to local `pchousestore_dev`. `tools/PcHouseStore.Bootstrap` provisions or recovers a Manager and performs explicit operator credential resets locally without an HTTP endpoint; resets force next-use password change. Production use requires restricted deployment-host access and operator identity logs. The local `TEST-001` Shop and synthetic `MANAGER-TEST` fixture credentials are not in source control.
- **DEPLOYMENT BLOCKER:** Production requires separate signing/encryption PKCS#12 certificates and secret-store passwords; no production certificates are configured. Session timeout values remain unset per the approved pre-production deferral. The development issuer/client settings are local-only.
- **VALIDATION PENDING:** OIDC/PostgreSQL integration and frontend tests listed above have not been run; a successful API/frontend build does not establish the acceptance criteria.

## 11. Open questions and blockers

- **DECIDED — Shop credential lifecycle:** Managers provision, rotate, disable and reset credentials; first bootstrap and no-Manager recovery use the audited, identity-verified deployment-controlled owner/operator process described above.
- **DEFERRED TO PRE-PRODUCTION — Shop session policy:** Set idle/absolute timeout values and decide simultaneous-device, session reuse and workstation sign-out behavior before production. Keep values externally configurable.
- **DECIDED — Employee credential policy:** Manager provisioning/reset, forced change after reset, employee self-change using current password, password length baseline and no account lockout are recorded above and in employee-access-and-roles.md.
- **DECIDED — Fine-grained authorization:** Product Owner approved the MANAGER/TECHNICIAN matrix and dual-role union semantics on 2026-11-10; MANAGER grants full access.
- **DECIDED BASELINE — Signing-key storage:** use `system_services` for certificate metadata and envelope-encrypted PKCS#8 private material; keep the KEK in an external KMS/secret manager or protected host certificate store. Confirm the actual provider, access policy and recovery runbook before production.
- **PENDING — DATA-05:** Implement and review physical keys, indexes, concurrency strategy and EF mappings for ShopSession and EmployeeActionAuthorization before persistent implementation/migrations.
- **APPROVED — Technical review:** Product Owner validated the API contracts, persistence design, `system_services` key registry baseline and request-rate limit on 2026-11-10. PostgreSQL/Npgsql provider and concurrency validation remains an implementation gate.

## 12. Approval

- Product approval: APPROVED — Shop code/password, credential operations, password baseline, no-account-lock throttling, role matrix and dual-role precedence approved 2026-11-10; session timeout and multi-device policy remain deferred to pre-production.
- Owner review: COMPLETE — 2026-11-10
- Technical approval: APPROVED — design validated by Product Owner 2026-11-10
- Logical data-model approval (DATA-02): APPROVED
- Physical data-model / EF mapping (DATA-05): PENDING
