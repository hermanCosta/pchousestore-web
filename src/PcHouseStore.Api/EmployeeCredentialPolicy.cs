namespace PcHouseStore.Api;

public static class EmployeeCredentialPolicy
{
    public static bool IsValidPassword(string? password) =>
        !string.IsNullOrEmpty(password) && password.Length is >= 6 and <= 128;

    public static bool IsValidProfile(IReadOnlyCollection<string>? roles) =>
        roles is { Count: 1 } &&
        roles.All(role => role is "EMPLOYEE" or "MANAGER" or "ADMIN");
}
