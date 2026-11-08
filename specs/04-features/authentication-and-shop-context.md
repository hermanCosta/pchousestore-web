# Feature: Shop authentication and API access

**Status:** DRAFT — product and technical approval pending  
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
| DAO | `src/main/java/com/pchouse/pchousestoremvn/dao/CompanyDAO.java` | `getCompanyDAO` | CONFIRMED — queries `Company` by matching `name` and `password`. |
| Model / persistence mapping | `src/main/java/com/pchouse/pchousestoremvn/models/Company.java` | `Company.password`, `@Column(name = "PASSWORD")` | CONFIRMED — stores a Company password field. |
| Shared authentication helper | `src/main/java/com/pchouse/pchousestoremvn/common/CommonExtension.java` | `encryptPassword`, `requestUserPassword` | CONFIRMED — Base64-encodes UTF-8 password bytes; `requestUserPassword` prompts for a password. Base64 is reversible encoding and is not a password hash. |
| DAO | `src/main/java/com/pchouse/pchousestoremvn/dao/EmployeeDAO.java` | `getEmployeeByPasswordDAO` | CONFIRMED — finds an Employee by password alone. |
| Model / persistence mapping | `src/main/java/com/pchouse/pchousestoremvn/models/Employee.java` | `password`, `accessLevel` | CONFIRMED — has password and access-level fields; the meaning of each access level is not established by this mapping. |
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
| AUTH-11 | Employee password/recovery policy and multi-role assignment remain pending; do not invent them in the Shop OIDC flow. | Product-owner confirmation 2026-11-07 | OPEN |
| AUTH-08 | Never issue or validate home-grown JWTs; never use an ID token as an API access token; never implement Implicit or Resource Owner Password Credentials grants. | ADR-032 | DECIDED |
| AUTH-09 | Use established password hashing for new Shop and Employee credentials. Do not migrate Base64 or plaintext legacy password values as hashes. | Security discovery | DECIDED |

## 5. Proposed redesign (REDESIGN)

1. React starts an OIDC Authorization Code + PKCE flow with `state`, `nonce` and `S256`.
2. The authorization server presents the Shop sign-in UI, validates the stable Shop code and its password, and establishes a Shop-only server-side session and secure OIDC cookie.
3. OpenIddict issues an authorization code to an exactly registered redirect URI. React redeems it and retains tokens in memory only.
4. React sends only the OAuth access token to the versioned API. The API requires the `shop.api` scope and an active ShopSession for `/api/v1/**`.
5. Shop logout revokes the ShopSession and the related OpenIddict authorization state. Refresh tokens are not issued in v1.
6. When a protected business feature is approved and implemented, its UI requests the acting employee's credentials. The API validates the employee and permission for that action, creates a one-use authorization and consumes it in the same transaction as the business write.

The authorization server's cookie is confined to the OIDC sign-in session. It is not an implicit authentication fallback for `/api/v1/**`.

## 6. Data requirements

Logical records required by the approved architecture:

- `ShopAccessCredential`, separate from Employee credentials and stored using a vetted password hasher.
- `ShopSession`, containing Shop/Company context, creation and expiry times, and revocation state; never an Employee identity.
- OpenIddict application, authorization and token persistence using the selected EF Core context/provider.
- `EmployeeActionAuthorization`, scoped to ShopSession, Shop and action code, short-lived, single-use, and consumed atomically with the protected business transaction.

Physical keys, indexes, concurrency strategy, session uniqueness and full EF mappings depend on DATA-02 approval. Do not generate production migrations from this SPEC draft.

## 7. API and protocol contracts

- OIDC endpoints follow OpenIddict protocol metadata and the Authorization Code + PKCE flow; do not add a custom password-to-token endpoint.
- All application endpoints under `/api/v1/**` require a validated bearer token, active ShopSession and `shop.api` scope by default.
- Public endpoints are limited to liveness/readiness checks and the OIDC protocol endpoints that must be public.
- API contracts use DTOs; persistence entities are not serialized.
- Exact Shop credential challenge and employee-action authorization HTTP contracts remain pending the session/authorization data model and feature acceptance. Do not expose these as implemented endpoints in the foundation.
- OpenAPI documents the OAuth2 Authorization Code flow and `shop.api` scope.

## 8. UI flows and states

- Shop sign-in redirects to the authorization server; React does not collect a password to exchange it at the OAuth token endpoint.
- Shop logout clears in-memory SPA tokens and requests server-side ShopSession/OpenIddict revocation.
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

## 10. Test scenarios

- Authorization integration tests: successful PKCE exchange; bad/missing verifier; state/nonce and exact redirect URI handling; no refresh token issuance.
- API integration tests: issuer/audience/signature/scope/lifetime validation; no employee claims; revoked session rejection; public health endpoint behavior.
- Persistence/concurrency tests: ShopSession revocation visibility; one-use employee authorization rejects replay and concurrent double consumption.
- Frontend tests: memory-only token handling, redirect/callback failures, logout and ensuring an ID token is not attached to API requests.
- Use MySQL-compatible persistence integration coverage for OpenIddict and session state before declaring the feature complete.

## 11. Open questions and blockers

- **OPEN — Shop credential lifecycle:** The stable Shop code is the login identifier. Who provisions, rotates, recovers and disables Shop access credentials, and how is recovery authorized?
- **DEFERRED TO PRE-PRODUCTION — Shop session policy:** Set idle/absolute timeout values and decide simultaneous-device, session reuse and workstation sign-out behavior before production. Keep values externally configurable.
- **OPEN — Employee credential policy:** Approve password policy and reset/recovery. Confirm lockout/rate-limit behavior while preserving the shared ShopSession for other employees.
- **OPEN — Fine-grained authorization:** Approve the detailed MANAGER/TECHNICIAN action matrix and whether an Employee may hold both roles before freezing action policies.
- **OPEN — Deployment security:** Decide signing-certificate/key storage, rotation and recovery with the deployment environment before production.
- **BLOCKED — DATA-02:** Approve relational keys/constraints and concurrency for ShopSession and EmployeeActionAuthorization before persistent implementation and migrations.
- **BLOCKED — SPEC approval:** Product and technical approval are required before implementing this feature.

## 12. Approval

- Product approval: PARTIAL — Shop code + password confirmed; session policy deferred to pre-production; employee credential policy remains open
- Technical approval: PENDING
- Data-model approval (DATA-02): PENDING
