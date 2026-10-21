using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.Associates.Commands.UpdateAssociate;
using IngenIA365ERP.Application.Core.Associates.Integration;
using IngenIA365ERP.Application.Core.People.Commands.DeletePerson;
using IngenIA365ERP.Application.Tests.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Deposits.Withdrawals;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Withdrawals;

/// <summary>
/// Feature 014, US10 (T097–T101, T104; api.md §11; data-model §6.1, §6.2; FR-005, FR-076 a FR-080): el trámite del retiro del asociado —radicar, desistir,
/// decidir con el punto del acta (quien radica no decide) y el aviso a Asociados—, la liquidación con su huella, el cruce contra un crédito por la
/// aplicación de pagos (todo o nada), el descuento por pérdidas, el mínimo irreducible de la entidad que retiene la devolución, el reverso del pago que
/// deja el retiro por rehacer y el resultado de Tesorería que paga o reintegra. Sobre <see cref="AportesDePrueba"/>: Ana con 1.000.000 de aportes. (nuevo)
/// </summary>
public sealed class RetiroDeAsociadoTests : IDisposable
{
    private readonly AportesDePrueba _a = new();
    private readonly IContratoDeObligaciones _obligaciones = Substitute.For<IContratoDeObligaciones>();
    private readonly IMotorDeCreditoEnDesembolso _motor = Substitute.For<IMotorDeCreditoEnDesembolso>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private Application.Lending.Deposits.Lines.LendingLineDto? _linea;

    public RetiroDeAsociadoTests()
    {
        _sender.Send(Arg.Any<IssueLendingDocumentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IssuedLendingDocumentDto>(new Error(DocumentErrors.CodigoSinFirmante, "Sin firmante en la prueba.")));
    }

    public void Dispose() => _a.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    private RetirosDeAsociado Retiros() => new(_a.Db, _a.G.Auditoria);

