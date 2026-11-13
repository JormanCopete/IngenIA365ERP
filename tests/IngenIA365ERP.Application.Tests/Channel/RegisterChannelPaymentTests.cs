using FluentAssertions;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Tests.Receipts.BankItems;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Channel;

/// <summary>
/// Feature 021, US5 (T104): el aviso de un pago aprobado entra como recibo «en línea» de Recaudo, por los contratos, a nombre del asociado. Un pago aprobado
/// nunca se rechaza: lo que no se aplica queda a favor del asociado y el aviso sale en revisión (FR-046). Sobre el escenario de Recaudo (contratos simulados).
/// </summary>
public sealed class RegisterChannelPaymentTests
{
    private readonly BancosDePrueba _b = new();
    private readonly ChannelPaymentDestination _destino;
    private readonly CollectionAgreement _convenio;

    public RegisterChannelPaymentTests()
    {
        var e = _b.K.C.E;
        e.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "RL", Name = "Recibo en línea", Kind = ReceiptDocumentKind.OnlineReceipt });
        _convenio = new CollectionAgreement
        {
            Code = "AVAL", Name = "Pasarela", Kind = CollectionAgreementKind.Gateway, OwnBankAccountPublicId = _b.Cuenta.PublicId, BranchId = e.OficinaA.Id,
        };
        e.Db.CollectionAgreements.Add(_convenio);
        e.Db.SaveChanges();
        _destino = new ChannelPaymentDestination { Name = "Avalpay", CollectionAgreementId = _convenio.Id };
        e.Db.ChannelPaymentDestinations.Add(_destino);
        e.Db.SaveChanges();
        // El actor lo pone ActorDeLaPeticion: el asociado que pagó, sin usuario del ERP.
        e.Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.AsociadoPorCanal(_b.K.Asociado.PublicId, "canal", "190.0.0.1"));
    }

    private RegisterChannelPaymentCommandHandler Handler() =>
        new(_b.K.C.E.Db, _b.Aplicacion, _b.K.Obligaciones, _b.K.Cuentas, _b.K.C.E.Alertas, _b.K.C.E.Reloj);

    private RegisterChannelPaymentCommand Aviso(string referencia, params ItemDelAvisoDto[] items) =>
        new(_b.K.Asociado.PublicId, referencia, _destino.PublicId, items.Sum(i => i.Amount), "COP",
            new DateTimeOffset(2026, 12, 1, 10, 41, 22, TimeSpan.FromHours(-5)),
            new AprobacionDeLaPasarelaDto(733112, 1598634446, "3324451", "1597317690", "_PSE_", "PSE", "Banco de pruebas"), items,
            new PagadorDelAvisoDto("190.0.0.1", "Navegador", Guid.NewGuid()))
        { OperationKey = RegisterChannelPaymentCommand.ClaveDeOperacion(referencia) };

    private ItemDelAvisoDto Credito(Guid credito, string modalidad, decimal valor, string? eleccion = null) =>
        new(ClaveDeConcepto.DeCredito(credito), modalidad, valor, eleccion);

    private ItemDelAvisoDto Aportes(decimal valor) => new(ClaveDeConcepto.DeCuenta(_b.K.Aportes), "Due", valor, null);

    [Fact]
    public void Es_una_escritura_idempotente_de_Recaudo()
    {
        // Recaudo inactivo lo responde ModuloActivoBehavior (422) y el canal reintenta; repetir la referencia es la misma operación.
        typeof(RegisterChannelPaymentCommand).Should().Implement<IEscrituraDeRecaudo>().And.Implement<IOperacionIdempotente>();
        RegisterChannelPaymentCommand.ClaveDeOperacion("ABC1").Should().Be(RegisterChannelPaymentCommand.ClaveDeOperacion("ABC1"))
            .And.NotBe(RegisterChannelPaymentCommand.ClaveDeOperacion("ABC2"));
    }

    [Fact]
    public async Task Aplica_por_el_contrato_como_recibo_en_linea_a_nombre_del_asociado()
    {
        var r = await Handler().Handle(Aviso("ABC2612010000000A1", Credito(_b.K.CreditoA, "Due", 332_500m)), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Outcome.Should().Be("Applied");
        var recibo = await _b.K.C.E.Db.IssuedReceipts.SingleAsync(x => x.PublicId == r.Value.ReceiptPublicId);
        recibo.Channel.Should().Be(ReceiptChannel.Online);
        recibo.CashierUserId.Should().BeNull();
        recibo.TotalMeans.Should().Be(332_500m);
        recibo.OwnBankAccountPublicId.Should().Be(_b.Cuenta.PublicId);
        recibo.OperationKey.Should().Be(IngenIA365ERP.Application.Receipts.BankItems.AplicacionNoInteractiva.ClaveDelRecibo(
            RegisterChannelPaymentCommandHandler.LlaveDelRecibo(_convenio.PublicId, "ABC2612010000000A1")));
        await _b.K.Obligaciones.Received(1).AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s =>
            s.Form == FormaDeAplicacion.NonInteractive && s.Payments.Count == 1 && s.Payments[0].Modality == ModalidadDePago.PastDue
            && s.Payments[0].ValueDate == new DateOnly(2026, 12, 1) && s.Payments[0].PayerPublicId == _b.K.Asociado.PublicId), Arg.Any<CancellationToken>());
        var aviso = await _b.K.C.E.Db.ChannelPaymentNotices.Include(n => n.Items).SingleAsync();
        aviso.Outcome.Should().Be(NoticeOutcome.Applied);
        aviso.ReceiptId.Should().Be(recibo.Id);
        aviso.Authorization.Should().Be("3324451");
        aviso.Items.Should().ContainSingle(i => i.ConceptKind == ConceptKind.Loan && i.Modality == "Due");
    }

    [Fact]
    public async Task Repetir_el_aviso_no_cambia_nada_y_da_la_misma_respuesta()
    {
        var aviso = Aviso("ABC2612010000000B2", Aportes(50_000m));
        var primero = await Handler().Handle(aviso, default);
        var segundo = await Handler().Handle(aviso, default);

        segundo.Value.Should().Be(primero.Value);
        (await _b.K.C.E.Db.ChannelPaymentNotices.CountAsync()).Should().Be(1);
        (await _b.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(1);
        await _b.K.Cuentas.Received(1).ConsignarAsync(Arg.Any<SolicitudDeConsignacion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_la_suma_no_cuadra_no_aplica_nada()
    {
        var aviso = Aviso("ABC2612010000000C3", Aportes(50_000m)) with { Total = 60_000m };
        var r = await Handler().Handle(aviso, default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(RegisterChannelPaymentCommandHandler.TotalNoCuadra);
        (await _b.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_obligacion_cancelada_deja_el_valor_a_favor_y_el_aviso_en_revision()
    {
        _b.K.Obligaciones.SimularAsync(Arg.Is<SolicitudDeSimulacion>(s => s.ObligationPublicId == _b.K.CreditoB), Arg.Any<CancellationToken>())
            .Returns(new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Rejected, "Lending.Payment.ObligationNotPayable", "El crédito está cancelado.", null));

        var r = await Handler().Handle(Aviso("ABC2612010000000D4", Credito(_b.K.CreditoB, "Due", 100_000m), Aportes(50_000m)), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Outcome.Should().Be("InReview");
        r.Value.Reason.Should().Contain("cancelado");
        var recibo = await _b.K.C.E.Db.IssuedReceipts.SingleAsync();
        recibo.TotalApplied.Should().Be(50_000m);
        recibo.TotalMeans.Should().Be(150_000m);
        (await _b.K.C.E.Db.PayerCreditBalances.SingleAsync()).Amount.Should().Be(100_000m);
        await _b.K.Obligaciones.DidNotReceive().AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>());
        await _b.K.C.E.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.CanalPagoEnRevision),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_ningun_producto_que_admita_el_pago_queda_en_revision_sin_recibo()
    {
        _b.K.Obligaciones.SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>())
            .Returns(new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Rejected, "Lending.Payment.ObligationNotPayable", "Cancelado.", null));

        var r = await Handler().Handle(Aviso("ABC2612010000000E5", Credito(_b.K.CreditoA, "Payoff", 5_450_000m)), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Outcome.Should().Be("InReview");
        r.Value.ReceiptPublicId.Should().BeNull();
        (await _b.K.C.E.Db.ChannelPaymentNotices.SingleAsync()).Outcome.Should().Be(NoticeOutcome.InReview);
    }

    [Fact]
    public async Task Lo_que_sobra_del_exigible_va_como_anticipo_a_las_proximas_cuotas()
    {
        _b.K.Obligaciones.SimularAsync(Arg.Is<SolicitudDeSimulacion>(s => s.Modality == ModalidadDePago.PastDue && s.Amount == 350_000m),
                Arg.Any<CancellationToken>())
            .Returns(ci => new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Answered, "OK", "",
                CajaConContratos.Simulacion(ci.Arg<SolicitudDeSimulacion>(), null, []) with { Surplus = 17_500m }));

        var r = await Handler().Handle(Aviso("ABC2612010000000F6", Credito(_b.K.CreditoA, "Due", 350_000m)), default);

        r.Value.Outcome.Should().Be("Applied");
        await _b.K.Obligaciones.Received(1).AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s =>
            s.Payments.Count == 2
            && s.Payments.Any(p => p.Modality == ModalidadDePago.PastDue && p.Amount == 332_500m)
            && s.Payments.Any(p => p.Modality == ModalidadDePago.Advance && p.Amount == 17_500m)), Arg.Any<CancellationToken>());
        (await _b.K.C.E.Db.PayerCreditBalances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_abono_a_capital_lleva_la_eleccion_del_asociado()
    {
        var r = await Handler().Handle(Aviso("ABC2612010000000G7", Credito(_b.K.CreditoA, "Prepayment", 1_000_000m, "ReduceInstallment")), default);

        r.Value.Outcome.Should().Be("Applied");
        await _b.K.Obligaciones.Received(1).AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s =>
            s.Payments[0].Modality == ModalidadDePago.ExtraordinaryPrincipal && s.Payments[0].Choice == EleccionDeAbono.ReduceInstallment),
            Arg.Any<CancellationToken>());
        (await _b.K.C.E.Db.ReceiptApplications.SingleAsync()).PrepaymentChoice.Should().Be((int)EleccionDeAbono.ReduceInstallment);
    }

    [Fact]
    public async Task Credito_y_aportes_en_un_solo_recibo_obligaciones_primero()
    {
        var orden = new List<string>();
        _b.K.Obligaciones.When(o => o.AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>())).Do(_ => orden.Add("credito"));
        _b.K.Cuentas.When(c => c.ConsignarAsync(Arg.Any<SolicitudDeConsignacion>(), Arg.Any<CancellationToken>())).Do(_ => orden.Add("cuenta"));

        var r = await Handler().Handle(Aviso("ABC2612010000000H8", Aportes(50_000m), Credito(_b.K.CreditoA, "Due", 332_500m)), default);

        r.Value.Outcome.Should().Be("Applied");
        orden.Should().Equal("credito", "cuenta");
        (await _b.K.C.E.Db.ReceiptApplications.CountAsync()).Should().Be(2);
        (await _b.K.C.E.Db.IssuedReceipts.SingleAsync()).TotalApplied.Should().Be(382_500m);
    }

    [Fact]
    public async Task El_comprobante_solo_lo_ve_la_persona_del_aviso()
    {
        var r = await Handler().Handle(Aviso("ABC2612010000000J9", Aportes(50_000m)), default);
        var auditoria = new AuditoriaDelCanal(_b.K.C.E.Servicios, NullLogger<AuditoriaDelCanal>.Instance);

        var otra = await new GetChannelReceiptDocumentQueryHandler(_b.K.C.E.Db, auditoria)
            .Handle(new GetChannelReceiptDocumentQuery(Guid.NewGuid(), r.Value.ReceiptPublicId!.Value), default);
        otra.IsFailure.Should().BeTrue();
        otra.Error.Code.Should().Be(GetChannelReceiptDocumentQueryHandler.NoEncontrado);

        var suya = await new GetChannelReceiptDocumentQueryHandler(_b.K.C.E.Db, auditoria)
            .Handle(new GetChannelReceiptDocumentQuery(_b.K.Asociado.PublicId, r.Value.ReceiptPublicId!.Value), default);
        suya.IsSuccess.Should().BeTrue(suya.IsFailure ? suya.Error.Code : null);
        suya.Value.Number.Should().Be(r.Value.ReceiptNumber);
    }
}
