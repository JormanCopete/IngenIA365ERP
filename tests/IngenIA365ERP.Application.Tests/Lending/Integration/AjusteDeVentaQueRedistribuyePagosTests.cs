using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, T1098 (decisión del dueño del 2026-10-18, pregunta 50 = A; mensajes.md §10.3; contabilidad.md §32): un ajuste de Comercial con fecha
/// valor anterior a un pago del crédito redistribuye ese pago. Sus filas <c>PaymentReallocation</c> ya llevaban el mensaje del ajuste; ahora el hecho
/// <c>CreditoAjustadoDesdeComercial</c> lleva también la reasignación en <c>reallocations</c>, como un <c>PagoAplicado</c>, para que Contabilidad la
/// asiente en el comprobante del ajuste. Escenario: el de los pagos (contrato de obligaciones real) con la línea que admite el origen comercial. (nuevo)
/// </summary>
public sealed class AjusteDeVentaQueRedistribuyePagosTests : IAsyncLifetime
{
    private PagosDePrueba _p = null!;
    private VentasACreditoDePrueba _v = null!;

    public async Task InitializeAsync()
    {
        var c = await CausacionDePrueba.CrearAsync(conDesembolso: false, version: VentasACreditoDePrueba.VersionDeVenta());
        _p = await PagosDePrueba.CrearAsync(causacion: c);
        _v = VentasACreditoDePrueba.Sobre(c.D);
    }

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task La_nota_credito_anterior_a_un_pago_lleva_su_reasignacion_en_el_hecho()
    {
        var venta = _v.NuevaVenta(900_000m);
        var (mensaje, _) = await _v.VenderAsync(venta);
        var credito = await _v.CreditoDeAsync(venta);
        var vencimiento = (await _p.Db.RepaymentInstallments.AsNoTracking().OrderBy(i => i.Id).FirstAsync()).DueDate;
        await _p.CausarHastaAsync(vencimiento.AddDays(3));
        _v.G.ActuarComo(_v.Cajera);
        var pago = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.FreeAmount, 100_000m, vencimiento.AddDays(1), obligacion: credito.PublicId));
        var numeroDelPago = (await _p.OperacionAsync(pago.OperationPublicId)).Number;

        // La nota crédito llega con fecha anterior al pago: baja el capital desde entonces y el pago cubre otra mezcla de conceptos.
        var ajuste = new AjusteDeVentaACreditoV1
        {
            AdjustmentClass = "CreditNote", Amount = -300_000m, OriginalMessageId = mensaje, OriginalCreditPaymentPublicId = venta.Pago,
            OriginalDocument = new DocumentRefV1 { PublicId = venta.Documento, DocumentClass = DocumentClass.PosEquivalentDocument, Number = venta.Numero },
            PaymentMeansCode = "CREDASOC", Reason = "Devolución de mercancía", AccountsReceivableRecordedBy = "Cartera",
        };
        var id = await _v.EmitirAsync(venta, ajuste, relacionado: venta.Documento, documento: Guid.NewGuid(), numero: "NC01-T1098",
            fecha: vencimiento.AddDays(-5));
        (await _v.ConsumirAsync(id)).Should().BeOfType<ResultadoDeConsumo.Processed>();

        var operacion = await _p.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.Kind == LoanOperationKind.CommercialAdjustment);
        var filas = await _p.Db.LoanTransactions.AsNoTracking()
            .Where(t => t.OperationId == operacion.Id && t.Kind == LoanTransactionKind.PaymentReallocation).ToListAsync();
        filas.Should().NotBeEmpty("el pago posterior se redistribuye (FR-081)");

        var hecho = (await _v.MensajesAsync(CreditoAjustadoDesdeComercialV1.Type)).Single();
        filas.Should().OnlyContain(f => f.MessagePublicId == hecho.PublicId, "las filas de la reasignación las lleva el hecho del ajuste");
        var contenido = JsonSerializer.Deserialize<CreditoAjustadoDesdeComercialV1>(hecho.PayloadJson, OpcionesDeMensajes.Opciones)!;
        var reasignacion = contenido.Reallocations.Should().ContainSingle().Subject;
        reasignacion.Payment.Number.Should().Be(numeroDelPago);
        reasignacion.Payment.Type.Should().Be(PagoAplicadoV1.Type);
        reasignacion.Payment.MessageId.Should().NotBe(Guid.Empty, "el pago redistribuido tiene su PagoAplicado emitido");
        reasignacion.Amounts.Should().NotBeEmpty();
        reasignacion.Amounts.Sum(a => a.Amount).Should().Be(0m, "lo recibido por el pago no cambia: la reasignación suma cero");
    }
}
