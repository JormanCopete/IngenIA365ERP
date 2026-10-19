using FluentAssertions;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US6 (T168; research R13; FR-127, D34): el motor de la tasa negociada contra sus casos dorados <c>Casos/tasa-negociada-*.json</c> —US6-6:
/// la regla de antigüedad admite 1,5 puntos, se piden 1,2 con atribución de 0,5 y espera aprobador; se piden 2 y se rechaza con el máximo—, más la
/// especificidad, el desempate y la tasa aplicada.
/// </summary>
public class TasaNegociadaTests
{
    public static TheoryData<string> Casos()
    {
        var datos = new TheoryData<string>();
        foreach (var archivo in CasoDoradoDeCaptaciones.Archivos().Where(a => Path.GetFileName(a).StartsWith("tasa-negociada-", StringComparison.Ordinal)))
            datos.Add(Path.GetFileName(archivo));
        return datos;
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_reproduce_el_caso_dorado(string archivo)
    {
        var caso = CasoDoradoDeCaptaciones.Cargar(Path.Combine(CasoDoradoDeCaptaciones.DirectorioDeCasos, archivo));
        var e = CasoDoradoDeCaptaciones.Leer<EntradaJson>(caso.Entrada);
        var esperado = CasoDoradoDeCaptaciones.Leer<EsperadoJson>(caso.Esperado);
        var reglas = e.Reglas.Select((r, i) => new ReglaDeTasaNegociada(i + 1, r.Codigo, r.Prioridad, r.AntiguedadMinimaMeses, r.MontoMinimo, r.MontoMaximo,
            r.PlazoMinimoDias, r.PlazoMaximoDias, r.TasaBaseMinima, r.TasaBaseMaxima, r.SaldoDelTitularMinimo, r.SaldoDelTitularMaximo, r.Oficina, r.Canal,
            r.MargenMaximo)).ToList();
        e.Pedidos.Should().HaveCount(esperado.Evaluaciones.Count);
        for (var i = 0; i < e.Pedidos.Count; i++)
        {
            var p = e.Pedidos[i];
            var x = esperado.Evaluaciones[i];
            var ev = TasaNegociada.Evaluar(reglas,
                new PedidoDeTasaNegociada(p.AntiguedadMeses, p.Monto, p.PlazoDias, p.TasaDeTabla, p.SaldoDelTitular, p.Oficina, p.Canal, p.MargenPedido),
                p.Atribucion);
            ev.Regla?.Codigo.Should().Be(x.Regla, $"{archivo}, pedido {i + 1}");
            if (x.Regla is null) ev.Regla.Should().BeNull($"{archivo}, pedido {i + 1}");
            ev.CodigoDeRechazo.Should().Be(x.Codigo, $"{archivo}, pedido {i + 1}");
            ev.SeAplicaSola.Should().Be(x.SeAplicaSola, $"{archivo}, pedido {i + 1}");
            ev.EsperaAprobador.Should().Be(x.EsperaAprobador, $"{archivo}, pedido {i + 1}");
        }
    }

    [Fact]
    public void La_tasa_aplicada_es_la_tabla_mas_el_margen_y_el_rechazo_dice_el_maximo()
    {
        var regla = new ReglaDeTasaNegociada(1, "ANT5", 1, 60, null, null, null, null, null, null, null, null, null, null, 0.015m);
        var pedido = new PedidoDeTasaNegociada(72, 200_000_000m, 360, 0.09m, 250_000_000m, 1, null, 0.012m);
        var ev = TasaNegociada.Evaluar([regla], pedido, 0.005m);
        ev.TasaAplicada.Should().Be(0.102m);
        ev.MargenMaximo.Should().Be(0.015m);
        ev.EsperaAprobador.Should().BeTrue();
        TasaNegociada.Evaluar([regla], pedido with { MargenPedido = 0.02m }, 0.05m).Explicacion.Should().Contain("1.5000");
        TasaNegociada.Evaluar([regla], pedido, 0.012m).SeAplicaSola.Should().BeTrue("la atribución alcanza justo el margen");
    }

    [Fact]
    public void Gana_la_regla_que_fija_mas_condiciones()
    {
        var general = new ReglaDeTasaNegociada(1, "GEN", 1, null, null, null, null, null, null, null, null, null, null, null, 0.005m);
        var especifica = new ReglaDeTasaNegociada(2, "ESP", 9, 12, 1_000_000m, null, null, null, null, null, null, null, null, null, 0.01m);
        var pedido = new PedidoDeTasaNegociada(24, 5_000_000m, 90, 0.08m, 5_000_000m, 1, null, 0.003m);
        TasaNegociada.ReglaAplicable([general, especifica], pedido)!.Codigo.Should().Be("ESP");
        TasaNegociada.Especificidad(especifica).Should().Be(2);
        TasaNegociada.ReglaAplicable([general, especifica], pedido with { AntiguedadMeses = 6 })!.Codigo.Should().Be("GEN");
    }

    [Fact]
    public void Un_plazo_que_la_regla_exige_no_casa_sin_plazo()
    {
        var regla = new ReglaDeTasaNegociada(1, "P360", 1, null, null, null, 360, null, null, null, null, null, null, null, 0.01m);
        TasaNegociada.Casa(regla, new PedidoDeTasaNegociada(0, 1m, null, 0.05m, 0m, 1, null, 0.001m)).Should().BeFalse();
        TasaNegociada.Casa(regla, new PedidoDeTasaNegociada(0, 1m, 360, 0.05m, 0m, 1, "web", 0.001m)).Should().BeTrue();
    }

    private sealed class EntradaJson
    {
        public List<ReglaJson> Reglas { get; set; } = [];
        public List<PedidoJson> Pedidos { get; set; } = [];
    }

    private sealed class ReglaJson
    {
        public string Codigo { get; set; } = string.Empty;
        public int Prioridad { get; set; }
        public int? AntiguedadMinimaMeses { get; set; }
        public decimal? MontoMinimo { get; set; }
        public decimal? MontoMaximo { get; set; }
        public int? PlazoMinimoDias { get; set; }
        public int? PlazoMaximoDias { get; set; }
        public decimal? TasaBaseMinima { get; set; }
        public decimal? TasaBaseMaxima { get; set; }
        public decimal? SaldoDelTitularMinimo { get; set; }
        public decimal? SaldoDelTitularMaximo { get; set; }
        public int? Oficina { get; set; }
        public string? Canal { get; set; }
        public decimal MargenMaximo { get; set; }
    }

    private sealed class PedidoJson
    {
        public int AntiguedadMeses { get; set; }
        public decimal Monto { get; set; }
        public int? PlazoDias { get; set; }
        public decimal TasaDeTabla { get; set; }
        public decimal SaldoDelTitular { get; set; }
        public int Oficina { get; set; }
        public string? Canal { get; set; }
        public decimal MargenPedido { get; set; }
        public decimal? Atribucion { get; set; }
    }

    private sealed class EsperadoJson
    {
        public List<EvaluacionJson> Evaluaciones { get; set; } = [];
    }

    private sealed class EvaluacionJson
    {
        public string? Regla { get; set; }
        public string? Codigo { get; set; }
        public bool SeAplicaSola { get; set; }
        public bool EsperaAprobador { get; set; }
    }
}
