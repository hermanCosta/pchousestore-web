# v19 — OAuth 2.0 / OpenID Connect API authentication (5 November 2026)

## Added

- Explicit decision that the ASP.NET Core API requires OAuth 2.0 access-token protection and OpenID Connect for Shop sign-in.
- Authorization Code + PKCE (`S256`) flow for the React SPA; JWT Bearer access tokens for `/api/v1/**`.
- OpenIddict self-hosted Authorization Server selected as the default .NET implementation direction, with a standards-based boundary that can later use a compatible external OIDC provider.
- Shop-only token principal and claims (`company_id`, `shop_id`, `shop_session_id`); no EmployeeId, employee role or previous employee identity in a persistent token/session.
- Separate employee password challenge and one-use `EmployeeActionAuthorization` remain mandatory for protected business operations.
- Token validation, revocation, refresh-token rotation/reuse detection, CORS, HTTPS, memory-only SPA token handling and explicit prohibited flows.
- OAuth/OIDC regression criteria and migration backlog items.

## Explicit security boundaries

- No OAuth Implicit grant or Resource Owner Password Credentials grant.
- No home-grown JWT issuer, no ID token sent to the API, no client secret in React, and no raw credentials/tokens in logs.
- ShopSession remains an operational domain session and is independent from JWT access-token lifetime.

## References

- Microsoft Learn ASP.NET Core 10 JWT Bearer authentication.
- IETF RFC 9700 OAuth 2.0 Security Best Current Practice.
- Microsoft Learn SPA Authorization Code + PKCE guidance.
- OpenIddict ASP.NET Core integration (.NET 10 support).
