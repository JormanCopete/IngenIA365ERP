using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.MultiTenancy;

/// <summary>
/// Implementación de <see cref="ITenantDirectory"/> sobre <see cref="TenantDbContext"/>
/// (BD <c>IngenIA365ERP_Admin</c>). Aislada del <c>ApplicationDbContext</c>
/// para no contaminar la BD operacional con queries cross-database.
///
/// Resilientes ante schema mismatch: si la tabla / columnas no existen aún,
/// loguea y devuelve <c>null</c> — el login sigue su flujo, simplemente
/// emite el JWT sin claim <c>tenant_id</c> resuelto.
/// </summary>
public sealed class TenantDirectory : ITenantDirectory
{
    private readonly TenantDbContext _db;
    private readonly ILogger<TenantDirectory> _logger;

    public TenantDirectory(TenantDbContext db, ILogger<TenantDirectory> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TenantDirectoryEntry?> FindBySubdomainOrNitAsync(
        string subdomainOrNit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(subdomainOrNit)) return null;

        try
        {
            var row = await _db.Tenants
                .AsNoTracking()
                .Where(t => t.Identifier == subdomainOrNit)
                .Select(t => new { t.InternalId, t.PublicId, t.Identifier, t.Name })
                .FirstOrDefaultAsync(ct);

            if (row is null) return null;
            return new TenantDirectoryEntry(
                row.InternalId,
                row.PublicId,
                row.Identifier ?? subdomainOrNit,
                row.Name ?? string.Empty);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 207 or 208)
        {
            _logger.LogError(ex,
                "TenantDirectory: ADM_Tenants (IngenIA365ERP_Admin) está desfasada o no existe. " +
                "Verifica la cadena ConnectionStrings:TenantConnection y los scripts " +
                "database/schema/IngenIA365ERP_Schema_07_SEC_AUD_WEB_ADM.sql + " +
                "database/migration/16_Tenant_Add_Nit_LegalFields.sql.");
            return null;
        }
    }
}
