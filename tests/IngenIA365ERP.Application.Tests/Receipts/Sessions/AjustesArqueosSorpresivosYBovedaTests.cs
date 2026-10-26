using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Application.Receipts.Vaults;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals.Transactions;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Sessions;

/// <summary>
/// Feature 015, T0084 (FR-033, FR-035, FR-182; US2-5): el ajuste de una sesión cerrada, aprobado por un aprobador presente y enlazado con su hecho; el arqueo
/// sorpresivo de una sesión con acta (no la cierra); el billete retenido con acta, sin tocar el saldo; el saldo de la bóveda por denominación es una proyección
/// que la verificación del cuadre compara con sus movimientos. Escrita en la fase 5; se corre al final del plan.
/// </summary>
public class AjustesArqueosSorpresivosYBovedaTests
{
    private readonly CajaDePrueba _c = new();

    private async Task<CashSessionDto> CerradaConFaltanteAsync()
    {
        var sesion = await _c.AbrirAsync();
        await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, [_c.EnBilletes("Moneda", (_c.B50, 9), (_c.B20, 2), (_c.B1, 9))]), default);
        return sesion;
    }

    [Fact]
    public async Task Un_ajuste_solo_en_sesion_cerrada_con_soporte_y_aprobador_presente()
    {
        var abierta = await _c.AbrirAsync(_c.C2, _c.E.OtroCajero);
        var enAbierta = await _c.Ajustar().Handle(new RegisterSessionAdjustmentCommand(abierta.PublicId, SessionAdjustmentKind.CountCorrection,
            _c.Efectivo.PublicId, 1_000, "Apareció", Guid.NewGuid()), default);
        enAbierta.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSesionNoCerrada);

        var sesion = await CerradaConFaltanteAsync();
        var huella = $"{SessionAdjustmentKind.CountCorrection}:{_c.Efectivo.PublicId:N}:1000.00";
        var aprobada = new ApprovalRequest
        {
            Subject = ApprovalSubjects.CashSessionAdjustment, SourceType = ApprovalSourceTypes.ReceiptsOperation, SourcePublicId = sesion.PublicId,
            SourceLabel = "Ajuste", Amount = 1_000, OperationDate = EscenarioDeRecaudo.Hoy, CreatedByUserId = _c.E.Supervisor.Id,
            RequestedByUserId = _c.E.Supervisor.Id, Status = ApprovalRequestStatus.Approved, CurrentLevel = 1, ContentSha256 = huella, RequestedAt = DateTime.UtcNow,
        };
        aprobada.SellarNiveles([new NivelDeAprobacion(1, 0m, "Receipts.Session.Adjust")]);
        aprobada.Decisions.Add(new ApprovalDecision { Level = 1, Decision = ApprovalDecisionKind.Approve, DecidedByUserId = _c.E.OtroCajero.Id, DecidedByName = "oscar", DecidedAt = DateTime.UtcNow });
        _c.E.Db.ApprovalRequests.Add(aprobada);
        await _c.E.Db.SaveChangesAsync();
        _c.E.Como(_c.E.Supervisor);

        var r = await _c.Ajustar().Handle(new RegisterSessionAdjustmentCommand(sesion.PublicId, SessionAdjustmentKind.CountCorrection, _c.Efectivo.PublicId,
            1_000, "Apareció la moneda", Guid.NewGuid()) { ApprovalRequestPublicId = aprobada.PublicId }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code + r.Error.Message : string.Empty);
        r.Value.Number.Should().Be("000001");
        var ajuste = await _c.E.Db.CashSessionAdjustments.SingleAsync();
        ajuste.ApprovedByUserId.Should().Be(_c.E.OtroCajero.Id);
        var hecho = await _c.E.Db.IntegrationMessages.SingleAsync(m => m.Type == AjusteDeSesionRegistradoV1.Type);
        hecho.PayloadJson.Should().Contain(DiferenciaDeArqueoDeRecaudoAprobadaV1.Type, "el ajuste se enlaza con el hecho de la diferencia que corrige");
        (await _c.E.Db.ReceiptsCashSessions.SingleAsync(s => s.PublicId == sesion.PublicId)).Status.Should().Be(CashSessionState.Closed, "no se reabre");
    }

    [Fact]
    public async Task El_arqueo_sorpresivo_lleva_acta_y_no_cierra_la_sesion()
    {
        var sesion = await _c.AbrirAsync();
        _c.E.Como(_c.E.Supervisor);

        var r = await _c.Sorpresivo().Handle(new RegisterSurpriseCountCommand(sesion.PublicId, Guid.NewGuid(), [_c.EnBilletes(null, (_c.B50, 10))],
            _c.E.OtroCajero.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Kind.Should().Be(CashCountKind.Surprise);
        r.Value.SessionStatus.Should().Be(CashSessionState.Open);
        var arqueo = await _c.E.Db.ReceiptsCashCounts.SingleAsync();
        arqueo.MinutesAttachmentPublicId.Should().NotBeNull();
        arqueo.VerificationCode.Should().NotBeNullOrEmpty();
        (await _c.E.Db.IntegrationMessages.CountAsync()).Should().Be(0, "un arqueo sorpresivo no mueve dinero");
    }

    [Fact]
    public async Task El_billete_retenido_lleva_acta_y_no_toca_el_saldo_de_la_boveda()
    {
        await _c.SurtirBovedaAsync((_c.B50, 4));
        var sinActa = await _c.MoverBoveda().Handle(new RegisterVaultMovementCommand(_c.Boveda.PublicId, VaultMovementKind.RetainedCounterfeit,
            CajaDePrueba.Billetes((_c.B50, 1)), "Billete falso", _c.E.OtroCajero.PublicId), default);
        sinActa.Error.Code.Should().Be(ErroresDeRecaudo.CodigoActaRequerida);

        var r = await _c.MoverBoveda().Handle(new RegisterVaultMovementCommand(_c.Boveda.PublicId, VaultMovementKind.RetainedCounterfeit,
            CajaDePrueba.Billetes((_c.B50, 1)), "Billete falso", _c.E.OtroCajero.PublicId, MinutesAttachmentPublicId: Guid.NewGuid()), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Amount.Should().Be(0);
        (await MovimientosDeBoveda.SaldoAsync(_c.E.Db, _c.Boveda.Id, default)).Should().Be(200_000);
    }

    [Fact]
    public async Task El_saldo_de_la_boveda_es_una_proyeccion_que_se_verifica_contra_sus_movimientos()
    {
        await _c.SurtirBovedaAsync((_c.B50, 4), (_c.B20, 5));
        (await MovimientosDeBoveda.DenominacionesQueNoCuadranAsync(_c.E.Db, _c.Boveda.Id, default)).Should().BeEmpty();

        var saldo = await _c.E.Db.VaultDenominationBalances.FirstAsync(b => b.CashDenominationId == _c.B20.Id);
        saldo.Quantity = 4;
        await _c.E.Db.SaveChangesAsync();

        (await MovimientosDeBoveda.DenominacionesQueNoCuadranAsync(_c.E.Db, _c.Boveda.Id, default)).Should().ContainSingle().Which.Should().Be(_c.B20.Id);
    }

    [Fact]
    public async Task La_boveda_exige_doble_custodia_en_sus_movimientos()
    {
        _c.E.Como(_c.E.Supervisor);
        var r = await _c.MoverBoveda().Handle(new RegisterVaultMovementCommand(_c.Boveda.PublicId, VaultMovementKind.RemittanceIn,
            CajaDePrueba.Billetes((_c.B50, 1)), "Remesa devuelta"), default);
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoDobleCustodia);
    }
}
