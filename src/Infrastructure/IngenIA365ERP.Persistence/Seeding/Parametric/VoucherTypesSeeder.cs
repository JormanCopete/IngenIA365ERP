using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Tipos de comprobante sembrados (feature 009, FR-018, FR-019; <c>voucher-types.json</c>): el
/// manual <c>CG</c>, los reservados a cada módulo (un código, un módulo), la apertura <c>AP</c>,
/// el cierre <c>CI</c> y la depreciación <c>DP</c>. Reemplaza al <c>PayrollVoucherTypeSeeder</c>
/// de la feature 005, que sólo sembraba <c>NM</c>. Idempotente por <c>Code</c>; nunca actualiza
/// lo existente (los sembrados no cambian de uso: <c>IsSeeded</c>).
/// </summary>
public sealed class VoucherTypesSeeder : IDataSeeder
{
    public const string Recurso = "voucher-types.json";

    public int Order => 60;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public sealed record Semilla(string Code, string Name, VoucherUsage Usage, string? Module);

    public static IReadOnlyList<Semilla> Semillas() => RecursoJson.Leer<List<Semilla>>(Recurso);

    public static IReadOnlyList<VoucherType> Catalogo() => Semillas().Select(s => new VoucherType
    {
        Code = s.Code.Trim().ToUpperInvariant(),
        Name = s.Name,
        Usage = s.Usage,
        ModuleCode = string.IsNullOrWhiteSpace(s.Module) ? null : s.Module.Trim().ToUpperInvariant(),
        NextNumber = 1,
        IsActive = true,
        IsSeeded = true,
        CreatedBy = SeedContext.ParametricCreatedBy,
    }).ToList();

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var existentes = await db.VoucherTypes.IgnoreQueryFilters().Select(v => v.Code).ToListAsync(ct);
        var existentesSet = existentes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var insertadas = 0;
        foreach (var tipo in Catalogo())
        {
            if (existentesSet.Contains(tipo.Code)) continue;
            db.VoucherTypes.Add(tipo);
            insertadas++;
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }
}
