using FluentAssertions;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Application.Treasury.Results;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Lotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Treasury.Orders;

/// <summary>
/// 2026-11-15 (pedido del dueño): varias cuentas por pagar de la misma persona se pagan en un solo giro. Se agrupan en una agrupadora con la suma, que es la
/// que entra al lote; girada, cada agrupada queda girada con su propio resultado; rechazada o desagrupada, vuelven a quedar listas. Y una orden programada para
/// otro banco no entra al lote de otra cuenta.
/// </summary>
public class AgrupacionDeOrdenesTests
{
    private readonly EscenarioDeTesoreria _t = new();

    private AuditoriaDeTesoreria Auditoria => new(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance);

    private GroupPaymentOrdersCommandHandler Agrupar() =>
        new(_t.E.Db, _t.E.Actor, _t.Cerrojo, _t.Numerador, _t.Validador, _t.Parametros, Auditoria, _t.E.Reloj);

    private UngroupPaymentOrderCommandHandler Desagrupar() => new(_t.E.Db, _t.E.Actor, _t.Cerrojo, Auditoria);

    private PaymentOrder Factura(decimal neto, string numero) =>
        _t.Orden(neto, PaymentOrderState.Ready, origen: PaymentOrderOrigin.SupplierInvoice, clase: "SupplierInvoice", numero: numero);

    private async Task<PaymentOrder> AgrupadaAsync(params PaymentOrder[] ordenes)
    {
        var r = await Agrupar().Handle(new GroupPaymentOrdersCommand(ordenes.Select(o => o.PublicId).ToList(), "Un solo giro") { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        return await _t.E.Db.PaymentOrders.FirstAsync(o => o.PublicId == r.Value.PublicId);
    }

    [Fact]
    public async Task Dos_facturas_de_la_misma_persona_quedan_en_una_agrupadora_con_la_suma()
    {
        var a = Factura(300_000m, "OP-A");
        var b = Factura(200_000m, "OP-B");

        var agrupadora = await AgrupadaAsync(a, b);

        agrupadora.NetAmount.Should().Be(500_000m);
        agrupadora.OrderKind.Should().Be(AgrupacionDeOrdenes.ClaseAgrupada);
        agrupadora.DestinationAccountNumber.Should().Be(a.DestinationAccountNumber);
        a.Status.Should().Be(PaymentOrderState.Grouped);
        b.Status.Should().Be(PaymentOrderState.Grouped);
        a.ConsolidatedIntoOrderId.Should().Be(agrupadora.Id);
        b.ConsolidatedIntoOrderId.Should().Be(agrupadora.Id);
    }

    [Fact]
    public async Task No_se_agrupan_ordenes_de_personas_distintas_ni_giros_de_otros_modulos()
    {
        var otra = new Person { FirstName = "Otra", LastName = "Persona", TaxId = "3001", IdType = "C" };
        _t.E.Db.People.Add(otra);
        _t.E.Db.SaveChanges();
        var a = Factura(100m, "OP-A");
        var b = Factura(100m, "OP-B");
        b.BeneficiaryPersonId = otra.Id;
        var giro = _t.Orden(100m, PaymentOrderState.Ready, numero: "OP-C");
        _t.E.Db.SaveChanges();

        foreach (var par in new[] { new[] { a, b }, new[] { a, giro } })
            (await Agrupar().Handle(new GroupPaymentOrdersCommand(par.Select(o => o.PublicId).ToList(), "x") { OperationKey = Guid.NewGuid() }, default))
                .Error.Code.Should().Be("Treasury.Order.NotGroupable");
        a.Status.Should().Be(PaymentOrderState.Ready);
    }

    [Fact]
    public async Task Desagrupar_libera_las_agrupadas_y_anula_la_agrupadora()
    {
        var a = Factura(300_000m, "OP-A");
        var b = Factura(200_000m, "OP-B");
        var agrupadora = await AgrupadaAsync(a, b);

        var r = await Desagrupar().Handle(new UngroupPaymentOrderCommand(agrupadora.PublicId, "Cambio de planes") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        agrupadora.Status.Should().Be(PaymentOrderState.Voided);
        new[] { a, b }.Should().OnlyContain(o => o.Status == PaymentOrderState.Ready && o.ConsolidatedIntoOrderId == null);
    }

    [Fact]
    public async Task Girada_la_agrupadora_cada_agrupada_queda_girada_con_su_resultado()
    {
        var a = Factura(300_000m, "OP-A");
        var b = Factura(200_000m, "OP-B");
        var agrupadora = await AgrupadaAsync(a, b);
        agrupadora.Status = PaymentOrderState.Sent;
        await _t.E.Db.SaveChangesAsync();

        var r = await _t.Resultados.RegistrarAsync(agrupadora, new ResultadoARegistrar(DisbursementOutcome.Disbursed, EscenarioDeRecaudo.Hoy, "REF-9", null, null,
            PaymentResponseSource.Manual, Guid.NewGuid()), new QuienRegistra(_t.E.Cajera.Id, Guid.NewGuid(), "Cajera"), default);
        await _t.E.Db.SaveChangesAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        new[] { a, b }.Should().OnlyContain(o => o.Status == PaymentOrderState.Disbursed);
        var resultados = await _t.E.Db.DisbursementResults.Where(x => x.PaymentOrderId == a.Id || x.PaymentOrderId == b.Id).ToListAsync();
        resultados.Select(x => x.Amount).Should().BeEquivalentTo([300_000m, 200_000m]);
        resultados.Should().OnlyContain(x => x.TreasuryReference == "REF-9");
    }

    [Fact]
    public async Task Rechazada_la_agrupadora_las_agrupadas_vuelven_a_quedar_listas()
    {
        var a = Factura(300_000m, "OP-A");
        var b = Factura(200_000m, "OP-B");
        var agrupadora = await AgrupadaAsync(a, b);
        agrupadora.Status = PaymentOrderState.Sent;
        await _t.E.Db.SaveChangesAsync();

        var r = await _t.Resultados.RegistrarAsync(agrupadora, new ResultadoARegistrar(DisbursementOutcome.Rejected, EscenarioDeRecaudo.Hoy, "REF-9", null,
            "Cuenta inexistente", PaymentResponseSource.Manual, Guid.NewGuid()), new QuienRegistra(_t.E.Cajera.Id, Guid.NewGuid(), "Cajera"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        new[] { a, b }.Should().OnlyContain(o => o.Status == PaymentOrderState.Ready && o.ConsolidatedIntoOrderId == null);
    }

    [Fact]
    public void Una_orden_programada_para_otro_banco_no_entra_al_lote()
    {
        var candidata = new OrdenCandidata(1, PaymentOrderState.Ready, PaymentForm.Transfer, 100m, false, false, false, [], VerificacionDeDestino.Limpia,
            ProgramadaParaOtroBanco: true);

        var armado = ArmadoDeLote.Armar([candidata], PaymentBatchForm.File);

        armado.Incluidas.Should().BeEmpty();
        armado.Pendientes.Single().Motivo.Should().Be(ArmadoDeLote.MotivoOtroBanco);
    }
}
