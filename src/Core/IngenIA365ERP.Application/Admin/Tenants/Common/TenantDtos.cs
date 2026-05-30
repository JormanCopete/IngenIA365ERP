namespace IngenIA365ERP.Application.Admin.Tenants.Common;

public sealed record TenantListItemDto(
    Guid PublicId,
    string Name,
    string? Nit,
    string? Subdomain,
    string PlanType,
    bool IsActive,
    bool IsSuspended,
    int BranchCount,
    DateTime? ActivatedAt,
    DateTime? SuspendedAt);

public sealed record TenantDetailDto(
    Guid PublicId,
    string Name,
    string SchemaName,
    string? Subdomain,
    string PlanType,
    string? Nit,
    string? LegalName,
    string? LegalAddress,
    string? TaxRegime,
    string ContactEmail,
    string? ContactPhone,
    bool IsActive,
    bool IsSuspended,
    int MaxUsers,
    long StorageLimitMb,
    DateTime? ActivatedAt,
    DateTime? SuspendedAt,
    DateTime CreatedAt);

public static class TenantErrorCodes
{
    public const string NitTaken = "Admin.Tenants.NitTaken";
    public const string SubdomainTaken = "Admin.Tenants.SubdomainTaken";
    public const string SchemaTaken = "Admin.Tenants.SchemaTaken";
    public const string AlreadyActive = "Admin.Tenants.AlreadyActive";
    public const string AlreadySuspended = "Admin.Tenants.AlreadySuspended";
}
