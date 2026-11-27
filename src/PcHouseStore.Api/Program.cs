using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using PcHouseStore.Api;
using PcHouseStore.Application;
using PcHouseStore.Infrastructure;
using PcHouseStore.Infrastructure.Persistence;
using PcHouseStore.Infrastructure.Persistence.Entities;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddAntiforgery(options => options.FormFieldName = "__RequestVerificationToken");
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
var issuer = builder.Configuration["Authentication:Issuer"] ?? throw new InvalidOperationException("Authentication:Issuer is required.");
var spaOrigin = builder.Configuration["Authentication:SpaOrigin"] ?? throw new InvalidOperationException("Authentication:SpaOrigin is required.");
var spaClientId = builder.Configuration["Authentication:SpaClientId"] ?? "pchousestore-spa";
var apiAudience = builder.Configuration["Authentication:ApiAudience"] ?? "pc-house-api";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
}).AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "pchousestore.oidc";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = false;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetValue("Authentication:CookieLifetimeMinutes", 480));
});
builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<PcHouseStoreDbContext>())
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(issuer));
        options.SetAuthorizationEndpointUris("connect/authorize");
        options.SetTokenEndpointUris("connect/token");
        options.SetEndSessionEndpointUris("connect/logout");
        options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        options.RegisterScopes(Scopes.OpenId, Scopes.Profile, "shop.api");
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(builder.Configuration.GetValue("Authentication:AccessTokenMinutes", 10)));
        options.DisableAccessTokenEncryption();
        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentSigningCertificate();
            options.AddDevelopmentEncryptionCertificate();
        }
        else
        {
            var signingPath = builder.Configuration["Authentication:SigningCertificatePath"]
                ?? throw new InvalidOperationException("Authentication:SigningCertificatePath is required outside Development.");
            var signingPassword = builder.Configuration["Authentication:SigningCertificatePassword"];
            var encryptionPath = builder.Configuration["Authentication:EncryptionCertificatePath"]
                ?? throw new InvalidOperationException("Authentication:EncryptionCertificatePath is required outside Development.");
            var encryptionPassword = builder.Configuration["Authentication:EncryptionCertificatePassword"];
            options.AddSigningCertificate(X509CertificateLoader.LoadPkcs12FromFile(signingPath, signingPassword));
            options.AddEncryptionCertificate(X509CertificateLoader.LoadPkcs12FromFile(encryptionPath, encryptionPassword));
        }
        var aspNetCore = options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough();
        if (builder.Environment.IsDevelopment()) aspNetCore.DisableTransportSecurityRequirement();
    })
    .AddValidation(options =>
    {
        options.AddAudiences(apiAudience);
        options.UseLocalServer();
        options.UseAspNetCore();
    });
builder.Services.Configure<OpenIddictServerOptions>(options =>
{
    options.CodeChallengeMethods.Clear();
    options.CodeChallengeMethods.Add("S256");
});
builder.Services.AddCors(options => options.AddPolicy("spa", policy => policy
    .WithOrigins(spaOrigin).AllowAnyHeader().AllowAnyMethod()));
var shopApiPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser().RequireAssertion(context => context.User.HasScope("shop.api"))
    .RequireClaim("client_id", spaClientId).Build();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = shopApiPolicy;
    options.AddPolicy("ShopApi", shopApiPolicy);
});
builder.Services.AddScoped<IPasswordHasher<ShopAccessCredentialRecord>, PasswordHasher<ShopAccessCredentialRecord>>();
builder.Services.AddScoped<IPasswordHasher<EmployeeRecord>, PasswordHasher<EmployeeRecord>>();
builder.Services.AddHostedService<OidcClientRegistration>();
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isEmployeeChallengeEndpoint =
            path.StartsWith("/api/v1/employees", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/access-credential", StringComparison.OrdinalIgnoreCase);
        if (!path.StartsWith("/connect/", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/account/login", StringComparison.OrdinalIgnoreCase) &&
            !isEmployeeChallengeEndpoint)
            return RateLimitPartition.GetNoLimiter("non-authentication");

        var key = $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}:{path}";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        return ValueTask.CompletedTask;
    };
});

