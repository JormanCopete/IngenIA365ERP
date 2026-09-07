using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Admin.Branches.Common;

/// <summary>
/// Qué sucursales puede ver y tocar quien hace la petición.
///
/// <para>
/// <b>Por qué hace falta.</b> Las sucursales viven en <c>ADM_Branches</c>, en la
/// base administrativa, que es compartida por diseño — el aislamiento por base
/// de datos no las cubre. Y el permiso <c>Admin.Branches.*</c> no está en la
/// lista de retención SaaS-global, correctamente: las sucursales son de la
/// cooperativa. El resultado era que cualquier administrador de cooperativa
/// tenía el permiso y nada acotaba su alcance.
/// </para>
///
/// <para>
/// El listado filtraba por cooperativa <i>sólo si quien llamaba enviaba el
/// identificador</i>: omitirlo devolvía las sucursales de todas. Y actualizar y
/// desactivar localizaban la sucursal por su identificador público sin
/// comprobar de quién era, así que bastaba conocerlo.
/// </para>
/// </summary>
internal static class AlcanceDeSucursales
{
    /// <summary>
    /// Id interno de la cooperativa a la que se acota la operación, o <c>null</c>
    /// si quien llama es el administrador maestro y puede cruzar cooperativas.
    /// </summary>
    /// <returns>
    /// <c>(false, ...)</c> cuando no hay cooperativa que resolver y quien llama no
    /// es el maestro: se deniega, no se abre a todas.
    /// </returns>
    public static async Task<(bool Permitido, int? Cooperativa, string? Codigo, string? Mensaje)>
        ResolverAsync(
            IAdminDbContext db,
            ICurrentTenantService cooperativaActual,
            ICurrentCentralUserContext usuario,
            CancellationToken ct)
    {
        if (usuario.IsGlobalMasterAdmin)
        {
            // El maestro administra el conjunto de cooperativas: puede ver todas.
            return (true, null, null, null);
        }

        if (!Guid.TryParse(cooperativaActual.TenantId, out var publica))
        {
            return (false, null, "Session.TenantNotSelected",
                "Hace falta una cooperativa activa para trabajar con sucursales.");
        }

        var interno = await db.Tenants
            .AsNoTracking()
            .Where(t => t.PublicId == publica)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct);

        return interno is null
            ? (false, null, "Generic.NotFound", "Cooperativa no encontrada.")
            : (true, interno, null, null);
    }
}
