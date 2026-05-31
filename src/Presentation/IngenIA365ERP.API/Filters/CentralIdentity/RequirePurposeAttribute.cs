using Microsoft.AspNetCore.Http;

namespace IngenIA365ERP.API.Filters.CentralIdentity;

/// <summary>
/// Filtro Carter/endpoint para JWT con claim <c>purpose</c> acotado (T047a).
/// Acepta uno o más purposes válidos; rechaza con <c>403 Identity.WrongTokenPurpose</c>
/// si el JWT presenta otro.
///
/// <para>Uso típico:</para>
/// <list type="bullet">
///   <item><c>[RequirePurpose("mfa-verify")]</c> sobre <c>/api/auth/mfa/verify</c></item>
///   <item><c>[RequirePurpose("mfa-enroll","full")]</c> sobre <c>/api/profile/mfa/enroll</c>
///         (admite enrollment forzado desde login y voluntario desde perfil).</item>
///   <item><c>[RequirePurpose("tenant-select","full")]</c> sobre <c>/api/sessions/select-tenant</c></item>
///   <item><c>[RequirePurpose("full")]</c> sobre endpoints de negocio normales.</item>
/// </list>
///
/// <para>
/// Convención: tokens sin claim <c>purpose</c> son tratados como <c>full</c> para
/// compatibilidad hacia atrás con código de Fase 0 que no lo emite. Una vez
/// retirado el emisor antiguo, este fallback se elimina.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequirePurposeAttribute : Attribute
{
    public IReadOnlyList<string> AllowedPurposes { get; }

    public RequirePurposeAttribute(params string[] allowedPurposes)
    {
        if (allowedPurposes is null || allowedPurposes.Length == 0)
            throw new ArgumentException("Debe especificar al menos un purpose.", nameof(allowedPurposes));

        AllowedPurposes = allowedPurposes;
    }

    /// <summary>
    /// Helper para los endpoints Carter: extrae el claim purpose del HttpContext
    /// y verifica si es uno de los permitidos. Retorna <c>true</c> si OK.
    /// </summary>
    public static bool IsAllowed(HttpContext http, IReadOnlyList<string> allowedPurposes)
    {
        var purposeClaim = http.User?.FindFirst("purpose")?.Value ?? "full";
        return allowedPurposes.Contains(purposeClaim);
    }
}
