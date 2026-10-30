using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Sessions;

/// <summary>
/// Feature 015, T0083 y T0095 (FR-033, FR-034, FR-184, FR-185): el cierre del día exige todas las sesiones cerradas (<c>Receipts.Day.SessionsOpen</c>) y sin
/// diferencias por aprobar (<c>.DifferencesPending</c>); el suplente cierra con su suplencia vigente; la tarea <c>recaudo.sesiones-olvidadas</c> marca
/// <c>ForgottenOpen</c> pasada la hora de corte y alerta <c>Recaudo.SesionOlvidada</c>; el supervisor cierra con testigo y la diferencia la aprueba un tercero;
/// la olvidada no recibe operaciones; y una hora de corte que no cae antes del ciclo de Cartera se rechaza
/// (<c>Receipts.Setup.CutoffAfterLendingCycle</c>). Escrita en la fase 5; se corre al final del plan.
/// </summary>
public class CierreDelDiaYSesionOlvidadaTests
{
    private readonly CajaDePrueba _c = new();

    private Task<IngenIA365ERP.Application.Common.Models.Result<DayCloseDto>> CerrarDiaAsync() =>
        _c.CerrarDia().Handle(new CloseCollectionPointDayCommand(_c.Punto.PublicId, EscenarioDeRecaudo.Hoy), default);

