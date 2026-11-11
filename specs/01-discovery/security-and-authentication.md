# Security and Authentication — Legacy Findings and Target Requirements

**Status:** DISCOVERY + TARGET DECISIONS RECORDED

## Legacy observations

- `CommonExtension.encryptPassword` uses Java Base64 encoding of the password bytes. Base64 is reversible encoding, not secure password hashing.
- `LoginView` writes username and encoded password values to standard output during sign-in.
- `CompanyDAO.getCompanyDAO` compares company name and password directly in a query.
- `EmployeeDAO.getEmployeeByPasswordDAO` looks up an employee by password alone.
- Sale and service-order screens request an employee password to authorize certain actions.
- Employee exposes an `accessLevel` string, but a full role/permission matrix has not yet been discovered.

## Target authentication model

### OAuth 2.0 / OpenID Connect API boundary (added v19)

- The prior discovery model established ShopSession and employee action authorization but did not specify the API token protocol. The target now explicitly requires OAuth 2.0 access tokens and OpenID Connect Shop sign-in.
- React uses Authorization Code + PKCE (`S256`); ASP.NET Core API endpoints validate JWT Bearer access tokens.
- OpenIddict is the default self-hosted authorization-server implementation direction.
- Token identity is the Shop access principal and active ShopSession, never an employee.
- Employee password authorization remains independent and per operation.
- See [API Authentication and Token Security](../02-architecture/api-authentication-and-token-security.md) for claims, token policy, endpoint controls and prohibited flows.

### Shop login — persistent operational session

1. The application login selects a Shop and validates that Shop's access credential.
2. The server establishes a `ShopSession` whose security context contains `CompanyId`, `ShopId`, session ID and expiry/revocation metadata.
3. The session identity is the Shop. It must not contain an active `EmployeeId`, employee username or employee role as the logged-in principal.
4. Multiple employees may use the same active Shop session/workstation. The person who authenticated the last operation is not assumed to be the person performing the next one.
5. Shop session lifetime, idle timeout, rotation, revocation and workstation sign-out still need approval.

### Employee challenge — one protected operation at a time

1. Before committing a protected business command, the UI prompts for the executing employee's individual credential. One atomic command may create multiple related rows (for example, an order and its initial payment), all attributed to the same employee action authorization.
2. The API validates the employee's password, active status, Company ownership relative to the current Shop and Company-wide role permission for the requested action.
3. The API creates a short-lived `EmployeeActionAuthorization` scoped to the current `ShopSession`, `ShopId` and action code.
4. The authorization is single-use and consumed atomically with the business operation. The resulting record stores the employee actor through that authorization.
5. The next protected action prompts again. The previous employee identity/authorization is not reused implicitly.
6. The API ignores/rejects browser-supplied actor IDs that do not match the consumed authorization.

The employee challenge mechanism is a password. The same Company-wide employee account/password and role assignments work in every Shop; the persistent ShopSession still contains no Employee identity. Credential verification must use a vetted password-hashing approach and constant-time-safe framework primitives; raw credentials must never be persisted or logged.

## Target requirements

1. Never migrate or reuse legacy password values as valid password hashes.
2. Use ASP.NET Core Identity or another established credential-verification framework where suitable. Keep Shop access credentials and Employee credentials logically distinct.
3. Never log passwords, hashes, access tokens, session cookies or entered PINs.
4. A valid ShopSession authenticates only Shop context; it does not by itself authorize sales, service operations, payments, refunds, stock movements or cash movements.
5. Verify active Company-wide Employee role assignment and action permission on every employee challenge; no per-Shop employee assignment is required.
6. Enforce one-time consumption and short expiry of `EmployeeActionAuthorization` server-side, under concurrency.
7. Record employee actor, Shop, action, timestamp and target on every protected operation. Do not infer actor from workstation or session history.
8. **Target policy updated 2026-11-10:** limit authentication request rates by source/network and endpoint. Failed attempts must not lock/disable credentials or create credential-specific rejection windows. The earlier lockout suggestion is superseded by the product-owner decision in `product-owner-decisions.md`.
9. Use OAuth 2.0/OIDC Authorization Code + PKCE, API JWT Bearer validation, strict redirect/CORS policy, ShopSession revocation and least-privilege database accounts. Protect cookie-based authorization-server state with Secure/HttpOnly/SameSite settings and CSRF controls where applicable.
10. Audit security-relevant events including Shop sign-in/out, employee challenge success/failure, authorization consumption and privileged actions.

## Open decisions

- Employee password recovery/reset workflow and password policy.
- The detailed action-by-action permission matrix for `MANAGER` and `TECHNICIAN`; these roles are Company-wide and work in all Shops.
- Whether the same employee may hold both roles (allowed by the current data model).
- Shop access credential provisioning, rotation and recovery remain open. Shop sign-in identifier is the stable Shop code (product-owner confirmation 2026-11-07).
- Shared workstation lifecycle, idle/absolute timeout values and whether a ShopSession may be active on several devices simultaneously must be decided before production; keep timeout values configurable (product-owner confirmation 2026-11-07).
- Initial access-token lifetime is 10 minutes. Final ShopSession idle/absolute timeout values remain open. Refresh tokens are disabled for the v1 SPA by ADR-034; reconsideration requires a later ADR with rotation and reuse detection.
- Final signing key/certificate storage, rotation and backup/recovery procedure remain deployment tasks.
