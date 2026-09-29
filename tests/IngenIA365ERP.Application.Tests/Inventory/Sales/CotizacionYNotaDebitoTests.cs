using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Inventory.Sales.Quotes;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 012, I6, T866 (FR-052, US14-5, T32; contracts/api.md §18.4, §23; mensajes.md §6.12, §8.2): la cotización con vigencia que no mueve
/// nada y se convierte en pedido con <c>FromOrder</c>; la nota débito sobre una factura, con concepto DIAN, sin kardex y con
/// <c>NotaDebitoEmitida</c> relacionada con la venta; cobrada con crédito, el crédito provisional con aprobación y el ajuste a Cartera.
/// </summary>
public class CotizacionYNotaDebitoTests
{
    private static readonly DateOnly Hoy = Catalog.CatalogoDePrueba.Hoy;

    private static async Task<(CicloComercialDePrueba C, Guid Cotizacion)> CotizacionAsync(DateOnly? vence)
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var borrador = CicloComercialDePrueba.Exito(await c.GuardarAsync(
            c.Documento("COT", lineas: [c.Linea(c.P1, 6m), c.Linea(c.P3, 2m)]) with { ValidUntil = vence }, RutasDeVenta.Cotizaciones));
        return (c, borrador.PublicId);
    }

    // ------------------------------------------------------------------------------------------ cotización --

    [Fact]
    public async Task La_cotizacion_sin_vigencia_no_confirma_y_confirmada_no_mueve_nada()
    {
        var (sin, sinVigencia) = await CotizacionAsync(null);
        var r = await sin.ConfirmarAsync(sinVigencia);
        r.Error.Code.Should().Be("Inventory.Document.FieldRequired");

        var (c, cotizacion) = await CotizacionAsync(Hoy.AddDays(10));
        var confirmada = await c.ConfirmarAsync(cotizacion);

        confirmada.IsSuccess.Should().BeTrue(confirmada.IsFailure ? $"{confirmada.Error.Code}: {confirmada.Error.Message}" : null);
        var documento = c.Doc(cotizacion);
        (documento.Status, documento.ValidUntil).Should().Be((DocumentStatus.Confirmed, Hoy.AddDays(10)));
        c.KardexDe(documento).Should().BeEmpty();
        c.MensajesDe(cotizacion).Should().BeEmpty();
        c.Db.Reservations.Should().BeEmpty();
    }

    [Fact]
    public async Task Convertir_la_cotizacion_crea_el_borrador_del_pedido_enlazado_y_vencida_no_se_convierte()
    {
        var (c, cotizacion) = await CotizacionAsync(Hoy.AddDays(3));
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(cotizacion));
        var convertir = new ConvertQuoteToOrderCommandHandler(c.Db, c.V.K.Actor, c.V.Compras.C.Reloj, c.V.K.Vista());

        var pedido = await convertir.Handle(new ConvertQuoteToOrderCommand(cotizacion), default);

        pedido.IsSuccess.Should().BeTrue(pedido.IsFailure ? $"{pedido.Error.Code}: {pedido.Error.Message}" : null);
        var documento = c.Doc(pedido.Value.PublicId);
        (documento.Class, documento.Status, documento.Number).Should().Be((DocumentClass.SalesOrder, DocumentStatus.Draft, (long?)null));
        documento.Lines.Where(l => !l.IsDeleted).Select(l => (l.QuantityBase, l.UnitPrice)).Should().BeEquivalentTo([(6m, 2000m), (2m, 10000m)]);
        var vinculo = c.Db.DocumentLinks.Include(l => l.LineLinks).Single(l => l.TargetDocumentId == documento.Id);
        (vinculo.Kind, vinculo.SourceDocumentId, vinculo.LineLinks.Count).Should().Be((DocumentLinkKind.FromOrder, c.Doc(cotizacion).Id, 2));

        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(documento.PublicId));
        c.Reservado(c.P1).Should().Be(6m, "el pedido convertido reserva al confirmarse");

        c.V.Compras.C.Reloj.HoyLocal.Returns(Hoy.AddDays(4));
        var vencida = await convertir.Handle(new ConvertQuoteToOrderCommand(cotizacion), default);
        vencida.Error.Code.Should().Be("Inventory.Quote.Expired");
    }

    [Fact]
    public async Task Anular_una_cotizacion_no_tiene_efecto_ni_mensajes()
    {
        var (c, cotizacion) = await CotizacionAsync(Hoy.AddDays(3));
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(cotizacion));

        var anulada = await c.AnularAsync(cotizacion);

        anulada.IsSuccess.Should().BeTrue(anulada.IsFailure ? $"{anulada.Error.Code}: {anulada.Error.Message}" : null);
        c.Doc(cotizacion).Status.Should().Be(DocumentStatus.Voided);
        c.KardexDe(c.Doc(anulada.Value.VoidingDocumentPublicId)).Should().BeEmpty();
        c.MensajesDe(anulada.Value.VoidingDocumentPublicId).Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------------------ nota débito --

    private static async Task<(CicloComercialDePrueba C, Guid Factura)> FacturaValidadaAsync()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var factura = await c.FacturaAsync("FV", [], c.Linea(c.P1, 2m));
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(factura));
        c.E.Estado(factura, ElectronicDocumentStatus.Validated, "CUFE-FV-1");
        return (c, factura);
    }

    /// <summary>La nota débito de la factura con un cargo en P1, cobrada en efectivo o por <paramref name="pagos"/> (dos guardados).</summary>
    private static async Task<Guid> NotaDebitoAsync(CicloComercialDePrueba c, Guid factura, string? concepto = "1",
        Func<decimal, IReadOnlyList<DocumentPaymentInput>>? pagos = null)
    {
        SalesDraftInput Entrada(IReadOnlyList<DocumentPaymentInput> p) =>
            c.Documento("NDV", origenes: [factura], lineas: c.Linea(c.P1, 1m)) with { CorrectionConceptCode = concepto, Payments = p };
        var borrador = CicloComercialDePrueba.Exito(await c.GuardarAsync(Entrada([]), RutasDeVenta.NotasDebito));
        var total = c.Doc(borrador.PublicId).AmountDue;
        CicloComercialDePrueba.Exito(await c.GuardarAsync(Entrada(pagos?.Invoke(total) ?? [c.V.Pago(c.V.Efectivo, total)]), RutasDeVenta.NotasDebito, borrador.PublicId));
        return borrador.PublicId;
    }

    [Fact]
    public async Task La_nota_debito_va_sobre_la_factura_con_concepto_sin_kardex_y_emite_su_mensaje_relacionado_con_la_venta()
    {
        var (c, factura) = await FacturaValidadaAsync();
        var fisico = c.Fisico(c.P1);

        var sinConcepto = await c.ConfirmarAsync(await NotaDebitoAsync(c, factura, concepto: null));
        var nota = await NotaDebitoAsync(c, factura);
        var confirmada = await c.ConfirmarAsync(nota);

        sinConcepto.Error.Code.Should().Be(ErroresDeVentas.CorrectionConceptRequiredCode);
        confirmada.IsSuccess.Should().BeTrue(confirmada.IsFailure ? $"{confirmada.Error.Code}: {confirmada.Error.Message}" : null);
        var documento = c.Doc(nota);
        (documento.Status, documento.CorrectionConceptCode).Should().Be((DocumentStatus.Confirmed, "1"));
        c.KardexDe(documento).Should().BeEmpty();
        c.Fisico(c.P1).Should().Be(fisico);
        c.MensajesDe(nota).Should().Equal(NotaDebitoEmitidaV1.Type);
        var mensaje = c.Db.IntegrationMessages.AsNoTracking().Single(m => m.OriginPublicId == nota);
        mensaje.RelatedPublicId.Should().Be(factura, "related = la venta que corrige");
        c.E.Electronico(nota).Kind.Should().Be(ElectronicDocumentKind.DebitNote);
        var entrada = await c.E.Fuente().LeerAsync(nota, default);
        entrada.Value.Correccion.Should().NotBeNull("el canónico referencia la factura que corrige (T888)");
        (entrada.Value.Correccion!.CorrectedDocumentPublicId, entrada.Value.Correccion.CorrectionConceptCode).Should().Be((factura, "1"));
    }

    [Fact]
    public async Task Los_pagos_de_la_nota_debito_suman_lo_que_se_cobra_y_sobre_algo_que_no_es_factura_no_hay_nota()
    {
        var (c, factura) = await FacturaValidadaAsync();

        var descuadrada = await c.ConfirmarAsync(await NotaDebitoAsync(c, factura, pagos: t => [c.V.Pago(c.V.Efectivo, t - 100m)]));
        var remision = await c.RemisionAsync(1m);
        var sobreRemision = await c.GuardarAsync(c.Documento("NDV", origenes: [remision.PublicId], lineas: c.Linea(c.P1, 1m)) with { CorrectionConceptCode = "1" },
            RutasDeVenta.NotasDebito);

        descuadrada.Error.Code.Should().Be("Payments.TotalMismatch");
        sobreRemision.Error.Code.Should().Be("Inventory.Sales.OriginInvalid");
    }

    [Fact]
    public async Task La_nota_debito_cobrada_a_credito_pide_la_aprobacion_del_credito_provisional_y_ajusta_la_venta_a_credito()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var venta = CicloComercialDePrueba.Exito(await c.GuardarAsync(c.Documento("FV", c.C.AsociadoX, lineas: c.Linea(c.P1, 2m)), RutasDeVenta.Facturas)).PublicId;
        var total = c.Doc(venta).AmountDue;
        CicloComercialDePrueba.Exito(await c.GuardarAsync(c.Documento("FV", c.C.AsociadoX, lineas: c.Linea(c.P1, 2m)) with
        {
            Payments = [c.C.Credito(c.C.CredAsoc, total)],
        }, RutasDeVenta.Facturas, venta));
        (await c.ConfirmarAsync(venta)).Value.Status.Should().Be(DocumentStatus.PendingApproval);
        CicloComercialDePrueba.Exito(await c.C.AprobarAsync(c.C.SolicitudDeCredito(venta), CreditoDePrueba.Supervisor));
        c.C.ComoUsuario(CreditoDePrueba.Cajera);
        c.Doc(venta).Status.Should().Be(DocumentStatus.Confirmed);
        c.E.Estado(venta, ElectronicDocumentStatus.Validated, "CUFE-FV-2");
        var registrada = c.Db.IntegrationMessages.AsNoTracking().Single(m => m.OriginPublicId == venta && m.Type == VentaACreditoRegistradaV1.Type);

        SalesDraftInput Entrada(IReadOnlyList<DocumentPaymentInput> p) =>
            c.Documento("NDV", c.C.AsociadoX, [venta], c.Linea(c.P1, 1m)) with { CorrectionConceptCode = "1", Payments = p };
        var nota = CicloComercialDePrueba.Exito(await c.GuardarAsync(Entrada([]), RutasDeVenta.NotasDebito)).PublicId;
        var cargo = c.Doc(nota).AmountDue;
        CicloComercialDePrueba.Exito(await c.GuardarAsync(Entrada([c.C.Credito(c.C.CredAsoc, cargo)]), RutasDeVenta.NotasDebito, nota));

        var pendiente = await c.ConfirmarAsync(nota);

        pendiente.Value.Status.Should().Be(DocumentStatus.PendingApproval, "el crédito provisional pide aprobación (§23)");
        var solicitud = c.C.SolicitudDeCredito(nota);
        solicitud.Subject.Should().Be(Domain.Approvals.ApprovalSubjects.ProvisionalCredit);
        var pago = c.Db.DocumentPayments.AsNoTracking().Single(p => p.Document!.PublicId == nota && !p.IsDeleted);
        (pago.PendingValidation, pago.CreditOrigin).Should().Be((true, CreditOrigin.ProvisionalCredit));

        CicloComercialDePrueba.Exito(await c.C.AprobarAsync(solicitud, CreditoDePrueba.Supervisor));

        c.Doc(nota).Status.Should().Be(DocumentStatus.Confirmed);
        c.MensajesDe(nota).Should().Equal(NotaDebitoEmitidaV1.Type, AjusteDeVentaACreditoV1.Type);
        var ajuste = c.Db.IntegrationMessages.AsNoTracking().Single(m => m.OriginPublicId == nota && m.Type == AjusteDeVentaACreditoV1.Type);
        using var contenido = JsonDocument.Parse(ajuste.PayloadJson);
        var raiz = contenido.RootElement;
        (raiz.GetProperty("adjustmentClass").GetString(), raiz.GetProperty("amount").GetDecimal(), raiz.GetProperty("originalMessageId").GetGuid())
            .Should().Be(("DebitNote", cargo, registrada.PublicId), "monto positivo sobre la venta a crédito que ajusta");
        raiz.GetProperty("terms").ValueKind.Should().Be(JsonValueKind.Object);
        var entrega = c.Db.IntegrationMessageDeliveries.AsNoTracking().Single(d => d.MessageId == ajuste.Id);
        (entrega.Destination, entrega.Mode).Should().Be((IntegrationDestinations.Lending, DeliveryMode.Always));
    }
}
