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
///
/// <para>
/// <b>La cooperativa activa manda para todos, también para el maestro</b> (feature 018): las oficinas se trabajan
/// dentro de cada empresa. El maestro sin cooperativa activa, que es lo normal porque no es miembro de ninguna,
/// consulta todas y no escribe en ninguna; si a una cooperativa le falta la sede, la repara con
/// <c>POST /api/tenants/{id}/provision</c>.
/// </para>
/// </summary>
internal static class AlcanceDeSucursales
{
    /// <summary>
    /// Id interno de la cooperativa a la que se acota la operación, o <c>null</c>
    /// si quien llama es el administrador maestro sin cooperativa activa.
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
        if (!Guid.TryParse(cooperativaActual.TenantId, out var publica))
        {
            return usuario.IsGlobalMasterAdmin
                ? (true, null, null, null)
                : (false, null, "Session.TenantNotSelected",
                    "Hace falta una cooperativa activa para trabajar con oficinas.");
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

    /// <summary>Como <see cref="ResolverAsync"/>, pero escribir exige cooperativa activa: el maestro sin ella sólo consulta.</summary>
    public static async Task<(bool Permitido, int? Cooperativa, string? Codigo, string? Mensaje)>
        ResolverParaEscribirAsync(
            IAdminDbContext db,
            ICurrentTenantService cooperativaActual,
            ICurrentCentralUserContext usuario,
            CancellationToken ct)
    {
        var alcance = await ResolverAsync(db, cooperativaActual, usuario, ct);
        return alcance is { Permitido: true, Cooperativa: null }
            ? (false, null, "Session.TenantNotSelected", "Las oficinas se crean y se modifican dentro de cada cooperativa.")
            : alcance;
    }
}
