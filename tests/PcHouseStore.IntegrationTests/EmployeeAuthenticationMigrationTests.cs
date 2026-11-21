using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PcHouseStore.Api;
using PcHouseStore.Infrastructure.Persistence.Migrations;

namespace PcHouseStore.IntegrationTests;

public sealed class EmployeeAuthenticationMigrationTests
{
    [Theory]
    [InlineData("123456", true)]
    [InlineData("12345", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("1234567890123456789012345678901234567890", true)]
    public void EmployeePasswordPolicy_UsesApprovedSixTo128CharacterRange(string? password, bool expected)
    {
        Assert.Equal(expected, EmployeeCredentialPolicy.IsValidPassword(password));
    }

    [Theory]
    [InlineData("EMPLOYEE")]
    [InlineData("MANAGER")]
    [InlineData("ADMIN")]
    public void EmployeeProfilePolicy_AcceptsExactlyOneSupportedProfile(string profile)
    {
        Assert.True(EmployeeCredentialPolicy.IsValidProfile([profile]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("TECHNICIAN")]
    [InlineData("MANAGER,EMPLOYEE")]
    public void EmployeeProfilePolicy_RejectsEmptyRetiredOrMultipleProfiles(string roles)
    {
        var profiles = roles.Length == 0 ? Array.Empty<string>() : roles.Split(',');
        Assert.False(EmployeeCredentialPolicy.IsValidProfile(profiles));
    }

    [Fact]
    public void ReconciliationMigration_DropsOneUseAuthorizationAndEnablesCurrentProfiles()
    {
        var migration = new RemoveEmployeeActionAuthorization();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var up = typeof(RemoveEmployeeActionAuthorization).GetMethod(
            "Up", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(up);
        up!.Invoke(migration, [builder]);

        Assert.Contains(builder.Operations, operation =>
            operation is DropTableOperation table && table.Name == "employee_action_authorization");
        Assert.Contains(builder.Operations, operation =>
            operation is AddCheckConstraintOperation check &&
            check.Name == "ck_employee_role_code" &&
            check.Sql.Contains("'EMPLOYEE'", StringComparison.Ordinal) &&
            check.Sql.Contains("'ADMIN'", StringComparison.Ordinal));
        Assert.Contains(builder.Operations, operation =>
            operation is CreateIndexOperation index &&
            index.Name == "uq_employee_one_active_profile" &&
            index.IsUnique);
    }
}
