using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Treasury.Integration;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Treasury;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Orders;

/// <summary>
/// Feature 015, US10, T0129 (R-18; FR-107 a FR-109, FR-114, FR-115, FR-127; mensajes.md §5): la bandeja única de órdenes. Consume las v1 y v2 con recibo único;
/// la repetida no se duplica; valor cero o forma inexistente quedan rechazadas al llegar; la pagada por archivo de Cartera queda lista sin entrar a lotes; la de
/// regiro enlaza la cadena; la validación deja pendiente lo que falta (cuenta no registrada, en espera, banco sin código, lista vinculante); la anulación del origen
/// actúa según el estado. Sólo entran órdenes de un módulo: Tesorería no tiene ruta para crear una con factura o gasto propio. Prueba escrita, sin correr.
/// </summary>
public class BandejaDeOrdenesTests
{
    private readonly EscenarioDeTesoreria _t = new();

    private ConsumePaymentOrderCommandHandler Consumir() => new(_t.E.Db, _t.Entrantes, _t.Cerrojo, _t.Numerador, _t.Validador, _t.Parametros, _t.E.Reloj);

    private ConsumePaymentOrderVoidCommandHandler Anular() => new(_t.E.Db, _t.Entrantes, _t.Cerrojo, _t.Motor, _t.Resultados, _t.E.Reloj);

    private async Task<(ResultadoDeConsumo Resultado, Guid Mensaje)> RecibirAsync(OrdenDeGiroEmitidaV1 orden)
    {
        var id = _t.Mensaje(orden, OrdenDeGiroEmitidaV1.Type, 1, _t.Beneficiaria.PublicId);
        var r = await Consumir().Handle(new ConsumePaymentOrderCommand([id]), default);
        return (r.Value, id);
    }

    [Fact]
    public async Task Una_orden_valida_entra_lista_con_su_recibo_unico()
    {
        var (r, id) = await RecibirAsync(_t.OrdenDeGiro());
        r.Should().BeOfType<ResultadoDeConsumo.Processed>();
        var orden = _t.E.Db.PaymentOrders.Single();
        orden.Status.Should().Be(PaymentOrderState.Ready);
        orden.Origin.Should().Be(PaymentOrderOrigin.LendingDisbursement);
        orden.BeneficiaryIsDebtor.Should().BeTrue();
        orden.Number.Should().StartWith("OP");
        _t.E.Db.TreasuryInboundMessages.Should().ContainSingle(m => m.MessagePublicId == id && m.Outcome == InboundMessageOutcome.Processed);
    }

    [Fact]
    public async Task El_mismo_mensaje_o_la_misma_orden_no_se_duplican()
    {
        var orden = _t.OrdenDeGiro();
        var (_, id) = await RecibirAsync(orden);
        (await Consumir().Handle(new ConsumePaymentOrderCommand([id]), default)).Value.Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>();
        await RecibirAsync(orden with { });
        _t.E.Db.PaymentOrders.Should().HaveCount(1);
    }

    [Fact]
    public async Task Valor_cero_o_negativo_queda_rechazada_al_llegar()
    {
        var (r, _) = await RecibirAsync(_t.OrdenDeGiro(neto: 0m));
        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Treasury.Order.InvalidAmount");
        _t.E.Db.PaymentOrders.Single().Status.Should().Be(PaymentOrderState.RejectedOnArrival);
    }

    [Fact]
    public async Task No_hay_forma_efectivo()
    {
        var (r, _) = await RecibirAsync(_t.OrdenDeGiro(forma: "Cash"));
        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Treasury.Order.InvalidForm");
    }

    [Fact]
    public async Task La_pagada_por_archivo_de_Cartera_queda_lista_sin_validar()
    {
        await RecibirAsync(_t.OrdenDeGiro(porArchivo: true));
        var orden = _t.E.Db.PaymentOrders.Single();
        orden.PaidByLendingFile.Should().BeTrue();
        orden.Status.Should().Be(PaymentOrderState.Ready);
    }

    [Fact]
    public async Task El_regiro_enlaza_la_cadena()
    {
        var primera = _t.OrdenDeGiro();
        await RecibirAsync(primera);
        await RecibirAsync(_t.OrdenDeGiro(reemplaza: primera.OrderPublicId));
        var ordenes = _t.E.Db.PaymentOrders.OrderBy(o => o.Id).ToList();
        ordenes[1].ReplacesOrderId.Should().Be(ordenes[0].Id);
    }

