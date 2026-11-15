using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Platform.Modules;
using IngenIA365ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IngenIA365ERP.Persistence.Platform;

/// <summary>
/// Lee los módulos habilitados de la base administrativa con una caché en memoria de 60 s por cooperativa (feature 020, R1).
/// </summary>
internal sealed class ModulosHabilitadosDesdeAdmin(IAdminDbContext admin, IMemoryCache cache, ICurrentTenantService cooperativa)
    : IModulosHabilitados
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(60);
    private static readonly IReadOnlySet<string> Ninguno = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlySet<string>> HabilitadosAsync(Guid tenantPublicId, CancellationToken ct)
    {
        if (tenantPublicId == Guid.Empty) return Ninguno;
        if (cache.TryGetValue(Clave(tenantPublicId), out IReadOnlySet<string>? guardados) && guardados is not null)
            return guardados;

        var codigos = await admin.TenantModules.AsNoTracking()
            .Where(m => m.TenantId == tenantPublicId && m.IsEnabled)
            .Select(m => m.ModuleCode)
            .ToListAsync(ct);
        IReadOnlySet<string> conjunto = codigos.Where(ModulosHabilitables.Existe).ToHashSet(StringComparer.OrdinalIgnoreCase);
        cache.Set(Clave(tenantPublicId), conjunto, Vigencia);
        return conjunto;
    }

    public async Task<bool> EstaHabilitadoAsync(Guid tenantPublicId, string codigo, CancellationToken ct) =>
        (await HabilitadosAsync(tenantPublicId, ct)).Contains(codigo);

    public Task<IReadOnlySet<string>> DeLaCooperativaActualAsync(CancellationToken ct) =>
        Guid.TryParse(cooperativa.TenantId, out var id) ? HabilitadosAsync(id, ct) : Task.FromResult(Ninguno);

    public void Invalidar(Guid tenantPublicId) => cache.Remove(Clave(tenantPublicId));

    private static string Clave(Guid tenantPublicId) => $"modulos-habilitados:{tenantPublicId:N}";
}
