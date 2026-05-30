namespace IngenIA365ERP.API.Filters;

/// <summary>
/// Marca un endpoint Carter como requiriendo el permiso indicado en el JWT
/// del request (claim <c>perm</c>). Aplicado vía el extension method
/// <see cref="PermissionAuthorizationExtensions.RequirePermission"/>.
///
/// El <see cref="PermissionAuthorizationFilter"/> lee este metadata y, si
/// el usuario no tiene el permiso, responde <c>404 Generic.NotFound</c>
/// (indistinguible de un endpoint inexistente — FR-017, SC-005).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute
{
    public string PermissionCode { get; }

    public RequirePermissionAttribute(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
            throw new ArgumentException("El código de permiso es obligatorio.", nameof(permissionCode));
        PermissionCode = permissionCode;
    }
}
