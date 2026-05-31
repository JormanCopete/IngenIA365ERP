using Microsoft.AspNetCore.Http;

namespace IngenIA365ERP.API.Filters.CentralIdentity;

/// <summary>
/// Filtro para endpoints de administración de empresa (T046). Verifica:
/// <list type="number">
///   <item><c>purpose == full</c> (rechaza challenge tokens, ver
///         <see cref="RequirePurposeAttribute"/>).</item>
///   <item><c>tenant_admin == true</c> en los claims del JWT.</item>
///   <item><c>active_tenant_id</c> coincide con el <c>{tenantPublicId}</c>
///         de la ruta (si la ruta lo incluye).</item>
/// </list>
///
/// <para>El master admin (<c>is_global_master_admin == true</c>) pasa el filtro
/// automáticamente sobre cualquier tenant — el spec autoriza al master a operar
/// sobre cualquier empresa.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireTenantAdminAttribute : Attribute
{
    /// <summary>
    /// Verifica si el contexto cumple los requisitos. Retorna un resultado typed
    /// que el endpoint Carter convierte a HTTP (403 + errorCode).
    /// </summary>
    public static TenantAdminGuardResult Check(HttpContext http, string? routeTenantPublicId = null)
    {
        var user = http.User;
        if (user?.Identity?.IsAuthenticated != true)
            return TenantAdminGuardResult.Unauthenticated();

        var purpose = user.FindFirst("purpose")?.Value ?? "full";
        if (purpose != "full")
            return TenantAdminGuardResult.WrongPurpose(purpose);

        // Master admin bypass.
        var isMaster = string.Equals(
            user.FindFirst("is_global_master_admin")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        if (isMaster)
            return TenantAdminGuardResult.Allowed();

        var isTenantAdmin = string.Equals(
            user.FindFirst("tenant_admin")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        if (!isTenantAdmin)
            return TenantAdminGuardResult.NotAdmin();

        if (!string.IsNullOrEmpty(routeTenantPublicId))
        {
            var jwtTenant = user.FindFirst("active_tenant_id")?.Value;
            if (!string.Equals(jwtTenant, routeTenantPublicId, StringComparison.OrdinalIgnoreCase))
                return TenantAdminGuardResult.WrongTenant(jwtTenant, routeTenantPublicId);
        }

        return TenantAdminGuardResult.Allowed();
    }
}

public sealed record TenantAdminGuardResult(bool IsAllowed, string? ErrorCode, string? Message)
{
    public static TenantAdminGuardResult Allowed() => new(true, null, null);

    public static TenantAdminGuardResult Unauthenticated() =>
        new(false, "Identity.Unauthenticated", "Se requiere autenticación.");

    public static TenantAdminGuardResult WrongPurpose(string actual) =>
        new(false, "Identity.WrongTokenPurpose",
            $"Este endpoint requiere purpose=full, recibido '{actual}'.");

    public static TenantAdminGuardResult NotAdmin() =>
        new(false, "Membership.NotTenantAdmin", "Se requiere ser administrador de la empresa.");

    public static TenantAdminGuardResult WrongTenant(string? jwtTenant, string routeTenant) =>
        new(false, "Membership.WrongActiveTenant",
            $"El JWT está activo en otro tenant ({jwtTenant ?? "<none>"}); la ruta exige {routeTenant}.");
}
