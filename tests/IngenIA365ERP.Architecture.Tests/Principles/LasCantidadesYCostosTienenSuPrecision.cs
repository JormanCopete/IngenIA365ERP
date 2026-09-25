using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T002 (decisiones-transversales T19, §2.18), FR-017: cantidades, costos, precios,
/// montos y tarifas se guardan en decimal exacto con la precisión de
/// <c>PrecisionDeInventario</c> —cantidad (18,4), factor (18,6), costo y precio unitario (18,6),
/// monto (18,2), tarifa como fracción (9,6)—. La convención global (18,2) trunca un costo promedio
/// o una tarifa de ICA por mil sin avisar.
///
/// <para>
/// Esqueleto del Setup: <see cref="Columnas"/> empieza vacía y la prueba afirma la regla sobre cada
/// elemento; con la lista vacía pasa porque no hay nada que violar, no por un <c>return</c>
/// temprano. La llena el bloque que configura cada entidad (base de inventario, fase 3, y las
/// historias siguientes): el archivo de configuración EF (relativo a la raíz), la propiedad y el
/// ayudante de <c>PrecisionDeInventario</c> que debe usar.
/// </para>
/// </summary>
public class LasCantidadesYCostosTienenSuPrecision
{
    private const string Inv = "src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Inventory/";

    /// <summary>(configuración EF relativa a la raíz, propiedad, ayudante de precisión). Los agrega cada bloque.</summary>
    private static readonly (string Configuracion, string Propiedad, string Precision)[] Columnas =
    [
        // Base de inventario, fase 3 (T135, T136): documento genérico (data-model §5.1, §5.4, §5.5, §5.7).
        (Inv + "InventoryDocumentConfiguration.cs", "ExchangeRate", "Factor"),
        (Inv + "InventoryDocumentConfiguration.cs", "Subtotal", "Monto"),
        (Inv + "InventoryDocumentConfiguration.cs", "DiscountTotal", "Monto"),
        (Inv + "InventoryDocumentConfiguration.cs", "TaxTotal", "Monto"),
        (Inv + "InventoryDocumentConfiguration.cs", "WithholdingTotal", "Monto"),
        (Inv + "InventoryDocumentConfiguration.cs", "Total", "Monto"),
        (Inv + "InventoryDocumentConfiguration.cs", "AmountDue", "Monto"),
        (Inv + "InventoryDocumentConfiguration.cs", "CostTotal", "Monto"),
        (Inv + "InventoryDocumentLineConfiguration.cs", "Quantity", "Cantidad"),
        (Inv + "InventoryDocumentLineConfiguration.cs", "Factor", "Factor"),
        (Inv + "InventoryDocumentLineConfiguration.cs", "QuantityBase", "Cantidad"),
        (Inv + "InventoryDocumentLineConfiguration.cs", "RoundingQuantity", "Cantidad"),
        (Inv + "InventoryDocumentLineConfiguration.cs", "UnitPrice", "PrecioUnitario"),
        (Inv + "InventoryDocumentLineConfiguration.cs", "GrossAmount", "Monto"),
        (Inv + "InventoryDocumentLineConfiguration.cs", "DiscountAmount", "Monto"),
        (Inv + "InventoryDocumentLineConfiguration.cs", "NetAmount", "Monto"),
        (Inv + "InventoryDocumentLineConfiguration.cs", "UnitCost", "CostoUnitario"),
        (Inv + "InventoryDocumentLineConfiguration.cs", "TotalCost", "Monto"),
        (Inv + "DocumentLineLinkConfiguration.cs", "QuantityBase", "Cantidad"),
        (Inv + "DocumentTaxLineConfiguration.cs", "Rate", "Tarifa"),
        (Inv + "DocumentTaxLineConfiguration.cs", "AmountPerUnit", "Monto"),
        (Inv + "DocumentTaxLineConfiguration.cs", "TaxableUnits", "Cantidad"),
        (Inv + "DocumentTaxLineConfiguration.cs", "Base", "Monto"),
        (Inv + "DocumentTaxLineConfiguration.cs", "Amount", "Monto"),
    ];

    /// <summary>Espacios de nombres de entidades cuyos decimales tienen que declarar su precisión (T19).</summary>
    private static readonly string[] EspaciosVigilados =
    [
        "IngenIA365ERP.Domain.Entities.Inventory",
        "IngenIA365ERP.Domain.Entities.Core.Taxes",
    ];

    /// <summary>(entidad, propiedad) decimales que se admiten sin alias, con su motivo. Vacía a propósito.</summary>
    private static readonly (string Entidad, string Propiedad)[] Excepciones = [];

