using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PcHouseStore.Infrastructure.Persistence;
using PcHouseStore.Infrastructure.Persistence.Entities;

const string prefix = "EmployeeBootstrap";
var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var operation = configuration[$"{prefix}:Operation"] ?? "BootstrapManager";
var connectionString = configuration.GetConnectionString("PcHouseStore");
var companyId = configuration.GetValue<long?>($"{prefix}:CompanyId");
var employeeCode = configuration[$"{prefix}:EmployeeCode"]?.Trim().ToUpperInvariant();
var firstName = configuration[$"{prefix}:FirstName"]?.Trim();
var lastName = configuration[$"{prefix}:LastName"]?.Trim();
var phone = configuration[$"{prefix}:Phone"]?.Trim();
var initialPassword = configuration[$"{prefix}:InitialPassword"];

if (string.IsNullOrWhiteSpace(connectionString) || companyId is null or <= 0 ||
    string.IsNullOrWhiteSpace(employeeCode) || string.IsNullOrEmpty(initialPassword) ||
    (operation == "BootstrapManager" && (string.IsNullOrWhiteSpace(firstName) ||
        string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(phone))))
{
    Console.Error.WriteLine("Operator configuration is incomplete. Set ConnectionStrings__PcHouseStore, EmployeeBootstrap__CompanyId, EmployeeCode, InitialPassword, and the profile fields for BootstrapManager.");
    return 2;
}

if (initialPassword.Length is < 12 or > 128 || employeeCode.Length > 40)
{
    Console.Error.WriteLine("Bootstrap configuration contains a value outside the supported limits.");
    return 2;
}

if (operation is not ("BootstrapManager" or "ResetEmployeeCredential"))
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

var hasActiveManager = await db.EmployeeRoleAssignments.AnyAsync(assignment =>
    assignment.CompanyId == companyId && assignment.RoleCode == "MANAGER" && assignment.IsActive &&
    db.Employees.Any(employee => employee.Id == assignment.EmployeeId && employee.CompanyId == companyId && employee.IsActive));
if (hasActiveManager)
{
    Console.Error.WriteLine("Bootstrap refused: the Company already has an active Manager. Existing credentials were not changed.");
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
    EmployeeCode = employeeCode,
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
    RoleCode = "MANAGER",
    IsActive = true,
    AssignedAt = now
});
db.EmployeeAdministrationEvents.Add(new EmployeeAdministrationEventRecord
{
    CompanyId = companyIdValue,
    ActorType = "DEPLOYMENT_OPERATOR",
    ActorIdentifier = $"{Environment.UserDomainName}\\{Environment.UserName}@{Environment.MachineName}",
    TargetEmployeeId = employee.Id,
    ActionCode = "EMPLOYEE_BOOTSTRAP_MANAGER_CREATED",
    OccurredAt = now
});
await db.SaveChangesAsync();
await transaction.CommitAsync();

Console.WriteLine($"Manager employee {employee.EmployeeCode} was provisioned for Company {companyIdValue}. A password change is required before any protected action.");
return 0;
