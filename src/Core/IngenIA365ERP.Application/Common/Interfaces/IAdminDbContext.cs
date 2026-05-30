using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Interfaces;

/// <summary>
/// DbContext de la BD multi-tenant administrativa (<c>IngenIA365ERP_Admin</c>).
/// Solo contiene las tablas <c>ADM_*</c> — el directorio de cooperativas y sus
/// sucursales. Las tablas operacionales (<c>SEC_*</c>, <c>COR_*</c>, etc.)
/// viven en <see cref="IApplicationDbContext"/>.
///
/// Cualquier handler que cree, actualice o consulte tenants / branches
/// inyecta esta abstracción — no <c>IApplicationDbContext.Tenants</c>, que
/// apunta a la BD equivocada.
/// </summary>
public interface IAdminDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<TenantBranch> TenantBranches { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
