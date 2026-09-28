using IngenIA365ERP.API.Filters.CentralIdentity;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="ILimitesPorPermiso"/> (feature 012, T34, T082; contracts/api.md §1.3; pregunta C4). De los roles
/// activos del usuario de <see cref="IActorActual"/> que conceden el permiso, el <b>mayor</b> límite vigente a la
/// fecha; un rol que lo concede sin fila vigente —o con <c>MaxAmount</c> nulo— significa sin límite.
///
/// <para>
/// «Conceder» se lee igual que <see cref="PermisosDeLaPeticion"/>: por <c>SEC_RolePermissions</c> vivos. Los comodines
/// de los roles integrados (<c>*</c>, <c>*.View</c>) no se evalúan aquí porque el sembrador ya los materializa en
/// filas; así un rol con <c>*</c> concede el permiso exactamente cuando la puerta de la ruta lo deja pasar. El
/// administrador maestro no lleva roles por cooperativa: sin límite, como su atajo en la puerta.
/// </para>
/// </summary>
internal sealed class LimitesPorPermiso(IApplicationDbContext db, IActorActual actorActual, IHttpContextAccessor accessor)
    : ILimitesPorPermiso
{
    public async Task<decimal?> MontoMaximoAsync(string permiso, DateOnly fecha, CancellationToken ct = default)
    {
        if (accessor.HttpContext is { } http && RequireMasterAdminAttribute.Check(http).IsAllowed) return null;

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return null;

        return await MontoMaximoDeAsync(db, usuario, permiso, fecha, ct);
    }

    /// <summary>El mismo cálculo para cualquier usuario (lo reusa el aprobador presente); vive en Application (I3, T655).</summary>
    internal static Task<decimal?> MontoMaximoDeAsync(IApplicationDbContext db, int usuario, string permiso, DateOnly fecha, CancellationToken ct) =>
        LimitesDePermisoPorUsuario.DeAsync(db, usuario, permiso, fecha, ct);

    /// <summary>Los roles activos del usuario que conceden el permiso.</summary>
    internal static Task<List<int>> RolesQueConcedenAsync(IApplicationDbContext db, int usuario, string permiso, CancellationToken ct) =>
        LimitesDePermisoPorUsuario.RolesQueConcedenAsync(db, usuario, permiso, ct);
}
