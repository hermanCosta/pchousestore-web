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

if (operation == "BootstrapDevelopment")
{
    var allowDevelopmentBootstrap = configuration.GetValue<bool>("DevelopmentBootstrap:Allow");
    var databaseConnection = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
    var companyName = configuration["DevelopmentBootstrap:CompanyName"]?.Trim();
    var companyAddress = configuration["DevelopmentBootstrap:CompanyAddress"]?.Trim();
    var companyContact = configuration["DevelopmentBootstrap:CompanyContact"]?.Trim();
    var companyEmail = configuration["DevelopmentBootstrap:CompanyEmail"]?.Trim();
    var countryCode = (configuration["DevelopmentBootstrap:CountryCode"] ?? "IE").Trim().ToUpperInvariant();
    var currencyCode = (configuration["DevelopmentBootstrap:CurrencyCode"] ?? "EUR").Trim().ToUpperInvariant();
    var companyTimeZone = configuration["DevelopmentBootstrap:TimeZone"] ?? "Europe/Dublin";
    var shopName = configuration["ShopCredentialProvisioning:Name"]?.Trim();
    var shopAddress = configuration["ShopCredentialProvisioning:Address"]?.Trim();
    var shopPhone = configuration["ShopCredentialProvisioning:Phone"]?.Trim();
    var shopPassword = configuration["ShopCredentialProvisioning:InitialPassword"];
    var adminFirstName = firstName;
    var adminLastName = lastName;
    var adminPhone = phone;
    var adminEmail = configuration["EmployeeBootstrap:Email"]?.Trim();

    if (!allowDevelopmentBootstrap ||
        databaseConnection.Database != "pchousestore_dev" ||
        !(databaseConnection.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
          databaseConnection.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
          databaseConnection.Host.Equals("::1", StringComparison.OrdinalIgnoreCase) ||
          databaseConnection.Host == "/tmp"))
    {
        Console.Error.WriteLine("Development bootstrap is restricted to the local pchousestore_dev database. Set DevelopmentBootstrap:Allow=true explicitly.");
        return 2;
    }

    if (string.IsNullOrWhiteSpace(companyName) || companyName.Length > 200 ||
        string.IsNullOrWhiteSpace(companyAddress) || string.IsNullOrWhiteSpace(companyContact) ||
        countryCode.Length != 2 || currencyCode.Length != 3 ||
        string.IsNullOrWhiteSpace(newShopCode) || newShopCode.Length > 40 ||
        string.IsNullOrWhiteSpace(shopName) || shopName.Length > 200 ||
        string.IsNullOrWhiteSpace(employeeCode) || employeeCode.Length > 40 ||
        string.IsNullOrWhiteSpace(adminFirstName) || string.IsNullOrWhiteSpace(adminLastName) ||
        string.IsNullOrWhiteSpace(adminPhone) || string.IsNullOrWhiteSpace(initialPassword) ||
        initialPassword.Length is < 6 or > 128 ||
        string.IsNullOrEmpty(shopPassword) || shopPassword.Length is < 12 or > 128)
    {
        Console.Error.WriteLine("Development bootstrap configuration is incomplete or contains values outside supported limits.");
        return 2;
    }

    var developmentOptions = new DbContextOptionsBuilder<PcHouseStoreDbContext>()
        .UseNpgsql(connectionString)
        .UseOpenIddict()
        .Options;

    await using var developmentDb = new PcHouseStoreDbContext(developmentOptions);
    if (!await developmentDb.Database.CanConnectAsync())
    {
        Console.Error.WriteLine("The configured development database is unavailable.");
        return 3;
    }

    var pendingMigrations = await developmentDb.Database.GetPendingMigrationsAsync();
    if (pendingMigrations.Any())
    {
        Console.Error.WriteLine("Apply all database migrations before running the development bootstrap.");
        return 3;
    }

    await using var developmentTransaction = await developmentDb.Database.BeginTransactionAsync();
    if (await developmentDb.Companies.AnyAsync() ||
        await developmentDb.Shops.AnyAsync() ||
        await developmentDb.Employees.AnyAsync())
    {
        Console.Error.WriteLine("Development bootstrap refused: the database already contains Company, Shop or Employee records. No data was changed.");
        return 4;
    }

    var createdAt = DateTime.UtcNow;
    var companyRecord = new CompanyRecord
    {
        Name = companyName,
        Address = companyAddress,
        ContactOne = companyContact,
        Email = string.IsNullOrWhiteSpace(companyEmail) ? null : companyEmail,
        CountryCode = countryCode,
        DefaultCurrencyCode = currencyCode,
        TimeZone = companyTimeZone,
        IsActive = true
    };
    developmentDb.Companies.Add(companyRecord);
    await developmentDb.SaveChangesAsync();

    var shopRecord = new ShopRecord
    {
        CompanyId = companyRecord.Id,
        Code = newShopCode,
        Name = shopName,
        Address = string.IsNullOrWhiteSpace(shopAddress) ? null : shopAddress,
        Phone = string.IsNullOrWhiteSpace(shopPhone) ? null : shopPhone,
        TimeZone = companyTimeZone,
        IsActive = true,
        NextServiceOrderDisplayNumber = 1
    };
    developmentDb.Shops.Add(shopRecord);
    await developmentDb.SaveChangesAsync();

    var personRecord = new PersonRecord
    {
        CompanyId = companyRecord.Id,
        FirstName = adminFirstName!,
        LastName = adminLastName!,
        ContactNo = adminPhone!,
        Email = string.IsNullOrWhiteSpace(adminEmail) ? null : adminEmail
    };
    developmentDb.People.Add(personRecord);
    await developmentDb.SaveChangesAsync();

    var adminRecord = new EmployeeRecord
    {
        CompanyId = companyRecord.Id,
        PersonId = personRecord.Id,
        EmployeeCode = employeeCode!,
        IsActive = true,
        MustChangePasswordAtNextUse = true,
        CredentialVersion = 1,
        CreatedAt = createdAt,
        UpdatedAt = createdAt
    };
    adminRecord.PasswordHash = new PasswordHasher<EmployeeRecord>().HashPassword(adminRecord, initialPassword);
    developmentDb.Employees.Add(adminRecord);
    await developmentDb.SaveChangesAsync();

    developmentDb.EmployeeRoleAssignments.Add(new EmployeeRoleAssignmentRecord
    {
        CompanyId = companyRecord.Id,
        EmployeeId = adminRecord.Id,
        RoleCode = "ADMIN",
        IsActive = true,
        AssignedAt = createdAt
    });

    var shopCredential = new ShopAccessCredentialRecord
    {
        ShopId = shopRecord.Id,
        CompanyId = companyRecord.Id,
        CredentialVersion = 1,
        IsActive = true,
        CreatedAt = createdAt,
        UpdatedAt = createdAt
    };
    shopCredential.PasswordHash = new PasswordHasher<ShopAccessCredentialRecord>()
        .HashPassword(shopCredential, shopPassword);
    developmentDb.ShopAccessCredentials.Add(shopCredential);
    developmentDb.ShopAccessCredentialEvents.Add(new ShopAccessCredentialEventRecord
    {
        ShopId = shopRecord.Id,
        CompanyId = companyRecord.Id,
        EventType = "PROVISIONED",
        CredentialVersion = 1,
        OccurredAt = createdAt,
        ActorType = "DEPLOYMENT_OPERATOR",
        Reason = "Initial local development credentials provisioned by the deployment operator."
    });

    await developmentDb.SaveChangesAsync();
    await developmentTransaction.CommitAsync();

    Console.WriteLine("Local development Company, Shop, Shop credential and ADMIN employee were provisioned.");
    Console.WriteLine($"Shop login code: {shopRecord.Code}");
    Console.WriteLine($"Initial ADMIN employee code: {adminRecord.EmployeeCode}");
    Console.WriteLine("The ADMIN employee must change the initial password before performing protected actions.");
    return 0;
}

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
await db.SaveChangesAsync();
await transaction.CommitAsync();

Console.WriteLine($"ADMIN employee {employee.EmployeeCode} was provisioned for Company {companyIdValue}. A password change is required before any protected action.");
return 0;
