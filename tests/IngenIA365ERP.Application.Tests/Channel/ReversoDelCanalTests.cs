using FluentAssertions;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Payments;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Voidings;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Tests.Receipts.BankItems;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Channel;

/// <summary>
/// Feature 021, US7 (T119; FR-052, FR-053): el reverso de un pago del canal. Antes del corte del destino va por la pasarela y el ERP anula el recibo con su
/// asiento espejo (el dinero sale del medio, sin saldo a favor); después, o si el canal dice que pasó el corte, o si el pago dejó saldo a favor, es una
/// devolución por Tesorería (saldo a favor y orden de pago). Repetirlo no reversa dos veces. Lo que el asociado ve lo prueban el canal (Go) y la PWA.
/// </summary>
public sealed class ReversoDelCanalTests
{
    private const string Ref = "ABC2612010000000R1";
    private static readonly DateTimeOffset Aprobado = new(2026, 12, 1, 10, 41, 22, TimeSpan.FromHours(-5));

    private readonly BancosDePrueba _b = new();
    private readonly ChannelPaymentDestination _destino;
    private readonly ICanalSatelite _canal = Substitute.For<ICanalSatelite>();
    private readonly Application.Common.Interfaces.ICurrentTenantService _cooperativa = Substitute.For<Application.Common.Interfaces.ICurrentTenantService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly Guid _coop = Guid.NewGuid();

