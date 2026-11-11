namespace PcHouseStore.Infrastructure.Persistence.Entities;

// Persistence records for the DATA-05 authentication foundation. These are never API DTOs.
public sealed class CompanyRecord
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class ShopRecord
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class EmployeeRecord
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class EmployeeRoleAssignmentRecord
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long EmployeeId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime AssignedAt { get; set; }
    public long? AssignedByEmployeeId { get; set; }
}

public sealed class ShopAccessCredentialRecord
{
    public long ShopId { get; set; }
    public long CompanyId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public long CredentialVersion { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RotatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long? UpdatedByEmployeeId { get; set; }
}

public sealed class ShopAccessCredentialEventRecord
{
    public long Id { get; set; }
    public long ShopId { get; set; }
    public long CompanyId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public long CredentialVersion { get; set; }
    public DateTime OccurredAt { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public long? ActorEmployeeId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class ShopSessionRecord
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long ShopId { get; set; }
    public string? OidcAuthorizationId { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? IdleExpiresAt { get; set; }
    public DateTime? AbsoluteExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
    public long RowVersion { get; set; }
}

public sealed class EmployeeActionAuthorizationRecord
{
    public long Id { get; set; }
    public byte[] AuthorizationTokenHash { get; set; } = [];
    public long ShopSessionId { get; set; }
    public long CompanyId { get; set; }
    public long ShopId { get; set; }
    public long? TargetShopId { get; set; }
    public long EmployeeId { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public byte[]? OperationContextHash { get; set; }
    public DateTime AuthenticatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public long RowVersion { get; set; }
}

public sealed class SystemServiceSigningKeyRecord
{
    public long Id { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? ActiveSigningSlot { get; private set; }
    public string KeyId { get; set; } = string.Empty;
    public string KeyPurpose { get; set; } = "SIGNING";
    public string Algorithm { get; set; } = "RS256";
    public byte[] PublicCertificateDer { get; set; } = [];
    public byte[]? PrivateKeyCiphertext { get; set; }
    public byte[]? CipherNonce { get; set; }
    public byte[]? CipherTag { get; set; }
    public string? KekReference { get; set; }
    public string Status { get; set; } = "STAGED";
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? RetiredAt { get; set; }
}

public sealed class SystemServiceKeyEventRecord
{
    public long Id { get; set; }
    public long SystemServiceId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public long? ActorEmployeeId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