var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("spa");
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1"))
    {
        var claims = context.User;
        if (!long.TryParse(claims.FindFirstValue("shop_session_id"), out var sessionId) ||
            !long.TryParse(claims.FindFirstValue("shop_id"), out var shopId) ||
            !long.TryParse(claims.FindFirstValue("company_id"), out var companyId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var db = context.RequestServices.GetRequiredService<PcHouseStoreDbContext>();
        var now = DateTime.UtcNow;
        var active = await db.ShopSessions.AsNoTracking().AnyAsync(x => x.Id == sessionId &&
            x.ShopId == shopId && x.CompanyId == companyId && x.RevokedAt == null &&
            (x.IdleExpiresAt == null || x.IdleExpiresAt > now) &&
            (x.AbsoluteExpiresAt == null || x.AbsoluteExpiresAt > now) &&
            db.Shops.Any(s => s.Id == shopId && s.CompanyId == companyId && s.IsActive) &&
            db.Companies.Any(c => c.Id == companyId && c.IsActive));
        if (!active)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }
    await next();
});
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/connect/authorize", async (HttpContext context, PcHouseStoreDbContext db) =>
{
    var oidcRequest = context.GetOpenIddictServerRequest();
    if (oidcRequest is null) return Results.BadRequest();
    var auth = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    if (!auth.Succeeded)
    {
        var target = context.Request.Path + context.Request.QueryString;
        return Results.Redirect($"/account/login?returnUrl={Uri.EscapeDataString(target)}");
    }

    if (!long.TryParse(auth.Principal?.FindFirstValue("shop_session_id"), out var sessionId))
        return Results.Forbid();
    var session = await db.ShopSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId);
    var now = DateTime.UtcNow;
    if (session is null || session.RevokedAt != null ||
        (session.IdleExpiresAt != null && session.IdleExpiresAt <= now) ||
        (session.AbsoluteExpiresAt != null && session.AbsoluteExpiresAt <= now))
        return Results.Forbid();

    var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        Claims.Name, Claims.Role);
    identity.AddClaim(new Claim(Claims.Subject, auth.Principal!.FindFirstValue(Claims.Subject)!));
    identity.AddClaim(new Claim("company_id", auth.Principal.FindFirstValue("company_id")!));
    identity.AddClaim(new Claim("shop_id", auth.Principal.FindFirstValue("shop_id")!));
    identity.AddClaim(new Claim("shop_session_id", sessionId.ToString()));
    identity.AddClaim(new Claim("client_id", oidcRequest.ClientId!));
    var principal = new ClaimsPrincipal(identity);
    principal.SetScopes(oidcRequest.GetScopes().Where(x => x is Scopes.OpenId or Scopes.Profile or "shop.api"));
    principal.SetResources(apiAudience);
    principal.SetDestinations(claim => claim.Type switch
    {
        Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
        "company_id" or "shop_id" or "shop_session_id" or "client_id" => [Destinations.AccessToken],
        _ => []
    });
    return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}).AllowAnonymous();

app.MapGet("/account/login", (HttpContext context, IAntiforgery antiforgery) =>
{
    var returnUrl = SafeLocalReturnUrl(context.Request.Query["returnUrl"].ToString());
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Content(RenderLoginPage(returnUrl, tokens.RequestToken), "text/html; charset=utf-8");
}).AllowAnonymous();

