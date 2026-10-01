using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 012 (T211; decisiones-transversales §2.14, Order 78; data-model §2.1): los tipos de bodega sembrados —principal,
/// punto de venta, averías y cuarentena (<see cref="WarehouseBehavior.Operational"/>) y tránsito
/// (<see cref="WarehouseBehavior.Transit"/>, el único tipo de tránsito que existe: el alta de otro responde
/// <c>Inventory.WarehouseType.TransitIsSystem</c>). Idempotente por código: lo que la cooperativa ya tiene, vivo o de baja,
/// no se toca. Espera a <c>InventarioComercialNucleo</c>.
/// </summary>
public sealed class WarehouseTypesSeeder : IDataSeeder
{
    public int Order => 78;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    /// <summary>El código del tipo de tránsito sembrado.</summary>
    public const string CodigoDeTransito = "TRANSITO";

    public static readonly IReadOnlyList<(string Codigo, string Nombre, WarehouseBehavior Comportamiento)> Sembrados =
    [
        ("PRINCIPAL", "Principal", WarehouseBehavior.Operational),
        ("PUNTOVENTA", "Punto de venta", WarehouseBehavior.Operational),
        ("AVERIAS", "Averías", WarehouseBehavior.Operational),
        ("CUARENTENA", "Cuarentena", WarehouseBehavior.Operational),
        (CodigoDeTransito, "Tránsito", WarehouseBehavior.Transit),
    ];

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        if (!await SemillasDeInventario.TieneLasTablasAsync(db, context.Logger, nameof(WarehouseTypesSeeder), ct)) return 0;
        return await AplicarAsync(db, ct);
    }

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var existentes = (await db.WarehouseTypes.IgnoreQueryFilters().Select(t => t.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var insertadas = 0;
        foreach (var (codigo, nombre, comportamiento) in Sembrados)
        {
            if (existentes.Contains(codigo)) continue;
            db.WarehouseTypes.Add(new WarehouseType
            {
                Code = codigo, Name = nombre, Behavior = comportamiento, IsSeeded = true, IsActive = true,
                CreatedBy = SeedContext.ParametricCreatedBy,
            });
            insertadas++;
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }
}
