using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Domain.Common.Parametros;

namespace IngenIA365ERP.Application.Inventory.Imports;

/// <summary>
/// Una de las dieciséis plantillas de la parametrización (feature 012, T49, T160; contracts/plantillas.md §0.1 y §0.7).
/// <see cref="Definicion"/> lleva las hojas; sus columnas las declara la historia que implementa la plantilla, aquí
/// mismo, porque el libro que se descarga y lo que el importador exige salen de la misma definición. (nuevo)
/// </summary>
/// <param name="Numero">El orden de carga (§0.1): cada plantilla sólo cita lo que cargaron las anteriores.</param>
/// <param name="RutaBase">Donde se publican <c>GET {base}/template.xlsx</c> y <c>POST {base}/import</c>.</param>
/// <param name="Comando">El comando de importación (nombre de tipo).</param>
/// <param name="ImportaDesde">La entrega desde la que existe su <c>POST …/import</c>.</param>
/// <param name="DescargaDesde">La entrega desde la que existe su <c>GET template.xlsx</c> (todas I1 salvo la matriz, I2).</param>
/// <param name="PermisoDeDescarga">El permiso de consulta del área (descargar la plantilla vacía).</param>
/// <param name="PermisoDeImportacion">El permiso de importar.</param>
/// <param name="PermisosAdicionales">Los permisos por hoja o columna de §0.7, como texto para la pantalla.</param>
public sealed record PlantillaDeParametrizacion(
    int Numero,
    DefinicionDePlantilla Definicion,
    string RutaBase,
    string Comando,
    EntregaDelComercio ImportaDesde,
    EntregaDelComercio DescargaDesde,
    string PermisoDeDescarga,
    string PermisoDeImportacion,
    string? PermisosAdicionales = null)
{
    public string Clave => Definicion.Clave;

    public string Nombre => Definicion.Nombre;

    public bool SeDescargaYa => DescargaDesde <= CatalogoDeParametros.EntregaVigente;

    public bool SeImportaYa => ImportaDesde <= CatalogoDeParametros.EntregaVigente;
}

/// <summary>
/// Las dieciséis plantillas en el orden de carga de contracts/plantillas.md §0.1 (FR-095), la única lista (T160): la
/// consulta <see cref="ListImportTemplatesQuery"/>, la ruta <c>GET /api/inventory/templates</c> y cada
/// <c>MapPlantilla</c> de la API leen de aquí. (nuevo)
/// </summary>
public static class CatalogoDePlantillas
{
    public const string ImpuestosClave = Core.Taxes.PlantillaDeImpuestos.Clave;
    public const string GruposContablesClave = "inventory.accounting-groups";
    public const string UnidadesClave = "inventory.units";
    public const string MarcasClave = "inventory.brands";
    public const string CategoriasClave = "inventory.product-categories";
    public const string ProductosClave = "inventory.products";
    public const string BodegasClave = "inventory.warehouses";
    public const string TiposDeDocumentoClave = Inventory.DocumentTypes.PlantillaDeTiposDeDocumento.Clave;
    public const string VendedoresClave = "inventory.salespeople";
    public const string PuntosDeVentaClave = "inventory.points-of-sale";
    public const string MediosDePagoClave = "core.payment-means";
    public const string ListasDePreciosClave = "inventory.price-lists";
    public const string TopesDeDescuentoClave = "inventory.discount-caps";
    public const string SaldoInicialClave = "inventory.opening-balances";
    public const string CifrasDeSolidoClave = "inventory.legacy-figures";
    public const string MatrizContableClave = "accounting.inventory-rules";

    private const string Datos = "Datos";

