using System.Security.Claims;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// Resuelve, para la petición en curso, qué permisos tiene quien la hace dentro
/// de la cooperativa activa.
///
/// <para>
/// Existe porque los permisos NO viajan en el token. El emisor de identidad
/// central no pone claims <c>perm</c>, y ponerlos tampoco serviría: el token se
/// emite en <c>/api/auth/*</c>, ruta exenta de resolución de cooperativa, así
/// que en ese instante todavía no se sabe en cuál va a operar. Además los
/// permisos son por cooperativa (<c>Role.TenantId</c>), y meterlos en el token
/// convertiría cada revocación de rol en 15 minutos de autoridad residual.
/// </para>
///
/// <para>
/// Son tres saltos, y ninguno es opcional:
/// identidad central (email del token) → fila en <c>SEC_Users</c>;
/// <c>active_tenant_id</c> (PublicId) → Id interno de la cooperativa;
/// y (usuario, cooperativa) → códigos.
/// </para>
///
/// <para>
/// <b>El puente por email es deuda conocida.</b> <c>SEC_Users</c> tiene columna
/// <c>CentralUserId</c> en el modelo de dominio, pero
/// <c>UserConfiguration</c> la declara <c>Ignore()</c> hasta que se aplique el
/// cutover pendiente, así que hoy no existe en la base. Se usa el mismo
/// predicado que <c>TenantUserProvisioner</c> al crear la fila, para que ambos
/// lados coincidan. Cuando esa columna exista, esto es un cambio de una línea.
/// </para>
/// </summary>
internal sealed class PermisosDeLaPeticion(
    IApplicationDbContext operativa,
    AdminDbContext admin,
    IUserPermissionResolver resolutor,
    ILogger<PermisosDeLaPeticion> logger)
{
    private const string ClaveMemo = "__permisos_resueltos";

    public async Task<IReadOnlyCollection<string>> ResolverAsync(
        HttpContext http, CancellationToken ct)
    {
        // Un endpoint puede declarar dos permisos; sin esto se consultaría dos veces.
        if (http.Items.TryGetValue(ClaveMemo, out var memo) &&
            memo is IReadOnlyCollection<string> yaResueltos)
        {
            return yaResueltos;
        }

        var resultado = await ResolverSinMemoAsync(http, ct);
        http.Items[ClaveMemo] = resultado;
        return resultado;
    }

    private async Task<IReadOnlyCollection<string>> ResolverSinMemoAsync(
        HttpContext http, CancellationToken ct)
    {
        var email = LeerEmail(http.User);
        if (string.IsNullOrWhiteSpace(email)) return [];

        var idCooperativa = await ResolverCooperativaAsync(http, ct);
        if (idCooperativa is null) return [];

        // Sin IgnoreQueryFilters a propósito: una fila soft-deleted no concede nada.
        var idUsuario = await operativa.Users
            .Where(u => (u.Username == email || u.Email == email) && u.IsActive)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);

        if (idUsuario is null)
        {
            logger.LogDebug(
                "La identidad central no tiene fila activa en SEC_Users para la cooperativa " +
                "{TenantId}. Sin permisos.", idCooperativa);
            return [];
        }

        return (IReadOnlyCollection<string>)await resolutor.ResolveForTenantAsync(
            idUsuario.Value, idCooperativa.Value, ct);
    }

    /// <summary>
    /// Id interno de la cooperativa activa. Para las rutas que pasan por
    /// <c>TenantResolutionMiddleware</c> ya está resuelto; para las exentas
    /// (<c>/api/admin</c>, <c>/api/saas/</c>) hay que resolverlo aquí.
    ///
    /// <para>
    /// Se repite el predicado del middleware —<c>IsActive</c> incluido— y no se
    /// confía sólo en el claim: un token emitido antes de suspender la
    /// cooperativa seguiría abriendo las rutas exentas.
    /// </para>
    /// </summary>
    private async Task<int?> ResolverCooperativaAsync(HttpContext http, CancellationToken ct)
    {
        if (http.Items.TryGetValue("TenantId", out var yaResuelto) && yaResuelto is int id)
        {
            return id;
        }

        var claim = http.User.FindFirst("active_tenant_id")?.Value;
        if (!Guid.TryParse(claim, out var publicId)) return null;

        var interno = await admin.Tenants
            .AsNoTracking()
            .Where(t => t.PublicId == publicId && t.IsActive)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct);

        if (interno is not null) http.Items["TenantId"] = interno.Value;
        return interno;
    }

    /// <summary>
    /// El emisor pone el claim como <c>email</c>, pero el mapeo de entrada por
    /// defecto de <c>JwtSecurityTokenHandler</c> lo renombra al URI largo de
    /// <see cref="ClaimTypes.Email"/>. Se leen los dos: si algún día se activa
    /// <c>MapInboundClaims = false</c>, esto sigue funcionando.
    /// </summary>
    private static string? LeerEmail(ClaimsPrincipal usuario)
    {
        var valor = usuario.FindFirst(ClaimTypes.Email)?.Value
                 ?? usuario.FindFirst("email")?.Value;
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }
}
