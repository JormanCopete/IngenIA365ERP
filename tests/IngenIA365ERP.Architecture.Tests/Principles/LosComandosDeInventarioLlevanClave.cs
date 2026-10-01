using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T001 (decisiones-transversales T13, §2.18; contracts/api.md §2.3), FR-016: toda
/// operación iniciada desde una pantalla o una integración lleva una clave de idempotencia. El
/// comando lo declara implementando <c>IOperacionIdempotente</c>; sin el marcador,
/// <c>IdempotencyBehavior</c> lo deja pasar y un doble clic confirma dos veces.
///
/// <para>
/// Llenada por la fase 2 (plataforma, T018) en dos partes: <see cref="ComandosConRuta"/>, los comandos de
/// plataforma de esta fase por nombre (tienen que existir), y el recorrido de
/// <see cref="CarpetasConClave"/>: todo <c>*Command</c> que implemente <c>IRequest</c> y que algún
/// archivo de <c>API/Endpoints</c> nombre (en código, no en comentarios) lleva la clave. Las consultas
/// —todo GET y los POST que son consultas de contracts/api.md §2.3 (<c>integrity/verify</c>,
/// <c>purchases/supplier-invoices/prefill</c>, <c>documents/{id}/prevalidate</c>,
/// <c>documents/{id}/cost-impact</c>, <c>sales/credit-evaluations</c>,
/// <c>/api/accounting/inventory/batches/preview</c>)— se nombran <c>*Query</c> y quedan fuera solas; si
/// alguna se nombra <c>*Command</c>, va en <see cref="ConsultasPorPost"/> con su ruta.
/// </para>
/// </summary>
public class LosComandosDeInventarioLlevanClave
{
    /// <summary>Comandos de plataforma con ruta que deben llevar clave (T018), por nombre de tipo.</summary>
    private static readonly string[] ComandosConRuta =
    [
        "AddParameterVersionCommand",
        "SaveApprovalPolicyCommand",
        "SetPermissionAmountLimitCommand",
        "DecideApprovalCommand",
        "AttendAlertCommand",
        "SaveAlertTypeCommand",
        "SetUserCommercialScopeCommand",
        // Fase 3 (T128): crear un rol desde un perfil sugerido, desde la pantalla de Roles.
        "CreateRoleFromTemplateCommand",
        // Fase 7, US4 (T324): la puesta en marcha —saldo inicial, cifras de referencia y activación de una bodega—.
        "ImportOpeningBalanceCommand",
        "ImportLegacyFiguresCommand",
        "ActivateWarehouseCommand",
        // Fase 8, US9 (T358): la compra directa y el registro de un evento RADIAN hecho por fuera.
        "ConfirmDirectPurchaseCommand",
        "RegisterExternalRadianEventCommand",
        // Fase 9, US10 (T381): despachar, recibir y resolver una diferencia de traslado.
        "DispatchTransferCommand",
        "ReceiveTransferCommand",
        "ResolveTransferDiscrepancyCommand",
        // Fase 10, US11 (T405): definir, abrir, capturar y cerrar un conteo, y generar su ajuste.
        "CreatePhysicalCountCommand",
        "UpdatePhysicalCountCommand",
        "OpenPhysicalCountCommand",
        "CapturePhysicalCountCommand",
        "ClosePhysicalCountCommand",
        "GenerateCountAdjustmentCommand",
        // I3, US5 (T562): medios de pago de Core (§22.1–§22.2), su disponibilidad y los puntos y cajas (§20.1, §22.3), precios y
        // topes (§19), la venta en el POS (§20.2), la caja (§21), las notas de venta (§18.3) y la entrega (§20.3).
        "CreatePaymentMeansCommand",
        "UpdatePaymentMeansCommand",
        "DeletePaymentMeansCommand",
        "ImportPaymentMeansCommand",
        "CreateCardNetworkCommand",
        "UpdateCardNetworkCommand",
        "DeleteCardNetworkCommand",
        "CreateCardAcquirerCommand",
        "UpdateCardAcquirerCommand",
        "DeleteCardAcquirerCommand",
        "CreateCardTerminalCommand",
        "UpdateCardTerminalCommand",
        "DeleteCardTerminalCommand",
        "CreateCashDenominationCommand",
        "UpdateCashDenominationCommand",
        "DeleteCashDenominationCommand",
        "SetPaymentMeansAvailabilityCommand",
        "CreatePointOfSaleCommand",
        "UpdatePointOfSaleCommand",
        "CreateCashRegisterCommand",
        "UpdateCashRegisterCommand",
        "ImportPointsOfSaleCommand",
        "CreatePriceListCommand",
        "UpdatePriceListCommand",
        "SetPriceListItemsCommand",
        "ImportPriceListsCommand",
        "CreateDiscountCapCommand",
        "ImportDiscountCapsCommand",
        "CreatePosDraftCommand",
        "UpdatePosDraftCommand",
        "AddPosLineCommand",
        "UpdatePosLineCommand",
        "RemovePosLineCommand",
        "SuspendPosDraftCommand",
        "ResumePosDraftCommand",
        "DiscardPosDraftCommand",
        "CheckoutPosDraftCommand",
        "OpenCashSessionCommand",
        "CloseCashSessionCommand",
        "RecountCashSessionCommand",
        "ExecuteDayCloseCommand",
        "ReopenDayCloseCommand",
        "SaveCreditNoteDraftCommand",
        "DeliverSalesDocumentCommand",
        "ReprintDocumentCommand",
        // I4, US8 (T744–T748; api.md §24, §14.7, §18.3.1): configuración y credencial, resoluciones, casos a/b/c, cambio de canal,
        // contingencias, factura en lugar del documento equivalente y el documento soporte semanal.
        "ConfigureEmissionCommand",
        "VerifyChannelCredentialCommand",
        "RegisterNumberingResolutionCommand",
        "UpdateNumberingResolutionCommand",
        "LinkResolutionToChannelCommand",
        "CorrectRejectedDocumentCommand",
        "CreateReplacementDraftCommand",
        "ReplaceRejectedDocumentCommand",
        "CancelRejectedDocumentCommand",
        "TransmitByCurrentChannelCommand",
        "OpenContingencyCommand",
        "CloseContingencyCommand",
        "ReplacePosDocumentWithInvoiceCommand",
        "GenerateWeeklySupportDocumentsCommand",
        // I5, US13 (T773; api.md §14.8, §14.9): el envío de la orden al proveedor, el cierre de su saldo y la emisión RADIAN desde el ERP.
        // Solicitudes, órdenes y costos adicionales se guardan por el ciclo común (SaveInventoryDraftCommand y sus vecinos, ya con clave).
        "SendPurchaseOrderCommand",
        "ClosePurchaseOrderBalanceCommand",
        "EmitRadianEventCommand",
    ];

