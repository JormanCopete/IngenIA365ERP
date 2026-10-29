using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Scope;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Security;

/// <summary>
/// El alcance de Tesorería de quien opera (feature 015, T0058; US12; R-30; FR-011, FR-106): las cuentas propias de sus filas en
/// <c>TES_UserBankAccountScopes</c>. Falla cerrado: sin usuario resuelto en <c>SEC_Users</c> (por <see cref="IActorActual"/>) o sin filas, nada; en
/// segundo plano, para el maestro global y con <c>Treasury.Scope.All</c>, todo. Scoped y memorizado por petición. (nuevo)
/// </summary>
public sealed class AlcanceDeTesoreriaDelActor(IActorActual actorActual, ICurrentUserPermissions permisos, IApplicationDbContext db) : IAlcanceDeTesoreria
{
    /// <summary>El permiso que da el alcance total de Tesorería (decisión del dueño del 2026-10-23, DT §2.7).</summary>
    public const string TodoElAlcance = "Treasury.Scope.All";

    private Task<AlcanceDeTesoreria>? _memo;

    public Task<AlcanceDeTesoreria> ObtenerAsync(CancellationToken ct = default) => _memo ??= ResolverAsync(ct);

    private async Task<AlcanceDeTesoreria> ResolverAsync(CancellationToken ct)
    {
        if (ContextoAmbiental.Activo || permisos.EsMaestroGlobal) return AlcanceDeTesoreria.Todo;
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return AlcanceDeTesoreria.Vacio;
        if ((await permisos.ListAsync(ct)).Contains(TodoElAlcance, StringComparer.OrdinalIgnoreCase)) return AlcanceDeTesoreria.Todo;
        return await DeUnUsuarioAsync(db, usuario, ct);
    }

    /// <summary>Las filas de un usuario por su <c>SEC_Users.Id</c>, sin mirar permisos.</summary>
    public static async Task<AlcanceDeTesoreria> DeUnUsuarioAsync(IApplicationDbContext db, int usuarioId, CancellationToken ct)
    {
        var cuentas = await db.UserBankAccountScopes.AsNoTracking().Where(s => s.UserId == usuarioId && !s.IsDeleted)
            .Select(s => s.BankAccountId).ToListAsync(ct);
        return new AlcanceDeTesoreria(false, cuentas.ToHashSet());
    }
}
