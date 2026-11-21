using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcHouseStore.Infrastructure.Persistence;
using PcHouseStore.Infrastructure.Persistence.Entities;

namespace PcHouseStore.Api;

public static class EmployeeAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeAdministrationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/employees").RequireAuthorization("ShopApi");
        group.MapPost("/queries", SearchAsync);
        group.MapPost("", CreateAsync);
        group.MapPost("/{employeeId:long}/query", GetAsync);
        group.MapPut("/{employeeId:long}", UpdateAsync);
        group.MapPatch("/{employeeId:long}/status", ChangeStatusAsync);
        group.MapPut("/{employeeId:long}/roles", ChangeRolesAsync);
        group.MapPost("/{employeeId:long}/credential-reset", ResetCredentialAsync);
        group.MapPut("/me/credential", ChangeOwnCredentialAsync);
        return routes;
    }

    private static async Task<IResult> SearchAsync(
        EmployeeDirectoryQuery request,
        ClaimsPrincipal principal,
        PcHouseStoreDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (request.Search?.Length > 200)
            return Problem(context, 400, "invalid_request", "Search text cannot exceed 200 characters.");

        if (!TryReadShopContext(principal, out var sessionId, out var shopId, out var companyId))
            return Problem(context, 401, "invalid_shop_token", "Shop authentication is required.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var session = await db.ShopSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (!IsSessionActive(session, companyId, shopId, now) ||
            !await db.Shops.AnyAsync(x => x.Id == shopId && x.CompanyId == companyId && x.IsActive, cancellationToken) ||
            !await db.Companies.AnyAsync(x => x.Id == companyId && x.IsActive, cancellationToken))
            return Problem(context, 401, "shop_session_revoked", "The Shop session is no longer active.");

        var query = db.Employees.AsNoTracking().Where(x => x.CompanyId == companyId);
        var term = request.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(term))
            query = query.Where(x => x.EmployeeCode.ToLower().Contains(term) ||
                db.People.Any(person => person.Id == x.PersonId && person.CompanyId == companyId &&
                    (person.FirstName.ToLower().Contains(term) || person.LastName.ToLower().Contains(term) ||
                     person.ContactNo.ToLower().Contains(term) || (person.Email != null && person.Email.ToLower().Contains(term)))));
        var employees = await query.OrderBy(x => x.EmployeeCode).ToListAsync(cancellationToken);
        var dtos = await ToDtosAsync(db, employees, cancellationToken);
        db.EmployeeAdministrationEvents.Add(new EmployeeAdministrationEventRecord
        {
            CompanyId = companyId,
            ActorType = "SHOP_SESSION",
            ActorIdentifier = sessionId.ToString(CultureInfo.InvariantCulture),
            ActionCode = "EMPLOYEE_DIRECTORY_READ",
            OccurredAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new EmployeeDirectoryResponse(dtos));
    }

    private static async Task<IResult> CreateAsync(
        EmployeeCreateRequest request,
        ClaimsPrincipal principal,
        PcHouseStoreDbContext db,
        IPasswordHasher<EmployeeRecord> hasher,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validation = ValidateProfile(request.FirstName, request.LastName, request.Phone, request.Email);
        if (validation is not null) return Problem(context, 400, "invalid_request", validation);
        var employeeCode = request.EmployeeCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(employeeCode) || employeeCode.Length > 40 || !ValidPassword(request.InitialPassword))
            return Problem(context, 400, "invalid_request", "Employee code or initial password is invalid.");
        if (!ValidRoles(request.Roles)) return Problem(context, 400, "invalid_roles", "Assign one or both supported employee roles.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await AuthenticateManagerAsync(request.EmployeeChallenge, principal, db, hasher, context, cancellationToken);
        if (result.Error is not null) return result.Error;

        var actor = result.Employee!;
        var companyId = actor.CompanyId;
        var exists = await db.Employees.AnyAsync(x => x.CompanyId == actor.CompanyId && x.EmployeeCode == employeeCode, cancellationToken);
        if (exists) return Problem(context, 409, "employee_code_conflict", "An employee with this code already exists in the Company.");

        var now = DateTime.UtcNow;
        var person = new PersonRecord
        {
            CompanyId = actor.CompanyId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            ContactNo = request.Phone.Trim(),
            Email = NormalizeEmail(request.Email)
        };
        db.People.Add(person);
        await db.SaveChangesAsync(cancellationToken);

        var employee = new EmployeeRecord
        {
            CompanyId = actor.CompanyId,
            PersonId = person.Id,
            EmployeeCode = employeeCode,
            IsActive = true,
            MustChangePasswordAtNextUse = true,
            CredentialVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        employee.PasswordHash = hasher.HashPassword(employee, request.InitialPassword);
        db.Employees.Add(employee);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var roleCode in request.Roles.Select(NormalizeRole).Distinct(StringComparer.Ordinal))
            db.EmployeeRoleAssignments.Add(new EmployeeRoleAssignmentRecord
            {
                CompanyId = actor.CompanyId,
                EmployeeId = employee.Id,
                RoleCode = roleCode,
                IsActive = true,
                AssignedAt = now,
                AssignedByEmployeeId = actor.Id
            });
        db.EmployeeAdministrationEvents.Add(NewEvent(actor.CompanyId, actor.Id, employee.Id, "EMPLOYEE_CREATED"));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var dto = (await ToDtosAsync(db, [employee], cancellationToken)).Single();
        return Results.Created($"/api/v1/employees/{employee.Id}", dto);
    }

    private static async Task<IResult> GetAsync(
        long employeeId,
        ClaimsPrincipal principal,
        PcHouseStoreDbContext db,
        IPasswordHasher<EmployeeRecord> hasher,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await AuthenticateManagerAsync(request.EmployeeChallenge, principal, db, hasher, context, cancellationToken);
        if (result.Error is not null) return result.Error;
        var actor = result.Employee!;
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == employeeId && x.CompanyId == actor.CompanyId, cancellationToken);
        if (employee is null) return Problem(context, 404, "employee_not_found", "The Employee was not found.");
        var dto = (await ToDtosAsync(db, [employee], cancellationToken)).Single();
        db.EmployeeAdministrationEvents.Add(NewEvent(actor.CompanyId, actor.Id, employeeId, "EMPLOYEE_READ"));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(dto);
    }

    private static async Task<IResult> UpdateAsync(
        long employeeId,
        EmployeeProfileUpdateRequest request,
        ClaimsPrincipal principal,
        PcHouseStoreDbContext db,
        IPasswordHasher<EmployeeRecord> hasher,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validation = ValidateProfile(request.FirstName, request.LastName, request.Phone, request.Email);
        if (validation is not null) return Problem(context, 400, "invalid_request", validation);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await AuthenticateManagerAsync(request.EmployeeChallenge, principal, db, hasher, context, cancellationToken);
        if (result.Error is not null) return result.Error;
        var actor = result.Employee!;
        var employee = await LockEmployeeAsync(db, employeeId, actor.CompanyId, cancellationToken);
        if (employee is null) return Problem(context, 404, "employee_not_found", "The Employee was not found.");

        var person = employee.PersonId is long personId
            ? await db.People.SingleOrDefaultAsync(x => x.Id == personId && x.CompanyId == actor.CompanyId, cancellationToken)
            : null;
        if (person is null)
        {
            person = new PersonRecord { CompanyId = actor.CompanyId };
            db.People.Add(person);
            await db.SaveChangesAsync(cancellationToken);
            employee.PersonId = person.Id;
        }
        person.FirstName = request.FirstName.Trim();
        person.LastName = request.LastName.Trim();
        person.ContactNo = request.Phone.Trim();
        person.Email = NormalizeEmail(request.Email);
        employee.UpdatedAt = DateTime.UtcNow;
        db.EmployeeAdministrationEvents.Add(NewEvent(actor.CompanyId, actor.Id, employeeId, "EMPLOYEE_PROFILE_UPDATED"));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangeStatusAsync(
        long employeeId,
        EmployeeStatusChangeRequest request,
        ClaimsPrincipal principal,
        PcHouseStoreDbContext db,
        IPasswordHasher<EmployeeRecord> hasher,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await AuthenticateManagerAsync(request.EmployeeChallenge, principal, db, hasher, context, cancellationToken);
        if (result.Error is not null) return result.Error;
        var actor = result.Employee!;
        var employee = await LockEmployeeAsync(db, employeeId, actor.CompanyId, cancellationToken);
        if (employee is null) return Problem(context, 404, "employee_not_found", "The Employee was not found.");
        employee.IsActive = request.IsActive;
        employee.UpdatedAt = DateTime.UtcNow;
        db.EmployeeAdministrationEvents.Add(NewEvent(actor.CompanyId, actor.Id, employeeId,
            request.IsActive ? "EMPLOYEE_REACTIVATED" : "EMPLOYEE_DEACTIVATED"));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangeRolesAsync(
        long employeeId,
        EmployeeRoleChangeRequest request,
        ClaimsPrincipal principal,
        PcHouseStoreDbContext db,
        IPasswordHasher<EmployeeRecord> hasher,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!ValidRoles(request.Roles)) return Problem(context, 400, "invalid_roles", "Assign one or both supported employee roles.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await AuthenticateManagerAsync(request.EmployeeChallenge, principal, db, hasher, context, cancellationToken);
        if (result.Error is not null) return result.Error;
        var actor = result.Employee!;
        var employee = await LockEmployeeAsync(db, employeeId, actor.CompanyId, cancellationToken);
        if (employee is null) return Problem(context, 404, "employee_not_found", "The Employee was not found.");

        var now = DateTime.UtcNow;
        var assignments = await db.EmployeeRoleAssignments.Where(x => x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);
        var desired = request.Roles.Select(NormalizeRole).ToHashSet(StringComparer.Ordinal);
        foreach (var assignment in assignments)
        {
            var shouldBeActive = desired.Contains(assignment.RoleCode);
            if (assignment.IsActive != shouldBeActive)
            {
                assignment.IsActive = shouldBeActive;
                if (shouldBeActive)
                {
                    assignment.AssignedAt = now;
                    assignment.AssignedByEmployeeId = actor.Id;
                }
            }
        }
        foreach (var missingRole in desired.Except(assignments.Select(x => x.RoleCode), StringComparer.Ordinal))
            db.EmployeeRoleAssignments.Add(new EmployeeRoleAssignmentRecord
            {
                CompanyId = actor.CompanyId,
                EmployeeId = employeeId,
                RoleCode = missingRole,
                IsActive = true,
                AssignedAt = now,
                AssignedByEmployeeId = actor.Id
            });
        employee.UpdatedAt = now;
        db.EmployeeAdministrationEvents.Add(NewEvent(actor.CompanyId, actor.Id, employeeId, "EMPLOYEE_ROLES_CHANGED"));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetCredentialAsync(
        long employeeId,
        EmployeeCredentialResetRequest request,
        ClaimsPrincipal principal,
        PcHouseStoreDbContext db,
        IPasswordHasher<EmployeeRecord> hasher,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!ValidPassword(request.NewPassword)) return PasswordProblem(context);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await AuthenticateManagerAsync(request.EmployeeChallenge, principal, db, hasher, context, cancellationToken);
        if (result.Error is not null) return result.Error;
        var actor = result.Employee!;
        var employee = await LockEmployeeAsync(db, employeeId, actor.CompanyId, cancellationToken);
        if (employee is null) return Problem(context, 404, "employee_not_found", "The Employee was not found.");
        var now = DateTime.UtcNow;
        employee.PasswordHash = hasher.HashPassword(employee, request.NewPassword);
        employee.MustChangePasswordAtNextUse = true;
        employee.CredentialVersion++;
        employee.UpdatedAt = now;
        db.EmployeeAdministrationEvents.Add(NewEvent(actor.CompanyId, actor.Id, employeeId, "EMPLOYEE_CREDENTIAL_RESET"));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangeOwnCredentialAsync(
        EmployeeCredentialChangeRequest request,
        ClaimsPrincipal principal,
        PcHouseStoreDbContext db,
        IPasswordHasher<EmployeeRecord> hasher,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!ValidPassword(request.NewPassword)) return PasswordProblem(context);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await AuthenticateEmployeeAsync(request.EmployeeChallenge, principal, db, hasher,
            context, cancellationToken, managerOnly: false, allowMandatoryPasswordChange: true);
        if (result.Error is not null) return result.Error;

        var actor = result.Employee!;
        var now = DateTime.UtcNow;
        actor.PasswordHash = hasher.HashPassword(actor, request.NewPassword);
        actor.MustChangePasswordAtNextUse = false;
        actor.CredentialVersion++;
        actor.UpdatedAt = now;
        db.EmployeeAdministrationEvents.Add(NewEvent(actor.CompanyId, actor.Id, actor.Id, "EMPLOYEE_PASSWORD_CHANGED"));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    internal static Task<EmployeeAuthenticationResult> AuthenticateManagerAsync(
        EmployeeChallengeRequest challenge,
        ClaimsPrincipal principal,
        PcHouseStoreDbContext db,
        IPasswordHasher<EmployeeRecord> hasher,
        HttpContext context,
        CancellationToken cancellationToken) =>
        AuthenticateEmployeeAsync(challenge, principal, db, hasher, context, cancellationToken,
            managerOnly: true, allowMandatoryPasswordChange: false);

    private static async Task<EmployeeAuthenticationResult> AuthenticateEmployeeAsync(
        EmployeeChallengeRequest challenge,
        ClaimsPrincipal principal,
        PcHouseStoreDbContext db,
        IPasswordHasher<EmployeeRecord> hasher,
        HttpContext context,
        CancellationToken cancellationToken,
        bool managerOnly,
        bool allowMandatoryPasswordChange)
    {
        if (!TryReadShopContext(principal, out var sessionId, out var shopId, out var companyId))
            return new EmployeeAuthenticationResult(null,
                Problem(context, 401, "invalid_shop_token", "Shop authentication is required."));

        var employeeCode = challenge.EmployeeCode?.Trim().ToUpperInvariant();
        var password = challenge.Password;
        if (string.IsNullOrWhiteSpace(employeeCode) || employeeCode.Length > 40 ||
            string.IsNullOrWhiteSpace(password) || password.Length > 128)
            return new EmployeeAuthenticationResult(null,
                Problem(context, 400, "invalid_employee_challenge", "Employee code and password are required."));

        var now = DateTime.UtcNow;
        var session = await db.ShopSessions
            .FromSqlInterpolated($"SELECT * FROM shop_session WHERE id = {sessionId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (!IsSessionActive(session, companyId, shopId, now) ||
            !await db.Shops.AnyAsync(x => x.Id == shopId && x.CompanyId == companyId && x.IsActive, cancellationToken) ||
            !await db.Companies.AnyAsync(x => x.Id == companyId && x.IsActive, cancellationToken))
            return new EmployeeAuthenticationResult(null,
                Problem(context, 401, "shop_session_revoked", "The Shop session is no longer active."));

        var actor = await db.Employees
            .FromSqlInterpolated($"SELECT * FROM employee WHERE company_id = {companyId} AND employee_code = {employeeCode} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (actor is null || !actor.IsActive ||
            hasher.VerifyHashedPassword(actor, actor.PasswordHash, password) == PasswordVerificationResult.Failed)
            return new EmployeeAuthenticationResult(null,
                Problem(context, 401, "invalid_employee_credentials", "Employee credentials are invalid."));

        if (!allowMandatoryPasswordChange && actor.MustChangePasswordAtNextUse)
            return new EmployeeAuthenticationResult(null,
                Problem(context, 403, "employee_password_change_required", "Change the employee password before performing another protected action."));

        if (managerOnly && !await db.EmployeeRoleAssignments.AnyAsync(x =>
                x.CompanyId == companyId && x.EmployeeId == actor.Id && x.IsActive &&
                (x.RoleCode == "MANAGER" || x.RoleCode == "ADMIN"), cancellationToken))
            return new EmployeeAuthenticationResult(null,
                Problem(context, 403, "action_not_permitted", "The employee is not permitted to perform this action."));

        return new EmployeeAuthenticationResult(actor, null);
    }

    private static async Task<EmployeeRecord?> LockEmployeeAsync(PcHouseStoreDbContext db, long employeeId, long companyId, CancellationToken cancellationToken) =>
        await db.Employees.FromSqlInterpolated($"SELECT * FROM employee WHERE id = {employeeId} AND company_id = {companyId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private static async Task<IReadOnlyList<EmployeeDto>> ToDtosAsync(PcHouseStoreDbContext db, IReadOnlyCollection<EmployeeRecord> employees, CancellationToken cancellationToken)
    {
        var employeeIds = employees.Select(x => x.Id).ToArray();
        var personIds = employees.Where(x => x.PersonId.HasValue).Select(x => x.PersonId!.Value).Distinct().ToArray();
        var people = await db.People.AsNoTracking().Where(x => personIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var roles = await db.EmployeeRoleAssignments.AsNoTracking()
            .Where(x => employeeIds.Contains(x.EmployeeId) && x.IsActive)
            .Select(x => new { x.EmployeeId, x.RoleCode }).ToListAsync(cancellationToken);
        return employees.Select(employee =>
        {
            people.TryGetValue(employee.PersonId ?? 0, out var person);
            return new EmployeeDto(employee.Id.ToString(CultureInfo.InvariantCulture), employee.EmployeeCode,
                person?.FirstName, person?.LastName, person?.ContactNo, person?.Email, employee.IsActive,
                roles.Where(x => x.EmployeeId == employee.Id).Select(x => x.RoleCode).OrderBy(x => x).ToArray(),
                employee.MustChangePasswordAtNextUse);
        }).ToArray();
    }

    private static EmployeeAdministrationEventRecord NewEvent(long companyId, long actorEmployeeId, long? targetEmployeeId, string actionCode) => new()
    {
        CompanyId = companyId,
        ActorEmployeeId = actorEmployeeId,
        ActorType = "EMPLOYEE",
        TargetEmployeeId = targetEmployeeId,
        ActionCode = actionCode,
        OccurredAt = DateTime.UtcNow
    };

    private static string? ValidateProfile(string? firstName, string? lastName, string? phone, string? email)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(phone))
            return "First name, last name and phone are required.";
        if (email?.Length > 320) return "Email cannot exceed 320 characters.";
        return null;
    }

    private static string? NormalizeEmail(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    private static bool ValidPassword(string? password) => !string.IsNullOrEmpty(password) && password.Length is >= 12 and <= 128;
    private static bool ValidRoles(IReadOnlyCollection<string>? roles) => roles is { Count: > 0 and <= 2 } &&
        roles.All(x => x is "MANAGER" or "TECHNICIAN") && roles.Distinct(StringComparer.Ordinal).Count() == roles.Count;
    private static string NormalizeRole(string role) => role.Trim().ToUpperInvariant();

    private static bool TryReadShopContext(ClaimsPrincipal principal, out long sessionId, out long shopId, out long companyId)
    {
        sessionId = 0;
        shopId = 0;
        companyId = 0;
        return long.TryParse(principal.FindFirstValue("shop_session_id"), out sessionId) &&
            long.TryParse(principal.FindFirstValue("shop_id"), out shopId) &&
            long.TryParse(principal.FindFirstValue("company_id"), out companyId);
    }

    private static bool IsSessionActive(ShopSessionRecord? session, long companyId, long shopId, DateTime now) =>
        session is not null && session.CompanyId == companyId && session.ShopId == shopId && session.RevokedAt is null &&
        (session.IdleExpiresAt is null || session.IdleExpiresAt > now) &&
        (session.AbsoluteExpiresAt is null || session.AbsoluteExpiresAt > now);

    private static IResult PasswordProblem(HttpContext context) =>
        Problem(context, 400, "password_policy_failed", "The password must contain between 12 and 128 characters.");

    private static IResult Problem(HttpContext context, int status, string code, string detail) => Results.Problem(
        statusCode: status,
        title: code,
        detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = context.TraceIdentifier });

    private static AuthorizationResult Failure(IResult error) => new(null, error);
    internal sealed record EmployeeAuthenticationResult(EmployeeRecord? Employee, IResult? Error);
}

public sealed record EmployeeDirectoryQuery(string? Search);
public sealed record EmployeeDirectoryResponse(IReadOnlyList<EmployeeDto> Items);
public sealed record EmployeeChallengeRequest(string EmployeeCode, string Password);
public sealed record EmployeeCreateRequest(string EmployeeCode, string FirstName, string LastName, string Phone, string? Email, string InitialPassword, string[] Roles, EmployeeChallengeRequest EmployeeChallenge);
public sealed record EmployeeProfileUpdateRequest(string FirstName, string LastName, string Phone, string? Email, EmployeeChallengeRequest EmployeeChallenge);
public sealed record EmployeeStatusChangeRequest(bool IsActive, EmployeeChallengeRequest EmployeeChallenge);
public sealed record EmployeeRoleChangeRequest(string[] Roles, EmployeeChallengeRequest EmployeeChallenge);
public sealed record EmployeeCredentialResetRequest(string NewPassword, EmployeeChallengeRequest EmployeeChallenge);
public sealed record EmployeeCredentialChangeRequest(string NewPassword, EmployeeChallengeRequest EmployeeChallenge);
public sealed record EmployeeDto(string EmployeeId, string EmployeeCode, string? FirstName, string? LastName,
    string? Phone, string? Email, bool IsActive, IReadOnlyList<string> Roles, bool MustChangePasswordAtNextUse);
