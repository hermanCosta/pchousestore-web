using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PcHouseStore.Infrastructure.Persistence;
using PcHouseStore.Infrastructure.Persistence.Entities;

const string prefix = "EmployeeBootstrap";
const string shopPrefix = "ShopCredentialProvisioning";
var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var operation = configuration[$"{prefix}:Operation"] ?? "BootstrapAdmin";
var connectionString = configuration.GetConnectionString("PcHouseStore");
var companyId = configuration.GetValue<long?>($"{prefix}:CompanyId");
var employeeCode = configuration[$"{prefix}:EmployeeCode"]?.Trim().ToUpperInvariant();
var currentShopCode = configuration[$"{shopPrefix}:CurrentCode"]?.Trim().ToUpperInvariant();
var newShopCode = configuration[$"{shopPrefix}:Code"]?.Trim().ToUpperInvariant();
var firstName = configuration[$"{prefix}:FirstName"]?.Trim();
var lastName = configuration[$"{prefix}:LastName"]?.Trim();
var phone = configuration[$"{prefix}:Phone"]?.Trim();
var initialPassword = operation == "ProvisionShopCredential"
    ? configuration[$"{shopPrefix}:InitialPassword"]
    : configuration[$"{prefix}:InitialPassword"];
var needsEmployeeCode = operation is "BootstrapAdmin" or "BootstrapManager" or "ResetEmployeeCredential";

if (string.IsNullOrWhiteSpace(connectionString) || companyId is null or <= 0 ||
    (needsEmployeeCode && string.IsNullOrWhiteSpace(employeeCode)) || string.IsNullOrEmpty(initialPassword) ||
    ((operation is "BootstrapAdmin" or "BootstrapManager") && (string.IsNullOrWhiteSpace(firstName) ||
        string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(phone))) ||
    (operation == "ProvisionShopCredential" &&
        (string.IsNullOrWhiteSpace(currentShopCode) || string.IsNullOrWhiteSpace(newShopCode))))
{
    Console.Error.WriteLine("Operator configuration is incomplete. Check the operation-specific Company, employee or Shop fields, and initial password settings.");
    return 2;
}

if (initialPassword.Length is > 128 || (operation == "ProvisionShopCredential" ? initialPassword.Length < 12 : initialPassword.Length < 6) ||
    (employeeCode?.Length > 40) || currentShopCode?.Length > 40 || newShopCode?.Length > 40)
{
    Console.Error.WriteLine("Bootstrap configuration contains a value outside the supported limits.");
    return 2;
}

if (operation is not ("BootstrapAdmin" or "BootstrapManager" or "ResetEmployeeCredential" or "ProvisionShopCredential"))
{
    Console.Error.WriteLine("Unsupported operator operation.");
    return 2;
}

var options = new DbContextOptionsBuilder<PcHouseStoreDbContext>()
    .UseNpgsql(connectionString)
    .UseOpenIddict()
    .Options;

await using var db = new PcHouseStoreDbContext(options);
await using var transaction = await db.Database.BeginTransactionAsync();

var company = await db.Companies.FromSqlInterpolated($"SELECT * FROM company WHERE id = {companyId} FOR UPDATE")
    .SingleOrDefaultAsync();
if (company is null || !company.IsActive)
{
    Console.Error.WriteLine("The configured Company does not exist or is inactive.");
    return 3;
}

var hasher = new PasswordHasher<EmployeeRecord>();
if (operation == "ProvisionShopCredential")
{
    var shop = await db.Shops.FromSqlInterpolated(
            $"SELECT * FROM shop WHERE company_id = {companyId} AND code = {currentShopCode} FOR UPDATE")
        .SingleOrDefaultAsync();
    if (shop is null || !shop.IsActive)
    {
        Console.Error.WriteLine("The target Shop does not exist or is inactive.");
        return 3;
    }

    var codeInUse = await db.Shops.AnyAsync(x => x.CompanyId == companyId && x.Code == newShopCode && x.Id != shop.Id);
    if (codeInUse)
    {
        Console.Error.WriteLine("The requested Shop code is already in use in this Company.");
        return 4;
    }

    var provisionedAt = DateTime.UtcNow;
    var credential = await db.ShopAccessCredentials.SingleOrDefaultAsync(x => x.ShopId == shop.Id && x.CompanyId == companyId);
    var isProvisioning = credential is null;
    if (credential is null)
    {
        credential = new ShopAccessCredentialRecord
        {
            ShopId = shop.Id,
            CompanyId = shop.CompanyId,
            CredentialVersion = 1,
            IsActive = true,
            CreatedAt = provisionedAt,
            UpdatedAt = provisionedAt
        };
        db.ShopAccessCredentials.Add(credential);
    }
    else
    {
        credential.CredentialVersion++;
        credential.IsActive = true;
        credential.RotatedAt = provisionedAt;
        credential.UpdatedAt = provisionedAt;
    }

    credential.PasswordHash = new PasswordHasher<ShopAccessCredentialRecord>().HashPassword(credential, initialPassword);
    shop.Code = newShopCode!;
    db.ShopAccessCredentialEvents.Add(new ShopAccessCredentialEventRecord
    {
        ShopId = shop.Id,
        CompanyId = shop.CompanyId,
        EventType = isProvisioning ? "PROVISIONED" : "RESET",
        CredentialVersion = credential.CredentialVersion,
        OccurredAt = provisionedAt,
        ActorType = "DEPLOYMENT_OPERATOR",
        Reason = $"Credential set by local deployment operator {Environment.UserDomainName}\\{Environment.UserName}@{Environment.MachineName}."
    });
    await db.ShopSessions.Where(x => x.ShopId == shop.Id && x.CompanyId == companyId && x.RevokedAt == null)
        .ExecuteUpdateAsync(update => update
            .SetProperty(x => x.RevokedAt, provisionedAt)
            .SetProperty(x => x.RevocationReason, "SHOP_CREDENTIAL_PROVISIONED")
            .SetProperty(x => x.RowVersion, x => x.RowVersion + 1));
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    Console.WriteLine($"Shop {newShopCode} credential was {(isProvisioning ? "provisioned" : "reset")}.");
    return 0;
}

