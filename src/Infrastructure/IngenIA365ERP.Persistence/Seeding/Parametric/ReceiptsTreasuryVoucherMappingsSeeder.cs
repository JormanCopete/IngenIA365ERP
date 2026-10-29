using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// El tipo de comprobante por defecto de cada hecho de Recaudo y Tesorería (feature 015, US8, T0264; contabilidad.md §8): el sugerido en
/// <see cref="MatricesDeRecaudoYTesoreria"/> (<c>RR</c>, <c>RB</c>, <c>MC</c>, <c>AR</c>, <c>TP</c>), sembrado «pendiente de validar» (<c>IsSeeded</c>, sin
/// conformidad) y <b>sin cuenta</b>: no se siembra ninguna regla de la matriz (P12). Cruce <c>OT</c> donde la cuenta lo exija. No toca un mapeo que ya exista; si
/// el tipo no existe o no es un tipo activo del módulo, avisa y sigue. Corre después de los tipos de comprobante (Order 60). (nuevo)
/// </summary>
public sealed class ReceiptsTreasuryVoucherMappingsSeeder : IDataSeeder
{
    public const string MigracionQueCreaLasTablas = "IntegracionContableDeRecaudoYTesoreria";

    public int Order => 93;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var aplicadas = await db.Database.GetAppliedMigrationsAsync(ct);
        if (!aplicadas.Any(m => m.EndsWith("_" + MigracionQueCreaLasTablas, StringComparison.Ordinal)))
        {
            context.Logger.LogInformation(
                "[Contabilidad.MapeoDeRecaudoSinTablas] La base no tiene todavía la migración {Migracion}: no se siembra el mapeo de Recaudo y Tesorería.",
                MigracionQueCreaLasTablas);
            return 0;
        }
        return await AplicarAsync(db, context.Logger, ct);
    }

    public static async Task<int> AplicarAsync(IApplicationDbContext db, ILogger logger, CancellationToken ct)
    {
        var tipos = (await db.VoucherTypes.Where(v => !v.IsDeleted).Select(v => new { v.Id, v.Code, v.Usage, v.ModuleCode, v.IsActive }).ToListAsync(ct))
            .ToDictionary(v => v.Code, StringComparer.OrdinalIgnoreCase);
        var otro = await db.CrossDocumentTypes.Where(c => !c.IsDeleted && c.Code == ConstructorDeComprobanteDeModulo.CruceOtro).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        var deRecaudo = (await db.ReceiptsVoucherMappings.IgnoreQueryFilters().Select(m => m.FactType).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var deTesoreria = (await db.TreasuryVoucherMappings.IgnoreQueryFilters().Select(m => m.FactType).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var insertadas = 0;
        foreach (var modulo in MatricesDeRecaudoYTesoreria.Todas)
        {
            var existentes = modulo == MatricesDeRecaudoYTesoreria.Recaudo ? deRecaudo : deTesoreria;
            foreach (var hecho in modulo.Hechos)
            {
                if (existentes.Contains(hecho.Tipo)) continue;
                if (!tipos.TryGetValue(hecho.ComprobanteSugerido, out var tipo) || tipo.Usage != VoucherUsage.Module
                    || !string.Equals(tipo.ModuleCode, modulo.Codigo, StringComparison.OrdinalIgnoreCase) || !tipo.IsActive)
                {
                    logger.LogWarning(
                        "[Semilla.MapeoDeRecaudoSinTipo] El hecho {Hecho} de {Modulo} no se mapea: el tipo de comprobante {Codigo} no existe o no es un tipo activo del módulo. Asígnele uno en Contabilidad.",
                        hecho.Tipo, modulo.Nombre, hecho.ComprobanteSugerido);
                    continue;
                }
                if (modulo == MatricesDeRecaudoYTesoreria.Recaudo)
                    db.ReceiptsVoucherMappings.Add(new ReceiptsVoucherMapping
                        { FactType = hecho.Tipo, VoucherTypeId = tipo.Id, CrossDocumentTypeId = otro, IsSeeded = true, CreatedBy = SeedContext.ParametricCreatedBy });
                else
                    db.TreasuryVoucherMappings.Add(new TreasuryVoucherMapping
                        { FactType = hecho.Tipo, VoucherTypeId = tipo.Id, CrossDocumentTypeId = otro, IsSeeded = true, CreatedBy = SeedContext.ParametricCreatedBy });
                insertadas++;
            }
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }
}
