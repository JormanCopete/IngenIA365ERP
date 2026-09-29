using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Application.Inventory.Purchasing.Consultas;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Inventory.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, I5, T770 (US13-1, US13-2; FR-050; contracts/api.md §14.9; data-model §9.6, §21; decisiones-transversales §3 T42a, T42d):
/// el cruce a tres vías al confirmar la factura del proveedor contra recepciones que vienen de una orden, sobre el ciclo común real.
/// <list type="bullet">
/// <item>las líneas que exceden la tolerancia quedan <c>Held</c> en <c>INV_PurchaseMatchLines</c>, con una solicitud por línea
/// (<c>PurchaseMatchException</c>, <c>SourceType = PurchaseMatchLine</c>) y la factura en <c>PendingApproval</c> sin número;</item>
/// <item>facturar más de lo recibido con orden no responde 422: queda retenida por cantidad y esa razón no se aprueba (T796);</item>
/// <item>un segundo intento de confirmar da de baja lógica las filas del anterior;</item>
/// <item>aprobada la última retenida, la factura se confirma en la transacción del aprobador con la diferencia de precio partida entre
/// existencia y vendido y un <c>AjusteDeCostoReconocido</c> por recepción afectada;</item>
/// <item>rechazada una, la factura vuelve a borrador; sin política en el tipo rige un nivel con <c>Inventory.Purchases.Approve</c>;</item>
/// <item>las consultas del cruce y la vista <c>purchase-matches</c> (T797, T798).</item>
/// </list>
/// Tolerancia de precio 1 % con <c>AmbasCondiciones</c>; orden de 100 unidades de P1 a $1.000.
/// </summary>
public class CruceATresViasTests
{
    private static readonly DateOnly Hoy = CatalogoDePrueba.Hoy;

    private sealed class Escenario
    {
        public required ComprasDePrueba C { get; init; }
        public List<SolicitudDeAprobacion> Pedidas { get; } = [];
        public Guid LineaDeOrden { get; set; }
        public InventoryDocumentDto Recepcion1 { get; set; } = null!;
        public InventoryDocumentDto Recepcion2 { get; set; } = null!;
    }

