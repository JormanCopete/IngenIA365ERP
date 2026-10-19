using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US15 (T214; FR-025, FR-109, FR-110; research §24): los motores del fondo de liquidez, del riesgo de liquidez (IRL y brecha) y de los depósitos
/// asegurados contra sus casos dorados <c>Casos/liquidez-*.json</c>, calculados por <c>Casos/calcular.py</c> —el régimen general al 10 % y 2 % (la prueba
/// independiente de la historia), la categoría plena por madurez residual, la transición, los pisos legales, el IRL por bandas con su brecha, la banda
/// abierta y lo asegurado por ahorrador—. Más las reglas que no son de un caso. (nuevo)
/// </summary>
public class FondoDeLiquidezTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    public static TheoryData<string> Casos()
    {
        var datos = new TheoryData<string>();
        foreach (var archivo in CasoDoradoDeCaptaciones.Archivos().Where(a => Path.GetFileName(a).StartsWith("liquidez-", StringComparison.Ordinal)))
            datos.Add(Path.GetFileName(archivo));
        return datos;
    }

    [Fact]
    public void Estan_los_casos_de_la_historia()
    {
        var nombres = CasoDoradoDeCaptaciones.Archivos().Select(Path.GetFileName).ToList();
        nombres.Should().Contain(["liquidez-01-general-diez-y-dos.json", "liquidez-02-plena-por-madurez-residual.json", "liquidez-03-plena-en-transicion.json",
            "liquidez-04-pisos-legales-y-cooperativa-mas-estricta.json", "liquidez-05-irl-y-brecha-por-bandas.json", "liquidez-06-brecha-con-banda-abierta.json",
            "liquidez-07-depositos-asegurados.json"]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_reproduce_el_caso_dorado(string archivo)
    {
        var caso = CasoDoradoDeCaptaciones.Cargar(Path.Combine(CasoDoradoDeCaptaciones.DirectorioDeCasos, archivo));
        var tipo = caso.Entrada!.Value.TryGetProperty("tipo", out var t) ? t.GetString() : "fondo";
        switch (tipo)
        {
            case "riesgo":
                Riesgo(archivo, caso);
                break;
            case "asegurados":
                Asegurados(archivo, caso);
                break;
            default:
                Fondo(archivo, caso);
                break;
        }
    }

    private static void Fondo(string archivo, CasoDoradoDeCaptaciones caso)
    {
        var e = caso.Entrada!.Value;
        var reglas = ReglasDelFondoDeLiquidez.Leer($"{{\"Cat\": {e.GetProperty("regla").GetRawText()}, \"*\": {e.GetProperty("general").GetRawText()}}}")!;
        var pisos = e.GetProperty("pisos").Deserialize<PisosJson>(Opciones)!;
        var depositos = e.GetProperty("depositos").Deserialize<List<DepositoJson>>(Opciones)!
            .Select(d => new DepositoParaFondo(Enum.Parse<ClaseDeDepositoParaLiquidez>(d.Clase), d.Capital, d.InteresCausado, d.InteresPorCausar,
                d.DiasAlVencimiento, d.RedencionAnticipadaPactada)).ToList();
        var esperado = CasoDoradoDeCaptaciones.Leer<EsperadoFondoJson>(caso.Esperado);

        var r = FondoDeLiquidez.Calcular(DateOnly.Parse(e.GetProperty("fecha").GetString()!), reglas["Cat"], reglas["*"],
            new PisosDelFondoDeLiquidez(pisos.Depositos, pisos.PermanenteSoloAlRetirarse, pisos.BandaMediaPlena, pisos.PermanenteSoloAlRetirarsePlena), depositos);

        r.Regimen.ToString().Should().Be(esperado.Regimen, archivo);
        r.Requerido.Should().Be(esperado.Requerido, archivo);
        r.Renglones.Select(x => (x.Clase.ToString(), x.Banda, x.Cuentas, x.Capital, x.Intereses, x.Base, x.Porcentaje, x.Requerido))
            .Should().Equal(esperado.Renglones.Select(x => (x.Clase, x.Banda, x.Cuentas, x.Capital, x.Intereses, x.Base, x.Porcentaje, x.Requerido)), archivo);
        if (esperado.Nuevo is null) r.RegimenNuevoEnTransicion.Should().BeNull(archivo);
        else
        {
            r.RegimenNuevoEnTransicion!.Requerido.Should().Be(esperado.Nuevo.Requerido, archivo);
            r.FinDeLaTransicion.Should().Be(DateOnly.Parse(esperado.FinDeLaTransicion!), archivo);
        }
        r.Explicacion.Should().NotBeEmpty();
    }

    private static void Riesgo(string archivo, CasoDoradoDeCaptaciones caso)
    {
        var e = CasoDoradoDeCaptaciones.Leer<EntradaRiesgoJson>(caso.Entrada);
        var esperado = CasoDoradoDeCaptaciones.Leer<EsperadoRiesgoJson>(caso.Esperado);

        var r = RiesgoDeLiquidez.Medir(e.Bandas, e.Activos.Select(a => new ActivoLiquido(a.Concepto, a.Valor, a.Descuento)).ToList(),
            e.Flujos.Select(f => new FlujoDeLiquidez(Enum.Parse<TipoDeFlujoDeLiquidez>(f.Tipo), f.Concepto, f.Dias, f.Monto)).ToList());

        r.ActivosLiquidosNetos.Should().Be(esperado.ActivosLiquidosNetos, archivo);
        r.Bandas.Select(b => (b.Banda, b.Entradas, b.SalidasContractuales, b.SalidasNoContractuales, b.Brecha, b.BrechaAcumulada, b.IrlAcumulado))
            .Should().Equal(esperado.Bandas.Select(b => (b.Banda, b.Entradas, b.SalidasContractuales, b.SalidasNoContractuales, b.Brecha, b.BrechaAcumulada,
                b.IrlAcumulado)), archivo);
        r.IrlMinimo.Should().Be(esperado.IrlMinimo, archivo);
        r.BandaDelIrlMinimo.Should().Be(esperado.BandaDelIrlMinimo, archivo);
        r.FueraDelHorizonte.Should().Be(esperado.FueraDelHorizonte, archivo);
    }

    private static void Asegurados(string archivo, CasoDoradoDeCaptaciones caso)
    {
        var e = CasoDoradoDeCaptaciones.Leer<EntradaAseguradosJson>(caso.Entrada);
        var esperado = CasoDoradoDeCaptaciones.Leer<EsperadoAseguradosJson>(caso.Esperado);

        var r = DepositosAsegurados.Calcular(e.Tope, e.Ahorradores.Select(a => new DepositosDeUnAhorrador(a.Persona, a.Cuentas, a.Depositos)).ToList());

        r.Select(x => (x.PersonaId, x.Cuentas, x.Depositos, x.Asegurado, x.NoAsegurado))
            .Should().Equal(esperado.Ahorradores.Select(x => (x.Persona, x.Cuentas, x.Depositos, x.Asegurado, x.NoAsegurado)), archivo);
        r.Sum(x => x.Asegurado).Should().Be(esperado.TotalAsegurado, archivo);
        r.Sum(x => x.NoAsegurado).Should().Be(esperado.TotalNoAsegurado, archivo);
    }

    // ----------------------------------------------------------------------------------------------------- reglas sueltas --

    [Fact]
    public void Una_categoria_sin_regla_propia_toma_la_de_cualquiera_y_el_json_mal_formado_no_da_reglas()
    {
        var reglas = ReglasDelFondoDeLiquidez.Leer("{\"*\": {\"regimen\": \"General\", \"porcentajeDepositos\": 0.10}}")!;
        ReglasDelFondoDeLiquidez.Para(reglas, "Intermedia")!.PorcentajeDepositos.Should().Be(0.10m);
        ReglasDelFondoDeLiquidez.Para(reglas, "Intermedia")!.PorcentajePermanenteSoloAlRetirarse.Should().Be(0.10m, "sin el suyo, el de los depósitos");
        ReglasDelFondoDeLiquidez.Leer("{ no es json").Should().BeNull();
        ReglasDelFondoDeLiquidez.Leer(string.Empty).Should().BeNull();
        ReglasDelFondoDeLiquidez.Leer("{\"*\": {\"porcentajeDepositos\": 1.5}}").Should().BeNull("un porcentaje es una fracción entre 0 y 1");
    }

    [Fact]
    public void Las_bandas_se_ordenan_y_siempre_terminan_abiertas()
    {
        var bandas = FondoDeLiquidez.Ordenadas([new BandaDeMadurez(540, 0.05m), new BandaDeMadurez(30, 0.10m)]);
        bandas.Select(b => b.HastaDias).Should().Equal(30, 540, null);
        FondoDeLiquidez.IndiceDeBanda(bandas, -5).Should().Be(0, "lo vencido va a la primera banda");
        FondoDeLiquidez.IndiceDeBanda(bandas, 541).Should().Be(2);
        FondoDeLiquidez.NombreDeBanda(bandas, 2).Should().Be("más de 540 días");
    }

    [Fact]
    public void Sin_depositos_el_requerido_es_cero()
    {
        var regla = ReglasDelFondoDeLiquidez.Leer("{\"*\": {\"porcentajeDepositos\": 0.10}}")!["*"];
        FondoDeLiquidez.Calcular(new DateOnly(2026, 10, 31), regla, regla, PisosDelFondoDeLiquidez.Ninguno, []).Requerido.Should().Be(0m);
    }

    [Fact]
    public void Sin_salidas_no_hay_irl_y_un_tope_negativo_no_se_admite()
    {
        var r = RiesgoDeLiquidez.Medir([15, 30], [new ActivoLiquido("Fondo", 100m, 0m)], [new FlujoDeLiquidez(TipoDeFlujoDeLiquidez.Entrada, "Cartera", 5, 10m)]);
        r.IrlMinimo.Should().BeNull();
        r.Bandas.Should().OnlyContain(b => b.IrlAcumulado == null);
        var tope = () => DepositosAsegurados.Calcular(-1m, []);
        tope.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ------------------------------------------------------------------------------------------------------------- JSON --

    private sealed class PisosJson
    {
        public decimal? Depositos { get; set; }
        public decimal? PermanenteSoloAlRetirarse { get; set; }
        public decimal? BandaMediaPlena { get; set; }
        public decimal? PermanenteSoloAlRetirarsePlena { get; set; }
    }

    private sealed class DepositoJson
    {
        public string Clase { get; set; } = string.Empty;
        public decimal Capital { get; set; }
        public decimal InteresCausado { get; set; }
        public decimal InteresPorCausar { get; set; }
        public int? DiasAlVencimiento { get; set; }
        public bool RedencionAnticipadaPactada { get; set; }
    }

    private sealed class EsperadoFondoJson
    {
        public string Regimen { get; set; } = string.Empty;
        public decimal Requerido { get; set; }
        public List<RenglonJson> Renglones { get; set; } = [];
        public NuevoJson? Nuevo { get; set; }
        public string? FinDeLaTransicion { get; set; }
    }

    private sealed class RenglonJson
    {
        public string Clase { get; set; } = string.Empty;
        public string? Banda { get; set; }
        public int Cuentas { get; set; }
        public decimal Capital { get; set; }
        public decimal Intereses { get; set; }
        public decimal Base { get; set; }
        public decimal Porcentaje { get; set; }
        public decimal Requerido { get; set; }
    }

    private sealed class NuevoJson
    {
        public decimal Requerido { get; set; }
    }

    private sealed class EntradaRiesgoJson
    {
        public List<int?> Bandas { get; set; } = [];
        public List<ActivoJson> Activos { get; set; } = [];
        public List<FlujoJson> Flujos { get; set; } = [];
    }

    private sealed class ActivoJson
    {
        public string Concepto { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public decimal Descuento { get; set; }
    }

    private sealed class FlujoJson
    {
        public string Tipo { get; set; } = string.Empty;
        public string Concepto { get; set; } = string.Empty;
        public int Dias { get; set; }
        public decimal Monto { get; set; }
    }

    private sealed class EsperadoRiesgoJson
    {
        public decimal ActivosLiquidosNetos { get; set; }
        public List<BandaJson> Bandas { get; set; } = [];
        public decimal? IrlMinimo { get; set; }
        public string? BandaDelIrlMinimo { get; set; }
        public decimal FueraDelHorizonte { get; set; }
    }

    private sealed class BandaJson
    {
        public string Banda { get; set; } = string.Empty;
        public decimal Entradas { get; set; }
        public decimal SalidasContractuales { get; set; }
        public decimal SalidasNoContractuales { get; set; }
        public decimal Brecha { get; set; }
        public decimal BrechaAcumulada { get; set; }
        public decimal? IrlAcumulado { get; set; }
    }

    private sealed class EntradaAseguradosJson
    {
        public decimal Tope { get; set; }
        public List<AhorradorJson> Ahorradores { get; set; } = [];
    }

    private sealed class AhorradorJson
    {
        public int Persona { get; set; }
        public int Cuentas { get; set; }
        public decimal Depositos { get; set; }
    }

    private sealed class EsperadoAseguradosJson
    {
        public List<AseguradoJson> Ahorradores { get; set; } = [];
        public decimal TotalAsegurado { get; set; }
        public decimal TotalNoAsegurado { get; set; }
    }

    private sealed class AseguradoJson
    {
        public int Persona { get; set; }
        public int Cuentas { get; set; }
        public decimal Depositos { get; set; }
        public decimal Asegurado { get; set; }
        public decimal NoAsegurado { get; set; }
    }
}