    public static IReadOnlyList<PlantillaDeParametrizacion> Todas { get; } =
    [
        // T166, T227–T229, T238: cada plantilla declara sus columnas junto a su comando (PlantillasDelCatalogo).
        // T166: la plantilla 1 declara sus columnas en Core/Taxes/PlantillaDeImpuestos.
        new(1, Core.Taxes.PlantillaDeImpuestos.Definicion,
            "/api/core/taxes", "ImportTaxCatalogCommand", EntregaDelComercio.I1, EntregaDelComercio.I1,
            "Core.Taxes.View", "Core.Taxes.Manage"),
        new(2, PlantillaDeGruposContables.Definicion,
            "/api/inventory/accounting-groups", "ImportAccountingGroupsCommand", EntregaDelComercio.I1, EntregaDelComercio.I1,
            "Inventory.Catalog.View", "Inventory.Catalog.Import"),
        new(3, PlantillaDeUnidades.Definicion,
            "/api/inventory/units", "ImportUnitsOfMeasureCommand", EntregaDelComercio.I1, EntregaDelComercio.I1,
            "Inventory.Catalog.View", "Inventory.Catalog.Import"),
        new(4, PlantillaDeMarcas.Definicion,
            "/api/inventory/brands", "ImportBrandsCommand", EntregaDelComercio.I1, EntregaDelComercio.I1,
            "Inventory.Catalog.View", "Inventory.Catalog.Import"),
        new(5, PlantillaDeCategorias.Definicion,
            "/api/inventory/product-categories", "ImportProductCategoriesCommand", EntregaDelComercio.I1, EntregaDelComercio.I1,
            "Inventory.Catalog.View", "Inventory.Catalog.Import"),
        new(6, PlantillaDeProductos.Definicion,
            "/api/inventory/products", "ImportProductsCommand", EntregaDelComercio.I1, EntregaDelComercio.I1,
            "Inventory.Catalog.View", "Inventory.Catalog.Import",
            "Cambiar el grupo de un producto con existencia: Inventory.Catalog.ReclassifyAccountingGroup"),
        new(7, PlantillaDeBodegas.Definicion,
            "/api/inventory/warehouses", "ImportWarehousesCommand", EntregaDelComercio.I1, EntregaDelComercio.I1,
            "Inventory.Warehouses.View", "Inventory.Warehouses.Manage",
            "stockNegativo: Inventory.Parameters.Manage"),
        // T153: la plantilla 8 declara sus columnas en Inventory/DocumentTypes/PlantillaDeTiposDeDocumento.
        new(8, Inventory.DocumentTypes.PlantillaDeTiposDeDocumento.Definicion,
            "/api/inventory/document-types", "ImportDocumentTypesCommand", EntregaDelComercio.I1, EntregaDelComercio.I1,
            "Inventory.DocumentTypes.View", "Inventory.DocumentTypes.Manage",
            "Hoja NivelesDeAprobacion: Inventory.ApprovalPolicies.Manage; modo de paso y lotes: Inventory.Parameters.Manage; "
            + "tipo fiscal sin paso: Inventory.DocumentTypes.DisableFiscalPosting"),
        new(9, Def(VendedoresClave, "Vendedores", ModuloDeAuditoria.Inventory, Datos),
            "/api/inventory/salespeople", "ImportSalespeopleCommand", EntregaDelComercio.I1, EntregaDelComercio.I1,
            "Inventory.Salespeople.View", "Inventory.Salespeople.Manage"),
        new(10, PlantillasQueSeImportanConI3.PuntosDeVenta,
            "/api/inventory/points-of-sale", "ImportPointsOfSaleCommand", EntregaDelComercio.I3, EntregaDelComercio.I1,
            "Inventory.PointsOfSale.View", "Inventory.PointsOfSale.Manage"),
        new(11, PlantillasQueSeImportanConI3.MediosDePago,
            "/api/core/payment-means", "ImportPaymentMeansCommand", EntregaDelComercio.I3, EntregaDelComercio.I1,
            "Core.PaymentMeans.View", "Core.PaymentMeans.Manage",
            "Columnas de disponibilidad (puntos, canales, tiposDeDocumento): Inventory.PointsOfSale.Manage"),
        new(12, PlantillasQueSeImportanConI3.ListasDePrecios,
            "/api/inventory/price-lists", "ImportPriceListsCommand", EntregaDelComercio.I3, EntregaDelComercio.I1,
            "Inventory.Prices.View", "Inventory.Prices.Manage"),
        new(13, PlantillasQueSeImportanConI3.TopesDeDescuento,
            "/api/inventory/discount-caps", "ImportDiscountCapsCommand", EntregaDelComercio.I3, EntregaDelComercio.I1,
            "Inventory.Prices.View", "Inventory.DiscountCaps.Manage"),
        new(14, Def(SaldoInicialClave, "Saldo inicial", ModuloDeAuditoria.Inventory, Datos),
            "/api/inventory/opening-balances", "ImportOpeningBalanceCommand", EntregaDelComercio.I1, EntregaDelComercio.I1,
            "Inventory.Warehouses.View", "Inventory.OpeningBalance.Load",
            "Confirmar el documento: Inventory.OpeningBalance.Approve (fuera de la plantilla)"),
        new(15, Def(CifrasDeSolidoClave, "Cifras de SOLIDO", ModuloDeAuditoria.Inventory, Datos),
            "/api/inventory/legacy-figures", "ImportLegacyFiguresCommand", EntregaDelComercio.I1, EntregaDelComercio.I1,
            "Inventory.Warehouses.View", "Inventory.LegacyFigures.Import"),
        new(16, Def(MatrizContableClave, "Matriz de reglas contables", ModuloDeAuditoria.Accounting, Datos),
            "/api/accounting/inventory/rules", "ImportInventoryPostingRulesCommand", EntregaDelComercio.I2, EntregaDelComercio.I2,
            "Accounting.InventoryRules.View", "Accounting.InventoryRules.Manage"),
    ];

    /// <summary>La plantilla por su clave (<c>inventory.products</c>).</summary>
    public static PlantillaDeParametrizacion Por(string clave) =>
        Todas.FirstOrDefault(p => string.Equals(p.Clave, clave, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"No hay una plantilla «{clave}» en CatalogoDePlantillas.", nameof(clave));

    /// <summary>
    /// Las hojas sin columnas: las columnas las declara la historia que implementa cada plantilla (reemplazando esta
    /// definición por la completa, con <see cref="DefinicionDePlantilla"/> y sus <see cref="ColumnaDePlantilla"/>).
    /// </summary>
    private static DefinicionDePlantilla Def(string clave, string nombre, string modulo, params string[] hojas) =>
        new(clave, nombre, modulo, hojas.Select(h => new HojaDePlantilla(h, [])).ToList());
}
