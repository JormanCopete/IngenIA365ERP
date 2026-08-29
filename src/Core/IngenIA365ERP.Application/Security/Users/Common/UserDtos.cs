namespace IngenIA365ERP.Application.Security.Users.Common;

/// <summary>
/// Proyección de usuario para listados — solo los campos seguros y útiles.
/// </summary>
/// <param name="IsMfaEnabled">
/// <c>null</c> = no se sabe. Y esa distinción es el arreglo: estos tres datos
/// se leían de <c>SEC_Users</c>, donde ya no los escribe nadie —el segundo
/// factor se inscribe sobre la identidad central, el último acceso lo sella
/// ella y el bloqueo por intentos vive en Redis—, así que la pantalla decía
/// «MFA: No», «Bloqueado: (vacío)» y último acceso en blanco para todo el
/// mundo. Un dato inventado con aspecto de dato real es peor que un hueco:
/// quien administra usuarios toma decisiones con él.
/// </param>
/// <param name="IsLocked"><c>null</c> = no se sabe (la persona no tiene correo con el que consultarlo).</param>
public sealed record UserListItemDto(
    Guid PublicId,
    string Username,
    string? Email,
    bool IsActive,
    bool? IsMfaEnabled,
    bool? IsLocked,
    DateTime? LastLoginAt,
    IReadOnlyList<string> RoleCodes);

/// <summary>Detalle de un usuario individual.</summary>
/// <param name="IsMfaEnabled">
/// <c>null</c> = no se sabe. Mismo arreglo que en <see cref="UserListItemDto"/> y
/// por el mismo motivo: salía de <c>SEC_Users.IsMfaEnabled</c>, cuya única
/// escritura en todo el código es el seed poniéndola en <c>false</c>. O sea que
/// este endpoint respondía «segundo factor: no» para <b>todo el mundo</b>,
/// incluido quien tiene TOTP y passkeys inscritos. Ahora sale de la identidad
/// central, que es donde vive.
/// </param>
/// <param name="LastLoginAt">
/// También de la identidad central, y también <c>null</c> cuando no se sabe. La
/// columna homónima de <c>SEC_Users</c> tampoco la escribe nadie.
/// </param>
/// <param name="FailedLoginAttempts">
/// <b>Sigue saliendo de <c>SEC_Users</c> y sigue mintiendo</b>: el contador de
/// intentos vive en Redis. Se deja como estaba porque volverlo honesto obliga a
/// cambiar el tipo y a consultar Redis, y eso es un cambio de contrato aparte.
/// Lo mismo vale para <c>LockoutEndAt</c>, del que además no hay «hasta cuándo»
/// disponible: el listado sólo puede decir sí/no.
/// </param>
public sealed record UserDetailDto(
    Guid PublicId,
    string Username,
    string? Email,
    string? IdentificationNumber,
    int? PersonId,
    bool IsActive,
    bool IsDeleted,
    bool IsEmailVerified,
    bool? IsMfaEnabled,
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
