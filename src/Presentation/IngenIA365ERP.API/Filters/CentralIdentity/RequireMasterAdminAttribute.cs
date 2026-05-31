using Microsoft.AspNetCore.Http;

namespace IngenIA365ERP.API.Filters.CentralIdentity;

/// <summary>
/// Filtro para endpoints SaaS-globales (T047). Verifica:
/// <list type="number">
///   <item><c>purpose == full</c></item>
///   <item><c>is_global_master_admin == true</c></item>
/// </list>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireMasterAdminAttribute : Attribute
{
    public static MasterAdminGuardResult Check(HttpContext http)
    {
        var user = http.User;
        if (user?.Identity?.IsAuthenticated != true)
            return MasterAdminGuardResult.Unauthenticated();

        var purpose = user.FindFirst("purpose")?.Value ?? "full";
        if (purpose != "full")
            return MasterAdminGuardResult.WrongPurpose(purpose);

        var isMaster = string.Equals(
            user.FindFirst("is_global_master_admin")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        return isMaster
            ? MasterAdminGuardResult.Allowed()
            : MasterAdminGuardResult.NotMaster();
    }
}

public sealed record MasterAdminGuardResult(bool IsAllowed, string? ErrorCode, string? Message)
{
    public static MasterAdminGuardResult Allowed() => new(true, null, null);

    public static MasterAdminGuardResult Unauthenticated() =>
        new(false, "Identity.Unauthenticated", "Se requiere autenticación.");

    public static MasterAdminGuardResult WrongPurpose(string actual) =>
        new(false, "Identity.WrongTokenPurpose",
            $"Este endpoint requiere purpose=full, recibido '{actual}'.");

    public static MasterAdminGuardResult NotMaster() =>
        new(false, "Saas.NotMasterAdmin", "Solo el administrador master puede ejecutar esta operación.");
}