app.MapPost("/account/login", async (HttpContext context, PcHouseStoreDbContext db,
    IPasswordHasher<ShopAccessCredentialRecord> hasher, IAntiforgery antiforgery,
    CancellationToken cancellationToken) =>
{
    await antiforgery.ValidateRequestAsync(context);
    var form = await context.Request.ReadFormAsync(cancellationToken);
    var code = form["shopCode"].ToString().Trim();
    var password = form["password"].ToString();
    // Resolve the submitted code and active Company/Shop without returning account details.
    var candidate = await (from c in db.ShopAccessCredentials
                           join s in db.Shops on new { c.ShopId, c.CompanyId } equals new { ShopId = s.Id, s.CompanyId }
                           join co in db.Companies on c.CompanyId equals co.Id
                           where s.Code == code && s.IsActive && co.IsActive && c.IsActive
                           select new { Credential = c, Shop = s }).SingleOrDefaultAsync(cancellationToken);
    if (candidate is null || hasher.VerifyHashedPassword(candidate.Credential,
            candidate.Credential.PasswordHash, password) == PasswordVerificationResult.Failed)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Content(
            RenderLoginPage(SafeLocalReturnUrl(form["returnUrl"].ToString()), tokens.RequestToken,
                "Invalid Shop code or password."),
            "text/html; charset=utf-8",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    var now = DateTime.UtcNow;
    var session = new ShopSessionRecord
    {
        CompanyId = candidate.Shop.CompanyId,
        ShopId = candidate.Shop.Id,
        IssuedAt = now
    };
    db.ShopSessions.Add(session);
    await db.SaveChangesAsync(cancellationToken);
    var claims = new[]
    {
        new Claim(Claims.Subject, candidate.Shop.Id.ToString()),
        new Claim("company_id", candidate.Shop.CompanyId.ToString()),
        new Claim("shop_id", candidate.Shop.Id.ToString()),
        new Claim("shop_session_id", session.Id.ToString())
    };
    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
        new AuthenticationProperties { IsPersistent = false, AllowRefresh = false });
    return Results.Redirect(SafeLocalReturnUrl(form["returnUrl"].ToString()));
}).AllowAnonymous();

app.MapGet("/api/v1/session", async (ClaimsPrincipal principal, PcHouseStoreDbContext db,
    CancellationToken cancellationToken) =>
{
    var sessionId = long.Parse(principal.FindFirstValue("shop_session_id")!);
    var shopId = long.Parse(principal.FindFirstValue("shop_id")!);
    var now = DateTime.UtcNow;
    var active = await db.ShopSessions.AsNoTracking().AnyAsync(x => x.Id == sessionId &&
        x.RevokedAt == null && (x.IdleExpiresAt == null || x.IdleExpiresAt > now) &&
        (x.AbsoluteExpiresAt == null || x.AbsoluteExpiresAt > now), cancellationToken);
    if (!active) return Results.Unauthorized();
    var shop = await (from currentShop in db.Shops.AsNoTracking()
                      join company in db.Companies.AsNoTracking() on currentShop.CompanyId equals company.Id
                      where currentShop.Id == shopId
                      select new
                      {
                          currentShop.Code,
                          currentShop.Name,
                          currentShop.Address,
                          currentShop.Phone,
                          CompanyEmail = company.Email
                      }).SingleAsync(cancellationToken);
    return Results.Ok(new ShopSessionDto(principal.FindFirstValue("company_id")!,
        shopId.ToString(), sessionId.ToString(), shop.Code, shop.Name, shop.Address, shop.Phone, shop.CompanyEmail));
}).RequireAuthorization("ShopApi");

