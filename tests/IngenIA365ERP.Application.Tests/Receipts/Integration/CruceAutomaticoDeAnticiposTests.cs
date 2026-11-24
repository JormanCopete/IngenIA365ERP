using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Integration;
using IngenIA365ERP.Application.Tests.Receipts.BankItems;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Integration;

/// <summary>
/// 2026-11-23 (decisión 8a del dueño): el cruce automático del anticipo de concepto cuando Cartera crea la obligación que cubre (<c>ObligacionCreada</c>).
/// <list type="bullet">
/// <item>cruza la cuota del ciclo con el medio del anticipo, hasta lo que la cuota admite, y baja el anticipo de lo más antiguo a lo más nuevo;</item>
/// <item>si el anticipo no alcanza, abona lo disponible;</item>
/// <item>sin concepto que cubra la línea, sin anticipo o con la obligación rechazada, no hace nada;</item>
/// <item>sin tipo de documento «Cruce de anticipo», alerta y no toca el anticipo;</item>
/// <item>el aviso repetido no cruza dos veces;</item>
/// <item>si Cartera reversa el pago, el valor vuelve al anticipo sin que nadie confirme.</item>
/// </list>
/// </summary>
public sealed class CruceAutomaticoDeAnticiposTests
{
    private const string Linea = "ADMVEH";
    private const decimal Cuota = 150_000m;

    private readonly BancosDePrueba _b = new();
    private readonly IMensajesEntrantes _entrantes = Substitute.For<IMensajesEntrantes>();
    private readonly PaymentMeans _medio;
    private readonly MiscConcept _administracion;
    private readonly Guid _facturo = Guid.NewGuid();

