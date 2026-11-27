using FluentAssertions;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Application.Tests.Lending.Accrual;

/// <summary>
/// Feature 025, fase 5 (2026-11-24; HU5, FR-016, D3): la causación por período calendario.
/// <list type="bullet">
/// <item>la política lee <c>Causacion.Periodo</c> por línea con caída al general y de fábrica es el ciclo de la cuota;</item>
/// <item>el ciclo diario reconoce la cuota el primer día del tramo del calendario que contiene su vencimiento, no el día siguiente al vencimiento anterior
/// (el crédito de Ana vence el primero de cada mes: la cuota 1 se reconoce el 01-01, no el 02-12);</item>
/// <item>la caja ofrece como cuota del período la que vence en el tramo en curso; pagada, no hay otra (la siguiente es anticipo), y la cancelación no
/// cobra lo causado de un tramo sin empezar.</item>
/// </list>
/// </summary>
public class PeriodoCalendarioTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;

    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;

    public async Task InitializeAsync() => _c = await CausacionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private async Task VigenciaAsync(string clave, string valor, int? linea = null)
    {
        _c.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, Value = valor, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedBy = "test",
            ScopeKind = linea is null ? ParameterScopeKind.None : ParameterScopeKind.CreditLine, ScopeId = linea ?? 0,
        });
        await _c.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task De_fabrica_es_el_ciclo_de_la_cuota_y_la_linea_puede_pedir_el_periodo_calendario()
    {
        var linea = _c.Credito.CreditLineId;
        var otra = linea + 1000;
        await VigenciaAsync(ParametrosDeCartera.CausacionMomento, ParametrosDeCartera.CausacionAlInicioDelCiclo);
        await VigenciaAsync(ParametrosDeCartera.CausacionPeriodo, ParametrosDeCartera.PeriodoCalendario, linea);

        var politica = await new LectorDePoliticaDeReconocimiento(_c.D.Parametros()).LeerAsync(Hoy, [linea, otra], default);
        politica.PorPeriodoCalendario(linea).Should().BeTrue("la excepción por línea");
        politica.Automatico(linea)!.PeriodoCalendario.Should().BeTrue();
        politica.PorPeriodoCalendario(otra).Should().BeFalse("de fábrica, «CicloDeLaCuota»: nada cambia");
        politica.Automatico(otra)!.PeriodoCalendario.Should().BeFalse();
        (await PeriodoDeLaCuota_Calendario(otra)).Should().BeFalse("las demás siguen en el ciclo de la cuota");
    }

    private Task<bool> PeriodoDeLaCuota_Calendario(int linea) =>
        IngenIA365ERP.Application.Lending.Common.PeriodoDeLaCuota.CalendarioAsync(_c.D.Parametros(), Hoy, linea, default);

    [Fact]
    public async Task Por_periodo_calendario_la_cuota_se_reconoce_el_primer_dia_del_tramo_de_su_vencimiento()
    {
        var hasta = new DateOnly(2027, 1, 2);
        var cargado = await _c.Cargador().CargarAsync(_c.Credito.Id, null, hasta, default);
        cargado.Should().NotBeNull();
        var entrada = cargado!.Entrada!;
        var plan = MotorDeCartera.Reconstruir(entrada, Hoy).Plan!;
        var cuota1 = plan.Cuotas[0];
        cuota1.Vencimiento.Should().Be(new DateOnly(2027, 1, 1), "el crédito de Ana vence el primero de cada mes");
        PuestaAlDiaDeCredito.DiaDeReconocimiento(cuota1, plan.Entrada.Periodicidad).Should().Be(cuota1.Vencimiento,
            "el tramo de enero empieza el 01-01, después del inicio de su ciclo (02-12)");

        var reconocer = new ReconocimientoAutomatico(ModoDeReconocimiento.IngresoDiferido, [LoanConcept.CurrentInterest]);
        static IEnumerable<(DateOnly Dia, int? Cuota)> Reconocidas(IReadOnlyList<DiaCausado> dias) =>
            dias.SelectMany(d => d.Filas.Where(f => f.Clase == LoanTransactionKind.PeriodRecognition).Select(f => (d.Dia, f.Cuota)));

        Reconocidas(PuestaAlDiaDeCredito.Causar(entrada, Hoy, hasta, reconocer)).Should()
            .Equal([(Hoy.AddDays(1), (int?)1), (new DateOnly(2027, 1, 2), (int?)2)], "por el ciclo de la cuota, el día siguiente al vencimiento anterior");
        Reconocidas(PuestaAlDiaDeCredito.Causar(entrada, Hoy, hasta, reconocer with { PeriodoCalendario = true })).Should()
            .Equal([(new DateOnly(2027, 1, 1), (int?)1)], "por período calendario, el primer día del tramo que contiene su vencimiento");
    }

    // ------------------------------------------------------------------------------------------------------------- caja --

    /// <summary>Un crédito de 1.200.000 a 12 cuotas mensuales desembolsado el 09-10-2026: vence el 9 de cada mes.</summary>
    private static readonly PlanDePagos Plan = GeneradorDePlan.Generar(new EntradaDePlan(1_200_000m, new DateOnly(2026, 10, 9), 12,
        RepaymentPeriodicity.Monthly, AmortizationSystem.LevelPayment, 0.24m, DayCountConvention.Thirty360, RateEquivalence.Compound,
        RedondeoDeCartera.AlCentavo));

    /// <summary>A <paramref name="fecha"/>, con la cuota 1 pagada o no y lo causado de la cuota que se está causando.</summary>
    private static EstadoDelCredito Estado(DateOnly fecha, bool cuota1Pagada, int cuotaEnCurso, decimal causado)
    {
        var saldos = Plan.Cuotas.Where(c => !cuota1Pagada || c.Numero > 1).Select(c => new SaldoDeCuota(c.Numero, LoanConcept.Principal, c.Capital)).ToList();
        saldos.Add(new SaldoDeCuota(cuotaEnCurso, LoanConcept.CurrentInterest, causado));
        return EstadoDelCredito.Armado(fecha, Plan, saldos);
    }

    [Fact]
    public void El_24_con_la_cuota_del_mes_pagada_la_caja_no_ofrece_otra_y_la_cancelacion_no_cobra_el_tramo_sin_empezar()
    {
        var dia24 = new DateOnly(2026, 11, 24);
        var estado = Estado(dia24, cuota1Pagada: true, cuotaEnCurso: 2, causado: 5_000m);
        var cuota2 = Plan.Cuotas[1];

        ComprobantesDePago.CuotaDelPeriodo(estado).Should().Be(cuota2, "por el ciclo de la cuota, la próxima que vence");
        ComprobantesDePago.CuotaDelCiclo(estado).Should().Be(AplicadorDePagos.ValorDeCuota(estado, cuota2));
        ComprobantesDePago.CuotaDelPeriodo(estado, periodoCalendario: true).Should().BeNull("la de noviembre ya venció y está pagada");
        ComprobantesDePago.CuotaDelCiclo(estado, periodoCalendario: true).Should().Be(0m, "pagar la de diciembre es un anticipo");
        ComprobantesDePago.Exigible(estado, periodoCalendario: true).Should().Be(0m, "lo causado de diciembre no es del tramo en curso");

        AplicadorDePagos.ValorParaCancelar(estado, cuotaCompleta: false).Should().Be(Plan.Cuotas.Skip(1).Sum(c => c.Capital) + 5_000m);
        AplicadorDePagos.ValorParaCancelar(estado, cuotaCompleta: false, periodoCalendario: true).Should().Be(Plan.Cuotas.Skip(1).Sum(c => c.Capital),
            "lo causado de un tramo sin empezar nunca se cobra");

        var d = AplicadorDePagos.Distribuir(estado, new SolicitudDeDistribucion(PaymentModality.CycleInstallment, 100_000m, dia24) { PeriodoCalendario = true },
            Prelacion.PorDefecto, RedondeoDeCartera.AlCentavo);
        (d.Lineas.Count, d.Anticipo, d.Excedente).Should().Be((0, 0m, 100_000m), "la cuota del período ya está pagada");
    }

    [Fact]
    public void El_5_la_cuota_del_periodo_es_la_que_vence_el_9_y_su_causado_es_exigible()
    {
        var dia5 = new DateOnly(2026, 11, 5);
        var estado = Estado(dia5, cuota1Pagada: false, cuotaEnCurso: 1, causado: 20_000m);
        var cuota1 = Plan.Cuotas[0];

        ComprobantesDePago.CuotaDelPeriodo(estado, periodoCalendario: true).Should().Be(cuota1);
        ComprobantesDePago.Exigible(estado).Should().Be(0m, "por el ciclo de la cuota todavía no vence nada");
        ComprobantesDePago.Exigible(estado, periodoCalendario: true).Should().Be(20_000m, "lo causado del tramo en curso es exigible para un abono");
        ComprobantesDePago.CuotaDelCiclo(estado, periodoCalendario: true).Should().Be(ComprobantesDePago.CuotaDelCiclo(estado),
            "la cuota del período es la misma: el monto de la cuota no cambia");
    }

    [Fact]
    public void El_5_el_causado_del_tramo_no_es_mora_y_el_detalle_dice_cuanto_de_la_cuota_ya_es_exigible()
    {
        // 2026-11-26 (pedido del dueño): la caja mira lo vencido (Exigible sin el tramo en curso) y, si el recibo paga antes lo exigible, anticipar la cuota del
        // período cuesta su valor menos lo que de ella ya es exigible.
        var estado = Estado(new DateOnly(2026, 11, 5), cuota1Pagada: false, cuotaEnCurso: 1, causado: 20_000m);

        var conCalendario = ComprobantesDePago.Cuotas(estado, periodoCalendario: true);
        conCalendario.Single(q => q.Number == 1).DueInPeriod.Should().Be(20_000m);
        conCalendario.Where(q => q.Number > 1).Should().OnlyContain(q => q.DueInPeriod == 0m, "sus tramos no han empezado");
        ComprobantesDePago.Cuotas(estado).Should().OnlyContain(q => q.DueInPeriod == 0m, "por el ciclo de la cuota nada de una cuota por vencer es exigible");
        ComprobantesDePago.Exigible(estado).Should().Be(0m, "nada vencido: no hay mora");
    }
}