app.MapPut("/api/v1/shops/{shopId:long}/access-credential", async (
    long shopId,
    RotateShopCredentialRequest request,
    ClaimsPrincipal shopPrincipal,
    PcHouseStoreDbContext db,
    IPasswordHasher<ShopAccessCredentialRecord> hasher,
    IPasswordHasher<EmployeeRecord> employeeHasher,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    if (!long.TryParse(shopPrincipal.FindFirstValue("company_id"), out var companyId))
        return ApiProblem(context, StatusCodes.Status401Unauthorized, "invalid_shop_token", "Shop authentication is required.");
    if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length is < 12 or > 128 || request.ExpectedCredentialVersion <= 0)
        return ApiProblem(context, StatusCodes.Status400BadRequest, "password_policy_failed", "The new Shop password must contain between 12 and 128 characters and the expected version must be positive.");

    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    var authentication = await EmployeeAdministrationEndpoints.AuthenticateManagerAsync(
        request.EmployeeChallenge, shopPrincipal, db, employeeHasher, context, cancellationToken);
    if (authentication.Error is not null) return authentication.Error;
    var actor = authentication.Employee!;

    var targetShop = await db.Shops.AsNoTracking().SingleOrDefaultAsync(x => x.Id == shopId, cancellationToken);
    if (targetShop is null)
        return ApiProblem(context, StatusCodes.Status404NotFound, "shop_not_found", "The target Shop was not found.");
    if (targetShop.CompanyId != companyId)
        return ApiProblem(context, StatusCodes.Status403Forbidden, "target_shop_outside_company", "The target Shop is outside the active Company.");

    var credential = await db.ShopAccessCredentials.SingleOrDefaultAsync(x =>
        x.ShopId == shopId && x.CompanyId == companyId, cancellationToken);
    if (credential is null)
        return ApiProblem(context, StatusCodes.Status404NotFound, "shop_credential_not_found", "The Shop credential was not found.");
    if (credential.CredentialVersion != request.ExpectedCredentialVersion)
        return ApiProblem(context, StatusCodes.Status409Conflict, "credential_version_conflict", "The Shop credential changed. Reload it and retry.");

    var now = DateTime.UtcNow;
    credential.PasswordHash = hasher.HashPassword(credential, request.NewPassword);
    credential.CredentialVersion++;
    credential.RotatedAt = now;
    credential.UpdatedAt = now;
    credential.UpdatedByEmployeeId = actor.Id;
    db.ShopAccessCredentialEvents.Add(new ShopAccessCredentialEventRecord
    {
        ShopId = shopId,
        CompanyId = companyId,
        EventType = "ROTATED",
        CredentialVersion = credential.CredentialVersion,
        OccurredAt = now,
        ActorType = "EMPLOYEE",
        ActorEmployeeId = actor.Id,
        Reason = "Credential rotated after fresh employee credential validation."
    });
    await db.ShopSessions.Where(x => x.ShopId == shopId && x.CompanyId == companyId && x.RevokedAt == null)
        .ExecuteUpdateAsync(update => update
            .SetProperty(x => x.RevokedAt, now)
            .SetProperty(x => x.RevocationReason, "SHOP_CREDENTIAL_ROTATED")
            .SetProperty(x => x.RowVersion, x => x.RowVersion + 1), cancellationToken);
    await db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return Results.NoContent();
}).RequireAuthorization("ShopApi");

app.MapPatch("/api/v1/shops/{shopId:long}/access-credential", async (
    long shopId,
    ChangeShopCredentialStatusRequest request,
    ClaimsPrincipal shopPrincipal,
    PcHouseStoreDbContext db,
    IPasswordHasher<EmployeeRecord> employeeHasher,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    if (!long.TryParse(shopPrincipal.FindFirstValue("company_id"), out var companyId))
        return ApiProblem(context, StatusCodes.Status401Unauthorized, "invalid_shop_token", "Shop authentication is required.");
    if (request.ExpectedCredentialVersion <= 0)
        return ApiProblem(context, StatusCodes.Status400BadRequest, "invalid_request", "A positive expected credential version is required.");

    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    var authentication = await EmployeeAdministrationEndpoints.AuthenticateManagerAsync(
        request.EmployeeChallenge, shopPrincipal, db, employeeHasher, context, cancellationToken);
    if (authentication.Error is not null) return authentication.Error;
    var actor = authentication.Employee!;

    var targetShop = await db.Shops.AsNoTracking().SingleOrDefaultAsync(x => x.Id == shopId, cancellationToken);
    if (targetShop is null)
        return ApiProblem(context, StatusCodes.Status404NotFound, "shop_not_found", "The target Shop was not found.");
    if (targetShop.CompanyId != companyId)
        return ApiProblem(context, StatusCodes.Status403Forbidden, "target_shop_outside_company", "The target Shop is outside the active Company.");

    var credential = await db.ShopAccessCredentials.SingleOrDefaultAsync(x =>
        x.ShopId == shopId && x.CompanyId == companyId, cancellationToken);
    if (credential is null)
        return ApiProblem(context, StatusCodes.Status404NotFound, "shop_credential_not_found", "The Shop credential was not found.");
    if (credential.CredentialVersion != request.ExpectedCredentialVersion)
        return ApiProblem(context, StatusCodes.Status409Conflict, "credential_version_conflict", "The Shop credential changed. Reload it and retry.");

    var now = DateTime.UtcNow;
    credential.IsActive = request.IsActive;
    credential.CredentialVersion++;
    credential.UpdatedAt = now;
    credential.UpdatedByEmployeeId = actor.Id;
    db.ShopAccessCredentialEvents.Add(new ShopAccessCredentialEventRecord
    {
        ShopId = shopId,
        CompanyId = companyId,
        EventType = request.IsActive ? "ENABLED" : "DISABLED",
        CredentialVersion = credential.CredentialVersion,
        OccurredAt = now,
        ActorType = "EMPLOYEE",
        ActorEmployeeId = actor.Id,
        Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Status changed after fresh employee credential validation." : request.Reason.Trim()
    });
    await db.ShopSessions.Where(x => x.ShopId == shopId && x.CompanyId == companyId && x.RevokedAt == null)
        .ExecuteUpdateAsync(update => update
            .SetProperty(x => x.RevokedAt, now)
            .SetProperty(x => x.RevocationReason, request.IsActive ? "SHOP_CREDENTIAL_STATUS_CHANGED" : "SHOP_CREDENTIAL_DISABLED")
            .SetProperty(x => x.RowVersion, x => x.RowVersion + 1), cancellationToken);
    await db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return Results.NoContent();
}).RequireAuthorization("ShopApi");

