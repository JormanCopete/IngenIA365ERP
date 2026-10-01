using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 012 (T210; decisiones-transversales §2.14, Order 77; data-model §1.1): las unidades de medida básicas de cada
/// cooperativa desde <c>Data/inventario-unidades.json</c> (embebido), con sus decimales y su código UN/ECE Rec. 20
/// (UND/94, KG/KGM, LT/LTR…), marcadas <c>IsSeeded</c>. Idempotente por código; al cambiar <c>version</c> las pone al día
/// <see cref="SincronizacionDeCatalogo.SincronizarUnidadesAsync"/>. La tabla llega con el par
/// <c>InventarioComercialNucleo</c> (T440): hasta que esa migración esté aplicada, no hace nada.
/// </summary>
public sealed class InventoryUnitsSeeder : IDataSeeder
{
    public int Order => 77;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public const string Archivo = "inventario-unidades.json";

    public sealed record UnidadSembrada(string Code, string Name, string? Symbol, byte AllowedDecimals, string? DianUnitCode);

    public sealed record Semilla(string Version, IReadOnlyList<UnidadSembrada> Unidades);

    public static Semilla Leer() => RecursoJson.Leer<Semilla>(Archivo);

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        if (!await SemillasDeInventario.TieneLasTablasAsync(db, context.Logger, nameof(InventoryUnitsSeeder), ct)) return 0;
        return await AplicarAsync(db, DateTime.UtcNow, context.Logger, ct);
    }

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, DateTime ahora, ILogger? logger, CancellationToken ct)
    {
        var semilla = Leer();
        var unidades = semilla.Unidades
            .Select(u => new UnitOfMeasure { Code = u.Code, Name = u.Name, Symbol = u.Symbol, AllowedDecimals = u.AllowedDecimals, DianUnitCode = u.DianUnitCode })
            .ToList();
        var cambios = await SincronizacionDeCatalogo.SincronizarUnidadesAsync(db, semilla.Version, unidades, SeedContext.ParametricCreatedBy, ahora, logger, ct);
        if (cambios is null) return 0;
        await db.SaveChangesAsync(ct);
        return cambios.Value;
    }
}

/// <summary>
/// Lo que comparten las semillas del catálogo y las bodegas de la feature 012 (T210, T211): sus tablas llegan con el par
/// <c>InventarioComercialNucleo</c> (T440); antes de eso la semilla no hace nada (si no, el arranque fallaría en toda
/// cooperativa que tenga <c>PlataformaParaInventario</c>). (nuevo)
/// </summary>
internal static class SemillasDeInventario
{
    public static async Task<bool> TieneLasTablasAsync(DbContext.ApplicationDbContext db, ILogger logger, string semilla, CancellationToken ct)
    {
        var aplicadas = await db.Database.GetAppliedMigrationsAsync(ct);
        if (aplicadas.Any(m => m.EndsWith("_" + InventoryDocumentTypesSeeder.MigracionQueCreaLasTablas, StringComparison.Ordinal))) return true;
        logger.LogInformation("[Inventario.SemillaSinTablas] {Semilla}: la base no tiene todavía la migración {Migracion}.",
            semilla, InventoryDocumentTypesSeeder.MigracionQueCreaLasTablas);
        return false;
    }
}