    private static readonly string[] Alias = ["Cantidad", "Factor", "CostoUnitario", "PrecioUnitario", "Monto", "Tarifa"];

    [Fact]
    public void Toda_propiedad_decimal_del_comercio_declara_su_alias_de_precision()
    {
        var root = RepoPath.FindRepoRoot();
        var configuraciones = Directory.EnumerateFiles(
                Path.Combine(root, "src", "Infrastructure", "IngenIA365ERP.Persistence", "Configurations"), "*Configuration.cs", SearchOption.AllDirectories)
            .GroupBy(f => Path.GetFileNameWithoutExtension(f)[..^"Configuration".Length])
            .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.Contains("Inventory", StringComparison.Ordinal)).First());
        var infractores = new List<string>();

        var entidades = typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace is not null
                && (typeof(IngenIA365ERP.Domain.Common.BaseEntity).IsAssignableFrom(t) || typeof(IngenIA365ERP.Domain.Common.BaseEntityLong).IsAssignableFrom(t))
                && EspaciosVigilados.Any(n => t.Namespace == n || t.Namespace.StartsWith(n + ".", StringComparison.Ordinal)));

        foreach (var entidad in entidades)
        {
            var decimales = entidad.GetProperties().Where(p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?))
                .Where(p => !Excepciones.Contains((entidad.Name, p.Name))).ToList();
            if (decimales.Count == 0) continue;

            if (!configuraciones.TryGetValue(entidad.Name, out var archivo))
            {
                infractores.Add($"{entidad.Name}: tiene decimales y no se encontró {entidad.Name}Configuration.cs");
                continue;
            }

            var texto = File.ReadAllText(archivo);
            foreach (var p in decimales)
            {
                var mapeo = new Regex($@"\.{Regex.Escape(p.Name)}\)\s*\.\s*(?:{string.Join("|", Alias)})\(\)");
                if (!mapeo.IsMatch(texto))
                    infractores.Add($"{entidad.Name}.{p.Name}: su configuración no usa un alias de PrecisionDeInventario");
            }
        }

        Assert.True(infractores.Count == 0,
            "Decimales del comercio sin su precisión (FR-017, T19):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Toda_tarifa_es_fraccion_9_6_y_las_configuraciones_no_fijan_precision_a_mano()
    {
        var root = RepoPath.FindRepoRoot();
        var carpeta = Path.Combine(root, "src", "Infrastructure", "IngenIA365ERP.Persistence", "Configurations", "Inventory");
        var precision = File.ReadAllText(Path.Combine(carpeta, "PrecisionDeInventario.cs"));

        Assert.Matches(@"Tarifa\(this PropertyBuilder<decimal> propiedad\) => propiedad\.HasPrecision\(9, 6\)", precision);
        Assert.Matches(@"Tarifa\(this PropertyBuilder<decimal\?> propiedad\) => propiedad\.HasPrecision\(9, 6\)", precision);

        var aMano = Directory.EnumerateFiles(carpeta, "*.cs")
            .Where(f => Path.GetFileName(f) != "PrecisionDeInventario.cs")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\.HasPrecision\(|HasColumnType\(""(decimal|numeric)"))
            .Select(f => Path.GetRelativePath(root, f)).ToList();
        Assert.True(aMano.Count == 0,
            "Configuraciones de inventario que fijan la precisión sin PrecisionDeInventario (T19):\n  " + string.Join("\n  ", aMano));
    }

    [Fact]
    public void Cada_columna_de_cantidad_o_costo_usa_su_precision()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();

        foreach (var (configuracion, propiedad, precision) in Columnas)
        {
            var archivo = Path.Combine(root, configuracion);
            Assert.True(File.Exists(archivo), $"No existe {configuracion}: si la configuración se movió, actualizá Columnas.");
            var texto = File.ReadAllText(archivo);

            // La propiedad y, en la misma cadena fluida (antes del ';'), su ayudante de precisión.
            var mapeo = new Regex($@"\.{Regex.Escape(propiedad)}\b[^;]*\b{Regex.Escape(precision)}\b", RegexOptions.Compiled);
            if (!mapeo.IsMatch(texto))
                infractores.Add($"{configuracion}: {propiedad} no usa PrecisionDeInventario.{precision}");
        }

        Assert.True(infractores.Count == 0,
            "Columnas de inventario sin su precisión (FR-017, T19):\n  " + string.Join("\n  ", infractores));
    }
}
