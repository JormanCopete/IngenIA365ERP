using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, I5, T769 (FR-036, FR-048, FR-049; contracts/api.md §14.9; data-model §5.5, §9.8): la solicitud y la orden de compra y la
/// recepción contra orden sobre el ciclo común real.
/// <list type="bullet">
/// <item>la solicitud se aprueba por la política de su tipo y no escribe kardex ni emite mensajes;</item>
/// <item>la orden se mide con su <c>Total</c> contra el límite de <c>Inventory.Purchases.Confirm</c>: por encima queda
/// <c>PendingApproval</c> o, sin nivel que forzar, <c>Inventory.Approval.AmountExceedsLimit</c>;</item>
/// <item><c>pendingToOrder</c> y <c>pendingToReceive</c> salen de los vínculos <c>FromOrder</c> vigentes, nunca guardados;</item>
/// <item>recepción contra la orden de otro proveedor, contra una orden sin confirmar o con el saldo cerrado, y lo recibido de más fuera
/// de la tolerancia (<c>{ lineNumber, ordered, received, tolerance }</c>);</item>
/// <item>el cierre del saldo, «por recibir» en la posición de reposición, el PDF y el envío al proveedor.</item>
/// </list>
/// Las clases de I5 operan con la entrega I5 (<see cref="ComprasDePrueba.Entrega"/>); el despliegue sigue en I4 hasta el cierre de I5.
/// </summary>
public class SolicitudesYOrdenesTests
{
    private static readonly DateOnly Hoy = CatalogoDePrueba.Hoy;

    private static async Task<ComprasDePrueba> CrearAsync()
    {
        var c = await ComprasDePrueba.CrearAsync();
        c.Entrega = EntregaDelComercio.I5;
        return c;
    }

    // --------------------------------------------------------------------------------------------- borradores --

    private static SaveInventoryDraftRequest Solicitud(ComprasDePrueba c, DateOnly? necesitaPara, params SaveInventoryDraftLine[] lineas) =>
        new(c.Tipo("SOC"), null, c.K.Principal.PublicId, null, null, null, null, null, null, null, null, null, null, lineas, NeededBy: necesitaPara);

    private static SaveInventoryDraftRequest Orden(ComprasDePrueba c, params SaveInventoryDraftLine[] lineas) =>
        new(c.Tipo("ORC"), null, c.K.Principal.PublicId, null, null, null, null, null, null, null, null, null, null, lineas,
            SupplierPersonPublicId: c.ProveedorA.PublicId, ExpectedDate: Hoy.AddDays(10), PaymentTerms: "Crédito a 30 días");

    private static SaveInventoryDraftLine Pedida(ComprasDePrueba c, decimal cantidad) => new(null, c.P1, c.Base(c.P1), cantidad);

    private static SaveInventoryDraftLine ContraSolicitud(Guid lineaDeSolicitud, decimal cantidad, decimal precio) =>
        new(null, Guid.Empty, Guid.Empty, cantidad, UnitPrice: precio, RequestLinePublicId: lineaDeSolicitud);

    private static SaveInventoryDraftLine ContraOrden(Guid lineaDeOrden, decimal cantidad) =>
        new(null, Guid.Empty, Guid.Empty, cantidad, OrderLinePublicId: lineaDeOrden);

    private static async Task<(InventoryDocumentDto Orden, Guid Linea)> OrdenConfirmadaAsync(ComprasDePrueba c, decimal cantidad = 100m, decimal precio = 1_000m)
    {
        var orden = await c.ConfirmadoAsync(Orden(c, c.Linea(c.P1, cantidad, precio)));
        return (orden, (await c.LineasAsync(orden.PublicId)).Single().PublicId);
    }

    private static Task<Result<InventoryDocumentDto>> RecibirAsync(ComprasDePrueba c, Guid lineaDeOrden, decimal cantidad, Domain.Entities.Core.Person? proveedor = null) =>
        c.GuardarAsync(c.Recepcion(proveedor, lineas: [ContraOrden(lineaDeOrden, cantidad)]));

    private static GetPurchaseDocumentQueryHandler Detalle(ComprasDePrueba c) => new(c.C.Db, c.K.Vista(), c.Vinculos(), c.Calculo(), c.Pendientes());

    private static ClosePurchaseOrderBalanceCommandHandler Cierre(ComprasDePrueba c) => new(c.C.Db, c.K.Actor, c.C.Reloj, c.K.Vista(), c.K.Cerrojo);