    [Fact]
    public async Task El_dia_no_cierra_con_sesiones_abiertas_ni_con_diferencias_por_aprobar()
    {
        var sesion = await _c.AbrirAsync();
        _c.E.Como(_c.E.Supervisor);
        (await CerrarDiaAsync()).Error.Code.Should().Be(ErroresDeRecaudo.CodigoDiaConSesionesAbiertas);

        _c.E.Como(_c.E.Cajera);
        await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, [_c.EnBilletes("No cuadra", (_c.B50, 9))]), default);
        _c.E.Como(_c.E.Supervisor);
        (await CerrarDiaAsync()).Error.Code.Should().Be(ErroresDeRecaudo.CodigoDiaConDiferencias);
    }

    [Fact]
    public async Task El_dia_cierra_una_sola_vez_con_sus_totales_por_medio_y_su_codigo()
    {
        var sesion = await _c.AbrirAsync();
        await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, [_c.EnBilletes(null, (_c.B50, 10))]), default);
        _c.E.Como(_c.E.Supervisor);

        var r = await CerrarDiaAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.SessionCount.Should().Be(1);
        r.Value.Counted.Should().Be(500_000);
        r.Value.VerificationCode.Should().HaveLength(16);
        r.Value.ByDelegate.Should().BeFalse();
        (await CerrarDiaAsync()).Error.Code.Should().Be(ErroresDeRecaudo.CodigoDiaCerrado);
        _c.E.Como(_c.E.OtroCajero);
        var tarde = await _c.Abrir().Handle(new OpenCashSessionCommand(_c.C2.PublicId, CajaDePrueba.Billetes((_c.B50, 10))), default);
        tarde.Error.Code.Should().Be(ErroresDeRecaudo.CodigoDiaCerrado);
    }

    [Fact]
    public async Task El_suplente_con_suplencia_vigente_cierra_y_queda_registrado()
    {
        var sesion = await _c.AbrirAsync();
        await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, [_c.EnBilletes(null, (_c.B50, 10))]), default);
        _c.E.Db.SupervisorDelegations.Add(new SupervisorDelegation
        {
            CollectionPointId = _c.Punto.Id, SupervisorUserId = _c.E.Supervisor.Id, DelegateUserId = _c.E.OtroCajero.Id,
            ValidFrom = EscenarioDeRecaudo.Hoy, Reason = "Vacaciones",
        });
        await _c.E.Db.SaveChangesAsync();
        _c.E.Como(_c.E.OtroCajero);

        var r = await CerrarDiaAsync();

        r.Value.ByDelegate.Should().BeTrue();
    }

    [Fact]
    public async Task Pasada_la_hora_de_corte_la_sesion_queda_olvidada_y_se_alerta()
    {
        var sesion = await _c.AbrirAsync();
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.RecaudoHoraDeCorte, "09:00");

        var marcadas = await _c.Olvidadas().MarcarAsync(default);

        marcadas.Should().Be(1);
        var s = await _c.E.Db.ReceiptsCashSessions.SingleAsync();
        s.Status.Should().Be(CashSessionState.ForgottenOpen);
        s.IsForgotten.Should().BeTrue();
        await _c.E.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoSesionOlvidada), Arg.Any<CancellationToken>());
        var r = await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, [_c.EnBilletes(null, (_c.B50, 10))]), default);
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSesionOlvidada);
    }

    [Fact]
    public async Task Antes_de_la_hora_de_corte_la_sesion_del_dia_sigue_abierta()
    {
        await _c.AbrirAsync();
        (await _c.Olvidadas().MarcarAsync(default)).Should().Be(0, "son las 10:00 y el corte por defecto es a las 18:00");
    }

    [Fact]
    public async Task El_supervisor_cierra_la_olvidada_con_testigo_y_la_diferencia_la_aprueba_un_tercero()
    {
        var sesion = await _c.AbrirAsync();
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.RecaudoHoraDeCorte, "09:00");
        await _c.Olvidadas().MarcarAsync(default);
        _c.E.Como(_c.E.Supervisor);

        var sinTestigo = await _c.CerrarOlvidada().Handle(new CloseForgottenSessionCommand(sesion.PublicId, _c.E.CajeraBis.PublicId,
            [_c.EnBilletes("Faltan", (_c.B50, 9))]), default);
        sinTestigo.Error.Code.Should().Be(ErroresDeRecaudo.CodigoTestigoRequerido, "el testigo es la misma persona que la cajera");

        var cerrada = await _c.CerrarOlvidada().Handle(new CloseForgottenSessionCommand(sesion.PublicId, _c.E.OtroCajero.PublicId,
            [_c.EnBilletes("Faltan", (_c.B50, 9))]), default);
        cerrada.Value.SessionStatus.Should().Be(CashSessionState.ClosedWithDifferencePending);
        cerrada.Value.Kind.Should().Be(CashCountKind.Deferred);
        var linea = cerrada.Value.Lines.Single(l => l.PendingApproval);

        var elMismo = await _c.Aprobar().Handle(new ApproveCountDifferenceCommand(sesion.PublicId, linea.PublicId, "x"), default);
        elMismo.Error.Code.Should().Be(ErroresDeRecaudo.CodigoAutoaprobacionDeArqueo, "el supervisor que cerró no aprueba");
        _c.E.Como(_c.E.OtroCajero);
        (await _c.Aprobar().Handle(new ApproveCountDifferenceCommand(sesion.PublicId, linea.PublicId, "x"), default)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoAutoaprobacionDeArqueo, "el testigo no aprueba");
    }

    [Fact]
    public async Task Una_hora_de_corte_despues_del_ciclo_de_Cartera_se_rechaza()
    {
        ReglasDeParametrosDeRecaudo.CorteValido(new TimeOnly(18, 0), new TimeOnly(0, 30)).Should().BeTrue("el ciclo de madrugada es del día siguiente");
        ReglasDeParametrosDeRecaudo.CorteValido(new TimeOnly(18, 0), new TimeOnly(17, 0)).Should().BeFalse();
        var reglas = new ReglasDeParametrosDeRecaudo(new TimeOnly(17, 0));
        var definicion = ParametrosDeRecaudoYTesoreria.Definiciones.First(d => d.Clave == ParametrosDeRecaudoYTesoreria.RecaudoHoraDeCorte);

        var r = await reglas.EvaluarAsync(new AltaDeParametro(definicion, ParameterScopeKind.None, null, null, "18:00", EscenarioDeRecaudo.Hoy, false), default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoCorteDespuesDelCicloDeCartera);
    }
}
