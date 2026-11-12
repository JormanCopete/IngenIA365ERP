using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Inventory.Sales.CicloComercial;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// 2026-11-12 (decisión del dueño): la cooperativa no obligada a facturar electrónicamente hace la nota débito en papel
/// (<c>NonElectronicDebitNote</c>) sobre su comprobante de venta no electrónico: no va a la DIAN, numera con su consecutivo, el concepto de
/// corrección es opcional, emite los mismos mensajes que la electrónica y se anula con su documento contrario. Una cooperativa obligada no la
/// confirma; la nota en papel no va sobre una factura.
/// </summary>
public class NotaDebitoNoElectronicaTests
{
    private static async Task<VentasDePrueba> CooperativaAsync()
    {
        var v = await VentasDePrueba.CrearAsync();
        v.Entrega = EntregaDelComercio.I6;
        var tipo = new InventoryDocumentType { Code = "NDN", Name = "Nota débito en papel", Class = DocumentClass.NonElectronicDebitNote, IsActive = true, AllWarehouses = true };
        tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = "NDN", NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
        v.Db.InventoryDocumentTypes.Add(tipo);
        await v.Db.SaveChangesAsync();
        var registro = v.K.Registro();
        var emision = new EmisionDeInventario(v.Db);
        v.EfectosAdicionales.Add(new EfectoDeNotaDebitoNoElectronica(emision, v.K.Maestros(), v.Reglas(),
            new AnulacionDeVenta(registro, new ReversionDeKardex(v.Db, registro), emision, v.Db, v.Compras.C.Reloj), v.Db));
        return v;
    }

