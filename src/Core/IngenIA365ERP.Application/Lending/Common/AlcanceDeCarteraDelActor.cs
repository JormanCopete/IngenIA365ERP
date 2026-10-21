using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// El alcance de Cartera de quien opera (feature 013, T62, R131, T0285; FR-004, SC-015): lo que resuelve <c>AlcanceDeCarteraDeLaPeticion</c>
/// de la API una vez por petición. Molde: <c>AlcanceDeInventarioDeLaPeticion</c>.
///
/// <list type="bullet">
/// <item><b>Falla cerrado.</b> Sin usuario resuelto en <c>SEC_Users</c> (por <see cref="IActorActual"/>, nunca por el entero del token), sin
/// cooperativa o sin filas, nada. Las oficinas salen de <see cref="OficinasDelUsuario"/> (sin filas, ninguna) salvo
/// <see cref="TodasLasOficinas"/>; las líneas, de <c>LND_UserCreditLineScopes</c> vivas salvo <see cref="TodasLasLineas"/>; el maestro
/// global, todo.</item>
/// <item><b>En segundo plano</b> (<see cref="ContextoAmbiental"/>) el alcance es total: el proceso actúa sobre toda la cooperativa.</item>
/// <item><b>Una lectura por petición</b>: es Scoped y memoriza la tarea.</item>
/// </list>
/// </summary>
public sealed class AlcanceDeCarteraDelActor(
    IActorActual actorActual,
    ICurrentUserPermissions permisos,
    ICurrentTenantService cooperativaActual,
    IAdminDbContext admin,
    IApplicationDbContext db,
    OficinasDelUsuario oficinasDelUsuario) : IAlcanceDeCartera
{
    public const string TodasLasOficinas = "Lending.Scope.AllOffices";
    public const string TodasLasLineas = "Lending.Scope.AllLines";

    private Task<AlcanceDeCartera>? _memo;

    public Task<AlcanceDeCartera> ObtenerAsync(CancellationToken ct = default) => _memo ??= ResolverAsync(ct);

    private async Task<AlcanceDeCartera> ResolverAsync(CancellationToken ct)
    {
        if (ContextoAmbiental.Activo) return AlcanceDeCartera.Total;
        if (permisos.EsMaestroGlobal) return AlcanceDeCartera.Total;

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return AlcanceDeCartera.Vacio;
        if (!Guid.TryParse(cooperativaActual.TenantId, out var publica)) return AlcanceDeCartera.Vacio;
        var cooperativa = await admin.Tenants.AsNoTracking()
            .Where(t => t.PublicId == publica && t.IsActive)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct);
        if (cooperativa is null) return AlcanceDeCartera.Vacio;

        var concedidos = await permisos.ListAsync(ct);
        var todasLasOficinas = concedidos.Contains(TodasLasOficinas, StringComparer.OrdinalIgnoreCase);
        var todasLasLineas = concedidos.Contains(TodasLasLineas, StringComparer.OrdinalIgnoreCase);

        // Con alcance total también se leen las asignaciones: la por defecto es la oficina del evento de auditoría (F10).
        var oficinas = await oficinasDelUsuario.LeerAsync(usuario, cooperativa.Value, ct);
        List<int> lineas = todasLasLineas
            ? []
            : await db.UserCreditLineScopes.AsNoTracking()
                .Where(s => s.UserId == usuario && !s.IsDeleted)
                .Select(s => s.CreditLineId)
                .ToListAsync(ct);

        return new AlcanceDeCartera(
            todasLasOficinas,
            todasLasOficinas ? new HashSet<int>() : oficinas.Sucursales,
            oficinas.PorDefecto,
            todasLasLineas,
            lineas.ToHashSet());
    }
}
