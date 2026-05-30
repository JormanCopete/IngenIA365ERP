namespace IngenIA365ERP.Application.Admin.Branches.Common;

public sealed record BranchDto(
    Guid PublicId,
    Guid TenantPublicId,
    string Code,
    string Name,
    string? Address,
    string? City,
    string? Department,
    string? Phone,
    bool IsActive,
    bool IsHeadquarters);

public static class BranchErrorCodes
{
    public const string CodeTaken = "Admin.Branches.CodeTaken";
    public const string HeadquartersExists = "Admin.Branches.HeadquartersExists";
    public const string CannotDeactivateHeadquarters = "Admin.Branches.CannotDeactivateHeadquarters";
    public const string AlreadyInactive = "Admin.Branches.AlreadyInactive";
}
