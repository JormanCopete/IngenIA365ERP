using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US12 (T201; FR-025, FR-045 a FR-047, D13; research §22): el motor de la revalorización, los excedentes y la amortización contra sus casos
/// dorados <c>Casos/asamblea-*.json</c> —la prueba independiente (5 % con IPC 4 % y fondo menor: limitada y prorrateada), el saldo al cierre sin límite, el
/// valor repartido por el mayor residuo, la amortización hasta el mínimo y el año bisiesto al centavo—, calculados por <c>Casos/calcular.py</c>. Más las
/// reglas que no son de un caso. (nuevo)
/// </summary>
public class RevalorizacionYExcedentesTests
{
    public static TheoryData<string> Casos()
    {
        var datos = new TheoryData<string>();
        foreach (var archivo in CasoDoradoDeCaptaciones.Archivos().Where(a => Path.GetFileName(a).StartsWith("asamblea-", StringComparison.Ordinal)))
            datos.Add(Path.GetFileName(archivo));
        return datos;
    }

    [Fact]
    public void Estan_los_casos_de_la_historia()
    {
        var nombres = CasoDoradoDeCaptaciones.Archivos().Select(Path.GetFileName).ToList();
        nombres.Should().Contain(["asamblea-01-revalorizacion-limitada-por-ipc-y-prorrateada.json", "asamblea-02-revalorizacion-saldo-al-cierre-sin-limite.json",
            "asamblea-03-excedentes-por-valor.json", "asamblea-04-amortizacion-hasta-el-minimo.json", "asamblea-05-bisiesto-al-centavo.json"]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_reproduce_el_caso_dorado(string archivo)
    {
        var caso = CasoDoradoDeCaptaciones.Cargar(Path.Combine(CasoDoradoDeCaptaciones.DirectorioDeCasos, archivo));
        var e = CasoDoradoDeCaptaciones.Leer<EntradaJson>(caso.Entrada);
        var esperado = CasoDoradoDeCaptaciones.Leer<EsperadoJson>(caso.Esperado);

        var r = RevalorizacionYExcedentes.Calcular(new PedidoDeDistribucion(Enum.Parse<AssemblyDistributionKind>(e.Clase), e.Ejercicio,
            Enum.Parse<RevaluationBase>(e.Base), e.Porcentaje, e.Valor, e.Fondo, e.LimiteLegal, e.Unidad,
            e.Asociados.Select(a => new AsociadoEnLaDistribucion(a.Persona, a.Cuenta,
                a.Movimientos.Select(m => new MovimientoDeAportes(DateOnly.Parse(m.Fecha), m.Monto)).ToList(), a.MaximoAmortizable)).ToList()));

        r.BaseTotal.Should().Be(esperado.BaseTotal, archivo);
        decimal.Round(r.TasaAplicada, 10).Should().Be(esperado.TasaAplicada, archivo);
        r.LimitadaPorElLimiteLegal.Should().Be(esperado.LimitadaPorElLimiteLegal, archivo);
        r.TotalCalculado.Should().Be(esperado.TotalCalculado, archivo);
        r.Prorrateada.Should().Be(esperado.Prorrateada, archivo);
        r.Factor.Should().Be(esperado.Factor, archivo);
        r.TotalAplicado.Should().Be(esperado.TotalAplicado, archivo);
        r.Remanente.Should().Be(esperado.Remanente, archivo);
        r.Lineas.Select(l => (l.PersonaId, l.CuentaId, l.Base, l.Calculado, l.Aplicado, l.LimitadaPorMinimo))
            .Should().Equal(esperado.Lineas.Select(l => (l.Persona, l.Cuenta, l.Base, l.Calculado, l.Aplicado, l.LimitadaPorMinimo)), archivo);
        r.Explicacion.Should().NotBeEmpty();
    }

    [Fact]
    public void Prorrateada_la_suma_de_los_abonos_es_exactamente_el_fondo()
    {
        var asociados = Enumerable.Range(1, 7)
            .Select(i => new AsociadoEnLaDistribucion(i, 100 + i, [new MovimientoDeAportes(new DateOnly(2025, 1, 1), 1_000_000m + i * 333_333m)])).ToList();
        var r = RevalorizacionYExcedentes.Calcular(new PedidoDeDistribucion(AssemblyDistributionKind.Revaluation, 2026, RevaluationBase.YearEndBalance, 0.04m,
            null, 99_999m, 0.04m, 1m, asociados));
        r.Prorrateada.Should().BeTrue();
        r.TotalAplicado.Should().Be(99_999m);
        r.Lineas.Sum(l => l.Aplicado).Should().Be(99_999m);
        r.Lineas.Should().OnlyContain(l => l.Aplicado <= l.Calculado);
    }

    [Fact]
    public void Los_excedentes_no_tienen_limite_legal()
    {
        var r = RevalorizacionYExcedentes.Calcular(new PedidoDeDistribucion(AssemblyDistributionKind.SurplusDistribution, 2026, RevaluationBase.YearEndBalance,
            0.10m, null, 1_000_000m, 0.04m, 1m, [new AsociadoEnLaDistribucion(1, 1, [new MovimientoDeAportes(new DateOnly(2026, 1, 1), 1_000_000m)])]));
        r.LimitadaPorElLimiteLegal.Should().BeFalse();
        r.TasaAplicada.Should().Be(0.10m);
        r.TotalAplicado.Should().Be(100_000m);
    }

    [Fact]
    public void Sin_base_positiva_no_hay_lineas_y_el_fondo_queda_entero()
    {
        var r = RevalorizacionYExcedentes.Calcular(new PedidoDeDistribucion(AssemblyDistributionKind.Revaluation, 2026, RevaluationBase.AverageBalance, 0.03m,
            null, 500m, null, 1m, [new AsociadoEnLaDistribucion(1, 1, [new MovimientoDeAportes(new DateOnly(2027, 1, 1), 1_000m)])]));
        r.Lineas.Should().BeEmpty();
        r.Remanente.Should().Be(500m);
    }

    [Fact]
    public void Un_pedido_mal_formado_es_un_error_de_quien_llama()
    {
        var sinNada = () => RevalorizacionYExcedentes.Calcular(new PedidoDeDistribucion(AssemblyDistributionKind.Revaluation, 2026, RevaluationBase.AverageBalance,
            null, null, 1m, null, 1m, []));
        sinNada.Should().Throw<ArgumentException>();
        var unidad = () => RevalorizacionYExcedentes.Calcular(new PedidoDeDistribucion(AssemblyDistributionKind.Revaluation, 2026, RevaluationBase.AverageBalance,
            0.01m, null, 1m, null, 0.5m, []));
        unidad.Should().Throw<ArgumentOutOfRangeException>();
        var fondo = () => RevalorizacionYExcedentes.Calcular(new PedidoDeDistribucion(AssemblyDistributionKind.Revaluation, 2026, RevaluationBase.AverageBalance,
            0.01m, null, -1m, null, 1m, []));
        fondo.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Respuesta 91 B del dueño (E259): quien se retiró durante el ejercicio participa a prorrata del tiempo que fue asociado —el saldo promedio diario hasta
    /// la víspera de su retiro—, aun con la base «saldo al cierre», y lo que la liquidación le devolvió después no le resta.
    /// </summary>
    [Fact]
    public void Quien_se_retiro_en_el_ejercicio_participa_a_prorrata_del_tiempo_que_fue_asociado()
    {
        // 1.000.000 desde el 1 de enero; se retira el 1 de julio (181 días asociado: enero a junio) y la liquidación le devolvió todo el 15 de julio.
        List<MovimientoDeAportes> movimientos =
        [
            new(new DateOnly(2026, 1, 1), 1_000_000m),
            new(new DateOnly(2026, 7, 15), -1_000_000m),
        ];
        var retiro = new DateOnly(2026, 7, 1);
        RevalorizacionYExcedentes.SaldoPromedio(RevalorizacionYExcedentes.HastaElRetiro(movimientos, retiro), 2026)
            .Should().Be(decimal.Round(1_000_000m * 181 / 365, 2, MidpointRounding.ToEven));

        var completo = new AsociadoEnLaDistribucion(2, 20, [new MovimientoDeAportes(new DateOnly(2026, 1, 1), 1_000_000m)]);
        var retirado = new AsociadoEnLaDistribucion(1, 10, movimientos, RetiradoEl: retiro);
        var r = RevalorizacionYExcedentes.Calcular(new PedidoDeDistribucion(AssemblyDistributionKind.SurplusDistribution, 2026, RevaluationBase.YearEndBalance,
            null, 100_000m, 100_000m, null, 1m, [retirado, completo]));
        var lineaDelRetirado = r.Lineas.Single(l => l.PersonaId == 1);
        var lineaDelOtro = r.Lineas.Single(l => l.PersonaId == 2);
        lineaDelRetirado.Base.Should().Be(495_890.41m, "el saldo al cierre no aplica a quien se retiró: cuenta el tiempo que lo fue");
        lineaDelOtro.Base.Should().Be(1_000_000m, "quien sigue, con el saldo al cierre");
        (lineaDelRetirado.Aplicado + lineaDelOtro.Aplicado).Should().Be(100_000m);
        lineaDelRetirado.Aplicado.Should().Be(33_150m);
        r.Explicacion.Should().Contain(e => e.Contains("se retiraron durante el ejercicio"));

        // Retirado después del cierre: fue asociado todo el ejercicio.
        RevalorizacionYExcedentes.SaldoPromedio(RevalorizacionYExcedentes.HastaElRetiro([movimientos[0]], new DateOnly(2027, 2, 1)), 2026).Should().Be(1_000_000m);
    }

    [Fact]
    public void El_mayor_residuo_suma_exacto_y_desempata_por_el_primero()
    {
        RevalorizacionYExcedentes.MayorResto(10m, [1m, 1m, 1m], 1m).Should().Equal(4m, 3m, 3m);
        RevalorizacionYExcedentes.MayorResto(0.05m, [1m, 1m], 0.01m).Should().Equal(0.03m, 0.02m);
        RevalorizacionYExcedentes.MayorResto(0m, [1m, 2m], 1m).Should().Equal(0m, 0m);
    }

    private sealed class EntradaJson
    {
        public string Clase { get; set; } = string.Empty;
        public int Ejercicio { get; set; }
        public string Base { get; set; } = string.Empty;
        public decimal? Porcentaje { get; set; }
        public decimal? Valor { get; set; }
        public decimal Fondo { get; set; }
        public decimal? LimiteLegal { get; set; }
        public decimal Unidad { get; set; }
        public List<AsociadoJson> Asociados { get; set; } = [];
    }

    private sealed class AsociadoJson
    {
        public int Persona { get; set; }
        public int Cuenta { get; set; }
        public List<MovimientoJson> Movimientos { get; set; } = [];
        public decimal? MaximoAmortizable { get; set; }
    }

    private sealed class MovimientoJson
    {
        public string Fecha { get; set; } = string.Empty;
        public decimal Monto { get; set; }
    }

    private sealed class EsperadoJson
    {
        public decimal BaseTotal { get; set; }
        public decimal TasaAplicada { get; set; }
        public bool LimitadaPorElLimiteLegal { get; set; }
        public decimal TotalCalculado { get; set; }
        public bool Prorrateada { get; set; }
        public decimal? Factor { get; set; }
        public decimal TotalAplicado { get; set; }
        public decimal Remanente { get; set; }
        public List<LineaJson> Lineas { get; set; } = [];
    }

    private sealed class LineaJson
    {
        public int Persona { get; set; }
        public int Cuenta { get; set; }
        public decimal Base { get; set; }
        public decimal Calculado { get; set; }
        public decimal Aplicado { get; set; }
        public bool LimitadaPorMinimo { get; set; }
    }
}
