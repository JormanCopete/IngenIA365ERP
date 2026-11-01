using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.Common;

/// <summary>
/// El Id interno (<c>ADM_Tenants.Id</c>) de la cooperativa de la petición, que es contra lo que se compara
/// <c>ADM_Branches.TenantId</c>. <see cref="ICurrentTenantService.TenantId"/> trae el <c>PublicId</c>; nulo si no
/// hay cooperativa resuelta o no existe. (nuevo)
/// </summary>
internal static class CooperativaActiva
{
    public static async Task<int?> IdAsync(IAdminDbContext admin, ICurrentTenantService cooperativa, CancellationToken ct)
    {
        if (!Guid.TryParse(cooperativa.TenantId, out var publica)) return null;
        return await admin.Tenants.AsNoTracking()
            .Where(t => t.PublicId == publica)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct);
    }
}