    private OrdenesDeGiroDeCuentas Ordenes() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.Reversion(), _a.C.Parametros(), _a.G.Actor,
        _a.G.Reloj);

    private bool _conIntereses;

    private PlanesDeLiquidacionDeRetiro Planes() => new(_a.Db, _a.Reglas(), _motor, Ordenes(), _conIntereses ? _a.Liquidacion() : null);

    private CruceDeRetiro Cruce() => new(_a.Db, _obligaciones, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.G.Actor, _a.G.Reloj);

    private FileMembershipWithdrawalCommandHandler Radicar() => new(_a.Db, Retiros(), _a.G.Alcance, _a.G.Auditoria, _a.G.Intervenciones(), _a.Numerador(),
        _a.C.Cerrojo, _a.G.Actor, _a.G.Reloj);

    private DecideMembershipWithdrawalCommandHandler Decidir() => new(_a.Db, Retiros(), _a.G.Alcance, new PuntoDeActaCerrado(_a.Db), _a.G.Intervenciones(),
        _a.Emision(), _a.C.Cerrojo, _a.G.Actor, _a.G.Reloj);

    private PreviewMembershipWithdrawalSettlementQueryHandler Previa() => new(Retiros(), _a.G.Alcance, Planes(), _a.G.Reloj);

    private SettleMembershipWithdrawalCommandHandler Liquidar() => new(_a.Db, Retiros(), _a.G.Alcance, Planes(), Cruce(), Ordenes(), _a.G.Intervenciones(),
        _a.C.Cerrojo, _sender, _a.G.Actor, _a.G.Reloj, NullLogger<SettleMembershipWithdrawalCommandHandler>.Instance, _alertas,
        _conIntereses ? _a.LiquidacionALaFecha() : null);

    private ApproveMembershipRefundCommandHandler AprobarDevolucion() => new(_a.Db, Retiros(), _a.G.Alcance, _a.Reglas(), Ordenes(), _a.G.Intervenciones(),
        _a.C.Cerrojo, _a.G.Reloj);

    private static T Ok<T>(Result<T> r)
    {
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    // ------------------------------------------------------------------------------------------------------- escenario --

    /// <summary>Ana con 1.000.000 de aportes (500.000 de mínimo y 500.000 extraordinarios) en una línea con o sin descuento por pérdidas.</summary>
    private async Task<MemberAccountDto> AnaConAportesAsync(bool perdidas = false)
    {
        _linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version() with { LossDeductionEnabled = perdidas });
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana, _linea);
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 1_000_000m, ModalidadDeConsignacion.Free));
        return cuenta;
    }

    /// <summary>Un punto de acta cerrado del consejo sobre <paramref name="sujeto"/> con el resultado dado.</summary>
    private Guid Acta(Guid sujeto, CreditDecisionOutcome resultado = CreditDecisionOutcome.Approve, string tipo = LendingDecisionSubjects.MembershipWithdrawal)
    {
        var organo = _a.G.Organo("CONSEJO" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(), "Consejo de administración", GoverningBodyKind.BoardOfDirectors);
        var sesion = new Session
        {
            GoverningBodyId = organo.Id, Kind = SessionKind.Ordinary, Mode = SessionMode.InPerson, HeldOn = _a.G.HoyLocal, Status = SessionStatus.Closed,
            Number = "ACTA-15", CreatedBy = "test",
        };
        _a.Db.LendingSessions.Add(sesion);
        _a.Db.SaveChanges();
        var punto = new SessionItem
        {
            SessionId = sesion.Id, Order = 1, Kind = SessionItemKind.Decision, Rule = VotingRule.Majority, SubjectType = tipo, SubjectPublicId = sujeto,
            Title = "Retiro del asociado", Outcome = resultado, CreatedBy = "test",
        };
        _a.Db.SessionItems.Add(punto);
        _a.Db.SaveChanges();
        _a.Db.ChangeTracker.Clear();
        return punto.PublicId;
    }

    private async Task<MembershipWithdrawalDto> RadicadoAsync()
    {
        _a.G.ActuarComo(_a.C.Asesora);
        var r = Ok(await Radicar().Handle(new FileMembershipWithdrawalCommand(_a.C.Ana.PublicId, MembershipWithdrawalCause.Voluntary, null, null, [], null)
        {
            OperationKey = Guid.NewGuid(),
        }, default));
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<MembershipWithdrawalDto> AprobadoAsync()
    {
        var radicado = await RadicadoAsync();
        _a.G.ActuarComo(_a.C.Jefe);
        var aprobado = Ok(await Decidir().Handle(new DecideMembershipWithdrawalCommand(radicado.PublicId, DecisionSobreRetiro.Approve,
            Acta(radicado.PublicId), _a.G.HoyLocal, "Aprobado en la sesión 15") { OperationKey = Guid.NewGuid() }, default));
        _a.Db.ChangeTracker.Clear();
        _a.G.ActuarComo(_a.C.Asesora);
        return aprobado;
    }

    private async Task<MembershipWithdrawalSettlementPreviewDto> PreviaAsync(Guid retiro, LossDeductionInput? perdida = null)
    {
        var r = Ok(await Previa().Handle(new PreviewMembershipWithdrawalSettlementQuery(retiro, null, perdida), default));
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<Result<MembershipWithdrawalDto>> LiquidarAsync(Guid retiro, string huella, LossDeductionInput? perdida = null)
    {
        var r = await Liquidar().Handle(new SettleMembershipWithdrawalCommand(retiro, null, huella, perdida,
            new MembershipRefundInput(FormaDeDevolucionDeAportes.DisbursementOrder), "Liquidación del retiro") { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>Un crédito vivo de Ana que vale cancelar <paramref name="paraCancelar"/>, con su pago del cruce que el contrato «aplica».</summary>
    private async Task<(Loan Credito, LoanOperation Pago)> CreditoAsync(decimal paraCancelar, ResultadoDelContrato resultado = ResultadoDelContrato.Applied)
    {
        var credito = new Loan { Number = "CR-0001", DebtorPersonId = _a.C.Ana.Id, Status = LoanStatus.Active, CreditLineId = 1, BranchId = _a.C.Oficina.Id };
        _a.Db.Loans.Add(credito);
        await _a.Db.SaveChangesAsync();
        var pago = new LoanOperation
        {
            LoanId = credito.Id, Kind = LoanOperationKind.Payment, Number = "PAG-000001", ValueDate = _a.G.HoyLocal, AccountingDate = _a.G.HoyLocal,
            RegisteredAt = _a.G.UtcNow, Amount = paraCancelar, CollectorCode = "CAR", CollectorClass = nameof(ClaseDeRecaudador.OwnLoanPayoff),
        };
        _a.Db.LoanOperations.Add(pago);
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        _motor.LiquidarAsync(credito.Id, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new LiquidacionParaRecogida(paraCancelar, 0, false));
        _obligaciones.AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeAplicacion>();
            var item = s.Payments[0];
            return new RespuestaDeAplicacionDto(
                [resultado == ResultadoDelContrato.Applied
                    ? new RespuestaDelContratoDto<ComprobanteDeAplicacionDto>(resultado, "Lending.ObligationContract.Applied", "Aplicado",
                        new ComprobanteDeAplicacionDto(pago.PublicId, pago.Number, credito.PublicId, credito.Number, item.IdempotencyKey, item.Modality,
                            item.ValueDate, item.ValueDate, _a.G.UtcNow, item.Amount, item.Amount, 0m, 0m, 0m, [], null!, null, true, null, "huella",
                            "LND-2026.1", []))
                    : new RespuestaDelContratoDto<ComprobanteDeAplicacionDto>(resultado, "Lending.Payment.NotPayable", "No se puede pagar", null)],
                []);
        });
        return (credito, pago);
    }

    // ---------------------------------------------------------------------------------------------------------- trámite --

    [Fact]
    public async Task Se_radica_quien_radica_no_aprueba_y_al_aprobar_con_acta_se_avisa_a_Asociados()
    {
        await AnaConAportesAsync();
        var radicado = await RadicadoAsync();
        radicado.Status.Should().Be(MembershipWithdrawalStatus.Filed);
        radicado.Number.Should().StartWith(RetirosDeAsociado.Prefijo);

        var mismo = await Decidir().Handle(new DecideMembershipWithdrawalCommand(radicado.PublicId, DecisionSobreRetiro.Approve, Acta(radicado.PublicId),
            _a.G.HoyLocal, "Aprobado") { OperationKey = Guid.NewGuid() }, default);
        mismo.Error.Code.Should().Be(RegistroDeIntervenciones.CodigoConflicto, "quien radica no aprueba (FR-005)");

        _a.G.ActuarComo(_a.C.Jefe);
        _a.Db.ChangeTracker.Clear();
        var otroSujeto = await Decidir().Handle(new DecideMembershipWithdrawalCommand(radicado.PublicId, DecisionSobreRetiro.Approve, Acta(Guid.NewGuid()),
            _a.G.HoyLocal, "Aprobado") { OperationKey = Guid.NewGuid() }, default);
        otroSujeto.Error.Code.Should().Be(ErroresDeRetiro.CodigoPuntoNoCorresponde, "el punto del acta es de este retiro");

        var aprobado = Ok(await Decidir().Handle(new DecideMembershipWithdrawalCommand(radicado.PublicId, DecisionSobreRetiro.Approve,
            Acta(radicado.PublicId), _a.G.HoyLocal, "Aprobado en la sesión 15") { OperationKey = Guid.NewGuid() }, default));
        aprobado.Status.Should().Be(MembershipWithdrawalStatus.Approved);
        aprobado.Approval!.EffectiveDate.Should().Be(_a.G.HoyLocal);
        var mensaje = await _a.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == RetiroDeAsociadoAprobadoV1.Type);
        mensaje.OriginPublicId.Should().Be(radicado.PublicId);
        (await _a.Db.MembershipWithdrawals.AsNoTracking().SingleAsync()).ApprovedMessagePublicId.Should().Be(mensaje.PublicId);
    }

    [Fact]
    public async Task Una_solicitud_viva_por_persona_y_solo_de_un_asociado_con_aportes()
    {
        (await Radicar().Handle(new FileMembershipWithdrawalCommand(_a.C.Ana.PublicId, MembershipWithdrawalCause.Voluntary, null, null, [], null)
            { OperationKey = Guid.NewGuid() }, default)).Error.Code.Should().Be(ErroresDeRetiro.CodigoNoAsociado, "sin cuenta de aportes viva");
        (await Radicar().Handle(new FileMembershipWithdrawalCommand(_a.C.Externa.PublicId, MembershipWithdrawalCause.Voluntary, null, null, [], null)
            { OperationKey = Guid.NewGuid() }, default)).Error.Code.Should().Be(ErroresDeRetiro.CodigoNoAsociado, "no es asociada");

        await AnaConAportesAsync();
        await RadicadoAsync();
        var otra = await Radicar().Handle(new FileMembershipWithdrawalCommand(_a.C.Ana.PublicId, MembershipWithdrawalCause.Exclusion, null, null, [], null)
            { OperationKey = Guid.NewGuid() }, default);
        otra.Error.Code.Should().Be(ErroresDeRetiro.CodigoEnCurso);
    }

    [Fact]
    public async Task Se_desiste_antes_de_aprobar_y_un_rechazo_exige_un_punto_que_niegue()
    {
        await AnaConAportesAsync();
        var radicado = await RadicadoAsync();
        var desistido = Ok(await new DesistMembershipWithdrawalCommandHandler(_a.Db, Retiros(), _a.G.Alcance, _a.G.Reloj)
            .Handle(new DesistMembershipWithdrawalCommand(radicado.PublicId, "Se arrepintió") { OperationKey = Guid.NewGuid() }, default));
        desistido.Status.Should().Be(MembershipWithdrawalStatus.Desisted);
        _a.Db.ChangeTracker.Clear();

        var otro = await RadicadoAsync();
        _a.G.ActuarComo(_a.C.Jefe);
        (await Decidir().Handle(new DecideMembershipWithdrawalCommand(otro.PublicId, DecisionSobreRetiro.Reject, Acta(otro.PublicId), null, "Negado")
            { OperationKey = Guid.NewGuid() }, default)).Error.Code.Should().Be(ErroresDeRetiro.CodigoPuntoNoCorresponde, "el punto aprobó");
        var negado = Ok(await Decidir().Handle(new DecideMembershipWithdrawalCommand(otro.PublicId, DecisionSobreRetiro.Reject,
            Acta(otro.PublicId, CreditDecisionOutcome.Deny), null, "El consejo lo negó") { OperationKey = Guid.NewGuid() }, default));
        negado.Status.Should().Be(MembershipWithdrawalStatus.Rejected);
        (await _a.Db.IntegrationMessages.AsNoTracking().AnyAsync(m => m.Type == RetiroDeAsociadoAprobadoV1.Type)).Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------------------------ liquidación --

    [Fact]
    public async Task Sin_creditos_se_devuelve_todo_por_orden_de_giro_con_sus_hechos()
    {
        await AnaConAportesAsync();
        var retiro = await AprobadoAsync();

        var previa = await PreviaAsync(retiro.PublicId);
        previa.Refund.Should().Be(1_000_000m);
        previa.Offsets.Should().BeEmpty();
        previa.EntityMinimum.WouldBreach.Should().BeFalse();

        (await LiquidarAsync(retiro.PublicId, "otra-huella")).Error.Code.Should().Be(ErroresDeRetiro.CodigoVistaPreviaCambio);

        var liquidado = Ok(await LiquidarAsync(retiro.PublicId, previa.Sha256));
        liquidado.Status.Should().Be(MembershipWithdrawalStatus.Settled);
        liquidado.Settlement!.Refund.Should().Be(1_000_000m);
        liquidado.Settlement.RefundDueOn.Should().Be(_a.G.HoyLocal.AddDays(60), "los días de devolución de la versión de aportes");
        liquidado.RefundOrders.Should().ContainSingle().Which.Amount.Should().Be(1_000_000m);
        liquidado.Lines.Where(l => l.Kind != MembershipWithdrawalLineKind.Refund).Sum(l => l.Amount).Should().Be(1_000_000m);

        var orden = await _a.Db.AccountPayoutOrders.AsNoTracking().SingleAsync();
        orden.MembershipWithdrawalId.Should().NotBeNull();
        (await _a.HechosAsync()).Should().Contain([DevolucionOrdenadaV1.Type, OrdenDeGiroEmitidaV2.Type]);
        var cuenta = await _a.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.Nature == LineNature.Contribution);
        cuenta.Status.Should().Be(MemberAccountStatus.Closing, "se cancela cuando la orden se gire");
        _sender.ReceivedCalls().Should().Contain(c => c.GetArguments()[0] is IssueLendingDocumentCommand, "la liquidación se emite al liquidar");
    }

    [Fact]
    public async Task T136_los_ahorros_que_ganan_interes_entran_con_sus_intereses_liquidados_a_la_fecha()
    {
        _conIntereses = true;
        _a.G.Valores.LeerAsync(IngenIA365ERP.Domain.Common.NationalValues.CodigosDeValorNacional.Uvt, Arg.Any<string>(), Arg.Any<DateOnly>(),
                Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new IngenIA365ERP.Application.Common.NationalValues.ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), "UVT", string.Empty,
                52_374m, null, null, new DateOnly(2026, 1, 1), null, null, "Prueba", 1, _a.G.UtcNow.AddYears(-1),
                IngenIA365ERP.Application.Common.NationalValues.EstadoDeRevision.Validada)));
        await AnaConAportesAsync();
        var linea = await _a.C.LineaDeCaptacionAsync("AHTRES", Accounts.CuentasDePrueba.ALaVista() with
        {
            Rate = new Application.Lending.Deposits.Lines.DepositRateInput { Kind = RateKind.Fixed, EffectiveAnnual = 0.03m },
        });
        var ahorro = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana));
        await _a.ConsignadaAsync(_a.Consignacion(ahorro, 10_000_000m, ModalidadDeConsignacion.Free));
        _a.G.HoyLocal = _a.G.HoyLocal.AddDays(14);
        _a.G.UtcNow = _a.G.UtcNow.AddDays(14);

        var retiro = await AprobadoAsync();
        var previa = await PreviaAsync(retiro.PublicId);
        var tasa = IngenIA365ERP.Domain.Lending.Rates.EquivalenciaDeTasas.TasaDiaria(0.03m, DayCountConvention.Actual365, RateEquivalence.Compound,
            RepaymentPeriodicity.Monthly);
        var interes = Math.Round(IngenIA365ERP.Domain.Lending.Deposits.InteresDeCaptacion.Seis(tasa * 10_000_000m * 14), 0, MidpointRounding.AwayFromZero);
        previa.Savings.Should().Be(10_000_000m + interes, "catorce días al 3 % EA, sin retención (bajo el umbral)");
        previa.Warnings.Should().Contain(w => w.Contains("intereses liquidados a la fecha"));

        var liquidado = Ok(await LiquidarAsync(retiro.PublicId, previa.Sha256));
        liquidado.Status.Should().Be(MembershipWithdrawalStatus.Settled);
        var liquidacion = await _a.Db.InterestSettlements.AsNoTracking().SingleAsync();
        (liquidacion.PeriodTo, liquidacion.GrossInterest).Should().Be((_a.G.HoyLocal, interes));
        liquidado.Settlement!.Refund.Should().Be(1_000_000m + 10_000_000m + interes);
    }

    [Fact]
    public async Task La_orden_girada_paga_el_retiro_y_cancela_la_cuenta_y_la_rechazada_reintegra()
    {
        await AnaConAportesAsync();
        var retiro = await AprobadoAsync();
        Ok(await LiquidarAsync(retiro.PublicId, (await PreviaAsync(retiro.PublicId)).Sha256));

        var orden = await _a.Db.AccountPayoutOrders.SingleAsync();
        Ok(Resultado(await Ordenes().AplicarResultadoAsync(orden, DisbursementOrderStatus.Rejected, "TES-1", _a.G.HoyLocal, "Cuenta inexistente", default)));
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        var saldo = (await LectorDeCuentas.SaldosAsync(_a.Db, [orden.MemberAccountId], default))[orden.MemberAccountId];
        LectorDeCuentas.SaldoDelTitular(LineNature.Contribution, saldo).Should().Be(1_000_000m, "el giro rechazado reintegra el saldo (FR-091)");
        (await _a.HechosAsync()).Should().Contain(OperacionDeCuentaReversadaV1.Type);

        // Otra persona ordena otra vez lo que quedó por devolver; esta vez se gira y el retiro queda pagado.
        _a.G.ActuarComo(_a.C.Jefe);
        var otraVez = Ok(await AprobarDevolucion().Handle(new ApproveMembershipRefundCommand(retiro.PublicId, null, null, "Nueva orden")
            { OperationKey = Guid.NewGuid() }, default));
        _a.Db.ChangeTracker.Clear();
        otraVez.RefundOrders.Should().HaveCount(2);
        var nueva = await _a.Db.AccountPayoutOrders.Where(o => o.Status == DisbursementOrderStatus.Issued).SingleAsync();
        Ok(Resultado(await Ordenes().AplicarResultadoAsync(nueva, DisbursementOrderStatus.Disbursed, "TES-2", _a.G.HoyLocal, null, default)));
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();

        (await _a.Db.MembershipWithdrawals.AsNoTracking().SingleAsync()).Status.Should().Be(MembershipWithdrawalStatus.Paid);
        (await _a.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.Nature == LineNature.Contribution)).Status.Should().Be(MemberAccountStatus.Closed);
        (await _a.HechosAsync()).Should().Contain(DevolucionGiradaV1.Type);
    }

    [Fact]
    public async Task El_minimo_irreducible_de_la_entidad_retiene_la_devolucion_no_el_retiro_y_avisa_al_gerente()
    {
        await AnaConAportesAsync();
        Parametro(ParametrosDeCartera.AportesMinimoIrreducibleEntidad, "800000");
        var retiro = await AprobadoAsync();

        var previa = await PreviaAsync(retiro.PublicId);
        previa.EntityMinimum.WouldBreach.Should().BeTrue();
        var liquidado = Ok(await LiquidarAsync(retiro.PublicId, previa.Sha256));
        liquidado.Status.Should().Be(MembershipWithdrawalStatus.Settled, "se rechaza la devolución, no el retiro (US10-3)");
        liquidado.Settlement!.RefundHeldByInstitutionalMinimum.Should().BeTrue();
        liquidado.RefundOrders.Should().BeEmpty();
        await _alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(x => x.TypeCode == TiposDeAlerta.DevolucionRetenidaPorMinimo), Arg.Any<CancellationToken>());

        // Mientras el capital no lo permita, aprobar la devolución sigue rechazándola; quien liquidó no la aprueba.
        (await AprobarDevolucion().Handle(new ApproveMembershipRefundCommand(retiro.PublicId, null, null, "Aprobada") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(RegistroDeIntervenciones.CodigoConflicto);
        _a.G.ActuarComo(_a.C.Jefe);
        (await AprobarDevolucion().Handle(new ApproveMembershipRefundCommand(retiro.PublicId, null, null, "Aprobada") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(ErroresDeAportes.CodigoMinimoDeLaEntidad);

        // Entra el aporte de otro asociado: el capital ya lo permite.
        _a.G.ActuarComo(_a.C.Asesora);
        var beto = await _a.CuentaDeAportesAsync(_a.C.Beto, _linea);
        await _a.ConsignadaAsync(_a.Consignacion(beto, 2_000_000m, ModalidadDeConsignacion.Free));
        _a.G.ActuarComo(_a.C.Jefe);
        var aprobada = Ok(await AprobarDevolucion().Handle(new ApproveMembershipRefundCommand(retiro.PublicId, null, null, "El capital ya lo permite")
            { OperationKey = Guid.NewGuid() }, default));
        aprobada.Settlement!.RefundHeldByInstitutionalMinimum.Should().BeFalse();
        aprobada.RefundOrders.Should().ContainSingle().Which.Amount.Should().Be(1_000_000m);
    }

    [Fact]
    public async Task Las_perdidas_se_descuentan_con_la_linea_que_lo_admite_y_un_punto_de_acta()
    {
        await AnaConAportesAsync(perdidas: false);
        var retiro = await AprobadoAsync();
        var perdida = new LossDeductionInput(Acta(Guid.NewGuid(), tipo: LendingDecisionSubjects.MembershipWithdrawal), 2025, 0.04m);
        (await Previa().Handle(new PreviewMembershipWithdrawalSettlementQuery(retiro.PublicId, null, perdida), default))
            .Error.Code.Should().Be(ErroresDeRetiro.CodigoPerdidasNoAdmitidas, "D15: desactivado por defecto");
    }

    [Fact]
    public async Task Con_la_linea_que_lo_admite_la_perdida_sale_de_los_aportes_con_su_hecho()
    {
        await AnaConAportesAsync(perdidas: true);
        var retiro = await AprobadoAsync();
        var perdida = new LossDeductionInput(Acta(Guid.NewGuid(), tipo: LendingDecisionSubjects.MembershipWithdrawal), 2025, 0.04m);

        var previa = await PreviaAsync(retiro.PublicId, perdida);
        previa.LossDeduction!.Amount.Should().Be(40_000m);
        previa.Refund.Should().Be(960_000m);
        var liquidado = Ok(await LiquidarAsync(retiro.PublicId, previa.Sha256, perdida));
        liquidado.Settlement!.LossDeduction.Should().Be(40_000m);
        liquidado.Lines.Should().Contain(l => l.Kind == MembershipWithdrawalLineKind.LossDeduction && l.AccountOperationNumber != null);
        (await _a.HechosAsync()).Should().Contain(PerdidaAplicadaAlRetiroV1.Type);
    }

    // ----------------------------------------------------------------------------------------------------------- cruce --

    [Fact]
    public async Task El_cruce_paga_el_credito_por_la_aplicacion_de_pagos_y_devuelve_el_remanente()
    {
        await AnaConAportesAsync();
        var (credito, pago) = await CreditoAsync(600_000m);
        var retiro = await AprobadoAsync();

        var previa = await PreviaAsync(retiro.PublicId);
        previa.Offsets.Should().ContainSingle().Which.Should().Match<MembershipWithdrawalOffsetDto>(o => o.Amount == 600_000m && o.PaysOff && o.Priority == 1);
        previa.Refund.Should().Be(400_000m);

        var liquidado = Ok(await LiquidarAsync(retiro.PublicId, previa.Sha256));
        await _obligaciones.Received(1).AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s =>
            s.Collector.Class == ClaseDeRecaudador.OwnLoanPayoff && s.Payments.Count == 1 && s.Payments[0].MeansClass == ClaseDeMedioDeRecaudo.InternalOffset
            && s.Payments[0].Modality == ModalidadDePago.FullPayoff && s.Payments[0].Amount == 600_000m && s.Payments[0].ObligationPublicId == credito.PublicId),
            Arg.Any<CancellationToken>());
        liquidado.Settlement!.Crossed.Should().Be(600_000m);
        liquidado.Lines.Where(l => l.Kind == MembershipWithdrawalLineKind.LoanOffset).Sum(l => l.Amount).Should().Be(-600_000m);
        liquidado.Lines.Should().Contain(l => l.Kind == MembershipWithdrawalLineKind.LoanOffset && l.LoanOperationNumber == pago.Number);

        var cruce = await _a.Db.MemberAccountOperations.AsNoTracking().SingleAsync(o => o.Kind == MemberAccountOperationKind.InternalOffset);
        cruce.RelatedLoanOperationId.Should().Be(pago.Id);
        cruce.PaymentMeansClass.Should().Be(nameof(ClaseDeMedioDeRecaudo.InternalOffset));
        (await _a.HechosAsync()).Should().Contain(CruceAlRetiroAplicadoV1.Type);
        (await _a.Db.MembershipWithdrawals.AsNoTracking().SingleAsync()).OffsetOperationId.Should().Be(cruce.Id);
    }

    [Fact]
    public async Task El_cruce_viaja_en_la_unidad_de_su_pago_y_lo_que_queda_debiendo_pasa_a_caja_por_defecto()
    {
        // Decisión del dueño del 2026-10-18 (D45): el cruce y el pago del crédito van en el mismo comprobante (misma unidad: origen el pago y su clave),
        // y quien liquida elige qué pasa con lo que los créditos siguen debiendo (por defecto, pasan a caja).
        await AnaConAportesAsync();
        var (_, pago) = await CreditoAsync(1_500_000m);
        var retiro = await AprobadoAsync();

        var previa = await PreviaAsync(retiro.PublicId);
        previa.PendingLoanBalance.Should().Be(500_000m);
        previa.FundsOrder.Should().Be(nameof(Domain.Lending.Deposits.OrdenDeFondosAlRetiro.AportesPrimero));
        previa.Warnings.Should().Contain(w => w.Contains("D45", StringComparison.Ordinal));
        Ok(await LiquidarAsync(retiro.PublicId, previa.Sha256));

        var mensaje = await _a.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CruceAlRetiroAplicadoV1.Type);
        mensaje.OriginPublicId.Should().Be(pago.PublicId, "en la unidad del PagoAplicado del crédito");
        mensaje.OriginEventKey.Should().Be(IngenIA365ERP.Application.Common.Integration.ClavesDeEvento.Confirmacion);
        mensaje.PayloadJson.Should().Contain("\"parts\"");
        (await _a.Db.MemberAccountTransactions.AsNoTracking().Where(t => t.Kind == MemberAccountTransactionKind.InternalOffset).Select(t => t.MessagePublicId)
            .Distinct().ToListAsync()).Should().Equal(mensaje.PublicId);
        (await _a.Db.MembershipWithdrawals.AsNoTracking().SingleAsync()).PendingLoanBalanceHandling.Should().Be(PendingLoanBalanceHandling.MoveToCashier);
    }

    [Fact]
    public async Task El_reverso_tardio_con_el_retiro_pagado_reabre_la_cuenta_con_el_permiso_superior_y_avisa()
    {
        // Decisión del dueño del 2026-10-18 (D44): hasta esa fecha se rechazaba con AccountsClosed (research E69).
        await AnaConAportesAsync();
        var (credito, pago) = await CreditoAsync(600_000m);
        var retiro = await AprobadoAsync();
        Ok(await LiquidarAsync(retiro.PublicId, (await PreviaAsync(retiro.PublicId)).Sha256));
        var orden = await _a.Db.AccountPayoutOrders.SingleAsync();
        Ok(Resultado(await Ordenes().AplicarResultadoAsync(orden, DisbursementOrderStatus.Disbursed, "TES-1", _a.G.HoyLocal, null, default)));
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        (await _a.Db.MembershipWithdrawals.AsNoTracking().SingleAsync()).Status.Should().Be(MembershipWithdrawalStatus.Paid);
        Parametro(ParametrosDeCartera.CaptacionesDestinatariosReversoTardio, "revisoria@coop.test; gerencia@coop.test");

        var reverso = new LoanOperation
        {
            LoanId = credito.Id, Kind = LoanOperationKind.PaymentReversal, Number = "REV-000002", ValueDate = _a.G.HoyLocal, AccountingDate = _a.G.HoyLocal,
            RegisteredAt = _a.G.UtcNow, Amount = 600_000m, ReversesOperationId = pago.Id, Reason = "Pago aplicado por error",
        };
        _a.Db.LoanOperations.Add(reverso);
        await _a.Db.SaveChangesAsync();

        var sinPermiso = Substitute.For<IngenIA365ERP.Application.Common.Interfaces.Security.ICurrentUserPermissions>();
        sinPermiso.ListAsync(Arg.Any<CancellationToken>()).Returns(["Lending.Payments.Reverse"]);
        (await new ArrastreDelCruceAlRetiro(_a.Db, _a.Reversion(), _a.C.Cerrojo, _a.G.Reloj, sinPermiso).ValidarAsync(pago, default))!.Code
            .Should().Be(ErroresDeRetiro.CodigoCuentasCerradas, "sin el permiso de nivel superior no procede");

        var conPermiso = Substitute.For<IngenIA365ERP.Application.Common.Interfaces.Security.ICurrentUserPermissions>();
        conPermiso.ListAsync(Arg.Any<CancellationToken>()).Returns([ArrastreDelCruceAlRetiro.PermisoDeReversoTardio]);
        var correo = Substitute.For<IngenIA365ERP.Application.Common.Interfaces.Notifications.IEmailSender>();
        var trasElCommit = new IngenIA365ERP.Application.Common.Persistence.TareasTrasElCommit();
        var arrastre = new ArrastreDelCruceAlRetiro(_a.Db, _a.Reversion(), _a.C.Cerrojo, _a.G.Reloj, conPermiso, _a.C.Parametros(), correo, _alertas,
            trasElCommit: trasElCommit);
        (await arrastre.ValidarAsync(pago, default)).Should().BeNull();
        var r = await arrastre.AlReversarAsync(pago, reverso, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();

        var cuenta = await _a.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.Nature == LineNature.Contribution);
        cuenta.Status.Should().Be(MemberAccountStatus.Closing, "la cuenta se reabre en cierre con el saldo a favor del exasociado");
        LectorDeCuentas.SaldoDelTitular(LineNature.Contribution, (await LectorDeCuentas.SaldosAsync(_a.Db, [cuenta.Id], default))[cuenta.Id])
            .Should().Be(600_000m);
        (await _a.Db.MembershipWithdrawals.AsNoTracking().SingleAsync()).Status.Should().Be(MembershipWithdrawalStatus.Settled,
            "vuelve a liquidado para ordenar la devolución");
        await _alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(x => x.TypeCode == TiposDeAlerta.ReversoTardioDeCruce), Arg.Any<CancellationToken>());
        // Fase 8 de la 014 (research E92): el correo no sale dentro de la transacción del reverso; sale cuando se confirma.
        await correo.DidNotReceive().SendAsync(Arg.Any<IngenIA365ERP.Application.Common.Interfaces.Notifications.EmailMessage>(), Arg.Any<CancellationToken>());
        trasElCommit.HayPendientes.Should().BeTrue("el correo queda anotado para después del commit");
        await trasElCommit.EjecutarAsync(null, default);
        await correo.Received(2).SendAsync(Arg.Any<IngenIA365ERP.Application.Common.Interfaces.Notifications.EmailMessage>(), Arg.Any<CancellationToken>());
        ArrastreDelCruceAlRetiro.Correos("a@b.co, sin-arroba ; c@d.co").Should().Equal("a@b.co", "c@d.co");
    }

    [Fact]
    public void El_motivo_de_Asociados_sale_del_mapeo_de_la_cooperativa()
    {
        // Decisión del dueño del 2026-10-18 (D46).
        const string Mapeo = "{\"Voluntary\": \"VOL\", \"Death\": \" FAL \"}";
        DecideMembershipWithdrawalCommandHandler.CodigoDeMotivo(Mapeo, MembershipWithdrawalCause.Voluntary).Should().Be("VOL");
        DecideMembershipWithdrawalCommandHandler.CodigoDeMotivo(Mapeo, MembershipWithdrawalCause.Death).Should().Be("FAL");
        DecideMembershipWithdrawalCommandHandler.CodigoDeMotivo(Mapeo, MembershipWithdrawalCause.Exclusion).Should().BeNull();
        DecideMembershipWithdrawalCommandHandler.CodigoDeMotivo("no es json", MembershipWithdrawalCause.Voluntary).Should().BeNull();
        DecideMembershipWithdrawalCommandHandler.CodigoDeMotivo("{}", MembershipWithdrawalCause.Voluntary).Should().BeNull();
    }

    [Fact]
    public async Task Si_el_pago_del_cruce_no_se_aplica_no_se_aplica_nada()
    {
        await AnaConAportesAsync();
        await CreditoAsync(600_000m, ResultadoDelContrato.Rejected);
        var retiro = await AprobadoAsync();

        var r = await LiquidarAsync(retiro.PublicId, (await PreviaAsync(retiro.PublicId)).Sha256);

        r.Error.Code.Should().Be(ErroresDeRetiro.CodigoCruceRechazado);
        (await _a.Db.MemberAccountOperations.AsNoTracking().AnyAsync(o => o.MembershipWithdrawalId != null)).Should().BeFalse();
        (await _a.Db.MembershipWithdrawals.AsNoTracking().SingleAsync()).Status.Should().Be(MembershipWithdrawalStatus.Approved);
    }

    [Fact]
    public async Task El_reverso_del_pago_cruzado_arrastra_el_debito_y_deja_el_retiro_por_rehacer()
    {
        await AnaConAportesAsync();
        var (credito, pago) = await CreditoAsync(600_000m);
        var retiro = await AprobadoAsync();
        Ok(await LiquidarAsync(retiro.PublicId, (await PreviaAsync(retiro.PublicId)).Sha256));

        var reverso = new LoanOperation
        {
            LoanId = credito.Id, Kind = LoanOperationKind.PaymentReversal, Number = "REV-000001", ValueDate = _a.G.HoyLocal, AccountingDate = _a.G.HoyLocal,
            RegisteredAt = _a.G.UtcNow, Amount = 600_000m, ReversesOperationId = pago.Id, Reason = "Pago aplicado por error",
        };
        _a.Db.LoanOperations.Add(reverso);
        await _a.Db.SaveChangesAsync();
        var arrastre = new ArrastreDelCruceAlRetiro(_a.Db, _a.Reversion(), _a.C.Cerrojo, _a.G.Reloj);
        (await arrastre.ValidarAsync(pago, default)).Should().BeNull();
        var r = await arrastre.AlReversarAsync(pago, reverso, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();

        var cruce = await _a.Db.MemberAccountOperations.AsNoTracking().SingleAsync(o => o.Kind == MemberAccountOperationKind.InternalOffset);
        cruce.ReversedByOperationId.Should().NotBeNull("el reverso del pago arrastra el débito (FR-077)");
        var leido = await _a.Db.MembershipWithdrawals.AsNoTracking().SingleAsync();
        leido.Status.Should().Be(MembershipWithdrawalStatus.ToRedo, "la liquidación queda por rehacer (US10-4)");
        leido.ToRedoReason.Should().Contain(pago.Number);
        (await arrastre.AlReversarAsync(pago, reverso, default)).IsSuccess.Should().BeTrue("una segunda vez no encuentra nada que arrastrar");
    }

    // ------------------------------------------------------------------------------------------------- Asociados (E-04) --

    [Fact]
    public async Task Asociados_consume_el_retiro_aprobado_una_sola_vez_y_ya_no_retira_por_su_cuenta()
    {
        await AnaConAportesAsync();
        await AprobadoAsync();
        var mensaje = await _a.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == RetiroDeAsociadoAprobadoV1.Type);
        var consumir = new ConsumeMembershipWithdrawalApprovedCommandHandler(_a.Db, new MensajesEntrantes(_a.Db), _a.G.Reloj);

        var r = Ok(await consumir.Handle(new ConsumeMembershipWithdrawalApprovedCommand([mensaje.PublicId]), default));
        r.Should().BeOfType<ResultadoDeConsumo.Processed>();
        mensaje.PayloadJson.Should().NotContain("\"withdrawalReasonCode\":\"", "sin mapeo de motivos (D46) no viaja código");
        _a.Db.ChangeTracker.Clear();
        var asociado = await _a.Db.Associates.AsNoTracking().SingleAsync(a => a.PersonId == _a.C.Ana.Id);
        asociado.Status.Should().Be(ConsumeMembershipWithdrawalApprovedCommandHandler.EstadoRetirado);
        asociado.WithdrawalDate.Should().Be(_a.G.HoyLocal);
        Ok(await consumir.Handle(new ConsumeMembershipWithdrawalApprovedCommand([mensaje.PublicId]), default))
            .Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>("el recibo es único por mensaje");
        (await _a.Db.AssociateInboundMessages.AsNoTracking().CountAsync()).Should().Be(1);

        var beto = await _a.Db.Associates.AsNoTracking().SingleAsync(a => a.PersonId == _a.C.Beto.Id);
        var aMano = await new UpdateAssociateCommandHandler(_a.Db, _a.G.Reloj, Substitute.For<ICurrentUserService>())
            .Handle(new UpdateAssociateCommand { PublicId = beto.PublicId, Status = "R" }, default);
        aMano.Error.Code.Should().Be(UpdateAssociateCommandHandler.WithdrawalOwnedByLendingCode);
    }

    [Fact]
    public async Task Con_un_retiro_en_curso_no_se_abren_cuentas_y_la_baja_de_la_persona_mira_los_productos()
    {
        await AnaConAportesAsync();
        await AprobadoAsync();
        var linea = await _a.C.LineaDeCaptacionAsync();
        (await _a.C.AbrirAsync(_a.C.Apertura(linea, _a.C.Ana))).Error.Code.Should().Be(ErroresDeRetiro.CodigoEnCurso, "sólo recibe operaciones del retiro");

        var captaciones = Substitute.For<IConsultasDeCartera>();
        captaciones.ProductosDeCaptacionAsync(Arg.Any<ConsultaDeProductosDeCaptacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new ProductosDeCaptacionDto(EstadoDeCartera.Active, EstadoDeCartera.Active,
                [new ProductosDePersonaDto(ci.Arg<ConsultaDeProductosDeCaptacion>().PersonPublicIds[0], true, false, [])]));
        var borrar = await new DeletePersonCommandHandler(_a.Db, _a.G.Reloj, Substitute.For<ICurrentUserService>(), new ConsultaDeObligacionesDePrueba(),
            captaciones).Handle(new DeletePersonCommand(_a.C.Ana.PublicId), default);
        borrar.Error.Code.Should().Be(DeletePersonCommandHandler.HasLiveDepositProductsCode);
    }

    // ------------------------------------------------------------------------------------------------------- utilidades --

    private void Parametro(string clave, string valor)
    {
        _a.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba", CreatedAt = _a.G.UtcNow.AddDays(-10),
        });
        _a.Db.SaveChanges();
    }

    private static Result<bool> Resultado(Result r) => r.IsSuccess ? Result.Success(true) : Result.Failure<bool>(r.Error);

    /// <summary>
    /// US6 (T175; FR-076; research E145): un CDAT vivo del asociado va en la liquidación como línea informativa <c>TermDepositBalance</c> con su saldo y su
    /// vencimiento; no suma a la devolución, no lo cancela el retiro (sigue hasta vencer y paga a su destino) y la vista previa lo avisa.
    /// </summary>
    [Fact]
    public async Task T175_un_CDAT_vivo_va_como_linea_informativa_y_sigue_hasta_su_vencimiento()
    {
        await AnaConAportesAsync();
        var vence = _a.G.HoyLocal.AddDays(90);
        var cdat = await CdatVivoAsync(5_000_000m, vence);
        var retiro = await AprobadoAsync();

        var previa = await PreviaAsync(retiro.PublicId);
        var linea = previa.Lines.Should().ContainSingle(l => l.Kind == MembershipWithdrawalLineKind.TermDepositBalance).Which;
        (linea.AccountPublicId, linea.Amount).Should().Be((cdat.PublicId, 5_000_000m));
        previa.Refund.Should().Be(1_000_000m, "el CDAT no entra a la devolución");
        previa.Warnings.Should().Contain(w => w.Contains("CD-9") && w.Contains("vencimiento"));

        var liquidado = Ok(await LiquidarAsync(retiro.PublicId, previa.Sha256));
        liquidado.Status.Should().Be(MembershipWithdrawalStatus.Settled);
        liquidado.Settlement!.Refund.Should().Be(1_000_000m);
        liquidado.Lines.Where(l => l.Kind is not (MembershipWithdrawalLineKind.Refund or MembershipWithdrawalLineKind.TermDepositBalance)).Sum(l => l.Amount)
            .Should().Be(1_000_000m, "las líneas que no son la devolución ni el CDAT suman lo que se devuelve");
        var guardada = await _a.Db.MembershipWithdrawalLines.AsNoTracking().SingleAsync(l => l.Kind == MembershipWithdrawalLineKind.TermDepositBalance);
        (guardada.MemberAccountId, guardada.Amount).Should().Be((cdat.Id, 5_000_000m));
        guardada.Notes.Should().Contain(vence.ToString("yyyy-MM-dd"));
        (await _a.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.Id == cdat.Id)).Status.Should().Be(MemberAccountStatus.Active, "sigue hasta su vencimiento");
        (await Ordenes().CuentasDelRetiroAsync(await _a.Db.MembershipWithdrawals.AsNoTracking().SingleAsync(), default))
            .Should().NotContain(a => a.Id == cdat.Id, "el retiro no espera el CDAT para quedar pagado");
    }

    /// <summary>Un CDAT activo de Ana con su capital y su versión de condiciones (sembrado directo: el retiro sólo lee su saldo y su vencimiento).</summary>
    private async Task<Domain.Entities.Lending.Deposits.Accounts.MemberAccount> CdatVivoAsync(decimal capital, DateOnly vence)
    {
        var linea = await _a.C.LineaDeCaptacionAsync("CDATR", Accounts.CuentasDePrueba.ALaVista());
        var lineaId = await _a.Db.CreditLines.AsNoTracking().Where(l => l.PublicId == linea.PublicId).Select(l => l.Id).SingleAsync();
        var hoy = _a.G.HoyLocal;
        var cuenta = Domain.Entities.Lending.Deposits.Accounts.MemberAccount.Abrir(lineaId, LineNature.Deposit, DepositClass.TermDeposit, _a.C.Ana.Id,
            _a.C.Oficina.Id, HandlingMode.Indistinct, hoy, null, _a.G.UtcNow);
        cuenta.Number = "CD-9";
        cuenta.Activar(hoy, _a.G.UtcNow);
        _a.Db.MemberAccounts.Add(cuenta);
        await _a.Db.SaveChangesAsync();
        var operacion = new Domain.Entities.Lending.Deposits.Transactions.MemberAccountOperation
        {
            MemberAccountId = cuenta.Id, Kind = MemberAccountOperationKind.TermDepositConstitution, Number = "CDC-9", Sequence = 1, ValueDate = hoy,
            AccountingDate = hoy, RegisteredAt = _a.G.UtcNow, Amount = capital, Payload = "{}", InputsSha256 = "prueba", RulesVersion = "LND-2026.1",
        };
        _a.Db.MemberAccountOperations.Add(operacion);
        await _a.Db.SaveChangesAsync();
        _a.Db.MemberAccountTermsVersions.Add(new Domain.Entities.Lending.Deposits.Accounts.MemberAccountTermsVersion
        {
            MemberAccountId = cuenta.Id, VersionNumber = 1, OperationId = operacion.Id, EffectiveValueDate = hoy, RegisteredAt = _a.G.UtcNow,
            Cause = AccountTermsCause.Constitution, RulesVersion = "LND-2026.1", InputsSha256 = "prueba", PrincipalAmount = capital, TermDays = 90, StartDate = hoy,
            MaturityDate = vence, PaymentDate = vence, TableAnnualRate = 0.1m, AppliedAnnualRate = 0.1m, DayCount = DayCountConvention.Actual360,
            RoundingUnit = 1m, RoundingMode = RoundingMode.AwayFromZero, RoundingMoment = RoundingMoment.PerTotal,
        });
        _a.Db.MemberAccountTransactions.Add(new Domain.Entities.Lending.Deposits.Transactions.MemberAccountTransaction
        {
            MemberAccountId = cuenta.Id, OperationId = operacion.Id, Kind = MemberAccountTransactionKind.TransferIn, Concept = MemberAccountConcept.Principal,
            Amount = capital, ValueDate = hoy, AccountingDate = hoy, RegisteredAt = _a.G.UtcNow,
        });
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        return cuenta;
    }
}
