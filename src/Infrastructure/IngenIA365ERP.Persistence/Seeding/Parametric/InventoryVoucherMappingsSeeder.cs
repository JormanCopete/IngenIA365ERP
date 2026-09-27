using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// El tipo de comprobante y el documento cruce por defecto de cada operación de la matriz de Inventario
/// (<c>ACC_InventoryVoucherMappings</c>; feature 012, T28, T485; contracts/contabilidad.md §2.6 y §9;
/// decisiones-transversales §2.14, Order 84), desde <c>inventory-voucher-mappings.json</c>.
///
/// <para>
/// Idempotente por <c>MappingKey</c> y <b>sin pisar</b>: si la clave ya existe —aunque la cooperativa la haya llevado a
/// otro tipo, o la haya dado de baja— no se toca. Un tipo de comprobante que falte o que la cooperativa tenga con otro uso
/// (no <c>Module</c>/<c>INV</c> o inactivo) deja la operación sin mapeo y un aviso en el log: mapear a ese tipo haría que
/// la validación previa y el consumidor respondieran <c>Accounting.VoucherType.NotAllowedForModule</c>; sin fila responden
/// <c>Accounting.VoucherType.NotFound</c>, que se corrige en la pantalla del mapeo. Un documento cruce que falte deja la
/// fila sin cruce, también con aviso.
/// </para>
///
/// <para>
/// La tabla llega con el par <c>IntegracionContableDeInventario</c> (T486): hasta que esa migración esté aplicada en la
/// base, la semilla no hace nada.
/// </para>
/// </summary>
public sealed class InventoryVoucherMappingsSeeder : IDataSeeder
{
    public const string Recurso = "inventory-voucher-mappings.json";

    /// <summary>La migración que crea la tabla del mapeo.</summary>
    public const string MigracionQueCreaLasTablas = "IntegracionContableDeInventario";

    /// <summary>El módulo dueño de los tipos de comprobante que admite el mapeo.</summary>
    public const string ModuloInventario = "INV";

    public int Order => 84;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public sealed record Semilla(string Operation, string VoucherTypeCode, string? CrossDocumentTypeCode, string? InventoryDocumentTypeCode = null);

    public static IReadOnlyList<Semilla> Semillas() => RecursoJson.Leer<List<Semilla>>(Recurso);

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var aplicadas = await db.Database.GetAppliedMigrationsAsync(ct);
        if (!aplicadas.Any(m => m.EndsWith("_" + MigracionQueCreaLasTablas, StringComparison.Ordinal)))
        {
            context.Logger.LogInformation(
                "[Contabilidad.MapeoSinTablas] La base no tiene todavía la migración {Migracion}: no se siembra el mapeo de Inventario.",
                MigracionQueCreaLasTablas);
            return 0;
        }
        return await AplicarAsync(db, context.Logger, ct);
    }

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, ILogger logger, CancellationToken ct)
    {
        var existentes = (await db.InventoryVoucherMappings.IgnoreQueryFilters().Select(m => m.MappingKey).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var tipos = (await db.VoucherTypes.Where(v => !v.IsDeleted)
                .Select(v => new { v.Id, v.Code, v.Usage, v.ModuleCode, v.IsActive })
                .ToListAsync(ct))
            .ToDictionary(v => v.Code, StringComparer.OrdinalIgnoreCase);
        var cruces = (await db.CrossDocumentTypes.Where(c => !c.IsDeleted).Select(c => new { c.Id, c.Code }).ToListAsync(ct))
            .ToDictionary(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase);

        var insertadas = 0;
        foreach (var semilla in Semillas())
        {
            if (existentes.Contains(InventoryVoucherMapping.ClaveDe(semilla.Operation, semilla.InventoryDocumentTypeCode))) continue;

            if (!tipos.TryGetValue(semilla.VoucherTypeCode, out var tipo)
                || tipo.Usage != VoucherUsage.Module
                || !string.Equals(tipo.ModuleCode, ModuloInventario, StringComparison.OrdinalIgnoreCase)
                || !tipo.IsActive)
            {
                logger.LogWarning(
                    "[Semilla.MapeoDeInventarioSinTipo] La operación {Operacion} no se mapea: el tipo de comprobante {Codigo} no existe o no es un tipo activo del módulo {Modulo} en esta cooperativa. Asígnele un tipo en el mapeo de Inventario.",
                    semilla.Operation, semilla.VoucherTypeCode, ModuloInventario);
                continue;
            }

            int? cruce = null;
            if (semilla.CrossDocumentTypeCode is { } codigoCruce)
            {
                if (cruces.TryGetValue(codigoCruce, out var idCruce)) cruce = idCruce;
                else
                    logger.LogWarning(
                        "[Semilla.MapeoDeInventarioSinCruce] La operación {Operacion} se mapea sin documento cruce: la cooperativa no tiene el tipo de documento cruce {Codigo}.",
                        semilla.Operation, codigoCruce);
            }

            db.InventoryVoucherMappings.Add(new InventoryVoucherMapping(semilla.Operation, semilla.InventoryDocumentTypeCode, tipo.Id, cruce)
            {
                CreatedBy = SeedContext.ParametricCreatedBy,
            });
            insertadas++;
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }
}