    public CruceAutomaticoDeAnticiposTests()
    {
        var e = _b.K.C.E;
        _medio = e.Medio("ANT", PaymentMeansClass.Other, 0, CashCountMethod.None);
        _administracion = new MiscConcept
        {
            Code = "ANTADM", Name = "Anticipo de administración", Direction = MiscConceptDirection.Income, IsAdvance = true, AdvanceMeansId = _medio.Id,
            AdvanceCreditLineCode = Linea,
        };
        e.Db.MiscConcepts.Add(_administracion);
        e.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "CA", Name = "Cruce de anticipo", Kind = ReceiptDocumentKind.AdvanceCrossingReceipt });
        e.Db.SaveChanges();

        _b.K.Busqueda.PorNumeroAsync(Arg.Any<BusquedaPorNumero>(), Arg.Any<CancellationToken>()).Returns(
            [new ObligacionEncontradaDto(ClaseDeObligacionEncontrada.Loan, _b.K.CreditoA, "CR-9", _b.K.Asociado.PublicId, "Ana Asociada", "2001", "Active")]);
        // La cuota del ciclo admite 150.000: lo demás sobra.
        _b.K.Obligaciones.SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeSimulacion>();
            var sobra = Math.Max(0m, s.Amount - Cuota);
            return new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Answered, "OK", "",
                CajaConContratos.Simulacion(s, null, []) with { AppliedAmount = s.Amount - sobra, Surplus = sobra });
        });
    }

    private ConsumeObligationCreatedCommandHandler Consumir() => new(_b.K.C.E.Db, _entrantes, _b.K.C.Cerrojo, _b.K.Obligaciones, _b.K.Busqueda, _b.Aplicacion,
        _b.K.C.E.Alertas, _b.K.C.E.Reloj);

    private PayerCreditBalance Anticipo(decimal valor, int diasAtras)
    {
        var saldo = new PayerCreditBalance
        {
            PersonId = _b.K.Asociado.Id, Origin = PayerCreditBalanceOrigin.ConceptAdvance, OriginDocumentPublicId = Guid.NewGuid(), OriginDocumentNumber = $"RC-{diasAtras}",
            BranchId = _b.K.C.E.OficinaA.Id, Amount = valor, Balance = valor, OpenedOn = EscenarioDeRecaudo.Hoy.AddDays(-diasAtras), MiscConceptId = _administracion.Id,
        };
        _b.K.C.E.Db.PayerCreditBalances.Add(saldo);
        _b.K.C.E.Db.SaveChanges();
        return saldo;
    }

    private Guid Aviso(string linea = Linea, string estado = ConsumeObligationCreatedCommandHandler.EstadoValidada, Guid? id = null, bool conUsuario = true)
    {
        var mensaje = id ?? Guid.NewGuid();
        var contenido = new ObligacionCreadaV1
        {
            Loan = new LoanRefV1 { LoanPublicId = _b.K.CreditoA, LoanNumber = "CR-9" }, CreditLineCode = linea, Amount = Cuota, OriginDate = EscenarioDeRecaudo.Hoy,
            Status = estado, CreditPaymentPublicId = Guid.NewGuid(),
        };
        var sobre = new IntegrationEnvelopeV1
        {
            MessageId = mensaje, Type = ObligacionCreadaV1.Type, Version = 1, Kind = IntegrationMessageKind.Business, OriginModule = "CAR",
            BranchPublicId = _b.K.C.E.OficinaA.PublicId,
            OriginUser = new UserRefV1 { CentralUserId = conUsuario ? _facturo : null, Name = "Quien facturó" },
        };
        _entrantes.LeerAsync(Arg.Is<IReadOnlyCollection<Guid>>(l => l.Contains(mensaje)), IntegrationDestinations.Receipts, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MensajeEntrante>>(
            [
                new MensajeEntrante(sobre, contenido, "{}", "sha", null,
                    new EntregaEntrante(IntegrationDestinations.Receipts, DeliveryMode.Always, DeliveryStatus.Pending, null, null, null, 0, null)),
            ]));
        return mensaje;
    }

    private async Task<ResultadoDeConsumo> ConsumirAsync(Guid mensaje)
    {
        var r = await Consumir().Handle(new ConsumeObligationCreatedCommand([mensaje]), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        return r.Value;
    }

    [Fact]
    public async Task Cruza_la_cuota_con_el_medio_del_anticipo_y_lo_baja_de_lo_mas_antiguo_a_lo_mas_nuevo()
    {
        var viejo = Anticipo(100_000m, 20);
        var nuevo = Anticipo(100_000m, 5);

        (await ConsumirAsync(Aviso())).Should().BeOfType<ResultadoDeConsumo.Processed>();

        var recibo = await _b.K.C.E.Db.IssuedReceipts.SingleAsync();
        (recibo.Channel, recibo.TotalApplied, recibo.PayerPersonId).Should().Be((ReceiptChannel.AdvanceCrossing, Cuota, _b.K.Asociado.Id));
        (await _b.K.C.E.Db.ReceiptDocumentTypes.SingleAsync(t => t.Id == recibo.DocumentTypeId)).Kind.Should().Be(ReceiptDocumentKind.AdvanceCrossingReceipt);
        (await _b.K.C.E.Db.ReceiptMeans.SingleAsync()).PaymentMeansId.Should().Be(_medio.Id);
        await _b.K.Obligaciones.Received().AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s => s.Payments.Single().Amount == Cuota
            && s.Payments.Single().Modality == ModalidadDePago.CycleInstallment && s.Payments.Single().ObligationPublicId == _b.K.CreditoA
            && s.Payments.Single().IdempotencyKey == ConsumeObligationCreatedCommandHandler.Clave(_b.K.CreditoA, _administracion.PublicId)), Arg.Any<CancellationToken>());

        (viejo.Balance, nuevo.Balance).Should().Be((0m, 50_000m), "se baja de lo más antiguo a lo más nuevo");
        _b.K.C.E.Db.PayerCreditBalances.Count().Should().Be(2, "no nace un saldo a favor genérico");
        (await _b.K.C.E.Db.ReceiptsInboundMessages.SingleAsync()).Outcome.Should().Be(InboundMessageOutcome.Processed);
    }

    [Fact]
    public async Task Si_el_anticipo_no_alcanza_abona_lo_disponible()
    {
        var saldo = Anticipo(60_000m, 3);

        await ConsumirAsync(Aviso());

        (await _b.K.C.E.Db.IssuedReceipts.SingleAsync()).TotalApplied.Should().Be(60_000m);
        saldo.Balance.Should().Be(0m);
    }

    [Fact]
    public async Task Sin_concepto_que_cubra_la_linea_sin_anticipo_o_con_la_obligacion_rechazada_no_hace_nada()
    {
        await ConsumirAsync(Aviso(linea: "OTRA"));
        await ConsumirAsync(Aviso());
        Anticipo(100_000m, 3);
        await ConsumirAsync(Aviso(estado: "ValidationFailed"));

        _b.K.C.E.Db.IssuedReceipts.Should().BeEmpty();
        _b.K.C.E.Db.ReceiptsInboundMessages.Count().Should().Be(3);
        await _b.K.C.E.Alertas.DidNotReceive().LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_tipo_de_documento_o_sin_quien_facturo_alerta_y_no_toca_el_anticipo()
    {
        var saldo = Anticipo(100_000m, 3);
        await ConsumirAsync(Aviso(conUsuario: false));

        _b.K.C.E.Db.ReceiptDocumentTypes.Remove(_b.K.C.E.Db.ReceiptDocumentTypes.Single(t => t.Kind == ReceiptDocumentKind.AdvanceCrossingReceipt));
        _b.K.C.E.Db.SaveChanges();
        await ConsumirAsync(Aviso());

        _b.K.C.E.Db.IssuedReceipts.Should().BeEmpty();
        saldo.Balance.Should().Be(100_000m);
        await _b.K.C.E.Alertas.Received(2).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoCruceDeAnticipoPendiente),
            Arg.Any<CancellationToken>());
        _b.K.C.E.Db.ReceiptsInboundMessages.Should().OnlyContain(r => r.Code == ConsumeObligationCreatedCommandHandler.CodigoPendiente);
    }

    [Fact]
    public async Task El_aviso_repetido_no_cruza_dos_veces()
    {
        var saldo = Anticipo(400_000m, 3);
        var aviso = Aviso();

        await ConsumirAsync(aviso);
        (await ConsumirAsync(aviso)).Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>();
        await ConsumirAsync(Aviso());

        (await _b.K.C.E.Db.IssuedReceipts.SingleAsync()).TotalApplied.Should().Be(Cuota, "la clave del recibo es la del crédito y el concepto");
        saldo.Balance.Should().Be(400_000m - Cuota);
    }

    [Fact]
    public async Task Si_Cartera_reversa_el_pago_el_valor_vuelve_al_anticipo_sin_que_nadie_confirme()
    {
        var saldo = Anticipo(200_000m, 3);
        await ConsumirAsync(Aviso());
        saldo.Balance.Should().Be(50_000m);
        var porcion = await _b.K.C.E.Db.ReceiptApplicationPortions.SingleAsync();

        var reverso = Guid.NewGuid();
        var contenido = new PagoReversadoPorCarteraV1
        {
            Loan = new LoanRefV1 { LoanPublicId = _b.K.CreditoA, LoanNumber = "CR-9" }, ReversalOperationPublicId = Guid.NewGuid(), ReversalNumber = "REV-1",
            Collector = new CollectorRefV1 { CollectorCode = Application.Receipts.Common.ClavesDeRecaudo.Codigo, CollectorClass = "Receipts",
                IdempotencyKey = porcion.IdempotencyKey, MeansClass = "Other" },
            ReversedAmount = Cuota, ValueDate = EscenarioDeRecaudo.Hoy, Reason = "Factura anulada", ReversedBy = new UserRefV1 { Name = "Cartera" },
        };
        var sobre = new IntegrationEnvelopeV1
        {
            MessageId = reverso, Type = PagoReversadoPorCarteraV1.Type, Version = 1, Kind = IntegrationMessageKind.Business, OriginModule = "CAR",
            BranchPublicId = _b.K.C.E.OficinaA.PublicId, OriginUser = new UserRefV1 { CentralUserId = Guid.NewGuid(), Name = "Quien anuló" },
        };
        _entrantes.LeerAsync(Arg.Is<IReadOnlyCollection<Guid>>(l => l.Contains(reverso)), IntegrationDestinations.Receipts, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MensajeEntrante>>(
            [
                new MensajeEntrante(sobre, contenido, "{}", "sha", null,
                    new EntregaEntrante(IntegrationDestinations.Receipts, DeliveryMode.Always, DeliveryStatus.Pending, null, null, null, 0, null)),
            ]));

        var r = await new ConsumeLendingPaymentReversalCommandHandler(_b.K.C.E.Db, _entrantes, _b.K.C.Cerrojo, _b.K.C.E.Alertas, _b.K.C.E.Reloj, _b.Emision)
            .Handle(new ConsumeLendingPaymentReversalCommand([reverso]), default);

        r.Value.Should().BeOfType<ResultadoDeConsumo.Processed>();
        (await _b.K.C.E.Db.ModuleReversals.SingleAsync()).Cause.Should().Be(ModuleReversalCause.ReturnedToAdvance);
        saldo.Balance.Should().Be(200_000m, "lo reversado vuelve al anticipo");
        _b.K.C.E.Db.PayerCreditBalances.Count().Should().Be(1, "no nace un saldo a favor genérico");
        _b.K.C.E.Db.PayerCreditBalanceMovements.Should().ContainSingle(m => m.Kind == PayerCreditBalanceMovementKind.ReturnedByModuleReversal && m.Amount == Cuota);
        await _b.K.C.E.Alertas.DidNotReceive().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoAplicacionReversadaPorModulo),
            Arg.Any<CancellationToken>());
    }
}
