using System.Security.Claims;
using IngenIA365ERP.Application.Common.Interfaces;

namespace IngenIA365ERP.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public int? UserId
    {
        get
        {
            var raw = User?.FindFirst("uid")?.Value
                   ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? UserName =>
        User?.FindFirst("name")?.Value
        ?? User?.Identity?.Name;

    /// <summary>
    /// Id INTERNO de la cooperativa activa, como cadena. No el PublicId.
    ///
    /// <para>
    /// Es lo que esperan quienes lo consumen: <c>ListRolesQueryHandler</c> hace
    /// <c>int.TryParse</c> sobre esto, y <c>AssignRoleCommandHandler</c> lo usa
    /// como parte de la clave de cache de permisos.
    /// </para>
    ///
    /// <para>
    /// Leia el claim <c>tenant_id</c>, que el emisor de identidad central nunca
    /// pone —emite <c>active_tenant_id</c>, y con el PublicId—, asi que siempre
    /// devolvia null: el listado de roles colapsaba a las plantillas y la
    /// invalidacion de cache escribia en una clave vacia, compartida entre
    /// cooperativas. Ahora sale de donde el Id interno ya esta resuelto.
    /// </para>
    /// </summary>
    public string? TenantId
    {
        get
        {
            var items = _httpContextAccessor.HttpContext?.Items;
            if (items is not null &&
                items.TryGetValue("TenantId", out var resuelto) &&
                resuelto is int id)
            {
                return id.ToString();
            }

            // Tokens del emisor heredado, que si trae el claim.
            return User?.FindFirst("tenant_id")?.Value;
        }
    }

    public IReadOnlyList<string> Roles =>
        User?.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList()
        ?? new List<string>();

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
