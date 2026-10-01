using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 012, I4, T739 (contracts/api.md §18.3.1; FR-063): la factura que pide el comprador después del documento equivalente. En una acción
/// confirma la nota de ajuste de anulación total sin devolución ni reintegros y la factura a nombre del comprador, del tipo del rol
/// <c>InvoiceOnRequest</c> de la caja, con las mismas líneas y los pagos trasladados sin sesión de caja, sin kardex, vinculada con
/// <c>ReplacementOf</c> y que se transmite después de la nota; el valor esperado distinto es <c>Payments.TotalMismatch</c>, un original enviado sin
/// respuesta es <c>AwaitingResponse</c> y un documento que no es un DEE no aplica.
/// </summary>
public class FacturaEnLugarDelDocumentoEquivalenteTests
{
    private static async Task<(VentaElectronicaDePrueba E, InventoryDocument Dee)> DocumentoEquivalenteAsync(
        ElectronicDocumentStatus estado = ElectronicDocumentStatus.Validated)
    {
        var e = await VentaElectronicaDePrueba.CrearAsync();
        var db = e.Db;
        var tipoDee = new InventoryDocumentType { Code = "DE", Name = "Documento equivalente", Class = DocumentClass.PosEquivalentDocument, FiscalPrefix = "POS", IsActive = true, AllWarehouses = true };
        var tipoNota = new InventoryDocumentType { Code = "NA", Name = "Nota de ajuste", Class = DocumentClass.PosAdjustmentNote, IsActive = true, AllWarehouses = true };
        tipoNota.Sequences.Add(new DocumentSequence { DocumentType = tipoNota, Prefix = "NA", NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
        db.InventoryDocumentTypes.AddRange(tipoDee, tipoNota);
        await db.SaveChangesAsync();
        db.CashRegisterDocumentTypes.Add(new CashRegisterDocumentType { CashRegisterId = e.V.Caja.Id, Role = CashRegisterDocumentRole.PosSale, DocumentTypeId = tipoDee.Id });
        db.CashRegisterDocumentTypes.Add(new CashRegisterDocumentType { CashRegisterId = e.V.Caja.Id, Role = CashRegisterDocumentRole.InvoiceOnRequest, DocumentTypeId = e.V.K.Tipo("FV").Id });

        // El DEE: una venta confirmada por el flujo real (con sus mensajes y su documento electrónico) que se vuelve documento equivalente de la
        // caja, porque la oficina no confirma ventas de caja.
        var (venta, confirmada) = await e.FacturaAsync();
        confirmada.IsSuccess.Should().BeTrue(confirmada.IsFailure ? $"{confirmada.Error.Code}: {confirmada.Error.Message}" : null);
        var dee = db.InventoryDocuments.Single(d => d.PublicId == venta);
        dee.Class = DocumentClass.PosEquivalentDocument;
        dee.DocumentTypeId = tipoDee.Id;
        dee.DocumentType = tipoDee;
        dee.PointOfSaleId = e.V.Punto.Id;
        dee.CashRegisterId = e.V.Caja.Id;
        dee.CashSessionId = e.V.Sesion.Id;
        var electronico = e.Electronico(venta);
        electronico.Kind = ElectronicDocumentKind.PosEquivalent;
        electronico.Status = estado;
        electronico.UniqueCode = estado == ElectronicDocumentStatus.Validated ? new string('e', 96) : null;
        await db.SaveChangesAsync();
        return (e, dee);
    }

    private static ReplacePosDocumentWithInvoiceCommandHandler Handler(VentaElectronicaDePrueba e)
    {
        var confirmacion = e.Confirmacion();
        return new ReplacePosDocumentWithInvoiceCommandHandler(e.Db, e.V.K.Actor, e.V.Compras.C.Reloj, e.V.Nota(), confirmacion, e.Traslado, e.Fiscal);
    }

    [Fact]
    public async Task En_una_accion_anula_el_DEE_con_su_nota_y_factura_a_nombre_del_comprador_sin_mover_pagos_ni_kardex()
    {
        var (e, dee) = await DocumentoEquivalenteAsync();
        var kardexAntes = e.Db.KardexEntries.Count();

        var r = await Handler(e).Handle(new ReplacePosDocumentWithInvoiceCommand(dee.PublicId, e.V.Ana.PublicId, "El cliente pide factura", dee.AmountDue), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var nota = e.V.Documento(r.Value.AdjustmentNotePublicId);
        nota.Class.Should().Be(DocumentClass.PosAdjustmentNote);
        nota.Status.Should().Be(DocumentStatus.Confirmed);
        nota.IsFullReversal.Should().BeTrue();
        nota.ReturnsGoods.Should().BeFalse();
        e.Db.DocumentPayments.Where(p => p.DocumentId == nota.Id && !p.IsDeleted).Should().BeEmpty("la nota no reintegra");

        var factura = e.V.Documento(r.Value.InvoicePublicId);
        factura.Class.Should().Be(DocumentClass.SalesInvoice);
        factura.Status.Should().Be(DocumentStatus.Confirmed);
        factura.Prefix.Should().Be("SETP");
        factura.CounterpartyPersonId.Should().Be(e.V.Ana.Id);
        factura.Total.Should().Be(dee.Total);
        var pagos = e.Db.DocumentPayments.Where(p => p.DocumentId == factura.Id && !p.IsDeleted).ToList();
        pagos.Should().ContainSingle().Which.Amount.Should().Be(dee.AmountDue);
        pagos[0].CashSessionId.Should().BeNull("la caja no registra ni entrada ni salida");
        e.Db.KardexEntries.Count().Should().Be(kardexAntes, "la mercancía no se mueve dos veces");
        e.Db.DocumentLinks.Should().Contain(l => l.SourceDocumentId == dee.Id && l.TargetDocumentId == factura.Id && l.Kind == DocumentLinkKind.ReplacementOf);

        var electronicaNota = e.Electronico(nota.PublicId);
        var electronicaFactura = e.Electronico(factura.PublicId);
        electronicaNota.CorrectsDocumentId.Should().Be(e.Electronico(dee.PublicId).Id);
        electronicaFactura.WaitsForDocumentId.Should().Be(electronicaNota.Id, "la factura se transmite después de la nota");
        e.V.MensajesDe(factura.PublicId).Should().Contain(Domain.Inventory.Documents.ClasesDeDocumento.VentaFacturada)
            .And.NotContain(Domain.Inventory.Documents.ClasesDeDocumento.CostoDeVentaReconocido);
        e.V.MensajesDe(nota.PublicId).Should().Contain(Domain.Inventory.Documents.ClasesDeDocumento.NotaCreditoEmitida)
            .And.NotContain(Domain.Inventory.Documents.ClasesDeDocumento.DevolucionRegistrada);
    }

    [Fact]
    public async Task Las_reglas_de_la_factura_despues_del_DEE()
    {
        var (e, dee) = await DocumentoEquivalenteAsync();
        var distinto = await Handler(e).Handle(new ReplacePosDocumentWithInvoiceCommand(dee.PublicId, e.V.Ana.PublicId, "pide factura", dee.AmountDue - 1m), default);
        distinto.Error.Code.Should().Be("Payments.TotalMismatch");

        var (e2, enviado) = await DocumentoEquivalenteAsync(ElectronicDocumentStatus.Sent);
        var sinRespuesta = await Handler(e2).Handle(new ReplacePosDocumentWithInvoiceCommand(enviado.PublicId, e2.V.Ana.PublicId, "pide factura", enviado.AmountDue), default);
        sinRespuesta.Error.Code.Should().Be(TransicionesDelDocumentoElectronico.CodigoEsperaRespuesta);

        var (factura, rf) = await e.FacturaAsync();
        rf.IsSuccess.Should().BeTrue();
        var noEsDee = await Handler(e).Handle(new ReplacePosDocumentWithInvoiceCommand(factura, e.V.Ana.PublicId, "pide factura", 4000m), default);
        noEsDee.Error.Code.Should().Be(ReplacePosDocumentWithInvoiceCommandHandler.InvoiceInsteadNotApplicableCode);
    }
}
