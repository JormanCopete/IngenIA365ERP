using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Los billetes y monedas en pesos vigentes para contar el efectivo en el arqueo (<c>COR_CashDenominations</c>; feature 012, I3,
/// T587; FR-099; data-model §16; decisiones-transversales §2.14, Order 85): billetes de 2.000 a 100.000 y monedas de 50 a 1.000,
/// ordenados de la denominación mayor a la menor. Idempotente por <c>(Currency, Kind, Value)</c> <b>incluidas las de baja</b>: una
/// denominación que la cooperativa dio de baja o cerró no vuelve. Un billete o moneda que el Banco de la República emita o retire
/// después se agrega o se cierra en la pantalla, no aquí.
///
/// <para>
/// La tabla llega con el par <c>VentasYPuntoDeVenta</c> (T586): hasta que esa migración esté aplicada en la base, la semilla no
/// hace nada.
/// </para>
/// </summary>
public sealed class CashDenominationsSeeder : IDataSeeder
{
    /// <summary>La migración que crea las tablas de ventas y caja.</summary>
    public const string MigracionQueCreaLasTablas = "VentasYPuntoDeVenta";

    public int Order => 85;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    /// <summary>Desde cuándo rigen: «desde siempre», como las demás semillas de Inventario.</summary>
    public static readonly DateOnly VigenciaDeLaSemilla = new(2000, 1, 1);

    /// <summary>Clase y valor de cada denominación, en el orden en que el arqueo las lista.</summary>
    public static readonly IReadOnlyList<(CashDenominationKind Clase, decimal Valor)> Denominaciones =
    [
        (CashDenominationKind.Bill, 100_000m),
        (CashDenominationKind.Bill, 50_000m),
        (CashDenominationKind.Bill, 20_000m),
        (CashDenominationKind.Bill, 10_000m),
        (CashDenominationKind.Bill, 5_000m),
        (CashDenominationKind.Bill, 2_000m),
        (CashDenominationKind.Coin, 1_000m),
        (CashDenominationKind.Coin, 500m),
        (CashDenominationKind.Coin, 200m),
        (CashDenominationKind.Coin, 100m),
        (CashDenominationKind.Coin, 50m),
    ];

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        if (!await TieneLaMigracionAsync(db, context.Logger, "[Ventas.DenominacionesSinTablas]", ct)) return 0;
        return await AplicarAsync(db, ct);
    }

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var moneda = CashDenomination.MonedaPorDefecto;
        var existentes = (await db.CashDenominations.IgnoreQueryFilters().Where(d => d.Currency == moneda)
                .Select(d => new { d.Kind, d.Value }).ToListAsync(ct))
            .Select(d => (d.Kind, d.Value))
            .ToHashSet();

        var insertadas = 0;
        short orden = 0;
        foreach (var (clase, valor) in Denominaciones)
        {
            orden++;
            if (existentes.Contains((clase, valor))) continue;
            db.CashDenominations.Add(new CashDenomination
            {
                Currency = moneda,
                Kind = clase,
                Value = valor,
                DisplayOrder = orden,
                IsActive = true,
                ValidFrom = VigenciaDeLaSemilla,
                CreatedBy = SeedContext.ParametricCreatedBy,
            });
            insertadas++;
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }

    /// <summary>¿La base ya tiene <see cref="MigracionQueCreaLasTablas"/>? Si no, lo deja en el log. Lo comparten las semillas de I3.</summary>
    internal static async Task<bool> TieneLaMigracionAsync(ApplicationDbContext db, ILogger logger, string marca, CancellationToken ct)
    {
        var aplicadas = await db.Database.GetAppliedMigrationsAsync(ct);
        if (aplicadas.Any(m => m.EndsWith("_" + MigracionQueCreaLasTablas, StringComparison.Ordinal))) return true;
        logger.LogInformation("{Marca} La base no tiene todavía la migración {Migracion}: no se siembra.", marca, MigracionQueCreaLasTablas);
        return false;
    }
}