app.MapEmployeeAdministrationEndpoints();
app.MapCatalogueEndpoints();

app.MapGet("/connect/logout", async (HttpContext context, PcHouseStoreDbContext db) =>
{
    var auth = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    if (long.TryParse(auth.Principal?.FindFirstValue("shop_session_id"), out var sessionId))
    {
        var session = await db.ShopSessions.SingleOrDefaultAsync(x => x.Id == sessionId);
        if (session is not null && session.RevokedAt is null)
        {
            session.RevokedAt = DateTime.UtcNow;
            session.RevocationReason = "SHOP_LOGOUT";
            await db.SaveChangesAsync();
        }
    }
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    var request = context.GetOpenIddictServerRequest();
    var redirect = request?.PostLogoutRedirectUri;
    if (string.IsNullOrWhiteSpace(redirect)) return Results.Redirect("/");
    if (!string.IsNullOrWhiteSpace(request?.State))
        redirect = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(redirect, "state", request.State);
    return Results.Redirect(redirect);
}).AllowAnonymous();

app.Run();

static string SafeLocalReturnUrl(string? value) =>
    !string.IsNullOrWhiteSpace(value) && value.StartsWith('/') && !value.StartsWith("//") ? value : "/";

static IResult ApiProblem(HttpContext context, int status, string code, string detail) => Results.Problem(
    statusCode: status,
    title: code,
    detail: detail,
    extensions: new Dictionary<string, object?>
    {
        ["code"] = code,
        ["traceId"] = context.TraceIdentifier
    });