    /// <summary>La nota en papel sobre <paramref name="origen"/> con un cargo de 1 P1, cobrada en efectivo (dos guardados).</summary>
    private static async Task<Guid> NotaAsync(VentasDePrueba v, Guid origen, string? concepto = null)
    {
        SalesDraftInput Entrada(IReadOnlyList<DocumentPaymentInput> pagos) =>
            v.Venta("NDN", v.Ana, pagos: pagos, lineas: [v.Linea(v.P1, 1m)]) with { OriginPublicIds = [origen], CorrectionConceptCode = concepto };
        var borrador = await v.Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Sales, Entrada([]).ComoBorrador(RutasDeVenta.NotasDebito)), default);
        borrador.IsSuccess.Should().BeTrue(borrador.IsFailure ? $"{borrador.Error.Code}: {borrador.Error.Message}" : null);
        var total = v.Documento(borrador.Value.PublicId).AmountDue;
        var conPagos = await v.Guardar().Handle(new SaveInventoryDraftCommand(borrador.Value.PublicId, DocumentClassGroup.Sales,
            Entrada([v.Pago(v.Efectivo, total)]).ComoBorrador(RutasDeVenta.NotasDebito)), default);
        conPagos.IsSuccess.Should().BeTrue(conPagos.IsFailure ? $"{conPagos.Error.Code}: {conPagos.Error.Message}" : null);
        return borrador.Value.PublicId;
    }

    [Fact]
    public async Task Sobre_el_comprobante_no_va_a_la_DIAN_numera_con_su_consecutivo_sin_concepto_y_emite_la_nota_debito()
    {
        var v = await CooperativaAsync();
        var recibo = await v.VentaConfirmadaAsync(cliente: v.Ana, lineas: [v.Linea(v.P1, 2m)]);
        var nota = await NotaAsync(v, recibo.PublicId);

        var r = await v.ConfirmarAsync(nota);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var documento = v.Documento(nota);
        (documento.Class, documento.Status, documento.Prefix, documento.Number, documento.CorrectionConceptCode)
            .Should().Be((DocumentClass.NonElectronicDebitNote, DocumentStatus.Confirmed, "NDN", (long?)1, (string?)null));
        v.Db.KardexEntries.AsNoTracking().Count(k => k.DocumentId == documento.Id).Should().Be(0);
        v.MensajesDe(nota).Should().Contain(NotaDebitoEmitidaV1.Type);
        v.Db.ElectronicDocuments.AsNoTracking().Count(e => e.SourceDocumentPublicId == nota).Should().Be(0, "en papel no va a la DIAN");
    }

    [Fact]
    public async Task Con_concepto_lo_valida_contra_el_catalogo_de_la_DIAN()
    {
        var v = await CooperativaAsync();
        var recibo = await v.VentaConfirmadaAsync(cliente: v.Ana, lineas: [v.Linea(v.P1, 2m)]);

        var malo = await v.ConfirmarAsync(await NotaAsync(v, recibo.PublicId, concepto: "ZZ"));
        var bueno = await v.ConfirmarAsync(await NotaAsync(v, recibo.PublicId, concepto: "1"));

        malo.Error.Code.Should().Be(ErroresDeVentas.CorrectionConceptRequiredCode);
        bueno.IsSuccess.Should().BeTrue(bueno.IsFailure ? $"{bueno.Error.Code}: {bueno.Error.Message}" : null);
    }

    [Fact]
    public async Task Se_anula_con_su_documento_contrario()
    {
        var v = await CooperativaAsync();
        var recibo = await v.VentaConfirmadaAsync(cliente: v.Ana, lineas: [v.Linea(v.P1, 2m)]);
        var nota = await NotaAsync(v, recibo.PublicId);
        (await v.ConfirmarAsync(nota)).IsSuccess.Should().BeTrue();

        var anulada = await v.Anular().Handle(new VoidInventoryDocumentCommand(nota, DocumentClassGroup.Sales, "Cargo equivocado"), default);

        anulada.IsSuccess.Should().BeTrue(anulada.IsFailure ? $"{anulada.Error.Code}: {anulada.Error.Message}" : null);
        v.Documento(nota).Status.Should().Be(DocumentStatus.Voided);
        v.MensajesDe(anulada.Value.VoidingDocumentPublicId).Should().Contain(DocumentoAnuladoV1.Type);
    }

    [Fact]
    public async Task La_cooperativa_obligada_no_la_confirma()
    {
        var v = await CooperativaAsync();
        var recibo = await v.VentaConfirmadaAsync(cliente: v.Ana, lineas: [v.Linea(v.P1, 2m)]);
        var nota = await NotaAsync(v, recibo.PublicId);
        v.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeFacturacionElectronica.Modulo, Key = ParametrosDeFacturacionElectronica.ObligadaAFacturar,
            ScopeKind = ParameterScopeKind.None, Value = "true", ValidFrom = Catalog.CatalogoDePrueba.Hoy, Reason = "obligada desde hoy",
        });
        await v.Db.SaveChangesAsync();

        var r = await v.ConfirmarAsync(nota);

        r.IsFailure.Should().BeTrue("una cooperativa obligada hace la nota débito electrónica");
        r.Error.Code.Should().BeOneOf(ErroresDeVentas.FiscalClassMismatchCode, "ElectronicInvoicing.NotReady");
    }

    [Fact]
    public async Task La_nota_en_papel_exige_su_comprobante_de_origen_y_no_va_sobre_una_factura()
    {
        var v = await CooperativaAsync();
        var factura = await v.Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Sales,
            v.Venta("FV", v.Ana, lineas: [v.Linea(v.P1, 1m)]).ComoBorrador()), default);
        SalesDraftInput Nota(IReadOnlyList<Guid>? origenes) => v.Venta("NDN", v.Ana, lineas: [v.Linea(v.P1, 1m)]) with { OriginPublicIds = origenes };

        var sinOrigen = await v.Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Sales, Nota(null).ComoBorrador(RutasDeVenta.NotasDebito)), default);
        var sobreFactura = await v.Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Sales,
            Nota([factura.Value.PublicId]).ComoBorrador(RutasDeVenta.NotasDebito)), default);

        sinOrigen.Error.Code.Should().Be(ErroresDelCicloComercial.OriginInvalidCode);
        sobreFactura.Error.Code.Should().Be(ErroresDelCicloComercial.OriginInvalidCode);
    }
}
