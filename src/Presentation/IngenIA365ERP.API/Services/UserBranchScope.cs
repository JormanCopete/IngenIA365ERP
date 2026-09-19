using IngenIA365ERP.API.Filters.CentralIdentity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="IUserBranchScope"/> para la petición en curso (feature 009, FR-035, R7). Tres
/// saltos: quien opera → sus filas activas en <c>SEC_UserBranchAssignments</c> de la cooperativa
/// activa (oficinas de <c>ADM_Branches</c>) → las sucursales de <c>COR_Branches</c> que las
/// referencian por <c>TenantBranchPublicId</c>. Sin asignaciones no hay restricción; con
/// asignaciones que ninguna sucursal contable referencia, el alcance queda vacío a propósito.
/// El maestro global no tiene alcance.
/// </summary>
internal sealed class UserBranchScope(
    IHttpContextAccessor accessor,
    PermisosDeLaPeticion peticion,
    IApplicationDbContext operativa,
    IAdminDbContext admin) : IUserBranchScope
{
    public async Task<AlcanceDeSucursales> ObtenerAsync(CancellationToken ct)
    {
        var http = accessor.HttpContext;
        if (http is null) return AlcanceDeSucursales.SinRestriccion;
        if (RequireMasterAdminAttribute.Check(http).IsAllowed) return AlcanceDeSucursales.SinRestriccion;

        var (usuario, cooperativa) = await peticion.ResolverUsuarioYCooperativaAsync(http, ct);
        if (usuario is null || cooperativa is null) return AlcanceDeSucursales.SinRestriccion;

        var asignaciones = await operativa.UserBranchAssignments.AsNoTracking()
            .Where(a => a.UserId == usuario && a.TenantId == cooperativa && a.IsActive && !a.IsDeleted)
            .Select(a => new { a.BranchId, a.IsDefault })
            .ToListAsync(ct);
        if (asignaciones.Count == 0) return AlcanceDeSucursales.SinRestriccion;

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

        return AlcanceDeSucursales.Limitado(sucursales.Select(s => s.Id), porDefecto);
    }
}