    /// <summary>
    /// Los comandos que se hacen desde una caja (contracts/api.md §18, «Canal pos»; T36, T562): la venta en el POS (§20.2) y la
    /// sesión de caja (§21.1–§21.2). Implementan además <c>IOperacionDePuntoDeVenta</c>, así la auditoría los registra con canal
    /// <c>pos</c>. Quedan fuera, a propósito, el cierre del día y su reapertura (un acto del supervisor sobre el punto, no sobre una
    /// sesión) y los movimientos de caja, que se guardan por el ciclo común de documentos (<c>SaveInventoryDraftCommand</c>).
    /// </summary>
    private static readonly string[] ComandosDePuntoDeVenta =
    [
        "CreatePosDraftCommand",
        "UpdatePosDraftCommand",
        "AddPosLineCommand",
        "UpdatePosLineCommand",
        "RemovePosLineCommand",
        "SuspendPosDraftCommand",
        "ResumePosDraftCommand",
        "DiscardPosDraftCommand",
        "CheckoutPosDraftCommand",
        "OpenCashSessionCommand",
        "CloseCashSessionCommand",
        "RecountCashSessionCommand",
    ];

    /// <summary>Carpetas de <c>src/Core/IngenIA365ERP.Application</c> cuyos comandos con ruta llevan clave. Una que no existe todavía cuenta como vacía.</summary>
    private static readonly string[] CarpetasConClave =
    [
        "Inventory",
        "ElectronicInvoicing",
        Path.Combine("Core", "Taxes"),
        Path.Combine("Core", "PaymentMeans"),
    ];

    /// <summary>Consultas enviadas por POST que se llaman <c>*Command</c> (contracts/api.md §2.3), con su ruta. Hoy ninguna.</summary>
    private static readonly Dictionary<string, string> ConsultasPorPost = new(StringComparer.Ordinal);

