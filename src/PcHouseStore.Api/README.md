# API authentication setup

The local API uses OpenIddict Authorization Code + PKCE. It registers the React public client in PostgreSQL at startup; it does not create a Company, Shop or password.

## Local run

1. Start PostgreSQL and apply the EF migrations to `pchousestore_dev`.
2. Trust the ASP.NET Core development certificate with `dotnet dev-certs https --trust`.
3. Start the API with the `https` launch profile (`https://localhost:7267`).
4. Start the SPA from `frontend/pchousestore-web` (`npm run dev`, normally `http://localhost:5173`).
5. This local `pchousestore_dev` database has a test-only Company/Shop with code `TEST-001`, an ASP.NET Core password hash and a `PROVISIONED` audit event. A fresh database still requires an authorized provisioning procedure to create an active Company, Shop and `ShopAccessCredential`. There is deliberately no public bootstrap endpoint, and the local fixture is not included in source control or migrations.

The SPA authority, client ID and API base URL can be overridden with `VITE_OIDC_AUTHORITY`, `VITE_OIDC_CLIENT_ID` and `VITE_API_BASE_URL`.

## Deployment configuration

Set `ConnectionStrings__PcHouseStore`, `Authentication__Issuer`, `Authentication__SpaOrigin`, `Authentication__SpaClientId` and `Authentication__ApiAudience` through the deployment environment. Outside Development, also provide separate PKCS#12 certificates using `Authentication__SigningCertificatePath` / `Authentication__SigningCertificatePassword` and `Authentication__EncryptionCertificatePath` / `Authentication__EncryptionCertificatePassword`. Keep certificate passwords in the deployment secret store. Production requires HTTPS.

`Authentication__AccessTokenMinutes` defaults to 10; `Authentication__CookieLifetimeMinutes` defaults to 480. ShopSession idle/absolute expiry remains unset until the approved pre-production policy is selected. The API checks the ShopSession, Shop and Company on each `/api/v1/**` request, so logout revocation takes effect even while a JWT is otherwise valid.
