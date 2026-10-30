using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Integration;
using IngenIA365ERP.Application.Receipts.Reversals;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Reversals;

/// <summary>
/// Feature 015, US5, T0171 (mensajes.md §3.1; FR-059, FR-062, FR-063, FR-195): la porción de un recibo vigente que Cartera reversó pasa a reversada y su aplicación a
/// <c>ReversedByModule</c> con <c>RCD_ModuleReversals</c>; sin causa queda por confirmar y una persona la confirma (dinero recibido: saldo a favor; no recibido: baja
/// del medio); alerta <c>Recaudo.AplicacionReversadaPorModulo</c>; repetido, sin efecto; clave desconocida, <c>Receipts.Reversal.UnknownApplication</c>; de otro
/// recaudador, <c>.NotOurs</c>; lo de Nómina, <c>Receipts.Voiding.PayrollOwned</c>; nunca devuelve ni reaplica solo. Prueba escrita, sin correr.
/// </summary>
public class ReversosDeModulosTests
{
    private readonly CajaConContratos _k = new();
    private readonly IMensajesEntrantes _entrantes = Substitute.For<IMensajesEntrantes>();

    private ConsumeLendingPaymentReversalCommandHandler Consumir() => new(_k.C.E.Db, _entrantes, _k.C.Cerrojo, _k.C.E.Alertas, _k.C.E.Reloj);

    private ConfirmModuleReversalCauseCommandHandler Confirmar() => new(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.Cerrojo, _k.C.Emision,
        new AuditoriaDeRecaudo(_k.C.E.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance), _k.C.E.Reloj);

    private async Task<ReceiptDto> ReciboAsync()
    {
        await _k.AbrirAsync();
        var op = await _k.PreparadaAsync([_k.Cuota(_k.CreditoA, 100_000)], [_k.Efectivo(100_000)]);
        return (await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default)).Value;
    }

    private Guid Aviso(string clave, string recaudador = ClavesDeRecaudo.Codigo, string clase = "Receipts")
    {
        var id = Guid.NewGuid();
        var contenido = new PagoReversadoPorCarteraV1
        {
            Loan = new LoanRefV1 { LoanPublicId = _k.CreditoA, LoanNumber = "CR-1" }, ReversalOperationPublicId = Guid.NewGuid(), ReversalNumber = "REV-1",
            Collector = new CollectorRefV1 { CollectorCode = recaudador, CollectorClass = clase, IdempotencyKey = clave, MeansClass = "Cash" },
            ReversedAmount = 100_000, ValueDate = EscenarioDeRecaudo.Hoy, Reason = "Pago mal aplicado", ReversedBy = new UserRefV1 { Name = "Cartera" },
        };
        var sobre = new IntegrationEnvelopeV1
        {
            MessageId = id, Type = PagoReversadoPorCarteraV1.Type, Version = 1, Kind = IntegrationMessageKind.Business, OriginModule = "CAR",
            BranchPublicId = _k.C.E.OficinaA.PublicId, OriginUser = new UserRefV1 { Name = "Cartera" },
        };
        _entrantes.LeerAsync(Arg.Is<IReadOnlyCollection<Guid>>(l => l.Contains(id)), IntegrationDestinations.Receipts, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MensajeEntrante>>(
            [
                new MensajeEntrante(sobre, contenido, "{}", "sha", null,
                    new EntregaEntrante(IntegrationDestinations.Receipts, DeliveryMode.Always, DeliveryStatus.Pending, null, null, null, 0, null)),
            ]));
        return id;
    }

    [Fact]
    public async Task El_aviso_reversa_la_porcion_y_queda_por_confirmar_con_alerta()
    {
        await ReciboAsync();
        var porcion = _k.C.E.Db.ReceiptApplicationPortions.Single();

        var r = await Consumir().Handle(new ConsumeLendingPaymentReversalCommand([Aviso(porcion.IdempotencyKey)]), default);

        r.Value.Should().BeOfType<ResultadoDeConsumo.Processed>();
        porcion.Status.Should().Be(PortionState.Reversed);
        _k.C.E.Db.ReceiptApplications.Single().Status.Should().Be(ApplicationState.ReversedByModule);
        var reverso = _k.C.E.Db.ModuleReversals.Single();
        reverso.Cause.Should().Be(ModuleReversalCause.PendingConfirmation);
        _k.C.E.Db.PayerCreditBalances.Should().BeEmpty("nunca devuelve ni reaplica solo");
        await _k.C.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoAplicacionReversadaPorModulo), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repetido_sin_efecto_y_las_claves_ajenas_se_rechazan()
    {
        await ReciboAsync();
        var porcion = _k.C.E.Db.ReceiptApplicationPortions.Single();
        var id = Aviso(porcion.IdempotencyKey);
        await Consumir().Handle(new ConsumeLendingPaymentReversalCommand([id]), default);

        (await Consumir().Handle(new ConsumeLendingPaymentReversalCommand([id]), default)).Value.Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>();
        _k.C.E.Db.ModuleReversals.Should().ContainSingle();

        ((ResultadoDeConsumo.Rejected)(await Consumir().Handle(new ConsumeLendingPaymentReversalCommand([Aviso("REC:no-existe")]), default)).Value)
            .Code.Should().Be(ErroresDeRecaudo.CodigoAplicacionDesconocida);
        ((ResultadoDeConsumo.Rejected)(await Consumir().Handle(new ConsumeLendingPaymentReversalCommand([Aviso("TES:x", "TES", "Treasury")]), default)).Value)
            .Code.Should().Be(ErroresDeRecaudo.CodigoDeOtroRecaudador);
        ((ResultadoDeConsumo.Rejected)(await Consumir().Handle(new ConsumeLendingPaymentReversalCommand([Aviso("NOM:x", "NOM", "Payroll")]), default)).Value)
            .Code.Should().Be(ErroresDeRecaudo.CodigoDeNomina);
    }

    [Fact]
    public async Task Dinero_recibido_deja_saldo_a_favor_y_no_recibido_da_de_baja_el_medio()
    {
        await ReciboAsync();
        var porcion = _k.C.E.Db.ReceiptApplicationPortions.Single();
        await Consumir().Handle(new ConsumeLendingPaymentReversalCommand([Aviso(porcion.IdempotencyKey)]), default);
        var reverso = _k.C.E.Db.ModuleReversals.Single();
        _k.C.E.Como(_k.C.E.Supervisor);

        var r = await Confirmar().Handle(new ConfirmModuleReversalCauseCommand(reverso.PublicId, ModuleReversalCause.MoneyReceived, "Visto en el arqueo")
            { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        _k.C.E.Db.PayerCreditBalances.Single().Origin.Should().Be(PayerCreditBalanceOrigin.ModuleReversal);
        _k.C.E.Db.IntegrationMessages.Count(m => m.Type == AplicacionReversadaPorModuloV1.Type).Should().Be(1);
        (await Confirmar().Handle(new ConfirmModuleReversalCauseCommand(reverso.PublicId, ModuleReversalCause.MoneyNotReceived, "otra vez")
            { OperationKey = Guid.NewGuid() }, default)).Error.Code.Should().Be("Receipts.Reversal.CauseAlreadyConfirmed");
    }
}
