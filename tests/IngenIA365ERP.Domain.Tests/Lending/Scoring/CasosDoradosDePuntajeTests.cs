using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Lending.Scoring;

namespace IngenIA365ERP.Domain.Tests.Lending.Scoring;

/// <summary>
/// Feature 013, T0364 (FR-037; quickstart §1.4): <see cref="MotorDePuntaje"/> coincide con los cálculos hechos a mano de <c>Casos/</c>.
/// Cada variable cae en el rango que contiene su valor (desde incluido, hasta excluido), aporta <c>peso × puntos</c> redondeado a cuatro
/// decimales (alias <c>Puntaje</c> 12,4) y el puntaje es la suma de los aportes; sin dato, la variable toma los puntos de su rango «sin
/// dato» o cero, y la explicación lo dice; bajo el mínimo no rechaza: sólo lo marca (sube al nivel de excepción, FR-037).
/// </summary>
public class CasosDoradosDePuntajeTests
{
    public static IEnumerable<object[]> Casos() =>
        CasoDoradoDePuntaje.Archivos().Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_de_K1()
    {
        CasoDoradoDePuntaje.Archivos().Select(Path.GetFileNameWithoutExtension).Should().BeEquivalentTo(
        [
            "01-tres-variables", "02-bajo-el-minimo", "03-sin-dato-de-central", "04-limites-de-rango", "05-escala-0-1000",
        ]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_caso_coincide_con_el_calculo_a_mano(string archivo)
    {
        var caso = CasoDoradoDePuntaje.Cargar(Path.Combine(CasoDoradoDePuntaje.DirectorioDeCasos, archivo));
        caso.Escenarios.Should().NotBeEmpty($"{archivo} no trae escenarios");
        var modelo = caso.ModeloDelMotor();
        modelo.Validar().Should().BeNull($"el modelo de {archivo} tiene que ser válido para la línea");

        foreach (var escenario in caso.Escenarios)
        {
            var r = MotorDePuntaje.Calcular(modelo, caso.Modelo.Minimo, escenario.Valores);
            var e = escenario.Esperado;

            using (new AssertionScope($"{caso.Nombre} · {escenario.Nombre}"))
            {
                r.Puntaje.Should().Be(e.Puntaje, "puntaje");
                r.BajoElMinimo.Should().Be(e.BajoElMinimo, "bajo el mínimo");
                r.Minimo.Should().Be(caso.Modelo.Minimo);
                decimal.Round(r.Puntaje, 4).Should().Be(r.Puntaje, "el puntaje tiene cuatro decimales como máximo");
                r.Variables.Sum(v => v.Aporte).Should().Be(r.Puntaje, "el puntaje es la suma de los aportes");
                foreach (var esperada in e.Variables)
                {
                    var v = r.Variables.Single(x => x.Codigo == esperada.Codigo);
                    v.Puntos.Should().Be(esperada.Puntos, $"puntos de {esperada.Codigo}");
                    v.Aporte.Should().Be(esperada.Aporte, $"aporte de {esperada.Codigo}");
                    v.SinDato.Should().Be(esperada.SinDato, $"sin dato en {esperada.Codigo}");
                    v.FueraDeRango.Should().Be(esperada.FueraDeRango, $"fuera de rango en {esperada.Codigo}");
                    v.Explicacion.Should().NotBeNullOrWhiteSpace();
                }
                foreach (var frase in e.ExplicacionContiene)
                    r.Explicacion.Texto.Should().Contain(frase);
                r.HuellaDeEntradas.Should().MatchRegex("^[0-9a-f]{64}$");
            }
        }
    }

    [Fact]
    public void La_huella_de_entradas_es_estable_y_cambia_con_un_valor()
    {
        var caso = CasoDoradoDePuntaje.Cargar(Path.Combine(CasoDoradoDePuntaje.DirectorioDeCasos, "01-tres-variables.json"));
        var modelo = caso.ModeloDelMotor();
        var valores = caso.Escenarios[0].Valores;
        var alReves = valores.Reverse().ToDictionary(x => x.Key, x => x.Value);

        var a = MotorDePuntaje.Calcular(modelo, caso.Modelo.Minimo, valores);
        var b = MotorDePuntaje.Calcular(modelo, caso.Modelo.Minimo, alReves);
        a.HuellaDeEntradas.Should().Be(b.HuellaDeEntradas, "el orden de los valores no es dato");
        MotorDePuntaje.Huella(modelo, caso.Modelo.Minimo, valores).Should().Be(a.HuellaDeEntradas);

        var otro = new Dictionary<string, decimal?>(valores) { [FuentesDePuntaje.PuntajeDeCentrales] = 721m };
        MotorDePuntaje.Calcular(modelo, caso.Modelo.Minimo, otro).HuellaDeEntradas.Should().NotBe(a.HuellaDeEntradas);
        MotorDePuntaje.Calcular(modelo, 601m, valores).HuellaDeEntradas.Should().NotBe(a.HuellaDeEntradas, "el mínimo también es entrada");
    }

    [Fact]
    public void Las_fuentes_que_publica_el_motor_son_las_que_acepta_la_linea()
    {
        MotorDePuntaje.Fuentes.Should().Equal(FuentesDePuntaje.Todas);
        MotorDePuntaje.Fuentes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Sin_minimo_nunca_queda_bajo_el_minimo()
    {
        var caso = CasoDoradoDePuntaje.Cargar(Path.Combine(CasoDoradoDePuntaje.DirectorioDeCasos, "02-bajo-el-minimo.json"));
        var r = MotorDePuntaje.Calcular(caso.ModeloDelMotor(), null, caso.Escenarios[0].Valores);
        r.BajoElMinimo.Should().BeFalse();
        r.Minimo.Should().BeNull();
    }

    [Fact]
    public void Un_modelo_invalido_es_un_error_de_quien_llama()
    {
        var modelo = new ModeloDePuntaje(null, [new VariableDePuntaje("X", "X", "Otra.Fuente", 1m, [new RangoDePuntaje(null, null, 1m)])]);
        var accion = () => MotorDePuntaje.Calcular(modelo, null, new Dictionary<string, decimal?>());
        accion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Un_rango_sin_dato_va_sin_limites_y_hay_a_lo_sumo_uno_por_variable()
    {
        var conLimites = new ModeloDePuntaje(null,
            [new VariableDePuntaje("X", "X", FuentesDePuntaje.PuntajeDeCentrales, 1m, [new RangoDePuntaje(0m, 10m, 5m, NoData: true)])]);
        conLimites.Validar().Should().NotBeNull();
        var dos = new ModeloDePuntaje(null,
            [new VariableDePuntaje("X", "X", FuentesDePuntaje.PuntajeDeCentrales, 1m,
                [new RangoDePuntaje(null, null, 5m, NoData: true), new RangoDePuntaje(null, null, 6m, NoData: true)])]);
        dos.Validar().Should().NotBeNull();
        var bien = new ModeloDePuntaje(null,
            [new VariableDePuntaje("X", "X", FuentesDePuntaje.PuntajeDeCentrales, 1m,
                [new RangoDePuntaje(0m, null, 5m), new RangoDePuntaje(null, null, 6m, NoData: true)])]);
        bien.Validar().Should().BeNull();
    }

    [Fact]
    public void El_rango_sin_dato_no_cambia_el_json_de_los_modelos_que_no_lo_usan()
    {
        var modelo = new ModeloDePuntaje(null,
            [new VariableDePuntaje("X", "X", FuentesDePuntaje.PuntajeDeCentrales, 1m, [new RangoDePuntaje(0m, null, 5m)])]);
        modelo.Escribir().Should().NotContain("noData");
        var conSinDato = new ModeloDePuntaje(null,
            [new VariableDePuntaje("X", "X", FuentesDePuntaje.PuntajeDeCentrales, 1m,
                [new RangoDePuntaje(0m, null, 5m), new RangoDePuntaje(null, null, 2m, NoData: true)])]);
        ModeloDePuntaje.Leer(conSinDato.Escribir())!.Variables[0].Ranges.Should().Contain(r => r.NoData);
    }
}