    private static InventoryAuditEmitter Auditoria(ComprasDePrueba c)
    {
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns(CooperativaDePrueba.PublicIdN);
        var servicios = new ServiceCollection()
            .AddSingleton(Substitute.For<IAuditService>())
            .AddSingleton<IApplicationDbContext>(c.C.Db)
            .AddSingleton(tenant)
            .AddSingleton(NominaTestData.UsuarioDePrueba("compras@coop", 7))
            .BuildServiceProvider();
        return new InventoryAuditEmitter(servicios, NullLogger<InventoryAuditEmitter>.Instance);
    }

    /// <summary>Un PDF de mentira que recuerda el modelo que recibió.</summary>
    private sealed class PdfDePrueba : IOrdenDeCompraEnPdf
    {
        public OrdenDeCompraImprimible? Ultima { get; private set; }

        public byte[] Generar(OrdenDeCompraImprimible orden)
        {
            Ultima = orden;
            return [0x25, 0x50, 0x44, 0x46];
        }
    }

    private static void ConNiveles(ComprasDePrueba c, Guid tipo, string permiso, bool forzado = false)
    {
        var niveles = new List<NivelDeAprobacion> { new(1, 0m, permiso) };
        var sinNiveles = Result.Success(new EvaluacionConPolitica(new EvaluacionDeAprobacion(ResultadoDeEvaluacion.SinAprobacion, [], null, false, false), null));
        var conNiveles = Result.Success(new EvaluacionConPolitica(new EvaluacionDeAprobacion(ResultadoDeEvaluacion.ConNiveles, niveles, forzado ? 50_000m : null, forzado, false), 1));
        c.K.Motor.EvaluarAsync(default!, default, default, default, default, default)
            .ReturnsForAnyArgs(ci => ci.ArgAt<Guid>(1) == tipo ? conNiveles : sinNiveles);
        c.K.Motor.SolicitarAsync(default!, default).ReturnsForAnyArgs(ci =>
        {
            var solicitud = new ApprovalRequest { SourcePublicId = ci.Arg<SolicitudDeAprobacion>().SourcePublicId, CurrentLevel = 1 };
            solicitud.SellarNiveles(niveles);
            return Result.Success<ApprovalRequest?>(solicitud);
        });
    }

    // ------------------------------------------------------------------------------------------------ solicitud --

    [Fact]
    public async Task La_solicitud_se_aprueba_por_la_politica_de_su_tipo_sin_kardex_ni_mensajes()
    {
        var c = await CrearAsync();
        var tipo = c.Tipo("SOC");
        ConNiveles(c, tipo, "Inventory.Purchases.Approve");
        var guardada = await c.GuardarAsync(Solicitud(c, Hoy.AddDays(7), Pedida(c, 100m)) with { RequestedByPersonPublicId = c.ProveedorB.PublicId });
        guardada.IsSuccess.Should().BeTrue(guardada.IsFailure ? $"{guardada.Error.Code}: {guardada.Error.Message}" : null);

        var r = await c.ConfirmarAsync(guardada.Value.PublicId);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Status.Should().Be(DocumentStatus.PendingApproval);
        r.Value.Number.Should().BeNull();
        await c.K.Motor.Received().EvaluarAsync(ApprovalSubjects.DocumentConfirmation, tipo, Arg.Any<DateOnly>(), 0m,
            "Inventory.Purchases.Confirm", Arg.Any<CancellationToken>());

        // La última aprobación reentra por el flujo canónico y la confirma.
        var servicios = new ServiceCollection().AddSingleton(c.Confirmacion()).BuildServiceProvider();
        var fuente = new FuenteDeAprobacionDeDocumento(c.C.Db, c.K.Maestros(), servicios);
        var solicitud = new ApprovalRequest { SourcePublicId = guardada.Value.PublicId, CurrentLevel = 1 };
        solicitud.SellarNiveles([new NivelDeAprobacion(1, 0m, "Inventory.Purchases.Approve")]);
        var aprobada = await fuente.AlAprobarAsync(solicitud, default);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? $"{aprobada.Error.Code}: {aprobada.Error.Message}" : null);
        await c.C.Db.SaveChangesAsync();

