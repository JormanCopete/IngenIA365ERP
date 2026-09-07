using System.Security.Claims;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// Implementación de <see cref="ICurrentCentralUserContext"/> que lee los
/// claims del <c>HttpContext.User</c> emitidos por <c>CentralJwtIssuer</c>
/// (T037). Singleton + <see cref="IHttpContextAccessor"/> — el contexto por
/// request se resuelve a través del accessor (no se cachea entre requests).
/// </summary>
internal sealed class CurrentCentralUserContextAccessor(
    IHttpContextAccessor httpContextAccessor) : ICurrentCentralUserContext
{
    private const string DefaultPurpose = "full";

    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? CentralUserId
    {
        get
        {
            var raw = User?.FindFirst("sub")?.Value
                ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email =>
        User?.FindFirst("email")?.Value ?? User?.FindFirst(ClaimTypes.Email)?.Value;

    public bool IsGlobalMasterAdmin =>
        bool.TryParse(User?.FindFirst("is_global_master_admin")?.Value, out var v) && v;

    public Guid? ActiveTenantPublicId
    {
        get
        {
            var raw = User?.FindFirst("active_tenant_id")?.Value;
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public bool TenantAdmin =>
        bool.TryParse(User?.FindFirst("tenant_admin")?.Value, out var v) && v;

    public string Purpose =>
        User?.FindFirst("purpose")?.Value ?? DefaultPurpose;

    public bool MfaVerified =>
        bool.TryParse(User?.FindFirst("mfa_verified")?.Value, out var v) && v;

    /// <summary>
    /// Un claim ausente, uno vacío y uno con basura dan todos <c>Ninguno</c>. No se
    /// distinguen a propósito: los tres significan «no consta con qué entró», y
    /// lanzar aquí tumbaría el ingreso de quien trae un token emitido por la
    /// versión anterior.
    /// </summary>
    public MetodosMfa MetodoMfa =>
        int.TryParse(User?.FindFirst("mfa_method")?.Value, out var v)
            ? (MetodosMfa)v
            : MetodosMfa.Ninguno;

    public bool IsAuthenticated => CentralUserId.HasValue;
}
