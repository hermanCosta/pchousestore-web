# Backend Guidelines

**Status:** DRAFT

- Target .NET 10 and current supported C# language version.
- ASP.NET Core Web API.
- EF Core with MySQL provider selected after compatibility verification.
- Use dependency injection and options pattern.
- Keep controllers/endpoints thin.
- Put orchestration in Application and invariants in Domain.
- Use DTOs; never serialize EF entities as public API contracts.
- Use decimal for financial values.
- Use CancellationToken on asynchronous I/O.
- Use transactions for multi-aggregate workflows where atomicity is required.
- Use optimistic concurrency where concurrent updates are possible.
- Centralize exception handling and return Problem Details.
- Validate input at API boundary and enforce domain invariants independently.
- Use structured logging with sensitive-data redaction.
- Add unit tests for domain/application behavior and integration tests for persistence/API.
- Document contracts through OpenAPI.
- Do not introduce MediatR, AutoMapper, generic repositories or Unit of Work by default; justify each dependency/pattern.

## Company and Shop context

- The initial deployment has one Company with multiple Shops. Do not implement CompanyMembership or multi-company tenant selection by default.
- Resolve the active Shop from the validated ShopSession, never from an untrusted request body/header alone.
- Scope every operational query and mutation to the session Shop, including detail lookups, searches, reports and exports.
- Shared master data (customers, employees, catalogue, products/services, devices and faults) is company-level by default.
- Do not trust a `shop_id`, `employee_id`, total or balance supplied by the browser as authoritative.

## Shop session and employee action authorization

- The persistent authentication principal represents the Shop and contains no active Employee identity or employee role.
- Before each protected committed business operation, validate the executing employee's password, active status, Company ownership relative to the active Shop and permission for the action. Employee accounts and MANAGER/TECHNICIAN roles are Company-wide; no ShopEmployee assignment is required.
- Create a short-lived, one-time EmployeeActionAuthorization scoped to ShopSession, Shop and action code (and, where practical, a canonical operation/request fingerprint). Roles are Company-wide `MANAGER`/`TECHNICIAN`; the same Employee password works in all Shops owned by the Company.
- Consume the authorization exactly once in the same database transaction as the business write. Reject expired, consumed, wrong-Shop, wrong-action and replayed authorizations.
- Persist the employee actor from the server-validated authorization, not from a client-supplied employee ID.
- Log successful/failed challenges without logging credentials, hashes or PINs.

## OAuth 2.0 / OpenID Connect and API protection

- Protect `/api/v1/**` with OAuth 2.0 access tokens; use OpenID Connect for the Shop sign-in protocol and JWT Bearer validation in ASP.NET Core.
- Default authorization-server implementation: OpenIddict Server integrated with ASP.NET Core and EF Core persistence. Keep the protocol standards-based so an external OIDC provider can replace it later.
- React uses Authorization Code + PKCE (`S256`); no client secret is permitted in browser code.
- The token subject is the Shop access principal and the token carries CompanyId, ShopId and ShopSessionId only. Never add EmployeeId, employee code or employee roles to a persistent Shop token.
- API validates signature, algorithm, issuer, audience, `exp`, `nbf`, `jti`, client and scope, then checks that ShopSession is active/revoked. ShopId is derived from verified server-issued claims, not request payloads.
- Require `shop.api` scope and policy authorization for API endpoints. Use 401 for invalid/missing tokens and 403 for valid tokens lacking permission.
- Keep access tokens in React memory only; no localStorage/sessionStorage/IndexedDB. Refresh tokens are optional; if enabled for the public client, rotate them and detect reuse.
- Do not use Implicit or Resource Owner Password Credentials grants. Do not use ASP.NET Core Identity's proprietary bearer tokens as a substitute for OAuth/OIDC. Do not implement a custom JWT issuer.
- Employee code/password remains a separate one-operation challenge, consumed atomically through EmployeeActionAuthorization. OAuth authenticates the Shop context; it does not replace employee attribution.
- Use HTTPS, exact redirect URI registration, strict CORS allowlists, rate limits, secure asymmetric signing keys, secret redaction and an OpenAPI OAuth2 security definition.

## Inventory and finance

- InventoryMovement must reference Product inventory only; Service catalogue items must not have stock.
- Store balances per `(shop_id, product_id)` in ShopInventory.
- Post ShopInventory balance changes and InventoryMovement rows atomically.
- A combined tender is one PaymentTransaction with multiple PaymentAllocation rows. Persist transaction amount once and validate allocation sums in a transaction.
- Refunds are positive magnitudes with an explicit REFUND kind and original-transaction link.
- Enforce one DailyClosing per `(shop_id,business_date)`. The close query must filter all source transactions and cash movements by the active Shop and Shop-local business date. Different Shops may close the same date independently; a Company-level dashboard may aggregate their results without creating a consolidated DailyClosing.
