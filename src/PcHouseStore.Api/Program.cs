using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
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
    .RequireAuthenticatedUser().RequireClaim("scope", "shop.api")
    .RequireClaim("client_id", spaClientId).Build();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = shopApiPolicy;
    options.AddPolicy("ShopApi", shopApiPolicy);
});
builder.Services.AddScoped<IPasswordHasher<ShopAccessCredentialRecord>, PasswordHasher<ShopAccessCredentialRecord>>();
builder.Services.AddHostedService<OidcClientRegistration>();
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/connect/", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/account/login", StringComparison.OrdinalIgnoreCase))
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
    var page = $"""<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>Entrar na pcHouseStore</title><main><h1>Entrar na Shop</h1><form method="post" action="/account/login"><input type="hidden" name="returnUrl" value="{System.Net.WebUtility.HtmlEncode(returnUrl)}"><input type="hidden" name="__RequestVerificationToken" value="{System.Net.WebUtility.HtmlEncode(tokens.RequestToken)}"><label>Código da Shop <input name="shopCode" autocomplete="username" required></label><label>Senha <input name="password" type="password" autocomplete="current-password" required></label><button type="submit">Entrar</button></form></main></html>""";
    return Results.Content(page, "text/html; charset=utf-8");
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
        return Results.Unauthorized();

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
    var now = DateTime.UtcNow;
    var active = await db.ShopSessions.AsNoTracking().AnyAsync(x => x.Id == sessionId &&
        x.RevokedAt == null && (x.IdleExpiresAt == null || x.IdleExpiresAt > now) &&
        (x.AbsoluteExpiresAt == null || x.AbsoluteExpiresAt > now), cancellationToken);
    if (!active) return Results.Unauthorized();
    return Results.Ok(new ShopSessionDto(principal.FindFirstValue("company_id")!,
        principal.FindFirstValue("shop_id")!, sessionId.ToString()));
}).RequireAuthorization("ShopApi");

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
    return Results.Redirect(string.IsNullOrWhiteSpace(redirect) ? "/" : redirect);
}).AllowAnonymous();

app.Run();

static string SafeLocalReturnUrl(string? value) =>
    !string.IsNullOrWhiteSpace(value) && value.StartsWith('/') && !value.StartsWith("//") ? value : "/";

public sealed record ShopSessionDto(string CompanyId, string ShopId, string ShopSessionId);
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
        descriptor.PostLogoutRedirectUris.Add(new Uri(origin.TrimEnd('/') + "/"));
        var application = await manager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null) await manager.CreateAsync(descriptor, cancellationToken);
        else await manager.UpdateAsync(application, descriptor, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
