namespace IngenIA365ERP.Application.Security.Users.Common;

/// <summary>
/// Proyección de usuario para listados — solo los campos seguros y útiles.
/// </summary>
public sealed record UserListItemDto(
    Guid PublicId,
    string Username,
    string? Email,
    bool IsActive,
    bool IsMfaEnabled,
    bool IsLocked,
    DateTime? LastLoginAt,
    IReadOnlyList<string> RoleCodes);

/// <summary>Detalle de un usuario individual.</summary>
public sealed record UserDetailDto(
    Guid PublicId,
    string Username,
    string? Email,
    string? IdentificationNumber,
    int? PersonId,
    bool IsActive,
    bool IsDeleted,
    bool IsEmailVerified,
    bool IsMfaEnabled,
    bool IsSaasOperator,
    bool MustChangePassword,
    DateTime? LastLoginAt,
    DateTime? LastPasswordChangeAt,
    int FailedLoginAttempts,
    DateTime? LockoutEndAt,
    IReadOnlyList<UserRoleDto> Roles,
    IReadOnlyList<UserBranchDto> Branches);

public sealed record UserRoleDto(Guid PublicId, string Code, string Name);
public sealed record UserBranchDto(Guid PublicId, string Code, string Name, bool IsDefault);

/// <summary>Códigos namespaced canónicos del módulo Users (FR-048).</summary>
public static class UserErrorCodes
{
    public const string UsernameTaken = "Security.Users.UsernameTaken";
    public const string EmailTaken = "Security.Users.EmailTaken";
    public const string AlreadyAssignedRole = "Security.Users.AlreadyAssignedRole";
    public const string NotAssignedRole = "Security.Users.NotAssignedRole";
    public const string RoleNotAssignable = "Security.Users.RoleNotAssignable";
    public const string BranchNotInTenant = "Security.Users.BranchNotInTenant";
    public const string AlreadyAssignedBranch = "Security.Users.AlreadyAssignedBranch";
    public const string AlreadyDisabled = "Security.Users.AlreadyDisabled";
    public const string NotDisabled = "Security.Users.NotDisabled";
    public const string NotLocked = "Security.Users.NotLocked";
    public const string PasswordPolicyViolation = "Auth.PasswordPolicyViolation";
}
