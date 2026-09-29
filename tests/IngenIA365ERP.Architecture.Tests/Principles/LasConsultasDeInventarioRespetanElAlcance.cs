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
        // Fase 7, US4 (T312–T314): los lotes y filas de cifras de referencia, la vista previa de la activación y los comparativos.
        "ListLegacyFigureBatchesQueryHandler",
        "ListLegacyFigureRowsQueryHandler",
        "GetWarehouseActivationPreviewQueryHandler",
        "LegacyComparisonKardexQueryHandler",
        "LegacyComparisonValuationQueryHandler",
        // Fase 8, US9 (T348, T349): las listas y el detalle de compras y la vista radian-events.
        "ListPurchaseReceiptsQueryHandler",
        "ListSupplierInvoicesQueryHandler",
        "RadianEventsReportQueryHandler",
        // Fase 9, US10 (T374): la lista y el detalle de traslados (origen o destino) y sus diferencias. Los destinos permitidos
        // (ListTransferDestinationsQueryHandler) no filtran a propósito: son todas las operativas activas (api.md §17.2).
        "ListTransfersQueryHandler",
        "GetTransferQueryHandler",
        "ListTransferDiscrepanciesQueryHandler",
        // Fase 10, US11 (T399, T400, T396): la lista, el detalle y las capturas de los conteos, la vista previa de su ajuste y la vista
        // count-differences.
        "ListPhysicalCountsQueryHandler",
        "GetPhysicalCountQueryHandler",
        "ListCountCapturesQueryHandler",
        "GetCountAdjustmentPreviewQueryHandler",
        "CountDifferencesReportQueryHandler",
        // Fase 21, US17 parte I1 (T955, T956): las vistas documents (origen o destino) y reorder-alerts (por bodega).
        "DocumentsReportQueryHandler",
        "ReorderAlertsReportQueryHandler",
        // Fase 12, US7 (I2; T444–T448, nota de T503/T524): la bandeja de mensajes y su detalle (por la bodega del documento de
        // origen) y las vistas messages y accounting-batches.
        "ListIntegrationMessagesQueryHandler",
        "GetIntegrationMessageQueryHandler",
        "MessagesReportQueryHandler",
        "AccountingBatchesReportQueryHandler",
        // Fase 13, US5 (I3; T613): la lista y el detalle de los documentos de venta (por la bodega o el punto de venta).
        "ListSalesDocumentsQueryHandler",
        "GetSalesDocumentQueryHandler",
        // Fase 14, US6 (I3; T657): el crédito de una venta (su pestaña «Crédito»), por la vista del documento.
        "GetSalesDocumentCreditQueryHandler",
        // I3 (T623, T624): las vistas de ventas y caja y los indicios de deterioro.
        "SalesBySessionReportQueryHandler",
        "SalesByRegisterReportQueryHandler",
        "SalesByPaymentMeansReportQueryHandler",
        "CashSessionReportQueryHandler",
        "DayCloseReportQueryHandler",
        "CardPaymentsReportQueryHandler",
        "CashMovementsReportQueryHandler",
        "CashDifferencesReportQueryHandler",
        "VoucherRedemptionsReportQueryHandler",
        "DiscountApprovalsReportQueryHandler",
        "ImpairmentReportQueryHandler",
        // I3, US5 (T562): puntos, cajas y disponibilidad de un medio (por punto); la venta en el POS (por el punto de la sesión o de
        // la venta, en BorradorDelPos); sesiones, esperado, movimientos, cierres del día y sus PDF (por punto, en SesionesDeCaja).
        // Precios y topes no filtran: son catálogos de la cooperativa, sin bodega ni punto (como los vendedores).
        "ListPointsOfSaleQueryHandler",
        "GetPointOfSaleQueryHandler",
        "ListCashRegistersQueryHandler",
        "GetPaymentMeansAvailabilityQueryHandler",
        "LookupPosProductQueryHandler",
        "GetPosDraftQueryHandler",
        "ListPosDraftsQueryHandler",
        "ListCashSessionsQueryHandler",
        "GetCashSessionQueryHandler",
        "GetCashSessionExpectedQueryHandler",
        "ListCashMovementsQueryHandler",
        "GetCashMovementQueryHandler",
        "ListDayClosesQueryHandler",
        "GetDayCloseQueryHandler",
        "GetCashCountReportQueryHandler",
        "GetCashMovementReceiptQueryHandler",
        // I6, US15 (T934): los lotes con existencia (FEFO) y las series de un producto, por bodega.
        "ListLotsQueryHandler",
        "ListSerialsQueryHandler",
        // I6, US17 (T960–T965, T967): las vistas de analítica, el sugerido de compras, el tope de faltantes y el tablero.
        "MarginReportQueryHandler",
        "TurnoverReportQueryHandler",
        "AbcReportQueryHandler",
        "NoMovementReportQueryHandler",
        "ExpiringReportQueryHandler",
        "PurchaseSuggestionReportQueryHandler",
        "ShrinkageCapReportQueryHandler",
        "GetInventoryDashboardQueryHandler",
    ];

    /// <summary>
    /// Servicios de Application que aplican el alcance por encargo del handler (T562): un handler que recibe uno de ellos en su
    /// constructor lo aplica sin nombrar <c>IAlcanceDeInventario</c>. La prueba exige que cada uno sí lo nombre.
    /// </summary>
    private static readonly string[] AplicadoresDeAlcance = ["SesionesDeCaja", "BorradorDelPos"];

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
            else if (!texto.Contains("IAlcanceDeInventario", StringComparison.Ordinal) && !RecibeUnAplicador(texto, consulta))
                infractores.Add($"{Path.GetRelativePath(root, archivo)}: {consulta} no aplica IAlcanceDeInventario");
        }

        foreach (var aplicador in AplicadoresDeAlcance)
        {
            var declaracion = new Regex($@"\bclass\s+{Regex.Escape(aplicador)}\b", RegexOptions.Compiled);
            var (archivo, texto) = fuentes.FirstOrDefault(f => declaracion.IsMatch(f.Texto));
            if (archivo is null)
                infractores.Add($"{aplicador}: no se encontró su declaración (si se renombró, actualizá AplicadoresDeAlcance)");
            else if (!texto.Contains("IAlcanceDeInventario", StringComparison.Ordinal))
                infractores.Add($"{Path.GetRelativePath(root, archivo)}: {aplicador} no aplica IAlcanceDeInventario");
        }

        Assert.True(infractores.Count == 0,
            "Consultas de inventario sin alcance de bodega o punto (T35):\n  " + string.Join("\n  ", infractores));
    }

    /// <summary>¿El constructor primario del handler recibe uno de <see cref="AplicadoresDeAlcance"/>?</summary>
    private static bool RecibeUnAplicador(string texto, string handler)
    {
        var constructor = Regex.Match(texto, $@"\bclass\s+{Regex.Escape(handler)}\s*\((?<parametros>[^)]*)\)");
        return constructor.Success
            && AplicadoresDeAlcance.Any(a => Regex.IsMatch(constructor.Groups["parametros"].Value, $@"\b{Regex.Escape(a)}\b"));
    }
}