    /// <summary>
    /// Feature 012, I4 (T746; contracts/dian.md §6.1): los dos comandos que hablan con el canal de emisión. No son
    /// <c>IOperacionIdempotente</c> a propósito: la clave abriría una transacción alrededor de la llamada al canal, que va sola, y el
    /// registro del resultado en otra. Repetirlos es inocuo por el arrendamiento de la fila, la unicidad del número y la regla del
    /// ambiguo (consultar antes de reenviar). Sus rutas igual exigen la cabecera <c>Idempotency-Key</c>
    /// (<see cref="Las_escrituras_de_facturacion_electronica_exigen_la_clave"/>).
    /// </summary>
    private static readonly Dictionary<string, string> IdempotentesPorElArrendamiento = new(StringComparer.Ordinal)
    {
        ["EmitElectronicDocumentCommand"] = "POST /api/electronic-invoicing/documents/{id}/retry",
        ["QueryElectronicDocumentStatusCommand"] = "POST /api/electronic-invoicing/documents/{id}/query-status",
    };

    /// <summary>
    /// T744–T748 (api.md §24, §14.7): toda escritura de <c>/api/electronic-invoicing</c> (POST o PUT) lleva <c>ConClaveDeOperacion()</c>,
    /// salvo el enlace de descarga, que es una consulta auditada como la de adjuntos de la 011.
    /// </summary>
    [Fact]
    public void Las_escrituras_de_facturacion_electronica_exigen_la_clave()
    {
        var root = RepoPath.FindRepoRoot();
        var carpeta = Path.Combine(root, "src", "Presentation", "IngenIA365ERP.API", "Endpoints", "ElectronicInvoicing");
        Assert.True(Directory.Exists(carpeta), "No existe Endpoints/ElectronicInvoicing (T744–T747).");
        // Cada ruta va desde su .MapX( hasta la siguiente (el mismo corte que LosEndpointsProtegidosExigenPermiso.Tramos).
        var escritura = new Regex(@"^\.Map(Post|Put)\(\s*""(?<ruta>[^""]*)""", RegexOptions.Compiled);
        var infractores = new List<string>();
        var revisadas = 0;

        foreach (var archivo in Directory.EnumerateFiles(carpeta, "*.cs"))
        {
            foreach (var tramo in LosEndpointsProtegidosExigenPermiso.Tramos(FuenteSinComentarios.Leer(archivo)))
            {
                var m = escritura.Match(tramo);
                if (!m.Success) continue;
                revisadas++;
                if (m.Groups["ruta"].Value.EndsWith("/download-link", StringComparison.Ordinal)) continue;
                if (!tramo.Contains(".ConClaveDeOperacion()", StringComparison.Ordinal))
                    infractores.Add($"{Path.GetFileName(archivo)}: {m.Groups["ruta"].Value} sin ConClaveDeOperacion()");
            }
        }

        Assert.True(revisadas >= 12, $"Se esperaban al menos 12 escrituras en Endpoints/ElectronicInvoicing; se encontraron {revisadas}.");
        Assert.True(infractores.Count == 0, "Escrituras de facturación electrónica sin Idempotency-Key (FR-016, T13):\n  " + string.Join("\n  ", infractores));
    }

    /// <summary>Las rutas de compras de I5 (T808; api.md §14.8, §14.9) que tienen que estar publicadas en <c>PurchasesEndpoints</c>.</summary>
    private static readonly string[] RutasDeComprasDeI5 =
    [
        "\"/requests\"", "\"/orders\"", "\"/{id:guid}/pdf\"", "\"/{id:guid}/send\"", "\"/{id:guid}/close-balance\"",
        "\"/matches\"", "\"/{id:guid}/match\"", "\"/landed-costs\"", "\"/{id:guid}/radian-events/emit\"",
    ];