    private static async Task<Escenario> CrearAsync(decimal recibido1 = 60m, decimal recibido2 = 38m)
    {
        var c = await ComprasDePrueba.CrearAsync();
        c.Entrega = EntregaDelComercio.I5;
        c.Parametro(ParametrosDeInventario.Modulo, ParametrosDeInventario.ComprasToleranciaPrecioPorcentaje, "0.01");
        c.Parametro(ParametrosDeInventario.Modulo, ParametrosDeInventario.ComprasReglaDeTolerancia, "AmbasCondiciones");
        var e = new Escenario { C = c };

        // El motor de prueba: cada solicitud queda pendiente en la base, con un nivel de Inventory.Purchases.Approve.
        c.K.Motor.SolicitarAsync(default!, default).ReturnsForAnyArgs(ci =>
        {
            var pedida = ci.Arg<SolicitudDeAprobacion>();
            e.Pedidas.Add(pedida);
            var solicitud = new ApprovalRequest
            {
                Subject = pedida.Subject, SourceType = pedida.SourceType, SourcePublicId = pedida.SourcePublicId, SourceLabel = pedida.SourceLabel,
                Amount = pedida.Amount, OperationDate = pedida.OperationDate, CreatedByUserId = pedida.CreatedByUserId, RequestedByUserId = 7,
                Status = ApprovalRequestStatus.Pending, CurrentLevel = 1, ContentSha256 = pedida.ContentSha256, RequestedAt = DateTime.UtcNow,
            };
            solicitud.SellarNiveles([new NivelDeAprobacion(1, 0m, "Inventory.Purchases.Approve")]);
            c.C.Db.ApprovalRequests.Add(solicitud);
            return Result.Success<ApprovalRequest?>(solicitud);
        });

        var orden = await c.ConfirmadoAsync(new SaveInventoryDraftRequest(c.Tipo("ORC"), null, c.K.Principal.PublicId, null, null, null, null, null,
            null, null, null, null, null, [c.Linea(c.P1, 100m, 1_000m)], SupplierPersonPublicId: c.ProveedorA.PublicId, ExpectedDate: Hoy.AddDays(10)));
        e.LineaDeOrden = (await c.LineasAsync(orden.PublicId)).Single().PublicId;
        e.Recepcion1 = await c.ConfirmadoAsync(c.Recepcion(lineas: [new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, recibido1, OrderLinePublicId: e.LineaDeOrden)]));
        e.Recepcion2 = await c.ConfirmadoAsync(c.Recepcion(remision: "REM-78", lineas: [new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, recibido2, OrderLinePublicId: e.LineaDeOrden)]));
        return e;
    }

    /// <summary>La factura de 60 contra la primera recepción y <paramref name="segunda"/> contra la segunda, al precio dado.</summary>
    private static async Task<SaveInventoryDraftRequest> FacturaAsync(Escenario e, decimal precio, decimal segunda = 38m, string numero = "4521")
    {
        var l1 = (await e.C.LineasAsync(e.Recepcion1.PublicId)).Single().PublicId;
        var l2 = (await e.C.LineasAsync(e.Recepcion2.PublicId)).Single().PublicId;
        return e.C.Factura(ComprasDePrueba.Documento(numero), null,
            new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, 60m, UnitPrice: precio, ReceiptLinePublicId: l1),
            new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, segunda, UnitPrice: precio, ReceiptLinePublicId: l2));
    }

    private static async Task<(Guid Factura, Result<ConfirmationResultDto> Confirmacion)> FacturarAsync(Escenario e, decimal precio, decimal segunda = 38m)
    {
        var guardada = await e.C.GuardarAsync(await FacturaAsync(e, precio, segunda));
        guardada.IsSuccess.Should().BeTrue(guardada.IsFailure ? $"{guardada.Error.Code}: {guardada.Error.Message}" : null);
        return (guardada.Value.PublicId, await e.C.ConfirmarAsync(guardada.Value.PublicId));
    }

    private static List<PurchaseMatchLine> Vivas(Escenario e, Guid factura)
    {
        var id = e.C.Documento(factura).Id;
        return e.C.C.Db.PurchaseMatchLines.Where(m => m.InvoiceDocumentId == id && !m.IsDeleted).OrderBy(m => m.InvoiceLineId).ToList();
    }

    private static DecisionDeCruce Decision(Escenario e)
    {
        var servicios = new ServiceCollection().AddSingleton(e.C.Confirmacion()).BuildServiceProvider();
        return new DecisionDeCruce(e.C.C.Db, e.C.C.Reloj, servicios);
    }

    private static ApprovalRequest SolicitudDe(Escenario e, PurchaseMatchLine fila) =>
        e.C.C.Db.ApprovalRequests.Single(r => r.PublicId == fila.ApprovalRequestPublicId);

    // ------------------------------------------------------------------------------------------------ retenida --

    [Fact]
    public async Task Las_lineas_que_exceden_quedan_retenidas_con_una_solicitud_por_linea_y_la_factura_en_aprobacion()
    {
        var e = await CrearAsync();
        var (factura, r) = await FacturarAsync(e, 1_020m, segunda: 40m);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Status.Should().Be(DocumentStatus.PendingApproval);
        r.Value.Number.Should().BeNull("en aprobación no consume número");
        r.Value.Approval.Should().NotBeNull();

        var filas = Vivas(e, factura);
        filas.Should().HaveCount(2);
        filas.Should().OnlyContain(f => f.Status == PurchaseMatchStatus.Held && f.ExceedsTolerance && f.ApprovalRequestPublicId != null);
        filas[0].Reasons.Should().Be("Price");
        filas[0].OrderedQuantity.Should().Be(100m);
        filas[0].ReceivedNotInvoicedQuantity.Should().Be(60m);
        filas[0].OrderedUnitPrice.Should().Be(1_000m);
        filas[0].InvoicedUnitPrice.Should().Be(1_020m);
        filas[0].PriceDifferenceAmount.Should().Be(1_200m);
        filas[0].PriceDifferenceRate.Should().Be(0.02m);
        filas[0].ToleranceJson.Should().Contain("\"Compras.ToleranciaPrecioPorcentaje\":0.01");
        filas[1].Reasons.Should().Be("Quantity,Price", "se facturan 40 sobre 38 recibidas: con orden no es un 422 sino una retención (T796)");
        filas[1].QuantityDifference.Should().Be(2m);

        e.Pedidas.Should().HaveCount(2).And.OnlyContain(p => p.Subject == ApprovalSubjects.PurchaseMatchException
            && p.SourceType == ApprovalSourceTypes.PurchaseMatchLine && p.DocumentTypePublicId == e.C.Tipo("FCP"));
        e.Pedidas.Select(p => p.SourcePublicId).Should().BeEquivalentTo(filas.Select(f => f.PublicId));
        e.Pedidas[0].Amount.Should().Be(1_200m);
        e.Pedidas[0].ScopeWarehousePublicId.Should().Be(e.C.K.Principal.PublicId, "el alcance es la bodega de la recepción");
        (await e.C.C.Db.KardexEntries.AnyAsync(k => k.DocumentId == e.C.Documento(factura).Id)).Should().BeFalse("retenida no toca el kardex");
    }

    [Fact]
    public async Task Dentro_de_la_tolerancia_no_se_retiene_y_la_fila_queda_sin_estado()
    {
        var e = await CrearAsync();
        var (factura, r) = await FacturarAsync(e, 1_005m);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Status.Should().Be(DocumentStatus.Confirmed);
        var filas = Vivas(e, factura);
        filas.Should().HaveCount(2).And.OnlyContain(f => f.Status == null && !f.ExceedsTolerance && f.Reasons == null);
        e.Pedidas.Should().BeEmpty();
        // La diferencia dentro de la tolerancia se reconoce como en dos vías (E6): 98 × $5.
        (await e.C.C.Db.KardexEntries.Where(k => k.DocumentId == e.C.Documento(factura).Id && k.Reason == KardexReason.PriceDifference).SumAsync(k => k.TotalCost))
            .Should().Be(490m);
    }

    [Fact]
    public async Task Sin_orden_no_se_escribe_cruce_y_facturar_de_mas_sigue_siendo_422()
    {
        var c = await ComprasDePrueba.CrearAsync();
        c.Entrega = EntregaDelComercio.I5;
        var recepcion = await c.RecepcionDeTresDocenasAsync();
        var guardada = await c.GuardarAsync(await c.FacturaContraAsync(recepcion, ComprasDePrueba.Documento(), cantidad: 4m));
        (await c.ConfirmarAsync(guardada.Value.PublicId)).Error.Code.Should().Be("Inventory.Purchase.InvoiceExceedsReceived");
        c.C.Db.PurchaseMatchLines.Should().BeEmpty();
    }

    // --------------------------------------------------------------------------------------------- cantidad --

    [Fact]
    public async Task Una_linea_retenida_por_cantidad_no_se_aprueba_por_excepcion()
    {
        var e = await CrearAsync();
        var (_, r) = await FacturarAsync(e, 1_000m, segunda: 40m);
        r.Value.Status.Should().Be(DocumentStatus.PendingApproval);

        var filas = Vivas(e, r.Value.PublicId);
        filas.Should().ContainSingle(f => f.Status == PurchaseMatchStatus.Held).Which.Reasons.Should().Be("Quantity");
        var retenida = filas.Single(f => f.Status == PurchaseMatchStatus.Held);

        var intento = await Decision(e).AlAprobarElNivelAsync(SolicitudDe(e, retenida), 99, ApprovalMethod.OwnSession, esElUltimo: true, default);
        intento.Error.Code.Should().Be(CruceDeCompra.CodigoCantidadNoAprobable);
        CatalogoDePrueba.Datos(intento.Error).Should().BeEquivalentTo(new { lineNumber = 2, receivedNotInvoiced = 38m, invoiced = 40m });
        retenida.Status.Should().Be(PurchaseMatchStatus.Held);
    }

    // --------------------------------------------------------------------------------- rechazo y segundo intento --

    [Fact]
    public async Task Rechazada_una_linea_la_factura_vuelve_a_borrador_y_el_segundo_intento_da_de_baja_las_filas_anteriores()
    {
        var e = await CrearAsync();
        var guardada = await e.C.GuardarAsync(await FacturaAsync(e, 1_020m, segunda: 40m));
        var factura = guardada.Value.PublicId;
        (await e.C.ConfirmarAsync(factura)).Value.Status.Should().Be(DocumentStatus.PendingApproval);
        var primeras = Vivas(e, factura);

        var devuelta = await Decision(e).AlDevolverAsync(SolicitudDe(e, primeras[1]), "Se factura más de lo recibido", default);
        devuelta.IsSuccess.Should().BeTrue(devuelta.IsFailure ? $"{devuelta.Error.Code}: {devuelta.Error.Message}" : null);
        await e.C.C.Db.SaveChangesAsync();

        e.C.Documento(factura).Status.Should().Be(DocumentStatus.Draft);
        primeras[1].Status.Should().Be(PurchaseMatchStatus.Rejected);
        SolicitudDe(e, primeras[0]).Status.Should().Be(ApprovalRequestStatus.Cancelled, "la factura volvió a borrador: la otra solicitud ya no tiene qué aprobar");

        // Corregida la cantidad a 38, el segundo intento recalcula: las filas del primero quedan de baja lógica.
        var corregida = await e.C.GuardarAsync(await FacturaAsync(e, 1_020m, segunda: 38m), factura);
        corregida.IsSuccess.Should().BeTrue(corregida.IsFailure ? $"{corregida.Error.Code}: {corregida.Error.Message}" : null);
        (await e.C.ConfirmarAsync(factura)).Value.Status.Should().Be(DocumentStatus.PendingApproval);

        primeras.Should().OnlyContain(f => f.IsDeleted && f.DeletedAt != null);
        var segundas = Vivas(e, factura);
        segundas.Should().HaveCount(2).And.OnlyContain(f => f.Status == PurchaseMatchStatus.Held && f.Reasons == "Price");
        segundas.Select(f => f.PublicId).Should().NotIntersectWith(primeras.Select(f => f.PublicId));
    }

    // ---------------------------------------------------------------------------------------------- aprobada --

    [Fact]
    public async Task Aprobada_la_ultima_retenida_la_factura_se_confirma_con_la_diferencia_partida_y_un_ajuste_por_recepcion()
    {
        var e = await CrearAsync();
        var c = e.C;
        // Salen 50 antes de facturar: quedan 48 y parte de la diferencia va al costo de lo vendido.
        var salida = await c.K.AjusteAsync(c.K.Borrador("AJN", causa: c.K.Causa(), lineas: [c.K.Linea(c.P1, 50m)]));
        salida.Confirmacion.IsSuccess.Should().BeTrue(salida.Confirmacion.IsFailure ? salida.Confirmacion.Error.Message : null);

        var (factura, r) = await FacturarAsync(e, 1_020m);
        r.Value.Status.Should().Be(DocumentStatus.PendingApproval);
        var filas = Vivas(e, factura);
        var decision = Decision(e);

        // La primera: queda aprobada y la factura sigue esperando la otra.
        (await decision.AlAprobarElNivelAsync(SolicitudDe(e, filas[0]), 99, ApprovalMethod.OwnSession, true, default)).IsSuccess.Should().BeTrue();
        var primera = await decision.AlAprobarAsync(SolicitudDe(e, filas[0]), default);
        primera.IsSuccess.Should().BeTrue(primera.IsFailure ? $"{primera.Error.Code}: {primera.Error.Message}" : null);
        primera.Value.Status.Should().Be(nameof(DocumentStatus.PendingApproval));
        await c.C.Db.SaveChangesAsync();
        filas[0].Status.Should().Be(PurchaseMatchStatus.Approved);
        c.Documento(factura).Status.Should().Be(DocumentStatus.PendingApproval);

        // La última: reentra por el flujo canónico y confirma en la transacción del aprobador.
        var ultima = await decision.AlAprobarAsync(SolicitudDe(e, filas[1]), default);
        ultima.IsSuccess.Should().BeTrue(ultima.IsFailure ? $"{ultima.Error.Code}: {ultima.Error.Message}" : null);
        await c.C.Db.SaveChangesAsync();
        ultima.Value.Status.Should().Be(nameof(DocumentStatus.Confirmed));
        ultima.Value.DisplayNumber.Should().NotBeNullOrEmpty();
        Vivas(e, factura).Should().OnlyContain(f => f.Status == PurchaseMatchStatus.Approved);

        var doc = c.Documento(factura);
        var ajustes = await c.C.Db.KardexEntries.Where(k => k.DocumentId == doc.Id && k.Kind == KardexEntryKind.CostAdjustment).ToListAsync();
        ajustes.Should().NotBeEmpty().And.OnlyContain(k => k.Reason == KardexReason.PriceDifference);

        var mensajes = await c.C.Db.IntegrationMessages.Where(m => m.OriginPublicId == factura && m.Type == "AjusteDeCostoReconocido").ToListAsync();
        mensajes.Select(m => m.OriginEventKey).Should().BeEquivalentTo(
            [$"Confirmation:{e.Recepcion1.PublicId:N}", $"Confirmation:{e.Recepcion2.PublicId:N}"], "uno por recepción afectada");
        var lineas = mensajes.SelectMany(m => JsonNode.Parse(m.PayloadJson)!["lines"]!.AsArray()).ToList();
        lineas.Sum(l => l!["inventoryAmount"]!.GetValue<decimal>() + l["soldAmount"]!.GetValue<decimal>()).Should().Be(1_960m);
        ajustes.Sum(k => k.TotalCost).Should().Be(lineas.Sum(l => l!["inventoryAmount"]!.GetValue<decimal>()), "el kardex ajusta lo que sigue en existencia");
        lineas.Sum(l => l!["soldAmount"]!.GetValue<decimal>()).Should().Be(240m, "50 de las 98 ya salieron: de las 60 de la primera recepción quedan 48, 12 × $20 van al costo de lo vendido");
    }

    // ----------------------------------------------------------------------------------------------- política --

    [Fact]
    public void Sin_politica_la_excepcion_del_cruce_pide_un_nivel_con_Inventory_Purchases_Approve()
    {
        var evaluacion = EvaluadorDePolitica.Evaluar(ApprovalSubjects.PurchaseMatchException, 1_200m, null, null);
        evaluacion.RequiereAprobacion.Should().BeTrue();
        evaluacion.ReglaFija.Should().BeTrue();
        evaluacion.Niveles.Should().Equal(new NivelDeAprobacion(1, 0m, "Inventory.Purchases.Approve"));
    }

    // ---------------------------------------------------------------------------------------- consultas (T797, T798) --

    [Fact]
    public async Task El_cruce_se_consulta_por_factura_en_la_lista_y_en_la_vista_con_precios_solo_con_Costs_Read()
    {
        var e = await CrearAsync();
        var (factura, _) = await FacturarAsync(e, 1_020m, segunda: 40m);
        var c = e.C;

        var porFactura = await new GetSupplierInvoiceMatchQueryHandler(c.C.Db, c.K.Vista()).Handle(new GetSupplierInvoiceMatchQuery(factura), default);
        porFactura.IsSuccess.Should().BeTrue();
        porFactura.Value.Should().HaveCount(2);
        var segunda = porFactura.Value[1];
        segunda.LineNumber.Should().Be(2);
        segunda.Product.Code.Should().Be("P1");
        segunda.Ordered.Should().Be(100m);
        segunda.Received.Should().Be(38m);
        segunda.Invoiced.Should().Be(40m);
        segunda.QuantityVariance.Should().Be(2m);
        segunda.InvoicePrice.Should().Be(1_020m);
        segunda.Reasons.Should().Equal("Quantity", "Price");
        segunda.Status.Should().Be(PurchaseMatchStatus.Held);
        segunda.ApprovalRequestPublicId.Should().NotBeNull();
        segunda.SupplierInvoice.SupplierNumber.Should().Be("FV4521");

        var lista = await new ListPurchaseMatchesQueryHandler(c.C.Db, c.K.Alcance, c.K.Vista())
            .Handle(new ListPurchaseMatchesQuery(PurchaseMatchStatus.Held, c.ProveedorA.PublicId, new PageRequest(1, 50)), default);
        lista.Value.Items.Should().HaveCount(2);
        lista.Value.TotalCount.Should().Be(2);
        (await new ListPurchaseMatchesQueryHandler(c.C.Db, c.K.Alcance, c.K.Vista())
            .Handle(new ListPurchaseMatchesQuery(PurchaseMatchStatus.Approved, null, new PageRequest(1, 50)), default)).Value.Items.Should().BeEmpty();

        var detalle = await new GetPurchaseDocumentQueryHandler(c.C.Db, c.K.Vista(), c.Vinculos(), c.Calculo(), c.Pendientes())
            .Handle(new GetPurchaseDocumentQuery(factura), default);
        detalle.Value.Match.Should().HaveCount(2, "el detalle de la factura trae su cruce (match?)");

        var vista = await new PurchaseMatchesReportQueryHandler(c.C.Db, c.K.Alcance, c.K.Permisos, c.C.Reloj)
            .Handle(new PurchaseMatchesReportQuery(new FiltrosDeInformeDeInventario(), null, PurchaseMatchStatus.Held), default);
        vista.IsSuccess.Should().BeTrue(vista.IsFailure ? vista.Error.Message : null);
        vista.Value.Filas.Should().HaveCount(2);
        vista.Value.Columnas.Select(x => x.Nombre).Should().Equal("Orden", "Recepción", "Factura", "Producto", "Pedido", "Recibido", "Facturado",
            "Precio pedido", "Precio facturado", "Diferencia (cantidad)", "Diferencia (precio)", "Dentro de tolerancia", "Estado", "Documento");
        vista.Value.Filas[1].Valores[11].Should().Be("No");
        vista.Value.Filas[1].Valores[12].Should().Be("Retenida");
        vista.Value.Filas[1].Valores[13].Should().Be(factura.ToString());

        // Sin Inventory.Costs.Read, los precios no salen.
        c.K.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);
        var sinCostos = await new GetSupplierInvoiceMatchQueryHandler(c.C.Db, c.K.Vista()).Handle(new GetSupplierInvoiceMatchQuery(factura), default);
        sinCostos.Value.Should().OnlyContain(f => f.OrderPrice == null && f.ReceiptPrice == null && f.InvoicePrice == null && f.PriceVariance == null);
        var vistaSinCostos = await new PurchaseMatchesReportQueryHandler(c.C.Db, c.K.Alcance, c.K.Permisos, c.C.Reloj)
            .Handle(new PurchaseMatchesReportQuery(new FiltrosDeInformeDeInventario(), null, null), default);
        vistaSinCostos.Value.Filas.Should().OnlyContain(f => f.Valores[7] == null && f.Valores[8] == null && f.Valores[10] == null);
    }
}
