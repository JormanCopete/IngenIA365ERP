using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// Las tablas <c>INV_</c> del documento genérico (feature 012, fase 3) ya están en el modelo, pero su migración es
/// <c>InventarioComercialNucleo</c> (T440, al cerrar I1), no <c>PlataformaParaInventario</c> (T186): decisiones-transversales
/// §2.15. Hasta entonces quedan <b>fuera de las migraciones</b> (<c>ExcludeFromMigrations</c>): el snapshot las conoce, así
/// que <c>AutoMigrate</c> no ve cambios pendientes, y ninguna migración las crea todavía. T440 borra esta clase y su llamada
/// en <c>ApplicationDbContext.OnModelCreating</c>, y el scaffold de ese par las crea.
/// </summary>
public static class NucleoComercialSinMigracion
{
    /// <summary>Las tablas que esperan a <c>InventarioComercialNucleo</c>.</summary>
    public static readonly IReadOnlySet<string> Tablas = new HashSet<string>(StringComparer.Ordinal)
    {
        "INV_DocumentTypes",
        "INV_DocumentTypeWarehouses",
        "INV_DocumentSequences",
        "INV_Documents",
        "INV_DocumentLines",
        "INV_DocumentLinks",
        "INV_DocumentLineLinks",
        "INV_DocumentPartySnapshots",
        "INV_DocumentTaxLines",
        // Fase 4 (US1, T206-T208): catálogo, bodegas, políticas de reorden y alcance por bodega.
        "INV_UnitsOfMeasure",
        "INV_ProductCategories",
        "INV_Brands",
        "INV_AccountingGroups",
        "INV_SalesChannels",
        "INV_Products",
        "INV_ProductUnits",
        "INV_ProductBarcodes",
        "INV_ProductTaxes",
        "INV_ProductAccountingGroupChanges",
        "INV_WarehouseTypes",
        "INV_Warehouses",
        "INV_WarehouseLocations",
        "INV_ReorderPolicies",
        "INV_AdjustmentCauses",
        "INV_UserWarehouseScopes",
        // Fase 5 (US2, T250): kardex y sus tres proyecciones.
        "INV_KardexEntries",
        "INV_StockBalances",
        "INV_StockDetails",
        "INV_CostStates",
    };

    /// <summary>Marca las tablas de <see cref="Tablas"/> como excluidas de las migraciones.</summary>
    public static void ExcluirDeLasMigraciones(ModelBuilder modelBuilder)
    {
        foreach (var entidad in modelBuilder.Model.GetEntityTypes())
        {
            var tabla = entidad.GetTableName();
            if (tabla is not null && Tablas.Contains(tabla))
                entidad.SetIsTableExcludedFromMigrations(true);
        }
    }
}
