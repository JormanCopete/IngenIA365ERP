using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Sessions;

/// <summary>
/// Feature 015, T0082 (FR-031 a FR-033, FR-183; D6, D7, Q7): lo contado dentro de la tolerancia cierra (<c>Closed</c>); sobre ella deja
/// <c>ClosedWithDifferencePending</c> hasta que otra persona la apruebe (<c>Receipts.Count.SelfApproval</c>); el faltante va a cargo del cajero o al gasto, el
/// sobrante a sobrantes, con su hecho <c>DiferenciaDeArqueoAprobada</c>; un faltante bajo la tolerancia queda con motivo y sin aprobación; toda diferencia lleva
/// motivo. Escrita en la fase 5; se corre al final del plan.
/// </summary>
public class CierreDeSesionTests
{
    private readonly CajaDePrueba _c = new();

    private async Task<CashCountDto> ContarAsync(CashSessionDto sesion, params ConteoDeMedioDto[] conteos)
    {
        var r = await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, conteos), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code + ": " + r.Error.Message : string.Empty);
        return r.Value;
    }

    [Fact]
    public async Task Sin_diferencia_la_sesion_queda_cerrada()
    {
        var sesion = await _c.AbrirAsync();

        var arqueo = await ContarAsync(sesion, _c.EnBilletes(null, (_c.B50, 10)));

        arqueo.SessionStatus.Should().Be(CashSessionState.Closed);
        arqueo.TotalDifference.Should().Be(0);
        (await _c.E.Db.ReceiptsCashCounts.SingleAsync()).IsBlind.Should().BeTrue();
        (await _c.E.Db.IntegrationMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_diferencia_sin_motivo_no_se_registra()
    {
        var sesion = await _c.AbrirAsync();

        var r = await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, [_c.EnBilletes(null, (_c.B50, 9))]), default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSinMotivo);
        (await _c.E.Db.ReceiptsCashCounts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_faltante_bajo_la_tolerancia_queda_con_motivo_sin_aprobacion_y_a_cargo_del_cajero()
    {
        var sesion = await _c.AbrirAsync();

        var arqueo = await ContarAsync(sesion, _c.EnBilletes("Faltó una moneda", (_c.B50, 9), (_c.B20, 2), (_c.B1, 9)));

        arqueo.SessionStatus.Should().Be(CashSessionState.Closed);
        var linea = arqueo.Lines.Single(l => l.MeansCode == "EF");
        linea.Difference.Should().Be(-1_000);
        linea.WithinTolerance.Should().BeTrue();
        linea.PendingApproval.Should().BeFalse();
        (await _c.E.Db.CashierReceivables.SingleAsync()).Amount.Should().Be(1_000);
        (await _c.E.Db.IntegrationMessages.SingleAsync()).Type.Should().Be(DiferenciaDeArqueoAprobadaV1.Type);
    }

    [Fact]
    public async Task Sobre_la_tolerancia_queda_por_aprobar_y_la_cajera_no_la_aprueba()
    {
        var sesion = await _c.AbrirAsync();
        var arqueo = await ContarAsync(sesion, _c.EnBilletes("No cuadra", (_c.B50, 9)));
        arqueo.SessionStatus.Should().Be(CashSessionState.ClosedWithDifferencePending);
        arqueo.AmountToApprove.Should().Be(50_000);
        var linea = arqueo.Lines.Single(l => l.PendingApproval);

        _c.E.Como(_c.E.CajeraBis);
        var propia = await _c.Aprobar().Handle(new ApproveCountDifferenceCommand(sesion.PublicId, linea.PublicId, "La apruebo"), default);
        propia.Error.Code.Should().Be(ErroresDeRecaudo.CodigoAutoaprobacionDeArqueo);

        _c.E.Como(_c.E.Supervisor);
        var aprobada = await _c.Aprobar().Handle(new ApproveCountDifferenceCommand(sesion.PublicId, linea.PublicId, "Revisado con la cajera"), default);

        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? aprobada.Error.Message : string.Empty);
        aprobada.Value.SessionStatus.Should().Be(CashSessionState.Closed);
        (await _c.E.Db.CashierReceivables.SingleAsync()).Amount.Should().Be(50_000);
        (await _c.E.Db.IntegrationMessages.SingleAsync()).Type.Should().Be(DiferenciaDeArqueoAprobadaV1.Type);
        var guardada = await _c.E.Db.ReceiptsCashCountLines.SingleAsync(l => l.PublicId == linea.PublicId);
        guardada.ApprovedByUserId.Should().Be(_c.E.Supervisor.Id);
        guardada.ApprovalReason.Should().Be("Revisado con la cajera");
    }

    [Fact]
    public async Task El_aprobador_puede_llevar_el_faltante_al_gasto()
    {
        var sesion = await _c.AbrirAsync();
        var linea = (await ContarAsync(sesion, _c.EnBilletes("No cuadra", (_c.B50, 9)))).Lines.Single(l => l.PendingApproval);
        _c.E.Como(_c.E.Supervisor);

        var malTratada = await _c.Aprobar().Handle(new ApproveCountDifferenceCommand(sesion.PublicId, linea.PublicId, "x", CountTreatment.Surplus), default);
        malTratada.Error.Code.Should().Be(ErroresDeRecaudo.CodigoTratamientoInvalido);
        var r = await _c.Aprobar().Handle(new ApproveCountDifferenceCommand(sesion.PublicId, linea.PublicId, "Gasto", CountTreatment.Expense), default);

        r.IsSuccess.Should().BeTrue();
        (await _c.E.Db.CashierReceivables.CountAsync()).Should().Be(0);
        var hecho = await _c.E.Db.IntegrationMessages.SingleAsync();
        hecho.PayloadJson.Should().Contain(RolesDeRecaudo.GastoFaltante);
    }

    [Fact]
    public async Task Con_el_tratamiento_sellado_en_gasto_el_faltante_no_va_al_cajero()
    {
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaTratamientoFaltante, "Expense");
        var sesion = await _c.AbrirAsync();

        await ContarAsync(sesion, _c.EnBilletes("Moneda", (_c.B50, 9), (_c.B20, 2), (_c.B1, 9)));

        (await _c.E.Db.CashierReceivables.CountAsync()).Should().Be(0);
        (await _c.E.Db.ReceiptsCashCountLines.SingleAsync(l => l.Difference != 0)).Treatment.Should().Be(CountTreatment.Expense);
    }

    [Fact]
    public async Task Un_sobrante_va_a_sobrantes_con_su_fecha_de_paso_a_ingreso()
    {
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaSobrantesDiasAntesDeIngreso, "30");
        var sesion = await _c.AbrirAsync();

        await ContarAsync(sesion, _c.EnBilletes("Sobró", (_c.B50, 10), (_c.B1, 1)));

        var sobrante = await _c.E.Db.CashSurpluses.SingleAsync();
        sobrante.Amount.Should().Be(1_000);
        sobrante.ToIncomeFrom.Should().Be(EscenarioDeRecaudo.Hoy.AddDays(30));
        sobrante.Status.Should().Be(CashSurplusState.Liability);
    }

    [Fact]
    public async Task Una_sesion_cerrada_no_vuelve_a_contarse()
    {
        var sesion = await _c.AbrirAsync();
        await ContarAsync(sesion, _c.EnBilletes(null, (_c.B50, 10)));

        var otra = await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, [_c.EnBilletes(null, (_c.B50, 10))]), default);

        otra.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSesionNoAbierta);
    }
}
