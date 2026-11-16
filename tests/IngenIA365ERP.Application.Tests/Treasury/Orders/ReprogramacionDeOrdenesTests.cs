using FluentAssertions;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Orders;

/// <summary>
/// 2026-11-15 (pedido del dueño): programar el pago de lo causado. La fecha de una orden recibida, pendiente o lista que no está en un lote se mueve con motivo y
/// queda auditada; una en lote, anulada o pagada por un archivo de Cartera no se toca, ni se programa para antes de hoy.
/// </summary>
public class ReprogramacionDeOrdenesTests
{
    private readonly EscenarioDeTesoreria _t = new();

    private ReschedulePaymentOrderCommandHandler Handler() =>
        new(_t.E.Db, _t.E.Actor, _t.Cerrojo, new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _t.E.Reloj);

    private IReadOnlyList<string> Eventos() => _t.E.Auditoria.ReceivedCalls()
        .Select(c => c.GetArguments().OfType<Application.Common.Interfaces.AuditLogCommand>().FirstOrDefault()?.Action).OfType<string>().ToList();

    private static ReschedulePaymentOrderCommand Comando(Guid orden, DateOnly fecha) => new(orden, fecha, "Flujo de caja") { OperationKey = Guid.NewGuid() };

    [Fact]
    public async Task Mueve_la_fecha_de_una_orden_lista_y_lo_audita()
    {
        var orden = _t.Orden(estado: PaymentOrderState.Ready);
        var nueva = _t.E.Reloj.HoyLocal.AddDays(10);

        var r = await Handler().Handle(Comando(orden.PublicId, nueva), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        orden.PayDate.Should().Be(nueva);
        r.Value.PayDate.Should().Be(nueva);
        Eventos().Should().Contain(ReprogramacionDeOrdenes.Evento);
    }

    [Fact]
    public async Task No_se_programa_para_antes_de_hoy()
    {
        var orden = _t.Orden(estado: PaymentOrderState.Ready);
        var antes = orden.PayDate;

        var r = await Handler().Handle(Comando(orden.PublicId, _t.E.Reloj.HoyLocal.AddDays(-1)), default);

        r.Error.Code.Should().Be("Treasury.Order.PayDateInPast");
        orden.PayDate.Should().Be(antes);
    }

    [Theory]
    [InlineData(PaymentOrderState.InBatch)]
    [InlineData(PaymentOrderState.Approved)]
    [InlineData(PaymentOrderState.Disbursed)]
    [InlineData(PaymentOrderState.Voided)]
    public async Task Una_orden_en_lote_o_cerrada_no_se_reprograma(PaymentOrderState estado)
    {
        var orden = _t.Orden(estado: estado);

        var r = await Handler().Handle(Comando(orden.PublicId, _t.E.Reloj.HoyLocal.AddDays(5)), default);

        r.Error.Code.Should().Be("Treasury.Order.NotReschedulable");
    }

    [Fact]
    public async Task Tampoco_la_que_paga_un_archivo_de_cartera_ni_la_que_el_origen_pidio_anular()
    {
        var deCartera = _t.Orden(estado: PaymentOrderState.Ready, numero: "OP-C");
        deCartera.PaidByLendingFile = true;
        var anulada = _t.Orden(estado: PaymentOrderState.Ready, numero: "OP-A");
        anulada.VoidRequested = true;
        _t.E.Db.SaveChanges();

        foreach (var o in new[] { deCartera, anulada })
            (await Handler().Handle(Comando(o.PublicId, _t.E.Reloj.HoyLocal.AddDays(5)), default)).Error.Code.Should().Be("Treasury.Order.NotReschedulable");
    }
}