    [Fact]
    public async Task Sin_cuenta_registrada_banco_con_codigo_o_con_lista_vinculante_queda_pendiente()
    {
        var sinRegistro = _t.OrdenDeGiro() with { Payee = _t.OrdenDeGiro().Payee with { BankAccountNumber = "999" } };
        await RecibirAsync(sinRegistro);
        ValidadorDeOrdenes.Motivos(_t.E.Db.PaymentOrders.Single()).Should().Contain("Treasury.Order.DestinationNotRegistered");

        _t.Listas.EvaluarAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoDeListasDeTesoreria([_t.Beneficiaria.Id], []));
        await RecibirAsync(_t.OrdenDeGiro());
        var vinculante = _t.E.Db.PaymentOrders.OrderByDescending(o => o.Id).First();
        vinculante.Status.Should().Be(PaymentOrderState.Pending);
        ValidadorDeOrdenes.Motivos(vinculante).Should().Contain("Treasury.Order.BindingListMatch");
    }

    [Fact]
    public async Task La_cuenta_en_espera_queda_pendiente()
    {
        _t.CuentaDeLaBeneficiaria.PayableFrom = EscenarioDeTesoreriaFechas.Manana;
        _t.E.Db.SaveChanges();
        await RecibirAsync(_t.OrdenDeGiro());
        ValidadorDeOrdenes.Motivos(_t.E.Db.PaymentOrders.Single()).Should().Contain("Treasury.Order.DestinationWaiting");
    }

    [Fact]
    public async Task La_anulacion_anula_en_la_bandeja_y_pide_la_anulacion_si_ya_se_envio()
    {
        var lista = _t.Orden();
        var enviada = _t.Orden(estado: PaymentOrderState.Sent);
        var quien = new UserRefV1 { CentralUserId = Guid.NewGuid(), Name = "Quien anuló" };
        foreach (var o in new[] { lista, enviada })
        {
            var id = _t.Mensaje(new AnulacionDeOrdenDeGiroV1 { OrderPublicId = o.OriginOrderPublicId, OrderNumber = o.OriginOrderNumber, Reason = "error" },
                AnulacionDeOrdenDeGiroV1.Type, 1, quien: quien);
            (await Anular().Handle(new ConsumePaymentOrderVoidCommand([id]), default)).Value.Should().BeOfType<ResultadoDeConsumo.Processed>();
        }
        lista.Status.Should().Be(PaymentOrderState.Voided);
        lista.VoidedByName.Should().Be("Quien anuló");
        enviada.Status.Should().Be(PaymentOrderState.Sent);
        enviada.VoidRequested.Should().BeTrue();
    }

    [Fact]
    public async Task La_anulacion_de_una_orden_desconocida_queda_rechazada()
    {
        var id = _t.Mensaje(new AnulacionDeOrdenDeGiroV1 { OrderPublicId = Guid.NewGuid(), OrderNumber = "X" }, AnulacionDeOrdenDeGiroV1.Type, 1);
        (await Anular().Handle(new ConsumePaymentOrderVoidCommand([id]), default)).Value.Should().BeOfType<ResultadoDeConsumo.Rejected>()
            .Which.Code.Should().Be("Treasury.Order.UnknownOrder");
    }

    [Fact]
    public void El_destino_acepta_las_ordenes_v1_y_v2_y_la_anulacion_y_reparte_por_la_clave()
    {
        var destino = new DestinoTesoreria(Substitute.For<MediatR.ISender>(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DestinoTesoreria>.Instance);
        destino.Destino.Should().Be(IntegrationDestinations.Treasury);
        destino.Acepta(OrdenDeGiroEmitidaV1.Type, 1).Should().BeTrue();
        destino.Acepta(OrdenDeGiroEmitidaV2.Type, 2).Should().BeTrue();
        destino.Acepta(AnulacionDeOrdenDeGiroV1.Type, 1).Should().BeTrue();
        destino.Acepta(PagoReversadoPorCarteraV1.Type, 1).Should().BeFalse("Tesorería no es recaudadora (D2)");
        // T2 (fase 42): la unidad dice de qué módulo viene; lo de Comercial (INV) son sus facturas de proveedor.
        DestinoTesoreria.ComandoDe(new UnidadDeConsumo(IntegrationDestinations.Treasury, Guid.NewGuid(), ClavesDeEvento.Confirmacion, [Guid.NewGuid()], null, 0)
                { OriginModule = "CAR" })
            .Should().BeOfType<ConsumePaymentOrderCommand>();
        DestinoTesoreria.ComandoDe(new UnidadDeConsumo(IntegrationDestinations.Treasury, Guid.NewGuid(), ClavesDeEvento.ConfirmacionPor(Guid.NewGuid()), [Guid.NewGuid()], null, 0)
                { OriginModule = "CAR" })
            .Should().BeOfType<ConsumePaymentOrderVoidCommand>();
        DestinoTesoreria.ComandoDe(new UnidadDeConsumo(IntegrationDestinations.Treasury, Guid.NewGuid(), ClavesDeEvento.Confirmacion, [Guid.NewGuid()], null, 0)
                { OriginModule = "INV" })
            .Should().BeOfType<IngenIA365ERP.Application.Treasury.Payables.ConsumeSupplierInvoiceMessageCommand>();
    }
}

internal static class EscenarioDeTesoreriaFechas
{
    public static DateOnly Manana => Receipts.EscenarioDeRecaudo.Hoy.AddDays(1);
}
