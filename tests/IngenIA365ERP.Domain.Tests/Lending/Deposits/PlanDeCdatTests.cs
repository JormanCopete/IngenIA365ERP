using FluentAssertions;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US6 (T166, T167; research R11; FR-023, FR-026, SC-002): el motor del CDAT contra sus casos dorados <c>Casos/cdat-*.json</c> —los de la
/// investigación (481.244 en base 365, 488.088 en base 360, 6 × 79.741 = 478.446 vencido, 79.111 anticipado), la prórroga con capitalización neta de
/// retención, la redención anticipada por reliquidación y por descuento, el vencimiento en día no hábil y un plazo irregular—, calculados a mano por
/// <c>Casos/calcular.py</c>. Más las reglas que no son de un caso: plazo de emisión, rangos, periodicidades y la causación que cuadra con el plan.
/// </summary>
public class PlanDeCdatTests
{
    public static TheoryData<string> Casos()
    {
        var datos = new TheoryData<string>();
        foreach (var archivo in CasoDoradoDeCaptaciones.Archivos().Where(a => Path.GetFileName(a).StartsWith("cdat-", StringComparison.Ordinal)))
            datos.Add(Path.GetFileName(archivo));
        return datos;
    }

    [Fact]
    public void Estan_los_casos_de_la_investigacion()
    {
        var nombres = CasoDoradoDeCaptaciones.Archivos().Select(Path.GetFileName).ToList();
        nombres.Should().Contain(["cdat-01-vencimiento-base-365.json", "cdat-02-vencimiento-base-360.json", "cdat-03-mensual-vencido.json",
            "cdat-04-mensual-anticipado.json", "cdat-05-prorroga-con-capitalizacion.json", "cdat-06-redencion-anticipada-reliquidacion.json",
            "cdat-07-vencimiento-en-dia-no-habil.json"]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_reproduce_el_caso_dorado(string archivo)
    {
        var caso = CasoDoradoDeCaptaciones.Cargar(Path.Combine(CasoDoradoDeCaptaciones.DirectorioDeCasos, archivo));
        var e = CasoDoradoDeCaptaciones.Leer<EntradaJson>(caso.Entrada);
        var esperado = CasoDoradoDeCaptaciones.Leer<EsperadoJson>(caso.Esperado);
        var calendario = Calendario(e.Festivos);
        var c = e.Condiciones.ACondiciones();
        var plan = PlanDeCdat.Generar(c, calendario);

        plan.Vencimiento.Should().Be(DateOnly.Parse(esperado.Vencimiento), archivo);
        plan.FechaDePagoAlVencimiento.Should().Be(DateOnly.Parse(esperado.FechaDePagoAlVencimiento), archivo);
        plan.PlazoDeEmision.ToString().Should().Be(esperado.PlazoDeEmision, archivo);
        plan.Periodos.Select(p => (p.Numero, p.Desde.ToString("yyyy-MM-dd"), p.Hasta.ToString("yyyy-MM-dd"), p.FechaDePago.ToString("yyyy-MM-dd"), p.Dias, p.Interes))
            .Should().Equal(esperado.Periodos.Select(p => (p.Numero, p.Desde, p.Hasta, p.FechaDePago, p.Dias, p.Interes)), archivo);
        plan.InteresTotal.Should().Be(esperado.InteresTotal, archivo);

        if (esperado.FinDeLaGracia is { } gracia)
            PlanDeCdat.FinDeLaGracia(plan.Vencimiento, e.Gracia ?? 0, calendario).Should().Be(DateOnly.Parse(gracia), archivo);
        foreach (var a in esperado.Acumulados ?? [])
            PlanDeCdat.CausadoAcumulado(plan, DateOnly.Parse(a.Dia)).Should().Be(a.Acumulado, $"{archivo} al {a.Dia}");

        if (e.Redencion is { } rd && esperado.Redencion is { } er)
        {
            var fecha = DateOnly.Parse(rd.Fecha);
            var penalidad = rd.Penalidad is null
                ? null
                : new PenalidadDeCdat(Enum.Parse<EarlyRedemptionPenaltyMode>(rd.Penalidad.Modo), rd.Penalidad.Tasa, rd.Penalidad.Fraccion);
            var r = PlanDeCdat.Redimir(c, fecha, PlanDeCdat.CausadoAcumulado(plan, fecha), rd.InteresPagado, penalidad);
            r.DiasCorridos.Should().Be(er.DiasCorridos, archivo);
            r.InteresCausado.Should().Be(er.InteresCausado, archivo);
            r.InteresReliquidado.Should().Be(er.InteresReliquidado, archivo);
            r.AjusteDeCausacion.Should().Be(er.AjusteDeCausacion, archivo);
            r.PenalidadSobreInteres.Should().Be(er.PenalidadSobreInteres, archivo);
            r.PenalidadSobreCapital.Should().Be(er.PenalidadSobreCapital, archivo);
            r.InteresAPagar.Should().Be(er.InteresAPagar, archivo);
            // Lo causado más los ajustes menos lo que se paga deja el interés por pagar en cero.
            (r.InteresCausado + r.AjusteDeCausacion + r.AjustePorRedondeo - r.PenalidadSobreInteres - r.InteresAPagar).Should().Be(0m, archivo);
        }

        if (e.Prorroga is { } pr && esperado.Prorroga is { } ep)
        {
            // La retención va antes de capitalizar (FR-063): la calcula su motor, con el umbral diario fuera del CDAT (D10).
            var retencion = RetencionSobreRendimientos.Calcular(
                new PoliticaDeRetencion(pr.TarifaDeRetencion, 0m, 0m, WithholdingThresholdScope.ExceptTermDeposit, false, null, 1m),
                new EntradaDeRetencion(PerfilDeRetencion.Standard, true, DepositClass.TermDeposit, plan.InteresTotal, []));
            retencion.Valor.Should().Be(ep.Retencion, archivo);
            var capitalizado = pr.Capitaliza ? plan.InteresTotal - retencion.Valor : 0m;
            capitalizado.Should().Be(ep.Capitalizado, archivo);
            var nuevo = PlanDeCdat.Generar(c with { Capital = c.Capital + capitalizado, Inicio = plan.Vencimiento }, calendario);
            nuevo.Condiciones.Capital.Should().Be(ep.NuevoCapital, archivo);
            nuevo.Vencimiento.Should().Be(DateOnly.Parse(ep.NuevoVencimiento), archivo);
            nuevo.InteresTotal.Should().Be(ep.NuevoInteresTotal, archivo);
        }
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void La_causacion_diaria_de_cada_periodo_suma_su_interes(string archivo)
    {
        var caso = CasoDoradoDeCaptaciones.Cargar(Path.Combine(CasoDoradoDeCaptaciones.DirectorioDeCasos, archivo));
        var e = CasoDoradoDeCaptaciones.Leer<EntradaJson>(caso.Entrada);
        var plan = PlanDeCdat.Generar(e.Condiciones.ACondiciones(), Calendario(e.Festivos));
        foreach (var p in plan.Periodos)
        {
            var suma = 0m;
            for (var d = p.Desde.AddDays(1); d <= p.Hasta; d = d.AddDays(1)) suma += PlanDeCdat.CausadoDelDia(plan, d);
            suma.Should().Be(p.Interes, $"{archivo}, período {p.Numero}");
        }
        PlanDeCdat.CausadoEntre(plan, plan.Condiciones.Inicio, plan.Vencimiento).Should().Be(plan.InteresTotal, archivo);
        PlanDeCdat.CausadoDelDia(plan, plan.Vencimiento.AddDays(1)).Should().Be(0m, "después del vencimiento el plan no causa");
    }

    [Theory]
    [InlineData(30, IssueTermBand.UnderSixMonths)]
    [InlineData(179, IssueTermBand.UnderSixMonths)]
    [InlineData(180, IssueTermBand.SixToTwelve)]
    [InlineData(360, IssueTermBand.SixToTwelve)]
    [InlineData(361, IssueTermBand.TwelveToEighteen)]
    [InlineData(539, IssueTermBand.TwelveToEighteen)]
    [InlineData(540, IssueTermBand.EighteenOrMore)]
    [InlineData(1080, IssueTermBand.EighteenOrMore)]
    public void El_plazo_de_emision_sigue_los_rangos_del_CUIF(int dias, IssueTermBand esperado) =>
        PlanDeCdat.PlazoDeEmision(dias).Should().Be(esperado);

    [Fact]
    public void Un_plazo_fuera_del_rango_se_rechaza_con_el_rango()
    {
        PlanDeCdat.ValidarPlazo(20, 30, 720)!.Codigo.Should().Be(PlanDeCdat.CodigoPlazoFueraDeRango);
        PlanDeCdat.ValidarPlazo(800, 30, 720)!.Mensaje.Should().Contain("30").And.Contain("720");
        PlanDeCdat.ValidarPlazo(180, 30, 720).Should().BeNull();
        PlanDeCdat.ValidarPlazo(180, null, null).Should().BeNull();
    }

    [Fact]
    public void La_periodicidad_y_la_modalidad_las_admite_la_linea()
    {
        var admitidas = new[] { RepaymentPeriodicity.Monthly, RepaymentPeriodicity.SinglePayment };
        var modalidades = new[] { InterestTiming.Arrears };
        PlanDeCdat.ValidarPeriodicidad(RepaymentPeriodicity.Quarterly, InterestTiming.Arrears, 180, admitidas, modalidades)!.Codigo
            .Should().Be(PlanDeCdat.CodigoPeriodicidadNoAdmitida);
        PlanDeCdat.ValidarPeriodicidad(RepaymentPeriodicity.Monthly, InterestTiming.Advance, 180, admitidas, modalidades).Should().NotBeNull();
        PlanDeCdat.ValidarPeriodicidad(RepaymentPeriodicity.SinglePayment, InterestTiming.Advance, 180, [], []).Should().NotBeNull("al vencimiento no hay anticipado");
        PlanDeCdat.ValidarPeriodicidad(RepaymentPeriodicity.SemiAnnual, InterestTiming.Arrears, 90, [], []).Should().NotBeNull("más larga que el plazo");
        PlanDeCdat.ValidarPeriodicidad(RepaymentPeriodicity.Monthly, InterestTiming.Arrears, 180, admitidas, modalidades).Should().BeNull();
    }

    [Fact]
    public void Sin_dias_de_gracia_la_gracia_termina_el_dia_del_vencimiento()
    {
        var calendario = Calendario([]);
        var vencimiento = new DateOnly(2027, 7, 10); // sábado
        PlanDeCdat.FinDeLaGracia(vencimiento, 0, calendario).Should().Be(vencimiento);
        PlanDeCdat.FinDeLaGracia(vencimiento, 1, calendario).Should().Be(new DateOnly(2027, 7, 12));
    }

    [Fact]
    public void La_redencion_sin_penalidad_paga_lo_causado_redondeado()
    {
        var c = new CondicionesDeCdat(10_000_000m, 0.1m, new DateOnly(2027, 1, 13), 180, RepaymentPeriodicity.SinglePayment, InterestTiming.Arrears,
            DayCountConvention.Actual360, new RedondeoDeCartera(1m, RoundingMode.AwayFromZero, RoundingMoment.PerTotal));
        var r = PlanDeCdat.Redimir(c, new DateOnly(2027, 7, 12), 488_088m, 0m, null);
        r.InteresAPagar.Should().Be(488_088m);
        r.CostoDeLaRedencion.Should().Be(0m);
        r.AjustePorRedondeo.Should().Be(0m);
    }

    private static CalendarioHabil Calendario(IEnumerable<string>? festivos) =>
        new((festivos ?? []).Select(DateOnly.Parse), [DayOfWeek.Saturday, DayOfWeek.Sunday]);

    private sealed class EntradaJson
    {
        public CondicionesJson Condiciones { get; set; } = new();
        public List<string>? Festivos { get; set; }
        public int? Gracia { get; set; }
        public List<string>? CausacionEn { get; set; }
        public RedencionJson? Redencion { get; set; }
        public ProrrogaJson? Prorroga { get; set; }
    }

    private sealed class CondicionesJson
    {
        public decimal Capital { get; set; }
        public decimal TasaEA { get; set; }
        public string Inicio { get; set; } = string.Empty;
        public int PlazoDias { get; set; }
        public string Periodicidad { get; set; } = "SinglePayment";
        public string Modalidad { get; set; } = "Arrears";
        public string Convencion { get; set; } = "Actual365";
        public decimal UnidadDeRedondeo { get; set; } = 1m;
        public string ModoDeRedondeo { get; set; } = "AwayFromZero";

        public CondicionesDeCdat ACondiciones() => new(Capital, TasaEA, DateOnly.Parse(Inicio), PlazoDias, Enum.Parse<RepaymentPeriodicity>(Periodicidad),
            Enum.Parse<InterestTiming>(Modalidad), Enum.Parse<DayCountConvention>(Convencion),
            new RedondeoDeCartera(UnidadDeRedondeo, Enum.Parse<RoundingMode>(ModoDeRedondeo), RoundingMoment.PerTotal));
    }

    private sealed class RedencionJson
    {
        public string Fecha { get; set; } = string.Empty;
        public decimal InteresPagado { get; set; }
        public PenalidadJson? Penalidad { get; set; }
    }

    private sealed class PenalidadJson
    {
        public string Modo { get; set; } = string.Empty;
        public decimal? Tasa { get; set; }
        public decimal? Fraccion { get; set; }
    }

    private sealed class ProrrogaJson
    {
        public bool Capitaliza { get; set; }
        public decimal TarifaDeRetencion { get; set; }
    }

    private sealed class EsperadoJson
    {
        public string Vencimiento { get; set; } = string.Empty;
        public string FechaDePagoAlVencimiento { get; set; } = string.Empty;
        public string PlazoDeEmision { get; set; } = string.Empty;
        public List<PeriodoJson> Periodos { get; set; } = [];
        public decimal InteresTotal { get; set; }
        public string? FinDeLaGracia { get; set; }
        public List<AcumuladoJson>? Acumulados { get; set; }
        public RedencionEsperadaJson? Redencion { get; set; }
        public ProrrogaEsperadaJson? Prorroga { get; set; }
    }

    private sealed class PeriodoJson
    {
        public int Numero { get; set; }
        public string Desde { get; set; } = string.Empty;
        public string Hasta { get; set; } = string.Empty;
        public string FechaDePago { get; set; } = string.Empty;
        public int Dias { get; set; }
        public decimal Interes { get; set; }
    }

    private sealed class AcumuladoJson
    {
        public string Dia { get; set; } = string.Empty;
        public decimal Acumulado { get; set; }
    }

    private sealed class RedencionEsperadaJson
    {
        public int DiasCorridos { get; set; }
        public decimal InteresCausado { get; set; }
        public decimal? InteresReliquidado { get; set; }
        public decimal AjusteDeCausacion { get; set; }
        public decimal PenalidadSobreInteres { get; set; }
        public decimal PenalidadSobreCapital { get; set; }
        public decimal InteresAPagar { get; set; }
    }

    private sealed class ProrrogaEsperadaJson
    {
        public decimal Retencion { get; set; }
        public decimal Capitalizado { get; set; }
        public decimal NuevoCapital { get; set; }
        public string NuevoVencimiento { get; set; } = string.Empty;
        public decimal NuevoInteresTotal { get; set; }
    }
}
