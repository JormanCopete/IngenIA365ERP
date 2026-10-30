using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Receipts.Paymasters;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Receipts.Pagadurias;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Paymasters;

/// <summary>
/// Feature 015, R1, T0382 (FR-082; FR-010): el calendario legal de giro de las pagadurías.
/// <list type="bullet">
/// <item>la fecha legal es el pago de la nómina más <c>Libranza.DiasHabilesGiro</c> (3, «pendiente de validar») días hábiles con <c>ICalendarioHabil</c>: sin sábados,
/// domingos ni festivos;</item>
/// <item>es un aviso informativo: registrar la fecha no bloquea ni cambia nada del archivo ni de los giros;</item>
/// <item>lo descontado y no girado pasada la fecha legal levanta <c>Recaudo.GiroDePagaduriaTardio</c> y aparece en el informe de morosas.</item>
/// </list>
/// Prueba escrita, sin correr.
/// </summary>
public sealed class CalendarioDePagaduriasTests
{
    private readonly PagaduriasDePrueba _p = new();
    private readonly ICalendarioHabil _calendario = Substitute.For<ICalendarioHabil>();

    public CalendarioDePagaduriasTests()
    {
        // Un festivo el lunes 2026-11-16 (sólo para la prueba).
        _calendario.ObtenerAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<IReadOnlyCollection<DayOfWeek>>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(ci => new CalendarioHabil([new DateOnly(2026, 11, 16)], ci.ArgAt<IReadOnlyCollection<DayOfWeek>>(2)));
        _p.B.K.C.E.Como(_p.B.K.C.E.Supervisor);
    }

    private RegisterPaymasterPayDateCommandHandler Registrar() => new(_p.B.K.C.E.Db, _p.B.K.C.E.Alcance, _p.B.K.C.E.Actor, _calendario, _p.B.K.C.E.Parametros,
        _p.B.Auditoria, _p.B.K.C.E.Reloj);

    [Fact]
    public void La_fecha_legal_cuenta_dias_habiles_desde_el_dia_siguiente()
    {
        var habil = new CalendarioHabil([new DateOnly(2026, 11, 16)], DiasDeDescanso.SabadoYDomingo);
        // Viernes 13: lunes 16 festivo, martes 17, miércoles 18, jueves 19.
        CalendarioLegalDeGiro.FechaLegal(habil, new DateOnly(2026, 11, 13), 3).Should().Be(new DateOnly(2026, 11, 19));
        CalendarioLegalDeGiro.Estado(100m, 80m, 80m, new DateOnly(2026, 11, 19), new DateOnly(2026, 11, 25)).Tardio.Should().BeFalse("todo lo descontado se giró");
        CalendarioLegalDeGiro.Estado(100m, 80m, 30m, new DateOnly(2026, 11, 19), new DateOnly(2026, 11, 20)).Should()
            .Match<EstadoDelPeriodoDePagaduria>(e => e.Tardio && e.Pendiente == 50m);
    }

    [Fact]
    public async Task Registrar_la_fecha_de_pago_calcula_la_fecha_legal_con_el_parametro_y_no_bloquea_nada()
    {
        var archivo = await _p.RespondidoAsync(2);
        var estadoAntes = archivo.Status;

        var r = await Registrar().Handle(new RegisterPaymasterPayDateCommand(_p.Pagaduria.PublicId, archivo.PeriodYear, archivo.PeriodMonth, new DateOnly(2026, 11, 13))
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.LegalRemittanceDate.Should().Be(new DateOnly(2026, 11, 19), "Libranza.DiasHabilesGiro vale 3 por defecto");
        (await _p.B.K.C.E.Db.PaymasterCalendar.SingleAsync()).BusinessDays.Should().Be(3);
        (await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFiles.AsNoTracking().SingleAsync(f => f.Id == archivo.Id)).Status.Should().Be(estadoAntes);

        _p.B.K.C.E.Parametro(ParametrosDeRecaudoYTesoreria.LibranzaDiasHabilesGiro, "5");
        var otra = await Registrar().Handle(new RegisterPaymasterPayDateCommand(_p.Pagaduria.PublicId, archivo.PeriodYear, archivo.PeriodMonth, new DateOnly(2026, 11, 13))
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        otra.Value.LegalRemittanceDate.Should().Be(new DateOnly(2026, 11, 23), "el parámetro manda; registrar otra vez corrige la misma fila");
        (await _p.B.K.C.E.Db.PaymasterCalendar.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Lo_no_girado_pasada_la_fecha_legal_alerta_y_entra_al_informe_de_morosas()
    {
        var archivo = await _p.RespondidoAsync(2);
        await Registrar().Handle(new RegisterPaymasterPayDateCommand(_p.Pagaduria.PublicId, archivo.PeriodYear, archivo.PeriodMonth, EscenarioDeRecaudo.Hoy.AddDays(-20))
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        var morosas = await new GetPaymasterCalendarQueryHandler(_p.B.K.C.E.Db, _p.B.K.C.E.Alcance, _p.B.K.C.E.Reloj)
            .Handle(new GetPaymasterCalendarQuery(OnlyLate: true), default);
        morosas.Value.Should().ContainSingle(f => f.PaymasterPersonPublicId == _p.Pagaduria.PublicId && f.Pending == 200_000m);

        var tarea = new Application.Receipts.Tareas.AntiguedadDeRecaudo(_p.B.K.C.E.Db, _p.B.K.C.E.Parametros, _p.B.Movimientos, _calendario, _p.B.K.C.E.Alertas,
            _p.B.K.C.E.Reloj);
        (await tarea.GirosDePagaduriaTardiosAsync(default)).Should().Be(1);
        await _p.B.K.C.E.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoGiroDePagaduriaTardio),
            Arg.Any<CancellationToken>());
    }
}
