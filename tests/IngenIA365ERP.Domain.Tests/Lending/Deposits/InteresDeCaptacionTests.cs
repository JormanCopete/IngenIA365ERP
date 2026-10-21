using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US5 (T127, T129; research R05, D7–D9, D31; FR-020, FR-060–FR-062): el interés de captación contra sus casos dorados <c>Casos/interes-*.json</c>
/// —6.884 por saldo diario y por promedio, 3.649 por saldo mínimo (investigación §3.3), tramos escalonados y por total, cambio de tasa a mitad de mes,
/// febrero bisiesto, mínimo para interés y una consignación con fecha valor hacia atrás—, calculados con <c>Casos/calcular.py</c>. Los saldos del día salen de
/// la reconstrucción de la cuenta (<see cref="ReconstruccionDeCuenta.SaldosDiarios"/>) a (D, R), como en el proceso. (nuevo)
/// </summary>
public class InteresDeCaptacionTests
{
    private sealed record EsperadoJson(decimal InteresCausado, decimal? InteresLiquidado, decimal? DiferenciaDeRedondeo);

    public static TheoryData<string> Casos()
    {
        var datos = new TheoryData<string>();
        foreach (var archivo in CasoDoradoDeCaptaciones.Archivos().Where(a => Path.GetFileName(a).StartsWith("interes-", StringComparison.Ordinal)))
            datos.Add(Path.GetFileName(archivo));
        return datos;
    }