    /// <summary>
    /// I5, T773 (T808; api.md §2.3, §14): toda escritura de <c>/api/inventory/purchases</c> (POST o PUT publicada en <c>PurchasesEndpoints</c>)
    /// lleva <c>ConClaveDeOperacion()</c>, salvo el prellenado desde el XML, que es una consulta. El ciclo común de solicitudes, órdenes y
    /// costos adicionales la pone adentro de <c>CicloDeDocumentoRutas</c>. Además, las rutas de I5 están todas.
    /// </summary>
    [Fact]
    public void Las_escrituras_de_compras_exigen_la_clave_y_estan_las_rutas_de_I5()
    {
        var root = RepoPath.FindRepoRoot();
        var archivo = Path.Combine(root, "src", "Presentation", "IngenIA365ERP.API", "Endpoints", "Inventory", "PurchasesEndpoints.cs");
        var texto = FuenteSinComentarios.Leer(archivo);
        var escritura = new Regex(@"^\.Map(Post|Put)\(\s*""(?<ruta>[^""]*)""", RegexOptions.Compiled);
        var infractores = new List<string>();
        var revisadas = 0;

        foreach (var tramo in LosEndpointsProtegidosExigenPermiso.Tramos(texto))
        {
            var m = escritura.Match(tramo);
            if (!m.Success) continue;
            revisadas++;
            if (m.Groups["ruta"].Value == "/prefill") continue;
            if (!tramo.Contains(".ConClaveDeOperacion()", StringComparison.Ordinal))
                infractores.Add($"{m.Groups["ruta"].Value} sin ConClaveDeOperacion()");
        }

        foreach (var ruta in RutasDeComprasDeI5.Where(r => !texto.Contains(r, StringComparison.Ordinal)))
            infractores.Add($"falta la ruta {ruta} (T808)");
        var ciclos = Regex.Matches(texto, @"\.MapCicloDeDocumento\(").Count;
        if (ciclos < 8) infractores.Add($"se esperaban 8 ciclos comunes (5 de I1/I4 más solicitudes, órdenes y costos adicionales); hay {ciclos}");

        Assert.True(revisadas >= 6, $"Se esperaban al menos 6 escrituras propias en PurchasesEndpoints; se encontraron {revisadas}.");
        Assert.True(infractores.Count == 0, "Compras de I5 (FR-016, T13, T808):\n  " + string.Join("\n  ", infractores));
    }

    /// <summary>
    /// I5, T845 (api.md §2.3, §9.3): el impacto en costos es una <b>consulta</b> por POST: se llama <c>*Query</c>, su ruta existe en
    /// <c>DocumentsEndpoints</c> con <c>Inventory.Costs.Read</c> y no exige la clave.
    /// </summary>
    [Fact]
    public void El_impacto_en_costos_es_una_consulta_sin_clave()
    {
        var root = RepoPath.FindRepoRoot();
        var archivo = Path.Combine(root, "src", "Presentation", "IngenIA365ERP.API", "Endpoints", "Inventory", "DocumentsEndpoints.cs");
        var tramo = LosEndpointsProtegidosExigenPermiso.Tramos(FuenteSinComentarios.Leer(archivo))
            .FirstOrDefault(t => t.Contains("\"/{id:guid}/cost-impact\"", StringComparison.Ordinal));

        Assert.True(tramo is not null, "Falta POST /api/inventory/documents/{id}/cost-impact (T845).");
        Assert.StartsWith(".MapPost(", tramo);
        Assert.Contains("GetDocumentCostImpactQuery", tramo);
        Assert.Contains("LeerCostos", tramo);
        Assert.DoesNotContain(".ConClaveDeOperacion()", tramo);
    }

    /// <summary>
    /// Comandos anteriores a la feature que la reescriben después, con la tarea que los reescribe: los de
    /// vendedores (se conservaron del módulo heredado, T036) los rehace US12 con <c>IOperacionIdempotente</c>
    /// (T424, T425), y en esa tarea salen de aquí. Si alguno ya lleva la clave, la prueba pide sacarlo.
    /// </summary>
    /// <remarks>Vacía desde la fase 11 (US12, T424–T426): los tres de vendedores ya llevan la clave.</remarks>
    private static readonly Dictionary<string, string> PendientesDeReescritura = new(StringComparer.Ordinal);

    private static readonly Regex DeclaracionDeComando = new(
        @"\b(record|class)\s+(?<nombre>\w+Command)\b(?<resto>[^{;]*)", RegexOptions.Compiled);

