using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Security;

/// <summary>
/// El alcance de Recaudo de quien opera (feature 015, T0058; US12; R-30; FR-011): lo que resuelve <c>AlcanceDeRecaudoDeLaPeticion</c> de la API una vez
/// por petición. Molde: <c>AlcanceDeCarteraDelActor</c>.
///
/// <list type="bullet">
/// <item><b>Falla cerrado.</b> Sin usuario resuelto en <c>SEC_Users</c> (por <see cref="IActorActual"/>, nunca por el entero del token), sin cooperativa o
/// sin filas, nada. Con <see cref="TodoElAlcance"/> o el maestro global, todo.</item>
/// <item><b>En segundo plano</b> (<see cref="ContextoAmbiental"/>) el alcance es total: el proceso actúa sobre toda la cooperativa.</item>
/// <item><b>Una lectura por petición</b>: es Scoped y memoriza la tarea.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class AlcanceDeRecaudoDelActor(
    IActorActual actorActual,
    ICurrentUserPermissions permisos,
    ICurrentTenantService cooperativaActual,
    IAdminDbContext admin,
    IApplicationDbContext db,
    OficinasDelUsuario oficinasDelUsuario,
    IDateTimeService reloj) : IAlcanceDeRecaudo
{
    /// <summary>El permiso que da el alcance total de Recaudo (DT §2.6).</summary>
    public const string TodoElAlcance = "Receipts.Scope.All";

    private Task<AlcanceDeRecaudo>? _memo;

    public Task<AlcanceDeRecaudo> ObtenerAsync(CancellationToken ct = default) => _memo ??= ResolverAsync(ct);

    private async Task<AlcanceDeRecaudo> ResolverAsync(CancellationToken ct)
    {
        if (ContextoAmbiental.Activo) return AlcanceDeRecaudo.Todo;
        if (permisos.EsMaestroGlobal) return AlcanceDeRecaudo.Todo;

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return AlcanceDeRecaudo.Vacio;
        var concedidos = await permisos.ListAsync(ct);
        if (concedidos.Contains(TodoElAlcance, StringComparer.OrdinalIgnoreCase)) return AlcanceDeRecaudo.Todo;
        return await AlcanceDeRecaudoDeUnUsuario.FilasAsync(db, admin, cooperativaActual, oficinasDelUsuario, usuario, reloj.HoyLocal, ct);
    }
}

/// <summary>
/// Las filas de alcance de Recaudo de <b>un</b> usuario por su <c>SEC_Users.Id</c> (feature 015, T0058), sin mirar sus permisos: lo usa el actor de la
/// petición y la pantalla de alcance. Falla cerrado: sin cooperativa, sólo las filas propias de Recaudo, sin oficinas. Las cajas suman las de las que el
/// usuario es cajero vigente a <c>hoy</c> (<c>RCD_CashBoxCashiers</c>): el cajero opera su caja sin otra asignación. (nuevo)
/// </summary>
public static class AlcanceDeRecaudoDeUnUsuario
{
    public static async Task<AlcanceDeRecaudo> FilasAsync(IApplicationDbContext db, IAdminDbContext admin, ICurrentTenantService cooperativaActual,
        OficinasDelUsuario oficinasDelUsuario, int usuarioId, DateOnly hoy, CancellationToken ct)
    {
        var oficinas = new HashSet<int>();
        if (Guid.TryParse(cooperativaActual.TenantId, out var publica)
            && await admin.Tenants.AsNoTracking().Where(t => t.PublicId == publica && t.IsActive).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct) is { } cooperativa)
            oficinas = (await oficinasDelUsuario.LeerAsync(usuarioId, cooperativa, ct)).Sucursales.ToHashSet();

        var puntos = await db.UserCollectionPointScopes.AsNoTracking().Where(s => s.UserId == usuarioId && !s.IsDeleted)
            .Select(s => s.CollectionPointId).ToListAsync(ct);
        var cajas = await db.UserCashBoxScopes.AsNoTracking().Where(s => s.UserId == usuarioId && !s.IsDeleted)
            .Select(s => s.CashBoxId).ToListAsync(ct);
        cajas.AddRange(await db.CashBoxCashiers.AsNoTracking()
            .Where(c => c.UserId == usuarioId && c.ValidFrom <= hoy && (c.ValidTo == null || c.ValidTo >= hoy))
            .Select(c => c.CashBoxId).ToListAsync(ct));
        var pagadurias = await db.UserPaymasterScopes.AsNoTracking().Where(s => s.UserId == usuarioId && !s.IsDeleted)
            .Select(s => s.PaymasterPersonId).ToListAsync(ct);
        return new AlcanceDeRecaudo(false, oficinas, puntos.ToHashSet(), cajas.ToHashSet(), pagadurias.ToHashSet());
    }
}
