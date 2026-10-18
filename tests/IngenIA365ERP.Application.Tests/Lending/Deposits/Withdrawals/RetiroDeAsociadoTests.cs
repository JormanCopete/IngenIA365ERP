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

    private PlanesDeLiquidacionDeRetiro Planes() => new(_a.Db, _a.Reglas(), _motor, Ordenes());

    private CruceDeRetiro Cruce() => new(_a.Db, _obligaciones, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.G.Actor, _a.G.Reloj);

    private FileMembershipWithdrawalCommandHandler Radicar() => new(_a.Db, Retiros(), _a.G.Alcance, _a.G.Auditoria, _a.G.Intervenciones(), _a.Numerador(),
        _a.C.Cerrojo, _a.G.Actor, _a.G.Reloj);

    private DecideMembershipWithdrawalCommandHandler Decidir() => new(_a.Db, Retiros(), _a.G.Alcance, new PuntoDeActaCerrado(_a.Db), _a.G.Intervenciones(),
        _a.Emision(), _a.C.Cerrojo, _a.G.Actor, _a.G.Reloj);

    private PreviewMembershipWithdrawalSettlementQueryHandler Previa() => new(Retiros(), _a.G.Alcance, Planes(), _a.G.Reloj);

    private SettleMembershipWithdrawalCommandHandler Liquidar() => new(_a.Db, Retiros(), _a.G.Alcance, Planes(), Cruce(), Ordenes(), _a.G.Intervenciones(),
        _a.C.Cerrojo, _sender, _a.G.Actor, _a.G.Reloj, NullLogger<SettleMembershipWithdrawalCommandHandler>.Instance, _alertas);

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
        var arrastre = new ArrastreDelCruceAlRetiro(_a.Db, _a.Reversion(), _a.C.Cerrojo);
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
}