    [Fact]
    public void Los_casos_de_la_investigacion_estan()
    {
        var nombres = CasoDoradoDeCaptaciones.Archivos().Select(Path.GetFileName).ToList();
        nombres.Should().Contain(["interes-01-saldo-diario.json", "interes-02-saldo-minimo.json", "interes-04-tramos-escalonados.json",
            "interes-05-tramos-por-saldo-total.json", "interes-06-cambio-de-tasa-a-mitad-de-mes.json", "interes-07-bisiesto.json"]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_coincide_con_el_caso_dorado(string archivo)
    {
        var caso = CasoDoradoDeCaptaciones.Cargar(Path.Combine(CasoDoradoDeCaptaciones.DirectorioDeCasos, archivo));
        foreach (var consulta in caso.Consultas)
        {
            var esperado = CasoDoradoDeCaptaciones.Leer<EsperadoJson>(consulta.Esperado);
            var desde = consulta.Desde ?? caso.Condiciones.FechaDeApertura;
            var periodo = Calcular(caso, desde, consulta.Fecha, consulta.InformacionHasta);

            periodo.Causado.Should().Be(esperado.InteresCausado, $"{archivo}, lo causado a {consulta.Fecha} con lo sabido en {consulta.InformacionHasta:O}");
            periodo.Dias.Should().OnlyContain(d => RedondeoDeCuenta(d.Causado), "la causación guarda seis decimales");
            if (esperado.InteresLiquidado is { } liquidado)
            {
                var redondeo = new RedondeoDeCartera(caso.Condiciones.UnidadDeRedondeo, RoundingMode.AwayFromZero, RoundingMoment.PerTotal);
                var liquidable = InteresDeCaptacion.Liquidable(periodo.Causado, redondeo);
                liquidable.Should().Be(liquidado, archivo);
                (liquidable - periodo.Causado).Should().Be(esperado.DiferenciaDeRedondeo!.Value, archivo);
            }
        }
    }

    [Fact]
    public void Un_dia_bajo_el_minimo_causa_cero_y_lo_explica()
    {
        var caso = CasoDoradoDeCaptaciones.Cargar(Path.Combine(CasoDoradoDeCaptaciones.DirectorioDeCasos, "interes-08-minimo-para-interes.json"));
        var periodo = Calcular(caso, new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 30), DateTime.MaxValue);
        var dia = periodo.Dias.Single(d => d.Dia == new DateOnly(2026, 4, 25));
        dia.BajoElMinimo.Should().BeTrue();
        dia.Causado.Should().Be(0m);
        dia.Explicacion.Should().Contain("mínimo para interés");
        periodo.Dias.Single(d => d.Dia == new DateOnly(2026, 4, 5)).Causado.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void La_causacion_es_por_diferencia_de_acumulados_y_suma_lo_causado_de_cada_dia()
    {
        var caso = CasoDoradoDeCaptaciones.Cargar(Path.Combine(CasoDoradoDeCaptaciones.DirectorioDeCasos, "interes-02-saldo-minimo.json"));
        var periodo = Calcular(caso, new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 30), DateTime.MaxValue);
        // Con el método del mínimo, el día en que el saldo baja el acumulado se recalcula y lo causado ese día es negativo: el libro no deriva.
        periodo.Dias.Single(d => d.Dia == new DateOnly(2026, 4, 21)).Causado.Should().BeNegative();
        periodo.Dias.Last().Acumulado.Should().Be(periodo.Causado);
    }

    [Fact]
    public void Un_dia_suspendido_no_causa()
    {
        var tasa = new TasaDeCaptacionDelDia(0.03m, [], RateTierMode.WholeBalance, 0m, DayCountConvention.Actual365, RateEquivalence.Compound,
            RepaymentPeriodicity.Monthly);
        var dias = Enumerable.Range(1, 3).Select(i => new DiaDeCaptacion(new DateOnly(2026, 4, i), 1_000_000m, tasa, Suspendido: i == 2)).ToList();
        var periodo = InteresDeCaptacion.Calcular(new EntradaDeInteres(BalanceMethod.DailyBalance, new DateOnly(2026, 3, 31), dias));
        periodo.Dias[1].Causado.Should().Be(0m);
        periodo.Dias[1].Suspendido.Should().BeTrue();
        periodo.Dias[0].Causado.Should().BeGreaterThan(0m);
    }

    [Theory]
    [InlineData(RepaymentPeriodicity.Monthly, null, "2026-04-20", "2026-04-01", "2026-04-30")]
    [InlineData(RepaymentPeriodicity.Monthly, (byte)15, "2026-04-20", "2026-04-16", "2026-05-15")]
    [InlineData(RepaymentPeriodicity.Monthly, (byte)15, "2026-04-15", "2026-03-16", "2026-04-15")]
    [InlineData(RepaymentPeriodicity.Monthly, (byte)31, "2026-02-27", "2026-02-01", "2026-02-28")]
    [InlineData(RepaymentPeriodicity.Quarterly, null, "2026-05-02", "2026-04-01", "2026-06-30")]
    [InlineData(RepaymentPeriodicity.Quarterly, (byte)15, "2026-01-10", "2025-12-16", "2026-03-15")]
    [InlineData(RepaymentPeriodicity.Quarterly, (byte)15, "2026-03-20", "2026-03-16", "2026-06-15")]
    public void El_periodo_de_liquidacion(RepaymentPeriodicity periodicidad, byte? dia, string fecha, string desde, string hasta)
    {
        var p = InteresDeCaptacion.PeriodoDeLiquidacion(periodicidad, dia, DateOnly.Parse(fecha));
        p.Should().Be((DateOnly.Parse(desde), DateOnly.Parse(hasta)));
        InteresDeCaptacion.PeriodoDeLiquidacion(RepaymentPeriodicity.SinglePayment, null, DateOnly.Parse(fecha)).Should().BeNull();
    }

    // ------------------------------------------------------------------------------------------------------- piezas --

    private static bool RedondeoDeCuenta(decimal valor) => decimal.Round(valor, InteresDeCaptacion.DecimalesDeCausacion) == valor;

    /// <summary>La ventana (desde, hasta] del caso con la información hasta R: saldos de la reconstrucción y la tasa que rige cada día.</summary>
    internal static InteresDelPeriodo Calcular(CasoDoradoDeCaptaciones caso, DateOnly desde, DateOnly hasta, DateTime informacionHasta)
    {
        var c = caso.Condiciones;
        var libro = caso.Operaciones.Select((o, i) => new MovimientoDeCuenta(i + 1,
            Enum.Parse<MemberAccountTransactionKind>(o.Tipo), MemberAccountConcept.Principal,
            o.Tipo is "Withdrawal" or "TransferOut" ? -o.Monto : o.Monto, o.FechaValor, o.RegistradoEn.ToUniversalTime())).ToList();
        var saldos = ReconstruccionDeCuenta.SaldosDiarios(libro, desde, hasta, informacionHasta == DateTime.MaxValue ? DateTime.MaxValue : informacionHasta.ToUniversalTime());
        var dias = saldos.Select(s => new DiaDeCaptacion(s.Dia, s.Saldo(MemberAccountConcept.Principal), TasaDelDia(c, s.Dia))).ToList();
        return InteresDeCaptacion.Calcular(new EntradaDeInteres(Enum.Parse<BalanceMethod>(c.MetodoDeSaldo), desde, dias));
    }

    private static TasaDeCaptacionDelDia TasaDelDia(CasoDoradoDeCaptaciones.CondicionesJson c, DateOnly dia)
    {
        var ea = c.TasaEA ?? 0m;
        var tramos = c.Tramos;
        var modo = c.ModoDeTramos;
        foreach (var cambio in c.CambiosDeTasa.Where(x => x.Desde <= dia).OrderBy(x => x.Desde))
        {
            ea = cambio.TasaEA ?? 0m;
            tramos = cambio.Tramos ?? tramos;
            modo = cambio.ModoDeTramos ?? modo;
        }
        return new TasaDeCaptacionDelDia(ea, tramos.Select(t => new TramoDeInteres(t.Desde, t.Hasta, t.Ea)).ToList(),
            modo is null ? RateTierMode.WholeBalance : Enum.Parse<RateTierMode>(modo), c.SaldoMinimoParaInteres,
            Enum.Parse<DayCountConvention>(c.Convencion), Enum.Parse<RateEquivalence>(c.Equivalencia),
            c.PeriodicidadDeLiquidacion is null ? RepaymentPeriodicity.Monthly : Enum.Parse<RepaymentPeriodicity>(c.PeriodicidadDeLiquidacion));
    }
}

/// <summary>
/// Feature 014, US5 (T128, T129; research R06, D10, P2, P27; FR-021, FR-063): la retención sobre rendimientos contra sus casos dorados
/// <c>Casos/retencion-*.json</c> —umbral no alcanzado, alcanzado con y sin componente inflacionario, el CDAT de la investigación (8.702 con el componente,
/// 19.524 sin él), el CDAT bajo el umbral si alcanza a todas las clases, la persona exenta y el autorretenedor—. (nuevo)
/// </summary>
public class RetencionSobreRendimientosTests
{
    private sealed record PoliticaJson(decimal Tarifa, decimal UmbralDiarioUvt, decimal Uvt, WithholdingThresholdScope Alcance, bool ComponenteInflacionarioAplica,
        decimal? ComponenteInflacionario, decimal UnidadDeRedondeo);

