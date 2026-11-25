using FluentAssertions;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Sessions;

/// <summary>
/// 2026-11-24 (pedido del dueño): con <c>Caja.ArqueoEstricto</c> apagado —el de fábrica— el arqueo de la caja de Recaudo es sencillo: se ve lo esperado
/// aunque la caja tenga arqueo ciego, el motivo se pide sólo fuera de la tolerancia, la diferencia queda resuelta al cerrar con el faltante al gasto, la
/// sesión olvidada la cierra su cajero o un supervisor sin testigo, y el arqueo sorpresivo y el ajuste no exigen adjunto.
/// </summary>
public class ArqueoSencilloTests
{
    private readonly CajaDePrueba _c = new(estricto: false);

    [Fact]
    public async Task El_parametro_viene_apagado()
    {
        (await ArqueoEstricto.EsAsync(_c.E.Parametros, EscenarioDeRecaudo.Hoy, default)).Should().BeFalse();
        (await new GetCashCountPolicyQueryHandler(_c.E.Parametros, _c.E.Reloj).Handle(new GetCashCountPolicyQuery(), default)).Value.Strict.Should().BeFalse();
    }

    [Fact]
    public async Task Se_ve_lo_esperado_aunque_la_caja_tenga_arqueo_ciego()
    {
        var sesion = await _c.AbrirAsync();

        var esperado = await _c.Esperado().Handle(new GetSessionExpectedQuery(sesion.PublicId), default);

        esperado.Value.Hidden.Should().BeFalse("Caja.ArqueoCiego viene encendido, pero sólo rige con el arqueo estricto");
        esperado.Value.Means.Should().Contain(m => m.Code == "EF" && m.Expected == 500_000);
    }

    [Fact]
    public async Task Dentro_de_la_tolerancia_no_pide_motivo()
    {
        var sesion = await _c.AbrirAsync();

        var r = await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId,
            [_c.EnBilletes(null, (_c.B50, 9), (_c.B20, 2), (_c.B1, 9))]), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.SessionStatus.Should().Be(CashSessionState.Closed);
        (await _c.E.Db.ReceiptsCashCounts.SingleAsync()).IsBlind.Should().BeFalse();
    }

    [Fact]
    public async Task Fuera_de_la_tolerancia_pide_motivo()
    {
        var sesion = await _c.AbrirAsync();

        var r = await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, [_c.EnBilletes(null, (_c.B50, 9))]), default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSinMotivo);
    }

    [Fact]
    public async Task La_diferencia_queda_resuelta_al_cerrar_con_el_faltante_al_gasto()
    {
        var sesion = await _c.AbrirAsync();

        var r = await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, [_c.EnBilletes("Faltó un billete", (_c.B50, 9))]), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.SessionStatus.Should().Be(CashSessionState.Closed, "nadie más tiene que aprobarla");
        r.Value.Lines.Should().NotContain(l => l.PendingApproval);
        var linea = await _c.E.Db.ReceiptsCashCountLines.SingleAsync(l => l.Difference != 0m);
        linea.Treatment.Should().Be(CountTreatment.Expense);
        linea.ApprovedByUserId.Should().Be(_c.E.Cajera.Id);
        linea.ApprovalReason.Should().Contain("arqueo sencillo");
        (await _c.E.Db.CashierReceivables.CountAsync()).Should().Be(0, "el faltante va al gasto, no al cajero");
        (await _c.E.Db.IntegrationMessages.CountAsync()).Should().Be(1, "el hecho de la diferencia sale igual");
    }

    [Fact]
    public async Task El_cajero_cierra_su_sesion_olvidada()
    {
        var sesion = await _c.AbrirAsync();
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.RecaudoHoraDeCorte, "09:00");
        await _c.Olvidadas().MarcarAsync(default);

        var r = await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, [_c.EnBilletes(null, (_c.B50, 10))]), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Kind.Should().Be(CashCountKind.Deferred);
    }

    [Fact]
    public async Task El_supervisor_cierra_la_olvidada_sin_testigo()
    {
        var sesion = await _c.AbrirAsync();
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.RecaudoHoraDeCorte, "09:00");
        await _c.Olvidadas().MarcarAsync(default);
        _c.E.Como(_c.E.Supervisor);

        var r = await _c.CerrarOlvidada().Handle(new CloseForgottenSessionCommand(sesion.PublicId, null, [_c.EnBilletes("Faltan", (_c.B50, 9))]), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.SessionStatus.Should().Be(CashSessionState.Closed);
    }

    [Fact]
    public async Task El_arqueo_sorpresivo_no_exige_acta()
    {
        var sesion = await _c.AbrirAsync();
        _c.E.Como(_c.E.Supervisor);

        var r = await _c.Sorpresivo().Handle(new RegisterSurpriseCountCommand(sesion.PublicId, null, [_c.EnBilletes(null, (_c.B50, 10))]), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
    }

    [Fact]
    public async Task Con_el_arqueo_estricto_el_sorpresivo_exige_acta()
    {
        var sesion = await _c.AbrirAsync();
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaArqueoEstricto, "true");
        _c.E.Como(_c.E.Supervisor);

        var r = await _c.Sorpresivo().Handle(new RegisterSurpriseCountCommand(sesion.PublicId, null, [_c.EnBilletes(null, (_c.B50, 10))]), default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoActaRequerida);
    }
}