        var documento = c.Documento(guardada.Value.PublicId);
        documento.Status.Should().Be(DocumentStatus.Confirmed);
        documento.ExpectedDate.Should().Be(Hoy.AddDays(7));
        documento.CounterpartyPersonId.Should().Be(c.ProveedorB.Id, "quien pide la solicitud es su contraparte");
        (await c.C.Db.KardexEntries.AnyAsync(k => k.DocumentId == documento.Id)).Should().BeFalse("la solicitud no mueve inventario (FR-036)");
        c.MensajesDe(documento.PublicId).Should().BeEmpty("la solicitud no emite mensajes (FR-036)");
    }

    [Fact]
    public async Task La_solicitud_no_lleva_precios_ni_proveedor_y_sin_fecha_no_se_confirma()
    {
        var c = await CrearAsync();
        (await c.GuardarAsync(Solicitud(c, Hoy, new SaveInventoryDraftLine(null, c.P1, c.Base(c.P1), 5m, UnitPrice: 10m))))
            .Error.Code.Should().Be("Validation.Invalid");
        (await c.GuardarAsync(Solicitud(c, Hoy, Pedida(c, 5m)) with { SupplierPersonPublicId = c.ProveedorA.PublicId }))
            .Error.Code.Should().Be("Validation.Invalid");
        (await c.GuardarAsync(Orden(c, c.Linea(c.P1, 5m, 10m)) with { NeededBy = Hoy }))
            .Error.Code.Should().Be("Validation.Invalid", "neededBy es de la solicitud");

        var sinFecha = await c.GuardarAsync(Solicitud(c, null, Pedida(c, 5m)));
        sinFecha.IsSuccess.Should().BeTrue();
        sinFecha.Value.Warnings.Should().Contain(w => w.Code == "Inventory.Document.FieldRequired");
        (await c.ConfirmarAsync(sinFecha.Value.PublicId)).Error.Should().BeOfType<ErrorConDatos>()
            .Which.Data.Should().BeEquivalentTo(new { field = "neededBy" });
    }

    // ---------------------------------------------------------------------------------------------------- orden --

    [Fact]
    public async Task La_orden_se_mide_con_su_Total_y_por_encima_del_limite_de_Confirm_queda_en_aprobacion()
    {
        var c = await CrearAsync();
        var tipo = c.Tipo("ORC");
        ConNiveles(c, tipo, "Inventory.Purchases.Approve", forzado: true);

        var guardada = await c.GuardarAsync(Orden(c, c.Linea(c.P3, 100m, 1_000m)));
        guardada.IsSuccess.Should().BeTrue(guardada.IsFailure ? $"{guardada.Error.Code}: {guardada.Error.Message}" : null);
        var r = await c.ConfirmarAsync(guardada.Value.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Status.Should().Be(DocumentStatus.PendingApproval);
        r.Value.Number.Should().BeNull("en aprobación no consume número");
        r.Value.Approval!.Reason.Should().Be("AmountLimit");
        // 100 × $1.000 más el IVA del 19 %: el Total, no el costo (§1.3).
        await c.K.Motor.Received().EvaluarAsync(ApprovalSubjects.DocumentConfirmation, tipo, Arg.Any<DateOnly>(), 119_000m,
            "Inventory.Purchases.Confirm", Arg.Any<CancellationToken>());
        (await c.C.Db.KardexEntries.AnyAsync(k => k.DocumentId == c.Documento(guardada.Value.PublicId).Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Sin_nivel_que_forzar_la_orden_por_encima_del_limite_responde_AmountExceedsLimit()
    {
        var c = await CrearAsync();
        c.K.Motor.EvaluarAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(
            Result.Failure<EvaluacionConPolitica>(InventoryErrors.AmountExceedsLimit(119_000m, 50_000m, "COP", "Inventory.Purchases.Confirm")));
        var guardada = await c.GuardarAsync(Orden(c, c.Linea(c.P3, 100m, 1_000m)));

        var r = await c.ConfirmarAsync(guardada.Value.PublicId);

        r.Error.Code.Should().Be("Inventory.Approval.AmountExceedsLimit");
        c.Documento(guardada.Value.PublicId).Status.Should().Be(DocumentStatus.Draft);
    }

    [Fact]
    public async Task La_orden_lleva_proveedor_fecha_de_entrega_y_precio_y_sus_condiciones_son_sus_notas()
    {
        var c = await CrearAsync();
        var sinPrecio = await c.GuardarAsync(Orden(c, Pedida(c, 5m)));
        sinPrecio.Value.Warnings.Should().Contain(w => w.Code == "Validation.Invalid");
        (await c.ConfirmarAsync(sinPrecio.Value.PublicId)).Error.Code.Should().Be("Validation.Invalid");

        var sinProveedor = await c.GuardarAsync(Orden(c, c.Linea(c.P1, 5m, 10m)) with { SupplierPersonPublicId = null });
        (await c.ConfirmarAsync(sinProveedor.Value.PublicId)).Error.Should().BeOfType<ErrorConDatos>()
            .Which.Data.Should().BeEquivalentTo(new { field = "counterparty" });

        (await c.GuardarAsync(Orden(c, c.Linea(c.P1, 5m, 10m)) with { Notes = "otra cosa" })).Error.Code.Should().Be("Validation.Invalid");

        var (orden, _) = await OrdenConfirmadaAsync(c);
        var detalle = await Detalle(c).Handle(new GetPurchaseDocumentQuery(orden.PublicId, DocumentClass.PurchaseOrder), default);
        detalle.Value.Plan!.ExpectedDate.Should().Be(Hoy.AddDays(10));
        detalle.Value.Plan.PaymentTerms.Should().Be("Crédito a 30 días");
        detalle.Value.Document.Notes.Should().Be("Crédito a 30 días");
    }

    // -------------------------------------------------------------------------------------------- pendientes --

    [Fact]
    public async Task Pendiente_por_ordenar_y_por_recibir_salen_de_los_vinculos_vigentes_y_no_se_guardan()
    {
        var c = await CrearAsync();
        var solicitud = await c.ConfirmadoAsync(Solicitud(c, Hoy.AddDays(5), Pedida(c, 100m)));
        var lineaDeSolicitud = (await c.LineasAsync(solicitud.PublicId)).Single().PublicId;

        var orden = await c.ConfirmadoAsync(Orden(c, ContraSolicitud(lineaDeSolicitud, 60m, 1_000m)));
        var lineaDeOrden = (await c.LineasAsync(orden.PublicId)).Single();
        lineaDeOrden.ProductId.Should().Be(c.K.ProductoId(c.P1), "la orden toma el producto de la solicitud");
        var vinculo = await c.C.Db.DocumentLinks.SingleAsync(l => l.TargetDocumentId == c.Documento(orden.PublicId).Id);
        vinculo.Kind.Should().Be(DocumentLinkKind.FromOrder);
        vinculo.SourceDocumentId.Should().Be(c.Documento(solicitud.PublicId).Id);

        (await Detalle(c).Handle(new GetPurchaseDocumentQuery(solicitud.PublicId), default)).Value.PendingLines!.Single()
            .PendingToOrder.Should().Be(40m);

        var recibida = await RecibirAsync(c, lineaDeOrden.PublicId, 25m);
        recibida.IsSuccess.Should().BeTrue(recibida.IsFailure ? $"{recibida.Error.Code}: {recibida.Error.Message}" : null);
        (await c.ConfirmarAsync(recibida.Value.PublicId)).IsSuccess.Should().BeTrue();
        // Un borrador no consume: sólo PendingApproval o Confirmed.
        (await RecibirAsync(c, lineaDeOrden.PublicId, 10m)).IsSuccess.Should().BeTrue();

        var pendiente = (await Detalle(c).Handle(new GetPurchaseDocumentQuery(orden.PublicId), default)).Value.PendingLines!.Single();
        pendiente.Quantity.Should().Be(60m);
        pendiente.Consumed.Should().Be(25m);
        pendiente.PendingToReceive.Should().Be(35m);
        pendiente.PendingToOrder.Should().BeNull();

        // Anulada la recepción, su cantidad vuelve a lo pendiente (sus vínculos ya no cuentan).
        (await c.Anular().Handle(new VoidInventoryDocumentCommand(recibida.Value.PublicId, DocumentClassGroup.Purchases, "se recibió en otra bodega"), default))
            .IsSuccess.Should().BeTrue();
        (await c.Pendientes().DeDocumentoAsync(c.Documento(orden.PublicId), default)).Single().Pendiente.Should().Be(60m);
    }

    [Fact]
    public async Task La_orden_desde_una_solicitud_sin_aprobar_no_se_guarda()
    {
        var c = await CrearAsync();
        var solicitud = await c.GuardarAsync(Solicitud(c, Hoy, Pedida(c, 10m)));
        var linea = (await c.LineasAsync(solicitud.Value.PublicId)).Single().PublicId;

        (await c.GuardarAsync(Orden(c, ContraSolicitud(linea, 10m, 1_000m)))).Error.Code.Should().Be("Inventory.PurchaseRequest.NotConfirmed");
    }

    // ------------------------------------------------------------------------------------ recepción contra orden --

    [Fact]
    public async Task Recepcion_contra_la_orden_de_otro_proveedor_se_rechaza()
    {
        var c = await CrearAsync();
        var (_, linea) = await OrdenConfirmadaAsync(c);

        var r = await RecibirAsync(c, linea, 10m, c.ProveedorB);

        r.Error.Code.Should().Be("Inventory.Purchase.OrderFromOtherSupplier");
        CatalogoDePrueba.Datos(r.Error).Should().BeEquivalentTo(new { lineNumber = 1 }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Una_orden_sin_confirmar_no_admite_recepciones()
    {
        var c = await CrearAsync();
        var borrador = await c.GuardarAsync(Orden(c, c.Linea(c.P1, 10m, 1_000m)));
        var linea = (await c.LineasAsync(borrador.Value.PublicId)).Single().PublicId;

        (await RecibirAsync(c, linea, 10m)).Error.Code.Should().Be("Inventory.PurchaseOrder.NotOpen");
    }

    [Fact]
    public async Task La_recepcion_contra_orden_toma_producto_unidad_y_precio_de_la_orden_y_bloquea_la_orden()
    {
        var c = await CrearAsync();
        var (orden, linea) = await OrdenConfirmadaAsync(c, 100m, 1_000m);

        var recibida = await RecibirAsync(c, linea, 60m);
        recibida.IsSuccess.Should().BeTrue(recibida.IsFailure ? $"{recibida.Error.Code}: {recibida.Error.Message}" : null);
        var l = (await c.LineasAsync(recibida.Value.PublicId)).Single();
        l.ProductId.Should().Be(c.K.ProductoId(c.P1));
        l.UnitPrice.Should().Be(1_000m);
        c.K.Bloqueos.Clear();

        (await c.ConfirmarAsync(recibida.Value.PublicId)).IsSuccess.Should().BeTrue();
        c.K.Bloqueos.Should().Contain(b => b.DocumentosDeOrigen.Contains(c.Documento(orden.PublicId).Id),
            "la orden entra al cerrojo como documento de origen (data-model §5.5)");
        (await c.C.Db.KardexEntries.SingleAsync(k => k.DocumentId == c.Documento(recibida.Value.PublicId).Id)).QuantityBase.Should().Be(60m);
    }

    [Fact]
    public async Task Lo_recibido_de_mas_solo_entra_dentro_de_la_tolerancia_de_cantidad()
    {
        var c = await CrearAsync();
        c.Parametro(ParametrosDeInventario.Modulo, ParametrosDeInventario.ComprasToleranciaCantidadPorcentaje, "0.02");
        var (_, linea) = await OrdenConfirmadaAsync(c, 100m);
        var primera = await RecibirAsync(c, linea, 60m);
        (await c.ConfirmarAsync(primera.Value.PublicId)).IsSuccess.Should().BeTrue();

        // 60 + 43 = 103 > 100 + 2 %: el borrador avisa y la confirmación lo rechaza con sus datos.
        var demas = await RecibirAsync(c, linea, 43m);
        demas.IsSuccess.Should().BeTrue();
        demas.Value.Warnings.Should().Contain(w => w.Code == "Inventory.Purchase.OverReceiptBeyondTolerance");
        var rechazo = await c.ConfirmarAsync(demas.Value.PublicId);
        rechazo.Error.Code.Should().Be("Inventory.Purchase.OverReceiptBeyondTolerance");
        CatalogoDePrueba.Datos(rechazo.Error).Should().BeEquivalentTo(new { lineNumber = 1, ordered = 100m, received = 103m, tolerance = 2m });
        c.Documento(demas.Value.PublicId).Status.Should().Be(DocumentStatus.Draft);

        // 60 + 42 = 102: dentro de la tolerancia.
        var dentro = await RecibirAsync(c, linea, 42m);
        dentro.Value.Warnings.Should().NotContain(w => w.Code == "Inventory.Purchase.OverReceiptBeyondTolerance");
        (await c.ConfirmarAsync(dentro.Value.PublicId)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_tolerancia_configurada_no_se_recibe_nada_de_mas()
    {
        var c = await CrearAsync();
        var (_, linea) = await OrdenConfirmadaAsync(c, 10m);
        var demas = await RecibirAsync(c, linea, 11m);

        CatalogoDePrueba.Datos((await c.ConfirmarAsync(demas.Value.PublicId)).Error)
            .Should().BeEquivalentTo(new { lineNumber = 1, ordered = 10m, received = 11m, tolerance = 0m });
    }

    // ------------------------------------------------------------------------------------------- cierre del saldo --

    [Fact]
    public async Task Cerrar_el_saldo_deja_la_orden_sin_recepciones_y_fuera_de_por_recibir()
    {
        var c = await CrearAsync();
        var (orden, linea) = await OrdenConfirmadaAsync(c, 100m);
        var recibida = await RecibirAsync(c, linea, 40m);
        (await c.ConfirmarAsync(recibida.Value.PublicId)).IsSuccess.Should().BeTrue();
        var pareja = (c.K.ProductoId(c.P1), c.K.Principal.Id);

        var posicion = await new PosicionDeReposicion(c.C.Db).LeerAsync([pareja], default);
        posicion[pareja].PorRecibir.Should().Be(60m, "lo pendiente de la orden confirmada cuenta como por recibir (FR-035)");

        var cierre = await Cierre(c).Handle(new ClosePurchaseOrderBalanceCommand(orden.PublicId, "El proveedor no tiene más"), default);
        cierre.IsSuccess.Should().BeTrue(cierre.IsFailure ? $"{cierre.Error.Code}: {cierre.Error.Message}" : null);
        var cerrada = c.Documento(orden.PublicId);
        cerrada.BalanceClosedAt.Should().NotBeNull();
        cerrada.BalanceClosedByUserId.Should().Be(KardexDePrueba.Usuario);
        cerrada.BalanceClosedReason.Should().Be("El proveedor no tiene más");

        (await new PosicionDeReposicion(c.C.Db).LeerAsync([pareja], default))[pareja].PorRecibir.Should().Be(0m);
        (await RecibirAsync(c, linea, 10m)).Error.Code.Should().Be("Inventory.PurchaseOrder.NotOpen");
        (await Cierre(c).Handle(new ClosePurchaseOrderBalanceCommand(orden.PublicId, "otra vez"), default)).Error.Code
            .Should().Be("Inventory.PurchaseOrder.NotOpen");
        var detalle = await Detalle(c).Handle(new GetPurchaseDocumentQuery(orden.PublicId), default);
        detalle.Value.Plan!.BalanceClosedReason.Should().Be("El proveedor no tiene más");
        detalle.Value.PendingLines!.Single().PendingToReceive.Should().Be(0m);
    }

    [Fact]
    public async Task Una_orden_sin_confirmar_no_cierra_su_saldo_y_otra_clase_es_404()
    {
        var c = await CrearAsync();
        var borrador = await c.GuardarAsync(Orden(c, c.Linea(c.P1, 10m, 1_000m)));
        (await Cierre(c).Handle(new ClosePurchaseOrderBalanceCommand(borrador.Value.PublicId, "no"), default)).Error.Code
            .Should().Be("Inventory.PurchaseOrder.NotOpen");

        var solicitud = await c.ConfirmadoAsync(Solicitud(c, Hoy, Pedida(c, 1m)));
        (await Cierre(c).Handle(new ClosePurchaseOrderBalanceCommand(solicitud.PublicId, "no"), default)).Error.Code
            .Should().Be("Inventory.Document.NotFound");
    }

    [Fact]
    public async Task Una_orden_en_borrador_ya_cuenta_como_por_recibir_sólo_cuando_se_confirma()
    {
        var c = await CrearAsync();
        await c.GuardarAsync(Orden(c, c.Linea(c.P1, 30m, 1_000m)));
        var pareja = (c.K.ProductoId(c.P1), c.K.Principal.Id);
        (await new PosicionDeReposicion(c.C.Db).LeerAsync([pareja], default))[pareja].PorRecibir.Should().Be(0m);
        await OrdenConfirmadaAsync(c, 30m);
        (await new PosicionDeReposicion(c.C.Db).LeerAsync([pareja], default))[pareja].PorRecibir.Should().Be(30m);
    }

    // ---------------------------------------------------------------------------------------- PDF y envío --

    [Fact]
    public async Task El_PDF_lleva_la_copia_fiscal_del_proveedor_la_entrega_las_condiciones_los_impuestos_y_los_totales()
    {
        var c = await CrearAsync();
        var orden = await c.ConfirmadoAsync(Orden(c, c.Linea(c.P3, 100m, 1_000m)));
        var pdf = new PdfDePrueba();

        var r = await new GetPurchaseOrderPdfQueryHandler(c.K.Vista(), new ModeloDeOrdenDeCompra(c.C.Db, c.Calculo()), [pdf])
            .Handle(new GetPurchaseOrderPdfQuery(orden.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.FileName.Should().StartWith("orden-de-compra-ORC");
        var modelo = pdf.Ultima!;
        modelo.Proveedor.Name.Should().Be("Distribuidora del Valle S.A.S.");
        modelo.Proveedor.TaxId.Should().Be("900123456");
        modelo.Bodega!.Code.Should().Be("PRIN");
        modelo.ExpectedDate.Should().Be(Hoy.AddDays(10));
        modelo.PaymentTerms.Should().Be("Crédito a 30 días");
        modelo.Lineas.Should().ContainSingle().Which.NetAmount.Should().Be(100_000m);
        modelo.Impuestos.Should().ContainSingle().Which.Amount.Should().Be(19_000m);
        modelo.Total.Should().Be(119_000m);

        var solicitud = await c.ConfirmadoAsync(Solicitud(c, Hoy, Pedida(c, 1m)));
        (await new GetPurchaseOrderPdfQueryHandler(c.K.Vista(), new ModeloDeOrdenDeCompra(c.C.Db, c.Calculo()), [pdf])
            .Handle(new GetPurchaseOrderPdfQuery(solicitud.PublicId), default)).Error.Code.Should().Be("Inventory.Document.NotFound");
    }

    [Fact]
    public async Task Enviar_exige_la_orden_confirmada_y_un_correo_y_queda_auditado()
    {
        var c = await CrearAsync();
        var correo = Substitute.For<IEmailSender>();
        var pdf = new PdfDePrueba();
        SendPurchaseOrderCommandHandler Enviar() =>
            new(c.C.Db, c.K.Vista(), new ModeloDeOrdenDeCompra(c.C.Db, c.Calculo()), [pdf], correo, Auditoria(c));

        var borrador = await c.GuardarAsync(Orden(c, c.Linea(c.P1, 10m, 1_000m)));
        (await Enviar().Handle(new SendPurchaseOrderCommand(borrador.Value.PublicId), default)).Error.Code
            .Should().Be("Inventory.PurchaseOrder.NotConfirmed");

        var (orden, _) = await OrdenConfirmadaAsync(c);
        (await Enviar().Handle(new SendPurchaseOrderCommand(orden.PublicId), default)).Error.Code
            .Should().Be("Inventory.PurchaseOrder.SupplierEmailMissing", "el proveedor no tiene correo en el maestro");

        c.ProveedorA.Email = "ventas@distrivalle.co";
        await c.C.Db.SaveChangesAsync();
        var enviado = await Enviar().Handle(new SendPurchaseOrderCommand(orden.PublicId), default);

        enviado.IsSuccess.Should().BeTrue(enviado.IsFailure ? $"{enviado.Error.Code}: {enviado.Error.Message}" : null);
        await correo.Received(1).SendAsync(Arg.Is<EmailMessage>(m => m.To == "ventas@distrivalle.co"
            && m.Attachments!.Single().ContentType == "application/pdf" && m.Attachments!.Single().FileName.StartsWith("orden-de-compra-")),
            Arg.Any<CancellationToken>());
        var fila = c.C.Db.AuditOutbox.Should().ContainSingle().Subject;
        var evento = AuditoriaEncadenada.LeerCarga(fila.PayloadJson!);
        evento.Action.Should().Be(AuditEventTypes.InventoryPurchaseOrderSent);
        evento.NewValuesJson.Should().Contain("ventas@distrivalle.co");

        // Un correo en la petición manda sobre el del maestro.
        (await Enviar().Handle(new SendPurchaseOrderCommand(orden.PublicId, "compras@otro.co"), default)).IsSuccess.Should().BeTrue();
        await correo.Received(1).SendAsync(Arg.Is<EmailMessage>(m => m.To == "compras@otro.co"), Arg.Any<CancellationToken>());
    }
}
