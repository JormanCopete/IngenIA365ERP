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

    /// <summary>
    /// Quien esta haciendo la operacion, para el rastro de auditoria.
    ///
    /// <para>
    /// Leia el claim <c>name</c> y, como respaldo, <c>Identity.Name</c>. El emisor
    /// de identidad central no pone ninguno de los dos: emite <c>sub, jti, iat,
    /// email, is_global_master_admin, mfa_verified, purpose</c> y poco mas. Asi que
    /// esto devolvia null y los handlers caian a su <c>?? "SYSTEM"</c>: todo el
    /// rastro de auditoria de las operaciones administrativas quedaba firmado por
    /// un literal, sin decir quien lo hizo. Eso vacia de sentido el Principio VII.
    /// </para>
    ///
    /// <para>
    /// Se lee el correo, que es lo que el emisor si pone. El mapeo de entrada por
    /// defecto lo renombra al URI largo de <see cref="ClaimTypes.Email"/>, asi que
    /// se miran los dos nombres.
    /// </para>
    /// </summary>
    public string? UserName =>
        User?.FindFirst(ClaimTypes.Email)?.Value
        ?? User?.FindFirst("email")?.Value
        ?? User?.FindFirst("name")?.Value
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