    public ReversoDelCanalTests()
    {
        var e = _b.K.C.E;
        e.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "RL", Name = "Recibo en línea", Kind = ReceiptDocumentKind.OnlineReceipt });
        e.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "AN", Name = "Anulación", Kind = ReceiptDocumentKind.Voiding });
        var convenio = new CollectionAgreement
        {
            Code = "AVAL", Name = "Pasarela", Kind = CollectionAgreementKind.Gateway, OwnBankAccountPublicId = _b.Cuenta.PublicId, BranchId = e.OficinaA.Id,
        };
        e.Db.CollectionAgreements.Add(convenio);
        e.Db.SaveChanges();
        _destino = new ChannelPaymentDestination { Name = "Avalpay", CollectionAgreementId = convenio.Id };
        e.Db.ChannelPaymentDestinations.Add(_destino);
        e.Db.SaveChanges();

        _b.K.Obligaciones.ReversarAsync(Arg.Any<SolicitudDeReverso>(), Arg.Any<CancellationToken>())
            .Returns(new RespuestaDelContratoDto<ComprobanteDeReversoDto>(ResultadoDelContrato.Applied, "OK", "", null));
        _cooperativa.TenantId.Returns(_coop.ToString());
        _canal.Configurado.Returns(true);
        _canal.BuscarIntentosAsync(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<IntentoDelCanalDto>>([]));
        _canal.ReversarAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<PedidoDeReversoAlCanal>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result.Success(new ReversoEnPasarelaDto(true, null, ci.ArgAt<PedidoDeReversoAlCanal>(2).Mode, DateTime.UtcNow)));
        _sender.Send(Arg.Any<ReverseChannelPaymentNoticeCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => Interno().Handle(ci.ArgAt<ReverseChannelPaymentNoticeCommand>(0), default));

        // El pago lo aplica el asociado (ActorDeLaPeticion); el reverso lo pide la cajera.
        e.Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.AsociadoPorCanal(_b.K.Asociado.PublicId, "canal", "190.0.0.1"));
        var aviso = new RegisterChannelPaymentCommandHandler(e.Db, new AplicacionDelCanal(e.Db, _b.Aplicacion, _b.K.Obligaciones, _b.K.Cuentas, e.Alertas, e.Reloj))
            .Handle(new RegisterChannelPaymentCommand(_b.K.Asociado.PublicId, Ref, _destino.PublicId, 332_500m, "COP", Aprobado,
                new AprobacionDeLaPasarelaDto(733112, 1598634446, "3324451", "1597317690", "_PSE_", "PSE", "Banco de pruebas"),
                [new ItemDelAvisoDto(ClaveDeConcepto.DeCredito(_b.K.CreditoA), "Due", 332_500m, null)], null)
            { OperationKey = RegisterChannelPaymentCommand.ClaveDeOperacion(Ref) }, default).GetAwaiter().GetResult();
        aviso.IsSuccess.Should().BeTrue(aviso.IsFailure ? aviso.Error.Code : null);
        aviso.Value.Outcome.Should().Be("Applied");
        e.Como(e.Cajera);
        Hora(15);
    }

    private void Hora(int hora, int dia = 1) =>
        _b.K.C.E.Reloj.AhoraLocal.Returns(new DateTimeOffset(2026, 12, dia, hora, 0, 0, TimeSpan.FromHours(-5)));

    private ReverseChannelPaymentNoticeCommandHandler Interno()
    {
        var e = _b.K.C.E;
        return new(e.Db, e.Actor, _b.K.C.Cerrojo, new AplicacionDeAnulacion(e.Db, _b.K.C.Numerador, _b.K.Obligaciones, _b.K.Cuentas, _b.Emision, e.Reloj),
            _b.Ordenes, _b.Emision, new AuditoriaDelCanal(e.Servicios, NullLogger<AuditoriaDelCanal>.Instance), e.Reloj);
    }

    private Task<Result<ChannelPaymentReversalDto>> Reversar(string motivo = "Pago doble") =>
        new ReverseChannelPaymentCommandHandler(_b.K.C.E.Db, _sender, _canal, _cooperativa, _b.K.C.E.Actor, _b.K.C.E.Reloj)
            .Handle(new ReverseChannelPaymentCommand(Ref, motivo), default);

    private Task<Receipt> Recibo() => _b.K.C.E.Db.IssuedReceipts.SingleAsync();

    [Fact]
    public async Task Antes_del_corte_la_pasarela_lo_reversa_y_el_recibo_se_anula_con_su_espejo_sin_saldo_a_favor()
    {
        var r = await Reversar();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Mode.Should().Be(nameof(ModoDeReversoDelCanal.Gateway));
        r.Value.RefundOrders.Should().BeEmpty();
        r.Value.VoidingNumber.Should().NotBeNullOrEmpty();
        (await Recibo()).Status.Should().Be(ReceiptState.Voided);
        var anulacion = await _b.K.C.E.Db.ReceiptVoidings.SingleAsync();
        anulacion.Status.Should().Be(ReceiptVoidingState.Applied);
        anulacion.PayerCreditBalanceId.Should().BeNull("la pasarela devolvió el dinero: el espejo sale del medio");
        anulacion.ApprovedByUserId.Should().Be(_b.K.C.E.Cajera.Id);
        (await _b.K.C.E.Db.PayerCreditBalances.CountAsync()).Should().Be(0);
        _b.K.C.E.Db.IntegrationMessages.Count(m => m.Type == ReciboAnuladoV1.Type).Should().Be(1);
        await _b.K.Obligaciones.Received(1).ReversarAsync(Arg.Any<SolicitudDeReverso>(), Arg.Any<CancellationToken>());
        await _canal.Received(1).ReversarAsync(_coop, Ref, Arg.Is<PedidoDeReversoAlCanal>(p =>
            p.Mode == PedidoDeReversoAlCanal.PorLaPasarela && p.CutoffLocalTime == "20:00" && p.Reason == "Pago doble" && p.RequestedBy == _b.K.C.E.Cajera.PublicId),
            Arg.Any<CancellationToken>());
        await _b.Ordenes.DidNotReceive().EmitirAsync(Arg.Any<SolicitudDeOrdenDePago>(), Arg.Any<CancellationToken>());
        var aviso = await _b.K.C.E.Db.ChannelPaymentNotices.SingleAsync();
        aviso.ReversedAt.Should().NotBeNull();
        aviso.ReversalReason.Should().Be("Pago doble");
    }

    [Fact]
    public async Task Repetirlo_no_reversa_dos_veces()
    {
        var primero = await Reversar();
        var segundo = await Reversar("Otro motivo");

        segundo.IsSuccess.Should().BeTrue();
        segundo.Value.VoidingPublicId.Should().Be(primero.Value.VoidingPublicId);
        (await _b.K.C.E.Db.ReceiptVoidings.CountAsync()).Should().Be(1);
        await _b.K.Obligaciones.Received(1).ReversarAsync(Arg.Any<SolicitudDeReverso>(), Arg.Any<CancellationToken>());
        await _canal.Received(1).ReversarAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<PedidoDeReversoAlCanal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Despues_del_corte_es_una_devolucion_por_Tesoreria_y_el_canal_lo_marca()
    {
        Hora(21);

        var r = await Reversar();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Mode.Should().Be(nameof(ModoDeReversoDelCanal.Treasury));
        r.Value.ChannelUpdated.Should().BeTrue();
        (await Recibo()).Status.Should().Be(ReceiptState.Voided);
        var saldo = await _b.K.C.E.Db.PayerCreditBalances.SingleAsync();
        saldo.Amount.Should().Be(332_500m);
        saldo.Balance.Should().Be(0m, "se devolvió entero con la orden de pago");
        await _b.Ordenes.Received(1).EmitirAsync(Arg.Is<SolicitudDeOrdenDePago>(o =>
            o.NetAmount == 332_500m && o.BeneficiaryPersonPublicId == _b.K.Asociado.PublicId && o.Concept.Contains(Ref)), Arg.Any<CancellationToken>());
        r.Value.RefundOrders.Should().ContainSingle().Which.Amount.Should().Be(332_500m);
        await _canal.DidNotReceive().ReversarAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Is<PedidoDeReversoAlCanal>(p => p.Mode == PedidoDeReversoAlCanal.PorLaPasarela),
            Arg.Any<CancellationToken>());
        await _canal.Received(1).ReversarAsync(_coop, Ref, Arg.Is<PedidoDeReversoAlCanal>(p => p.Mode == PedidoDeReversoAlCanal.PorTesoreria), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_pago_de_otro_dia_va_por_Tesoreria()
    {
        Hora(9, dia: 2);

        var r = await Reversar();

        r.Value.Mode.Should().Be(nameof(ModoDeReversoDelCanal.Treasury));
    }

    [Fact]
    public async Task Si_el_canal_dice_que_ya_paso_el_corte_sigue_por_Tesoreria()
    {
        _canal.ReversarAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Is<PedidoDeReversoAlCanal>(p => p.Mode == PedidoDeReversoAlCanal.PorLaPasarela), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ReversoEnPasarelaDto>(ErroresDelReversoDelCanal.FueraDeCorte, "Ya pasó el corte."));

        var r = await Reversar();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Mode.Should().Be(nameof(ModoDeReversoDelCanal.Treasury));
        await _b.Ordenes.Received(1).EmitirAsync(Arg.Any<SolicitudDeOrdenDePago>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_la_pasarela_no_lo_reversa_no_cambia_nada()
    {
        _canal.ReversarAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<PedidoDeReversoAlCanal>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ReversoEnPasarelaDto(false, "Transacción ya compensada")));

        var r = await Reversar();

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDelReversoDelCanal.RechazadoPorLaPasarela);
        r.Error.Message.Should().Contain("Transacción ya compensada");
        (await Recibo()).Status.Should().Be(ReceiptState.Valid);
        (await _b.K.C.E.Db.ChannelPaymentNotices.SingleAsync()).ReversedAt.Should().BeNull();
    }

    [Fact]
    public async Task Con_saldo_a_favor_del_pago_va_por_Tesoreria_y_devuelve_tambien_ese_saldo()
    {
        var recibo = await Recibo();
        _b.K.C.E.Db.PayerCreditBalances.Add(new PayerCreditBalance
        {
            PersonId = recibo.PayerPersonId, Origin = PayerCreditBalanceOrigin.NonInteractiveSurplus, OriginDocumentPublicId = recibo.PublicId,
            OriginDocumentNumber = recibo.Number, BranchId = recibo.BranchId, Amount = 10_000m, Balance = 10_000m, OpenedOn = EscenarioDeRecaudo.Hoy,
        });
        await _b.K.C.E.Db.SaveChangesAsync();

        var r = await Reversar();

        r.Value.Mode.Should().Be(nameof(ModoDeReversoDelCanal.Treasury));
        await _canal.DidNotReceive().ReversarAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Is<PedidoDeReversoAlCanal>(p => p.Mode == PedidoDeReversoAlCanal.PorLaPasarela),
            Arg.Any<CancellationToken>());
        await _b.Ordenes.Received(2).EmitirAsync(Arg.Any<SolicitudDeOrdenDePago>(), Arg.Any<CancellationToken>());
        (await _b.K.C.E.Db.PayerCreditBalances.SumAsync(s => s.Balance)).Should().Be(0m);
    }

    [Fact]
    public async Task Lo_que_la_pasarela_ya_reverso_en_un_intento_cortado_se_termina_por_la_pasarela_aunque_haya_pasado_el_corte()
    {
        Hora(22);
        _canal.BuscarIntentosAsync(_coop, null, null, Ref, Arg.Any<CancellationToken>()).Returns(Result.Success<IReadOnlyList<IntentoDelCanalDto>>(
            [new IntentoDelCanalDto(Ref, _b.K.Asociado.PublicId, "approved", 332_500m, DateTime.UtcNow, DateTime.UtcNow, "3324451", "PSE", "applied", null,
                ReversedAt: DateTime.UtcNow, ReversalKind: PedidoDeReversoAlCanal.PorLaPasarela)]));

        var r = await Reversar();

        r.Value.Mode.Should().Be(nameof(ModoDeReversoDelCanal.Gateway));
        await _b.Ordenes.DidNotReceive().EmitirAsync(Arg.Any<SolicitudDeOrdenDePago>(), Arg.Any<CancellationToken>());
        await _canal.DidNotReceive().ReversarAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<PedidoDeReversoAlCanal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_referencia_desconocida_no_existe()
    {
        var r = await new ReverseChannelPaymentCommandHandler(_b.K.C.E.Db, _sender, _canal, _cooperativa, _b.K.C.E.Actor, _b.K.C.E.Reloj)
            .Handle(new ReverseChannelPaymentCommand("ZZZ2612010000000Z9", "x"), default);

        r.Error.Code.Should().Be(ErroresDelReversoDelCanal.NoExiste);
    }
}
