# API Authentication and Token Security — OAuth 2.0 / OpenID Connect

**Status:** TARGET DECISION RECORDED — v19. Implementation and deployment review remain pending.

## 1. Decision

The ASP.NET Core API must be protected by OAuth 2.0 access tokens, with OpenID Connect (OIDC) used for the Shop sign-in protocol and JWT Bearer access tokens used by the API resource server.

The authorization principal is the **Shop access context**, not an employee. This preserves the product rule that a shared workstation/session belongs to a Shop and never to the last employee who used it.

### Selected v1 protocol profile

- Authorization Code grant with Proof Key for Code Exchange (PKCE), using `S256`.
- OpenID Connect sign-in for the Shop principal.
- JWT access tokens in the `Authorization: Bearer <access_token>` header for `/api/v1/**` endpoints.
- A public React SPA client; no client secret is shipped to the browser.
- A self-hosted authorization server implemented with OpenIddict in the ASP.NET Core solution is the default implementation choice. Keep the protocol standards-based so a compatible external OIDC provider can replace it later without changing business rules.
- OpenIddict token/authorization persistence uses its EF Core integration and the same MySQL deployment, subject to provider/migration compatibility tests.

Do not treat ASP.NET Core Identity API's proprietary bearer tokens as OAuth/OIDC access tokens. Use an actual OAuth/OIDC authorization server and standard JWT Bearer validation in the API.

## 2. Identity boundaries

### Shop principal

The user selects a Shop and enters that Shop's access credential on the authorization server's interactive sign-in page. Successful sign-in creates or resumes a server-side `ShopSession`.

The OIDC subject (`sub`) identifies the stable Shop access principal (the `ShopAccessCredential`/Shop identity), not a human employee. The access token includes the active `company_id`, `shop_id`, and `shop_session_id` claims so the resource API can resolve the exact operational context.

The token/session must never include:

- `employee_id`;
- employee code, name, or password;
- `MANAGER` or `TECHNICIAN` as the logged-in Shop principal's role;
- an employee identity inherited from a previous operation.

### Employee actor

Employee authentication remains a separate, action-level business authorization mechanism:

1. The bearer token authenticates the current Shop context.
2. For each protected write, React prompts for `employee_code` and password.
3. The API validates the employee hash, active status, Company ownership and role permission for the requested action.
4. It creates a short-lived `EmployeeActionAuthorization` bound to `ShopSession`, `Shop`, action code and operation-context hash.
5. It consumes that authorization exactly once in the same database transaction as the business command.

No employee access token is issued and the employee password challenge does not become an OAuth grant. A fresh employee challenge is required for the next protected action.

## 3. Authorization Code + PKCE flow

1. React starts an OIDC authorization request with a transaction-specific `state`, `nonce` and PKCE `code_challenge` using `S256`.
2. The authorization server presents the Shop login form. The Shop code/password is validated against `ShopAccessCredential` using a vetted password hasher.
3. The authorization server establishes/resumes a Shop-only server-side `ShopSession` and its own secure OIDC sign-in cookie. The cookie is an auth-server session for the Shop, not an employee.
4. The authorization server returns a short-lived, single-use authorization code to the exact registered redirect URI.
5. React redeems the code at the token endpoint using the matching PKCE `code_verifier`.
6. The authorization server issues an OIDC ID token for the Shop principal and an OAuth access token for the API. The ID token is for the client sign-in result; **only the access token is sent to the API**.
7. React calls the API with `Authorization: Bearer <access_token>`.
8. When the access token expires, the client uses a refresh token only if enabled; refresh tokens must be rotated with reuse detection. Alternatively, it can re-run authorization against the still-active Shop OIDC session.
9. Shop sign-out revokes the `ShopSession`, authorization/refresh-token family and server-side OIDC session. API calls associated with a revoked session are rejected.

The Shop password is entered only at the authorization endpoint's interactive login UI. Do not implement the OAuth Resource Owner Password Credentials grant (`grant_type=password`) or a custom username/password token endpoint.

## 4. Access token profile

The access token is a signed JWS JWT (not an encrypted JWE) using an asymmetric signing key/certificate and is intended only for the PcHouseStore API. Configure OpenIddict access-token encryption off for this resource-server profile (for example, `DisableAccessTokenEncryption()`), because the API validates the signature with its trusted public key. Keep authorization codes and refresh tokens protected/encrypted by OpenIddict. Only non-sensitive Shop context claims are permitted in the readable access token.

Required standard claims/validation inputs:

