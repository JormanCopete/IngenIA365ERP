using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, T330 (contracts/api.md §14.3; FR-019): la compra directa. Recepción y factura con las mismas líneas, la misma
/// cadena y el mismo modo; sin aprobación las dos confirmadas y los dos mensajes; con política del tipo de la recepción la
/// recepción queda en aprobación y la factura en borrador enlazada, y la última aprobación confirma las dos; el monto que se
/// evalúa es el total de la factura; una factura repetida no deja la recepción guardada.
/// </summary>
public class CompraDirectaCommandTests
{
    private static ConfirmDirectPurchaseCommand Pedido(ComprasDePrueba c, string numero = "4521", params SaveInventoryDraftLine[] lineas) =>
        new(c.Recepcion(lineas: lineas.Length > 0 ? lineas : [c.Linea(c.P1, 3m, 15_600m, c.Docena)]),
            new DirectPurchaseInvoiceRequest(c.Tipo("FCP"), ComprasDePrueba.Documento(numero)));

    [Fact]
    public async Task Sin_aprobacion_confirma_recepcion_y_factura_con_las_mismas_lineas_y_el_mismo_modo()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var r = await c.CompraDirectaHandler().Handle(Pedido(c), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);

        r.Value.Status.Should().Be(DocumentStatus.Confirmed);
        r.Value.Receipt.DisplayNumber.Should().Be("REC1");
        r.Value.SupplierInvoice.Status.Should().Be(DocumentStatus.Confirmed);
        r.Value.SupplierInvoice.DisplayNumber.Should().Be("FCP1");
        r.Value.SupplierInvoice.SupplierNumber.Should().Be("FV4521");

        var recepcion = c.Documento(r.Value.Receipt.PublicId);
        var factura = c.Documento(r.Value.SupplierInvoice.PublicId);
        factura.PostingMode.Should().Be(recepcion.PostingMode);
        factura.Total.Should().Be(recepcion.Total).And.Be(46_800m);
        (await c.LineasAsync(factura.PublicId)).Select(l => (l.Quantity, l.UnitPrice)).Should()
            .Equal((await c.LineasAsync(recepcion.PublicId)).Select(l => (l.Quantity, l.UnitPrice)));
        (await c.C.Db.DocumentLinks.CountAsync(l => l.SourceDocumentId == recepcion.Id && l.TargetDocumentId == factura.Id && l.Kind == DocumentLinkKind.InvoiceOfReceipt))
            .Should().Be(1);

        c.MensajesDe(recepcion.PublicId).Should().Equal("CompraRecibida");
        c.MensajesDe(factura.PublicId).Should().Equal("FacturaProveedorRegistrada");
        var kardex = await c.C.Db.KardexEntries.SingleAsync(k => k.DocumentId == recepcion.Id);
        kardex.QuantityBase.Should().Be(36m);
        kardex.UnitCost.Should().Be(1_300m);
        r.Value.RadianEvents.Should().HaveCount(2).And.OnlyContain(e => e.Status == SupplierInvoiceEventStatus.NotApplicable);
    }

    [Fact]
    public async Task Con_politica_de_la_recepcion_queda_en_aprobacion_y_la_ultima_aprobacion_confirma_las_dos()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var tipoRecepcion = c.Tipo("REC");
        var niveles = new List<NivelDeAprobacion> { new(1, 0m, "Inventory.Purchases.Approve") };
        var sinNiveles = Result.Success(new EvaluacionConPolitica(new EvaluacionDeAprobacion(ResultadoDeEvaluacion.SinAprobacion, [], null, false, false), null));
        var conNiveles = Result.Success(new EvaluacionConPolitica(new EvaluacionDeAprobacion(ResultadoDeEvaluacion.ConNiveles, niveles, null, false, false), 1));
        c.K.Motor.EvaluarAsync(default!, default, default, default, default, default)
            .ReturnsForAnyArgs(ci => ci.ArgAt<Guid>(1) == tipoRecepcion ? conNiveles : sinNiveles);
        ApprovalRequest? pedida = null;
        c.K.Motor.SolicitarAsync(default!, default).ReturnsForAnyArgs(ci =>
        {
            pedida = new ApprovalRequest { SourcePublicId = ci.Arg<SolicitudDeAprobacion>().SourcePublicId, CurrentLevel = 1 };
            pedida.SellarNiveles(niveles);
            return Result.Success<ApprovalRequest?>(pedida);
        });

        var r = await c.CompraDirectaHandler().Handle(Pedido(c, lineas: [c.Linea(c.P3, 10m, 1_000m)]), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Status.Should().Be(DocumentStatus.PendingApproval);
        r.Value.Approval.Should().NotBeNull();
        r.Value.SupplierInvoice.Status.Should().Be(DocumentStatus.Draft);
        await c.K.Motor.Received().EvaluarAsync(ApprovalSubjects.DocumentConfirmation, tipoRecepcion, Arg.Any<DateOnly>(), 11_900m,
            "Inventory.Purchases.Confirm", Arg.Any<CancellationToken>());

        // La última aprobación: reentra por el flujo canónico y confirma también la factura enlazada.
        var servicios = new ServiceCollection();
        servicios.AddSingleton(c.Confirmacion());
        servicios.AddSingleton<IConfirmacionEncadenada>(sp => new CompraDirectaEncadenada(c.C.Db, sp));
        var fuente = new FuenteDeAprobacionDeDocumento(c.C.Db, c.K.Maestros(), servicios.BuildServiceProvider());
        var aprobada = await fuente.AlAprobarAsync(pedida!, default);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? $"{aprobada.Error.Code}: {aprobada.Error.Message}" : null);
        await c.C.Db.SaveChangesAsync();

        c.Documento(r.Value.Receipt.PublicId).Status.Should().Be(DocumentStatus.Confirmed);
        var factura = c.Documento(r.Value.SupplierInvoice.PublicId);
        factura.Status.Should().Be(DocumentStatus.Confirmed);
        factura.PostingMode.Should().Be(c.Documento(r.Value.Receipt.PublicId).PostingMode);
        c.MensajesDe(factura.PublicId).Should().Contain("FacturaProveedorRegistrada");
    }

    [Fact]
    public async Task Una_factura_repetida_no_deja_la_recepcion_guardada()
    {
        var c = await ComprasDePrueba.CrearAsync();
        (await c.CompraDirectaHandler().Handle(Pedido(c, "900"), default)).IsSuccess.Should().BeTrue();
        var recepciones = await c.C.Db.InventoryDocuments.CountAsync(d => d.Class == DocumentClass.PurchaseReceipt);

        var repetida = await c.CompraDirectaHandler().Handle(Pedido(c, "900"), default);
        repetida.IsFailure.Should().BeTrue();
        repetida.Error.Code.Should().Be("Inventory.SupplierInvoice.Duplicate");
        (await c.C.Db.InventoryDocuments.CountAsync(d => d.Class == DocumentClass.PurchaseReceipt)).Should().Be(recepciones);
    }

    [Fact]
    public async Task El_tipo_de_la_recepcion_y_el_de_la_factura_son_de_su_clase()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var pedido = Pedido(c);
        var alReves = pedido with { Invoice = pedido.Invoice with { DocumentTypePublicId = c.Tipo("REC") } };
        (await c.CompraDirectaHandler().Handle(alReves, default)).Error.Code.Should().Be("Inventory.Document.TypeNotForRoute");
    }
}
