namespace IngenIA365ERP.Application.Security.Roles.Common;

/// <summary>
/// Snapshot de un rol con los permisos asignados. Identificado por
/// <c>PublicId</c> (Guid) por Principio VI.
/// </summary>
public sealed record RoleDto(
    Guid PublicId,
    string Code,
    string Name,
    string? Description,
    bool IsBuiltIn,
    bool IsAssignable,
    bool IsActive,
    IReadOnlyList<RolePermissionDto> Permissions);

/// <summary>Permiso asignado a un rol — proyección mínima.</summary>
public sealed record RolePermissionDto(
    Guid PublicId,
    string Code);

/// <summary>Códigos namespaced canónicos para fallos del módulo Roles.</summary>
public static class RoleErrorCodes
{
    public const string BuiltInImmutable = "Security.Roles.BuiltInImmutable";
    public const string CodeAlreadyExists = "Security.Roles.CodeAlreadyExists";
    public const string PermissionsInvalid = "Security.Roles.PermissionsInvalid";
    public const string CannotDeleteBuiltIn = "Security.Roles.CannotDeleteBuiltIn";
    public const string CannotDeleteWithUsers = "Security.Roles.CannotDeleteWithUsers";
}