if (operation == "ResetEmployeeCredential")
{
    var target = await db.Employees.FromSqlInterpolated(
            $"SELECT * FROM employee WHERE company_id = {companyId} AND employee_code = {employeeCode} FOR UPDATE")
        .SingleOrDefaultAsync();
    if (target is null)
    {
        Console.Error.WriteLine("The target employee does not exist in the configured Company.");
        return 3;
    }

    var resetAt = DateTime.UtcNow;
    target.PasswordHash = hasher.HashPassword(target, initialPassword);
    target.MustChangePasswordAtNextUse = true;
    target.CredentialVersion++;
    target.UpdatedAt = resetAt;
    db.EmployeeAdministrationEvents.Add(new EmployeeAdministrationEventRecord
    {
        CompanyId = companyId.Value,
        ActorType = "DEPLOYMENT_OPERATOR",
        ActorIdentifier = $"{Environment.UserDomainName}\\{Environment.UserName}@{Environment.MachineName}",
        TargetEmployeeId = target.Id,
        ActionCode = "EMPLOYEE_CREDENTIAL_RESET_BY_DEPLOYMENT_OPERATOR",
        OccurredAt = resetAt
    });
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    Console.WriteLine($"Credential reset for employee {target.EmployeeCode}. They must change it before any protected action.");
    return 0;
}

var hasActiveAdmin = await db.EmployeeRoleAssignments.AnyAsync(assignment =>
    assignment.CompanyId == companyId && assignment.RoleCode == "ADMIN" && assignment.IsActive &&
    db.Employees.Any(employee => employee.Id == assignment.EmployeeId && employee.CompanyId == companyId && employee.IsActive));
if (hasActiveAdmin)
{
    Console.Error.WriteLine("Bootstrap refused: the Company already has an active ADMIN. Existing credentials were not changed.");
    return 4;
}

var now = DateTime.UtcNow;
var companyIdValue = companyId.Value;
var person = new PersonRecord
{
    CompanyId = companyIdValue,
    FirstName = firstName!,
    LastName = lastName!,
    ContactNo = phone!
};
db.People.Add(person);
await db.SaveChangesAsync();

var employee = new EmployeeRecord
{
    CompanyId = companyIdValue,
    PersonId = person.Id,
    EmployeeCode = employeeCode!,
    IsActive = true,
    MustChangePasswordAtNextUse = true,
    CredentialVersion = 1,
    CreatedAt = now,
    UpdatedAt = now
};
employee.PasswordHash = hasher.HashPassword(employee, initialPassword);
db.Employees.Add(employee);
await db.SaveChangesAsync();

db.EmployeeRoleAssignments.Add(new EmployeeRoleAssignmentRecord
{
    CompanyId = companyIdValue,
    EmployeeId = employee.Id,
    RoleCode = "ADMIN",
    IsActive = true,
    AssignedAt = now
});
db.EmployeeAdministrationEvents.Add(new EmployeeAdministrationEventRecord
{
    CompanyId = companyIdValue,
    ActorType = "DEPLOYMENT_OPERATOR",
    ActorIdentifier = $"{Environment.UserDomainName}\\{Environment.UserName}@{Environment.MachineName}",
    TargetEmployeeId = employee.Id,
    ActionCode = "EMPLOYEE_BOOTSTRAP_ADMIN_CREATED",
    OccurredAt = now
});
await db.SaveChangesAsync();
await transaction.CommitAsync();

Console.WriteLine($"ADMIN employee {employee.EmployeeCode} was provisioned for Company {companyIdValue}. A password change is required before any protected action.");
return 0;