- `iss` — exact trusted issuer;
- `aud` — `pc-house-api` (or a single configured API audience);
- `sub` — stable Shop access principal;
- `client_id` — registered React client identifier;
- `iat`, `nbf`, `exp` — issued/not-before/expiry times;
- `jti` — unique token identifier;
- `scope` — includes `shop.api`;
- `company_id`, `shop_id`, `shop_session_id` — server-issued Shop context claims.

No employee claim or employee role is allowed in this token. `MANAGER`/`TECHNICIAN` permissions are evaluated only by the employee action challenge and authorization policy.

Initial token policy proposal:

- Access token lifetime: 10 minutes (configurable, keep short).
- Refresh token: optional for v1; if enabled for the public client, use rotation, family tracking, replay/reuse detection and revocation. Never store a raw refresh token in MySQL application tables or logs.
- Shop session idle/absolute lifetime: configurable and remains a separate operational policy; do not silently derive it from JWT expiry.
- Validate ShopSession active/revoked status at API authorization time (directly or through a short-lived server-side cache) so a logged-out/revoked Shop cannot continue using otherwise unexpired tokens.

The final expiry/idle values require a security/operations acceptance test; the 10-minute access-token lifetime is the initial technical default, not a lifetime for the Shop session.

## 5. API enforcement

- Configure ASP.NET Core `JwtBearer` authentication against the trusted OpenIddict issuer/metadata.
- Validate signature, allowed signing algorithm, issuer, audience, token lifetime, `jti`, required claims and `shop_session_id` status. Never decode a token without validating it.
- Apply a default authorization policy to all `/api/v1/**` endpoints. Public exceptions are limited to health/readiness and OIDC protocol endpoints as required.
- Require the `shop.api` scope for application API calls, then apply action-specific authorization policies.
- Derive `CompanyId` and `ShopId` from the verified token and active ShopSession record. Reject or ignore client-supplied Shop identifiers as authorization context.
- Apply request-rate limits to authorization, token and employee challenge endpoints by source/network and endpoint. Failed attempts never lock or disable credentials or create credential-specific rejection windows; do not invalidate the shared ShopSession.
- Use HTTPS only, exact registered redirect URIs, strict allowed CORS origins, secure token signing-key storage, short token lifetimes, security headers/CSP and redacted logs.
- React keeps access tokens in memory only; never write access/refresh/ID tokens to `localStorage`, `sessionStorage`, IndexedDB, analytics or browser logs. Never send ID tokens to the API.
- Since API calls use an explicit Authorization header, do not add cookie-based authentication as an implicit fallback for `/api/v1/**`. The authorization server's own HttpOnly cookie is only for its Shop sign-in session.
- Document OAuth security scheme and scopes in OpenAPI/Swagger; protected endpoints must be testable with the standard Authorization Code + PKCE flow.

## 6. Explicitly prohibited

- OAuth Implicit grant.
- OAuth Resource Owner Password Credentials grant (`password` grant).
- Home-grown JWT issuance or validation code.
- Using an OIDC ID token as an API access token.
- Sending a client secret in React/browser code.
- Persisting raw passwords, access tokens, refresh tokens, authorization codes or signing keys in logs or source control.
- Putting employee identity/roles into the Shop access token or reusing an employee's previous challenge.

## 7. Implementation components

- Authorization server: OpenIddict Server + ASP.NET Core integration; EF Core token/application/authorization persistence.
- Credentials: `ShopAccessCredential` with a vetted password hasher; keep it logically separate from `Employee.password_hash`.
- API resource server: `Microsoft.AspNetCore.Authentication.JwtBearer` plus ASP.NET Core policy-based authorization.
- Employee operation authorization: existing `EmployeeActionAuthorization` application/domain service; it is not replaced by OAuth.
- React: OIDC authorization-code client integration configured as a public SPA client with PKCE `S256` and memory-only token handling.

OpenIddict's ASP.NET Core integration documents support for ASP.NET Core/.NET 10. Package versions must be pinned and tested together during implementation; do not copy an old package version from a sample without compatibility review.

## 8. References

- Microsoft Learn — [Configure JWT bearer authentication in ASP.NET Core 10](https://learn.microsoft.com/pt-br/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- Microsoft Learn — [Application types and SPA authorization-code + PKCE](https://learn.microsoft.com/en-us/entra/identity-platform/v2-app-types)
- IETF — [RFC 9700: OAuth 2.0 Security Best Current Practice](https://www.rfc-editor.org/rfc/rfc9700.html)
- OpenIddict — [ASP.NET Core integration and .NET 10 compatibility](https://documentation.openiddict.com/integrations/aspnet-core)
- OpenIddict — [Introduction and authorization-code flow](https://documentation.openiddict.com/introduction)
- OpenIddict — [Token formats, signed JWT access tokens and access-token encryption](https://documentation.openiddict.com/configuration/token-formats)
