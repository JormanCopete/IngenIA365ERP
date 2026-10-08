using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Security;

/// <summary>
/// Lo que <see cref="OficinasDelUsuario"/> encontró: si el usuario tiene asignaciones activas en la cooperativa, las sucursales de
/// <c>COR_Branches</c> que las referencian y la que se propone por defecto. Distingue «sin filas» de «filas sin sucursal contable»,
/// que cada consumidor interpreta a su manera. (nuevo)
/// </summary>
public sealed record OficinasAsignadas(bool TieneAsignaciones, IReadOnlySet<int> Sucursales, int? PorDefecto)
{
    public static OficinasAsignadas SinAsignaciones { get; } = new(false, new HashSet<int>(), null);
}

/// <summary>
/// El salto común de las oficinas de un usuario (feature 013, T62, R131; extraído de <c>UserBranchScope</c> de la feature 009, FR-035,
/// R7): sus filas activas en <c>SEC_UserBranchAssignments</c> de la cooperativa (oficinas de <c>ADM_Branches</c>) → las sucursales de
/// <c>COR_Branches</c> que las referencian por <c>TenantBranchPublicId</c>. La propuesta es la de la asignación por defecto o, si ésa
/// no tiene sucursal contable, la primera que la tenga.
///
/// <para>
/// No decide nada: <c>UserBranchScope</c> conserva su semántica <b>abierta</b> (sin asignaciones no restringe;
/// <see cref="AlcanceDeSucursales.De"/>) y Cartera la suya <b>cerrada</b> (sin asignaciones no hay oficinas; <c>AlcanceDeCarteraDelActor</c>).
/// </para>
/// </summary>
public sealed class OficinasDelUsuario(IApplicationDbContext operativa, IAdminDbContext admin)
{
    /// <param name="usuarioId"><c>SEC_Users.Id</c>.</param>
    /// <param name="cooperativaId">Id interno de la cooperativa (<c>ADM_Tenants.Id</c>).</param>
    public async Task<OficinasAsignadas> LeerAsync(int usuarioId, int cooperativaId, CancellationToken ct = default)
    {
        var asignaciones = await operativa.UserBranchAssignments.AsNoTracking()
            .Where(a => a.UserId == usuarioId && a.TenantId == cooperativaId && a.IsActive && !a.IsDeleted)
            .Select(a => new { a.BranchId, a.IsDefault })
            .ToListAsync(ct);
        if (asignaciones.Count == 0) return OficinasAsignadas.SinAsignaciones;

        var idsAdm = asignaciones.Select(a => a.BranchId).Distinct().ToList();
        var oficinas = await admin.TenantBranches.AsNoTracking()
            .Where(b => idsAdm.Contains(b.Id) && b.IsActive && !b.IsDeleted)
            .Select(b => new { b.Id, b.PublicId })
            .ToListAsync(ct);
        var publicIds = oficinas.Select(o => o.PublicId).ToList();

        var sucursales = await operativa.Branches.AsNoTracking()
            .Where(b => !b.IsDeleted && b.TenantBranchPublicId != null && publicIds.Contains(b.TenantBranchPublicId.Value))
            .Select(b => new { b.Id, b.TenantBranchPublicId })
            .ToListAsync(ct);

        var porDefectoAdm = asignaciones.FirstOrDefault(a => a.IsDefault)?.BranchId;
        var porDefectoPublicId = oficinas.FirstOrDefault(o => o.Id == porDefectoAdm)?.PublicId;
        var porDefecto = sucursales.FirstOrDefault(s => s.TenantBranchPublicId == porDefectoPublicId)?.Id ?? sucursales.FirstOrDefault()?.Id;

        return new OficinasAsignadas(true, sucursales.Select(s => s.Id).ToHashSet(), porDefecto);
    }
}