static string RenderLoginPage(string returnUrl, string? requestToken, string? error = null)
{
    var encodedReturnUrl = System.Net.WebUtility.HtmlEncode(returnUrl);
    var encodedRequestToken = System.Net.WebUtility.HtmlEncode(requestToken);
    var errorMarkup = string.IsNullOrWhiteSpace(error)
        ? string.Empty
        : $"<p class=\"auth-error\" role=\"alert\">{System.Net.WebUtility.HtmlEncode(error)}</p>";

    return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="theme-color" content="#21466F">
  <title>Sign in to pcHouseStore</title>
  <style>
    :root {
      --pc-navy-900: #21466F;
      --pc-navy-800: #28547F;
      --pc-blue-600: #1688C9;
      --pc-cyan-400: #27B8E8;
      --pc-surface-0: #FFFFFF;
      --pc-surface-50: #F5F7FA;
      --pc-border: #DCE3EA;
      --pc-text-900: #243447;
      --pc-text-600: #5D6B79;
    }
    * { box-sizing: border-box; }
    body {
      display: grid;
      min-width: 320px;
      min-height: 100vh;
      margin: 0;
      padding: 2rem 1.25rem;
      place-items: center;
      background: var(--pc-surface-50);
      color: var(--pc-text-900);
      font-family: Inter, ui-sans-serif, system-ui, sans-serif;
      -webkit-font-smoothing: antialiased;
    }
    .auth-card {
      width: min(100%, 28rem);
      overflow: hidden;
      border: 1px solid var(--pc-border);
      border-radius: 1rem;
      background: var(--pc-surface-0);
      box-shadow: 0 1.25rem 3rem rgb(33 70 111 / 12%);
    }
    .auth-brand {
      display: flex;
      min-height: 5rem;
      align-items: center;
      gap: .75rem;
      padding: 1.25rem 1.75rem;
      background: var(--pc-navy-900);
      color: var(--pc-surface-0);
      font-size: 1rem;
      font-weight: 750;
      letter-spacing: .04em;
    }
    .auth-brand img {
      width: 2.75rem;
      height: 3rem;
      flex: none;
      object-fit: contain;
    }
    .auth-content { padding: 2rem 1.75rem 1.75rem; }
    h1 {
      margin: 0;
      color: var(--pc-navy-900);
      font-size: clamp(1.5rem, 4vw, 1.85rem);
      line-height: 1.2;
    }
    .auth-description {
      margin: .65rem 0 1.5rem;
      color: var(--pc-text-600);
      line-height: 1.55;
    }
    form { display: grid; gap: 1.1rem; }
    label {
      display: grid;
      gap: .45rem;
      font-size: .92rem;
      font-weight: 650;
    }
    input {
      width: 100%;
      min-height: 2.8rem;
      padding: .65rem .8rem;
      border: 1px solid var(--pc-border);
      border-radius: .5rem;
      background: var(--pc-surface-0);
      color: var(--pc-text-900);
      font: inherit;
    }
    input:focus-visible, button:focus-visible {
      outline: 3px solid var(--pc-cyan-400);
      outline-offset: 2px;
    }
    button {
      min-height: 2.75rem;
      margin-top: .25rem;
      padding: .7rem 1.15rem;
      border: 0;
      border-radius: .5rem;
      background: var(--pc-blue-600);
      color: var(--pc-surface-0);
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }
    button:hover { background: var(--pc-navy-800); }
    .auth-error {
      margin: 0 0 1rem;
      padding: .75rem .9rem;
      border: 1px solid #D6A3A3;
      border-radius: .5rem;
      background: #FFF7F7;
      color: #8B2525;
      line-height: 1.45;
    }
    .auth-error p { margin: 0 0 .75rem; }
    @media (max-width: 480px) {
      .auth-content { padding: 1.5rem 1.25rem 1.25rem; }
    }
  </style>
</head>
<body>
  <main class="auth-card">
    <div class="auth-brand"><img src="/assets/pc-house-logo.png" alt="PC House logo"><span>PC HOUSE STORE</span></div>
    <section class="auth-content">
      <h1>Shop sign in</h1>
      <p class="auth-description">Enter your Shop credentials to continue.</p>
      {{errorMarkup}}
      <form method="post" action="/account/login">
        <input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}">
        <input type="hidden" name="__RequestVerificationToken" value="{{encodedRequestToken}}">
        <label>Shop code<input name="shopCode" autocomplete="username" required></label>
        <label>Password<input name="password" type="password" autocomplete="current-password" required></label>
        <button type="submit">Sign in</button>
      </form>
    </section>
  </main>
</body>
</html>
""";
}

public sealed record ShopSessionDto(
    string CompanyId,
    string ShopId,
    string ShopSessionId,
    string ShopCode,
    string ShopName,
    string? ShopAddress,
    string? ShopPhone,
    string? CompanyEmail);
public sealed record RotateShopCredentialRequest(
    string NewPassword, long ExpectedCredentialVersion, PcHouseStore.Api.EmployeeChallengeRequest EmployeeChallenge);
public sealed record ChangeShopCredentialStatusRequest(
    bool IsActive, long ExpectedCredentialVersion, PcHouseStore.Api.EmployeeChallengeRequest EmployeeChallenge, string? Reason = null);
public partial class Program { }

public sealed class OidcClientRegistration(IServiceScopeFactory scopeFactory, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var clientId = configuration["Authentication:SpaClientId"] ?? "pchousestore-spa";
        var origin = configuration["Authentication:SpaOrigin"]!;
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = ClientTypes.Public,
            DisplayName = "pcHouseStore Web",
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + Scopes.OpenId,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + "shop.api"
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange }
        };
        descriptor.RedirectUris.Add(new Uri(origin.TrimEnd('/') + "/auth/callback"));
        descriptor.RedirectUris.Add(new Uri(origin.TrimEnd('/') + "/auth/silent-callback"));
        descriptor.PostLogoutRedirectUris.Add(new Uri(origin.TrimEnd('/') + "/auth/logout-callback"));
        var application = await manager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null) await manager.CreateAsync(descriptor, cancellationToken);
        else await manager.UpdateAsync(application, descriptor, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