    [Fact]
    public void Cada_comando_con_ruta_implementa_IOperacionIdempotente()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var comando in ComandosConRuta)
        {
            // La declaración hasta la llave o el punto y coma: ahí van la base y las interfaces.
            var declaracion = new Regex($@"\b(record|class)\s+{Regex.Escape(comando)}\b[^{{;]*", RegexOptions.Compiled);
            var encontrada = fuentes
                .Select(f => (f.Archivo, Match: declaracion.Match(f.Texto)))
                .FirstOrDefault(x => x.Match.Success);

            if (encontrada.Archivo is null)
                infractores.Add($"{comando}: no se encontró su declaración (si se renombró, actualizá ComandosConRuta)");
            else if (!encontrada.Match.Value.Contains("IOperacionIdempotente", StringComparison.Ordinal))
                infractores.Add($"{Path.GetRelativePath(root, encontrada.Archivo)}: {comando} no implementa IOperacionIdempotente");
        }

        Assert.True(infractores.Count == 0,
            "Comandos con ruta sin clave de idempotencia (FR-016):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Los_comandos_del_POS_y_de_la_caja_se_auditan_con_canal_pos()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var comando in ComandosDePuntoDeVenta)
        {
            var declaracion = new Regex($@"\b(record|class)\s+{Regex.Escape(comando)}\b[^{{;]*", RegexOptions.Compiled);
            var encontrada = fuentes
                .Select(f => (f.Archivo, Match: declaracion.Match(f.Texto)))
                .FirstOrDefault(x => x.Match.Success);

            if (encontrada.Archivo is null)
            {
                infractores.Add($"{comando}: no se encontró su declaración (si se renombró, actualizá ComandosDePuntoDeVenta)");
                continue;
            }

            var relativo = Path.GetRelativePath(root, encontrada.Archivo);
            if (!encontrada.Match.Value.Contains("IOperacionIdempotente", StringComparison.Ordinal))
                infractores.Add($"{relativo}: {comando} no implementa IOperacionIdempotente");
            if (!encontrada.Match.Value.Contains("IOperacionDePuntoDeVenta", StringComparison.Ordinal))
                infractores.Add($"{relativo}: {comando} no implementa IOperacionDePuntoDeVenta");
        }

        Assert.True(infractores.Count == 0,
            "Comandos del punto de venta sin clave o sin canal pos (T13, T36):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Cada_comando_con_ruta_de_inventario_y_sus_vecinos_implementa_IOperacionIdempotente()
    {
        var root = RepoPath.FindRepoRoot();
        var application = Path.Combine(root, "src", "Core", "IngenIA365ERP.Application");
        var endpoints = Path.Combine(root, "src", "Presentation", "IngenIA365ERP.API", "Endpoints");
        var codigoDeRutas = string.Join('\n', Directory.EnumerateFiles(endpoints, "*.cs", SearchOption.AllDirectories)
            .Select(FuenteSinComentarios.Leer));
        var infractores = new List<string>();
        var revisados = 0;
        var pendientesVistos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var carpeta in CarpetasConClave)
        {
            var ruta = Path.Combine(application, carpeta);
            if (!Directory.Exists(ruta)) continue; // carpeta aún no creada: no tiene nada que violar

            foreach (var archivo in Directory.EnumerateFiles(ruta, "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in DeclaracionDeComando.Matches(FuenteSinComentarios.Leer(archivo)))
                {
                    var nombre = m.Groups["nombre"].Value;
                    var resto = m.Groups["resto"].Value;
                    if (!resto.Contains("IRequest", StringComparison.Ordinal)) continue;
                    if (!Regex.IsMatch(codigoDeRutas, $@"\b{Regex.Escape(nombre)}\b")) continue; // sin ruta
                    if (ConsultasPorPost.ContainsKey(nombre) || IdempotentesPorElArrendamiento.ContainsKey(nombre)) continue;

                    revisados++;
                    var llevaClave = resto.Contains("IOperacionIdempotente", StringComparison.Ordinal);
                    var relativo = Path.GetRelativePath(root, archivo);
                    if (PendientesDeReescritura.TryGetValue(nombre, out var tarea))
                    {
                        pendientesVistos.Add(nombre);
                        if (llevaClave)
                            infractores.Add($"{relativo}: {nombre} ya lleva la clave; quitálo de PendientesDeReescritura ({tarea})");
                        continue;
                    }
                    if (!llevaClave)
                        infractores.Add($"{relativo}: {nombre} tiene ruta y no implementa IOperacionIdempotente");
                }
            }
        }

        foreach (var pendiente in PendientesDeReescritura.Keys.Where(p => !pendientesVistos.Contains(p)))
            infractores.Add($"{pendiente}: ya no es un comando con ruta en estas carpetas; quitálo de PendientesDeReescritura");

        Assert.True(revisados > 0, "El recorrido no encontró ningún comando con ruta: ¿cambió la forma de declararlos?");
        Assert.True(infractores.Count == 0,
            "Comandos con ruta sin clave de idempotencia (FR-016, contracts/api.md §2.3):\n  " + string.Join("\n  ", infractores));
    }
}
