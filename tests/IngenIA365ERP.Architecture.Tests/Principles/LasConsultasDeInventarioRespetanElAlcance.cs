using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T001 (decisiones-transversales T35, §2.18), FR-009: toda consulta de inventario
/// filtra por el alcance de bodega y punto de venta de quien pregunta (<c>IAlcanceDeInventario</c> y
/// <c>FiltroDeAlcance</c>); falla cerrado salvo el permiso de alcance total. Una consulta sin filtro
/// muestra existencias y documentos de bodegas ajenas.
///
/// <para>
/// <see cref="ConsultasDeInventario"/> lleva los nombres de tipo de los handlers de consulta que filtran por alcance, y
/// la prueba exige que el archivo que declara cada uno aplique <c>IAlcanceDeInventario</c> (directo o por
/// <c>FiltroDeAlcance</c>, cuyo archivo lo nombra en su documentación y firma). La llenó la plataforma (fase 2, T087) con
/// las consultas que ya existen —el alcance comercial de un usuario y la bandeja y el detalle de alertas— y cada
/// historia agrega las suyas (fase 3 en adelante; fases 11 y 21, US12 y US17). Las consultas de vendedores
/// (<c>Application/Inventory/Salespeople</c>) no están: son personas, no tienen bodega ni punto (FR-031).
/// </para>
/// </summary>
public class LasConsultasDeInventarioRespetanElAlcance
{
    /// <summary>Nombres de tipo de los handlers de consulta de inventario. Los agrega cada historia.</summary>
    private static readonly string[] ConsultasDeInventario =
    [
        "GetUserCommercialScopeQueryHandler",
        "ListAlertsQueryHandler",
        "GetAlertQueryHandler",
        // Fase 3, ciclo común (T148): la lista y el detalle genéricos de documentos.
        "ListInventoryDocumentsQueryHandler",
        "GetInventoryDocumentQueryHandler",
        // Fase 4, US1 (T220, T223, T225): bodegas, ubicaciones, políticas de reorden y la búsqueda con disponible por bodega.
        "ListWarehousesQueryHandler",
        "GetWarehouseQueryHandler",
        "ListLocationsQueryHandler",
        "ListReorderPoliciesQueryHandler",
        "SearchProductsQueryHandler",
        // Fase 5, US2 (T256, T258, T260): existencias, integridad y las vistas kardex y stock.
        "GetStockQueryHandler",
        "GetProductStockQueryHandler",
        "VerifyInventoryIntegrityQueryHandler",
        "KardexReportQueryHandler",
        "StockReportQueryHandler",
    ];

    [Fact]
    public void Cada_consulta_de_inventario_aplica_el_alcance()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var consulta in ConsultasDeInventario)
        {
            var declaracion = new Regex($@"\b(record|class)\s+{Regex.Escape(consulta)}\b", RegexOptions.Compiled);
            var (archivo, texto) = fuentes.FirstOrDefault(f => declaracion.IsMatch(f.Texto));

            if (archivo is null)
                infractores.Add($"{consulta}: no se encontró su declaración (si se renombró, actualizá ConsultasDeInventario)");
            else if (!texto.Contains("IAlcanceDeInventario", StringComparison.Ordinal))
                infractores.Add($"{Path.GetRelativePath(root, archivo)}: {consulta} no aplica IAlcanceDeInventario");
        }

        Assert.True(infractores.Count == 0,
            "Consultas de inventario sin alcance de bodega o punto (T35):\n  " + string.Join("\n  ", infractores));
    }
}