    private sealed record TitularJson(PerfilDeRetencion Perfil, bool PersonaNaturalNoObligada);

    private sealed record DiaJson(DateOnly Dia, decimal Interes);

    private sealed record EntradaJson(PoliticaJson Politica, TitularJson Titular, DepositClass Clase, decimal InteresBruto, List<DiaJson> InteresesDiarios);

    private sealed record EsperadoJson(bool UmbralAlcanzado, decimal Componente, decimal BaseGravable, decimal Valor, string? Motivo);

    public static TheoryData<string> Casos()
    {
        var datos = new TheoryData<string>();
        foreach (var archivo in CasoDoradoDeCaptaciones.Archivos().Where(a => Path.GetFileName(a).StartsWith("retencion-", StringComparison.Ordinal)))
            datos.Add(Path.GetFileName(archivo));
        return datos;
    }

    [Fact]
    public void Hay_casos_de_retencion()
    {
        Casos().Count.Should().BeGreaterThanOrEqualTo(8);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_coincide_con_el_caso_dorado(string archivo)
    {
        var caso = CasoDoradoDeCaptaciones.Cargar(Path.Combine(CasoDoradoDeCaptaciones.DirectorioDeCasos, archivo));
        var e = CasoDoradoDeCaptaciones.Leer<EntradaJson>(caso.Entrada);
        var esperado = CasoDoradoDeCaptaciones.Leer<EsperadoJson>(caso.Esperado);
        var p = e.Politica;
        var r = RetencionSobreRendimientos.Calcular(
            new PoliticaDeRetencion(p.Tarifa, p.UmbralDiarioUvt, p.Uvt, p.Alcance, p.ComponenteInflacionarioAplica, p.ComponenteInflacionario, p.UnidadDeRedondeo),
            new EntradaDeRetencion(e.Titular.Perfil, e.Titular.PersonaNaturalNoObligada, e.Clase, e.InteresBruto,
                e.InteresesDiarios.Select(d => new InteresDiarioDeRetencion(d.Dia, d.Interes)).ToList()));

        (r.UmbralAlcanzado, r.ComponenteInflacionarioValor, r.BaseGravable, r.Valor, r.MotivoDeExencion)
            .Should().Be((esperado.UmbralAlcanzado, esperado.Componente, esperado.BaseGravable, esperado.Valor, esperado.Motivo), archivo);
        r.Explicacion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void El_umbral_en_pesos_es_el_de_la_investigacion()
    {
        new PoliticaDeRetencion(0.07m, 0.055m, 52_374m, WithholdingThresholdScope.ExceptTermDeposit, true, null, 1m).UmbralEnPesos.Should().Be(2_880.57m);
    }

    [Fact]
    public void Sin_el_componente_publicado_no_se_descuenta()
    {
        var r = RetencionSobreRendimientos.Calcular(
            new PoliticaDeRetencion(0.04m, 0.055m, 52_374m, WithholdingThresholdScope.ExceptTermDeposit, true, null, 1m),
            new EntradaDeRetencion(PerfilDeRetencion.Standard, true, DepositClass.TermDeposit, 488_088m, []));
        r.Valor.Should().Be(19_524m);
        r.ComponenteInflacionario.Should().BeNull();
    }
}
