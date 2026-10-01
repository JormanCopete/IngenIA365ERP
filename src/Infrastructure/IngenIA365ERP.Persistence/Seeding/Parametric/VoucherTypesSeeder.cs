using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Tipos de comprobante sembrados (feature 009, FR-018, FR-019; <c>voucher-types.json</c>): el
/// manual <c>CG</c>, los reservados a cada módulo (un código, un módulo), la apertura <c>AP</c>,
/// el cierre <c>CI</c> y la depreciación <c>DP</c>. Reemplaza al <c>PayrollVoucherTypeSeeder</c>
/// de la feature 005, que sólo sembraba <c>NM</c>. Idempotente por <c>Code</c>; nunca actualiza
/// lo existente (los sembrados no cambian de uso: <c>IsSeeded</c>). Feature 012 (T484): suma <c>NV</c>, <c>CP</c>,
/// <c>TR</c>, <c>AC</c> y <c>CJ</c> de Inventario y avisa en el log cuando un código ya existe con otro uso.
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

    public Task<int> SeedAsync(SeedContext context, CancellationToken ct) => AplicarAsync(context.TenantDb!, context.Logger, ct);

    /// <summary>
    /// La semilla sobre cualquier contexto de la cooperativa (probable con InMemory). Inserta los códigos que faltan y no
    /// toca los existentes. Feature 012 (T484, T28): si la cooperativa ya tiene un código sembrado con <b>otro</b> uso o de
    /// otro módulo —un «TR» manual, por ejemplo—, no se salta en silencio: queda un aviso en el log con el código y el uso
    /// existente, porque el mapeo de Inventario tendrá que llevar esas operaciones a otro tipo.
    /// </summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, ILogger logger, CancellationToken ct)
    {
        var existentes = (await db.VoucherTypes.IgnoreQueryFilters()
                .Select(v => new { v.Code, v.Usage, v.ModuleCode, v.IsDeleted })
                .ToListAsync(ct))
            .GroupBy(v => v.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(v => v.IsDeleted).First(), StringComparer.OrdinalIgnoreCase);

        var insertadas = 0;
        foreach (var tipo in Catalogo())
        {
            if (existentes.TryGetValue(tipo.Code, out var existente))
            {
                if (existente.Usage != tipo.Usage || !string.Equals(existente.ModuleCode, tipo.ModuleCode, StringComparison.OrdinalIgnoreCase))
                    logger.LogWarning(
                        "[Semilla.TipoDeComprobanteEnUso] La cooperativa ya tiene el tipo de comprobante {Codigo} con uso {UsoExistente} y módulo {ModuloExistente}; la semilla lo trae con uso {UsoSemilla} y módulo {ModuloSemilla} y no lo toca. Lleve sus operaciones a otro tipo.",
                        tipo.Code, existente.Usage, existente.ModuleCode ?? "(ninguno)", tipo.Usage, tipo.ModuleCode ?? "(ninguno)");
                continue;
            }
            db.VoucherTypes.Add(tipo);
            insertadas++;
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }
}
