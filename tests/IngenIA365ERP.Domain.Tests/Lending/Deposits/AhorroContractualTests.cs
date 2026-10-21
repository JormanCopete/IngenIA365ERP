using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US7 (T181; FR-024, FR-026, FR-074; investigación financiera §3.4; SC-002): el motor del ahorro contractual contra sus casos dorados
/// <c>Casos/contractual-*.json</c> —12 cuotas de 200.000 al 6 % EA dan 2.465.306; al 8 %, 2.486.777 y la bonificación es 21.471; pagando sólo 10 de 12 el
/// contrato queda incumplido y se reliquida a la tasa de castigo—, calculados a mano por <c>Casos/calcular.py</c>. Más las reglas que no son de un caso: el
/// rango de la línea, la invariancia de capitalizar y los aportes del plan de un contrato migrado.
/// </summary>
public class AhorroContractualTests
{
    public static TheoryData<string> Casos()
    {
        var datos = new TheoryData<string>();
        foreach (var archivo in CasoDoradoDeCaptaciones.Archivos().Where(a => Path.GetFileName(a).StartsWith("contractual-", StringComparison.Ordinal)))
            datos.Add(Path.GetFileName(archivo));
        return datos;
    }

    [Fact]
    public void Estan_los_casos_de_la_investigacion_y_de_la_historia()
    {
        var nombres = CasoDoradoDeCaptaciones.Archivos().Select(Path.GetFileName).ToList();
        nombres.Should().Contain(["contractual-01-cumplido-tasa-base.json", "contractual-02-bonificacion-por-cumplimiento.json",
            "contractual-03-incumplido-pagando-10.json", "contractual-04-tolerancia-y-gracia.json"]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_reproduce_el_caso_dorado(string archivo)
    {
        var caso = CasoDoradoDeCaptaciones.Cargar(Path.Combine(CasoDoradoDeCaptaciones.DirectorioDeCasos, archivo));
        var e = CasoDoradoDeCaptaciones.Leer<EntradaJson>(caso.Entrada);
        var esperado = CasoDoradoDeCaptaciones.Leer<EsperadoJson>(caso.Esperado);
        var c = e.Condiciones.ACondiciones();
        var plan = AhorroContractual.Plan(c);

        plan.Vencimiento.Should().Be(DateOnly.Parse(esperado.Vencimiento), archivo);
        plan.PlazoDeEmision.ToString().Should().Be(esperado.PlazoDeEmision, archivo);
        plan.Cuotas.Should().HaveCount(esperado.Cuotas, archivo);
        plan.Cuotas[0].Vencimiento.Should().Be(DateOnly.Parse(esperado.PrimeraCuota), archivo);
        plan.Cuotas[^1].Vencimiento.Should().Be(DateOnly.Parse(esperado.UltimaCuota), archivo);
        plan.TotalPactado.Should().Be(esperado.TotalPactado, archivo);
        plan.InteresProyectadoBase.Should().Be(esperado.InteresProyectadoBase, archivo);
        plan.ValorFinalBase.Should().Be(esperado.ValorFinalBase, archivo);
        plan.InteresProyectadoBonificado.Should().Be(esperado.InteresProyectadoBonificado, archivo);
        plan.BonificacionProyectada.Should().Be(esperado.BonificacionProyectada, archivo);
        if (esperado.ValorFinalCumpliendo is { } cumpliendo) plan.ValorFinalCumpliendo.Should().Be(cumpliendo, archivo);

        var aportes = e.Aportes?.Select(a => new AporteDeContractual(DateOnly.Parse(a.Fecha), a.Monto)).ToList()
                      ?? plan.Cuotas.Select(q => new AporteDeContractual(q.Vencimiento, c.Cuota)).ToList();
        var corte = e.Corte is { } k ? DateOnly.Parse(k) : plan.Vencimiento;
        if (esperado.Bonificacion is { } eb)
            Comparar(AhorroContractual.Bonificacion(aportes, c.TasaBase, c.TasaBonificada!.Value, c.Convencion, corte, c.Redondeo), eb, archivo);
        if (esperado.Reliquidacion is { } er)
            Comparar(AhorroContractual.Reliquidar(aportes, c.TasaBase, c.TasaCastigo!.Value, c.Convencion, corte, c.Redondeo), er, archivo);

        if (e.Cumplimiento is { } cu && esperado.Cumplimiento is { } ec)
        {
            var cuotas = cu.Cuotas.Select(q => new CuotaParaCumplimiento(q.Numero, plan.Cuotas.First(p => p.Numero == q.Numero).Vencimiento, q.Valor ?? c.Cuota,
                q.Pagado, q.Anulada)).ToList();
            var r = AhorroContractual.Cumplimiento(cuotas, plan.Cuotas.Count, DateOnly.Parse(cu.Dia), cu.Tolerancia, cu.DiasDeGracia);
            r.Generadas.Should().Be(ec.Generadas, archivo);
            r.Pagadas.Should().Be(ec.Pagadas, archivo);
            r.Atrasadas.Should().Be(ec.Atrasadas, archivo);
            r.NumerosAtrasados.Should().Equal(ec.NumerosAtrasados, archivo);
            r.SuperaLaTolerancia.Should().Be(ec.SuperaLaTolerancia, archivo);
            r.Completo.Should().Be(ec.Completo, archivo);
        }
    }

    [Fact]
    public void Capitalizar_cada_mes_a_la_misma_tasa_compuesta_no_cambia_el_valor_final()
    {
        // FR-024: el interés de un aporte de su fecha al corte es A × ((1 + r_d)^N − 1) aunque se abone al capital cada mes (lo que hace la liquidación).
        var c = Condiciones();
        var aportes = new[] { new AporteDeContractual(new DateOnly(2027, 2, 15), 200_000m) };
        var directo = AhorroContractual.Interes(aportes, c.TasaBase, c.Convencion, new DateOnly(2027, 5, 15));
        var saldo = 200_000m;
        var desde = new DateOnly(2027, 2, 15);
        foreach (var hasta in new[] { new DateOnly(2027, 3, 15), new DateOnly(2027, 4, 15), new DateOnly(2027, 5, 15) })
        {
            saldo += AhorroContractual.Interes([new AporteDeContractual(desde, saldo)], c.TasaBase, c.Convencion, hasta);
            desde = hasta;
        }
        decimal.Round(saldo - 200_000m, 6).Should().Be(decimal.Round(directo, 6));
    }

    [Theory]
    [InlineData(150_000, 12, AhorroContractual.CodigoCuotaBajoElMinimo)]
    [InlineData(200_000, 5, AhorroContractual.CodigoPlazoFueraDeRango)]
    [InlineData(200_000, 37, AhorroContractual.CodigoPlazoFueraDeRango)]
    public void El_contrato_cabe_en_la_linea(int cuota, int meses, string codigo) =>
        AhorroContractual.ValidarContrato(cuota, meses, RepaymentPeriodicity.Monthly, 200_000m, 6, 36)!.Codigo.Should().Be(codigo);

    [Fact]
    public void Un_contrato_en_el_rango_no_tiene_problema() =>
        AhorroContractual.ValidarContrato(200_000m, 12, RepaymentPeriodicity.Monthly, 200_000m, 6, 36).Should().BeNull();

    [Fact]
    public void Un_plazo_menor_que_la_periodicidad_no_tiene_cuotas()
    {
        var act = () => AhorroContractual.Plan(Condiciones() with { Periodicidad = RepaymentPeriodicity.SemiAnnual, PlazoMeses = 5 });
        act.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(AhorroContractual.CodigoSinCuotas);
    }

    [Fact]
    public void Las_cuotas_empiezan_desde_el_vencimiento_de_la_anterior()
    {
        var plan = AhorroContractual.Plan(Condiciones());
        plan.CuotasEmpezadas(plan.Condiciones.Inicio).Select(q => q.Numero).Should().Equal(1);
        plan.CuotasEmpezadas(new DateOnly(2027, 2, 14)).Select(q => q.Numero).Should().Equal(1);
        plan.CuotasEmpezadas(new DateOnly(2027, 2, 15)).Select(q => q.Numero).Should().Equal(1, 2);
        plan.CuotasEmpezadas(plan.Vencimiento).Should().HaveCount(12);
        plan.CuotaQueVenceEl(new DateOnly(2027, 6, 15))!.Numero.Should().Be(5);
    }

    [Fact]
    public void Un_mes_corto_vence_el_ultimo_dia_sin_correr_las_cuotas_siguientes()
    {
        var plan = AhorroContractual.Plan(Condiciones() with { Inicio = new DateOnly(2027, 1, 31) });
        plan.Cuotas[0].Vencimiento.Should().Be(new DateOnly(2027, 2, 28));
        plan.Cuotas[1].Vencimiento.Should().Be(new DateOnly(2027, 3, 31));
    }

    [Fact]
    public void Los_aportes_de_un_contrato_migrado_son_las_cuotas_pagadas_del_plan()
    {
        var plan = AhorroContractual.Plan(Condiciones());
        var aportes = AhorroContractual.AportesDelPlan(plan, 3);
        aportes.Select(a => (a.Fecha, a.Monto)).Should().Equal(
            (new DateOnly(2027, 2, 15), 200_000m), (new DateOnly(2027, 3, 15), 200_000m), (new DateOnly(2027, 4, 15), 200_000m));
    }

    [Fact]
    public void Un_castigo_mayor_que_la_base_no_da_interes_de_mas()
    {
        var plan = AhorroContractual.Plan(Condiciones());
        var aportes = AhorroContractual.AportesDelPlan(plan, 12);
        AhorroContractual.Reliquidar(aportes, 0.06m, 0.09m, DayCountConvention.Thirty360, plan.Vencimiento, plan.Condiciones.Redondeo).Diferencia.Should().Be(0m);
    }

    [Fact]
    public void Una_cuota_anulada_no_cuenta_como_atrasada()
    {
        var plan = AhorroContractual.Plan(Condiciones());
        var cuotas = plan.Cuotas.Take(3).Select(q => new CuotaParaCumplimiento(q.Numero, q.Vencimiento, 200_000m, 0m, Anulada: q.Numero == 2)).ToList();
        var r = AhorroContractual.Cumplimiento(cuotas, 12, new DateOnly(2027, 5, 1), 0, 0);
        r.NumerosAtrasados.Should().Equal(1, 3);
        r.Generadas.Should().Be(2);
    }

    private static CondicionesDeContractual Condiciones() => new(200_000m, RepaymentPeriodicity.Monthly, 12, new DateOnly(2027, 1, 15), 0.06m, 0.08m, 0.03m,
        DayCountConvention.Thirty360, new RedondeoDeCartera(1m, RoundingMode.AwayFromZero, RoundingMoment.PerTotal));

    private static void Comparar(DiferenciaDeTasasDelContractual r, DiferenciaJson e, string archivo)
    {
        r.Aportes.Should().Be(e.Aportes, archivo);
        r.InteresBase.Should().Be(e.InteresBase, archivo);
        r.InteresOtraTasa.Should().Be(e.InteresOtraTasa, archivo);
        r.Diferencia.Should().Be(e.Diferencia, archivo);
        r.ValorFinalOtraTasa.Should().Be(e.ValorFinalOtraTasa, archivo);
    }

    // ------------------------------------------------------------------------------------------------------ esquema --

    public sealed class EntradaJson
    {
        public CondicionesJson Condiciones { get; set; } = new();
        public List<AporteJson>? Aportes { get; set; }
        public string? Corte { get; set; }
        public bool Bonificacion { get; set; }
        public bool Reliquidacion { get; set; }
        public CumplimientoJson? Cumplimiento { get; set; }
    }

    public sealed class CondicionesJson
    {
        public decimal Cuota { get; set; }
        public string Periodicidad { get; set; } = "Monthly";
        public int PlazoMeses { get; set; }
        public string Inicio { get; set; } = string.Empty;
        public decimal TasaBase { get; set; }
        public decimal? TasaBonificada { get; set; }
        public decimal? TasaCastigo { get; set; }
        public string Convencion { get; set; } = "Thirty360";
        public decimal UnidadDeRedondeo { get; set; } = 1m;
        public string ModoDeRedondeo { get; set; } = "AwayFromZero";

        public CondicionesDeContractual ACondiciones() => new(Cuota, Enum.Parse<RepaymentPeriodicity>(Periodicidad), PlazoMeses, DateOnly.Parse(Inicio), TasaBase,
            TasaBonificada, TasaCastigo, Enum.Parse<DayCountConvention>(Convencion),
            new RedondeoDeCartera(UnidadDeRedondeo, Enum.Parse<RoundingMode>(ModoDeRedondeo), RoundingMoment.PerTotal));
    }

    public sealed class AporteJson
    {
        public string Fecha { get; set; } = string.Empty;
        public decimal Monto { get; set; }
    }

    public sealed class CumplimientoJson
    {
        public string Dia { get; set; } = string.Empty;
        public int Tolerancia { get; set; }
        public int DiasDeGracia { get; set; }
        public List<CuotaJson> Cuotas { get; set; } = [];
    }

    public sealed class CuotaJson
    {
        public int Numero { get; set; }
        public decimal? Valor { get; set; }
        public decimal Pagado { get; set; }
        public bool Anulada { get; set; }
    }

    public sealed class EsperadoJson
    {
        public string Vencimiento { get; set; } = string.Empty;
        public string PlazoDeEmision { get; set; } = string.Empty;
        public int Cuotas { get; set; }
        public string PrimeraCuota { get; set; } = string.Empty;
        public string UltimaCuota { get; set; } = string.Empty;
        public decimal TotalPactado { get; set; }
        public decimal InteresProyectadoBase { get; set; }
        public decimal ValorFinalBase { get; set; }
        public decimal? InteresProyectadoBonificado { get; set; }
        public decimal? BonificacionProyectada { get; set; }
        public decimal? ValorFinalCumpliendo { get; set; }
        public DiferenciaJson? Bonificacion { get; set; }
        public DiferenciaJson? Reliquidacion { get; set; }
        public CumplimientoEsperadoJson? Cumplimiento { get; set; }
    }

    public sealed class DiferenciaJson
    {
        public decimal Aportes { get; set; }
        public decimal InteresBase { get; set; }
        public decimal InteresOtraTasa { get; set; }
        public decimal Diferencia { get; set; }
        public decimal ValorFinalOtraTasa { get; set; }
    }

    public sealed class CumplimientoEsperadoJson
    {
        public int Generadas { get; set; }
        public int Pagadas { get; set; }
        public int Atrasadas { get; set; }
        public List<int> NumerosAtrasados { get; set; } = [];
        public bool SuperaLaTolerancia { get; set; }
        public bool Completo { get; set; }
    }
}
