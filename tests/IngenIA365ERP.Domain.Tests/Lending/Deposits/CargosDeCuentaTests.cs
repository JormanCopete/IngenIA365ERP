using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US4 (T115; research R14; FR-128, D35): el motor de cargos contra sus casos dorados <c>Casos/cargo-*.json</c> —gratuitos, porcentaje
/// acotado, exoneraciones, pendiente y condonado por la línea—, calculados con <c>Casos/calcular.py</c>; y el disponible (FR-057, SC-007). (nuevo)
/// </summary>
public class CargosDeCuentaTests
{
    private sealed record CargoJson(
        string Codigo, ChargeKind Clase, ChargeTrigger Disparador, ChargeCalculation Modo, decimal? ValorFijo, decimal? Porcentaje,
        ChargeBase? BaseDelPorcentaje, decimal? Minimo, decimal? Maximo, int? Gratuitos, decimal? ExentoSiSaldoPromedio, decimal? ExentoSiSaldoMinimo,
        int? ExentoSiAntiguedadMeses, bool ExentoEmpleados, bool ExentoCuentaExentaGmf, bool ExentoMenores, List<Guid>? OficinasExentas,
        ChargeShortfallPolicy? PoliticaSiNoAlcanza);

    private sealed record ContextoJson(
        decimal MontoDeLaOperacion, decimal SaldoPromedio, decimal SaldoMinimo, int? MesesDeAntiguedad, bool TitularEsEmpleado, bool CuentaExentaDeGmf,
        bool TitularMenor, Guid Oficina, bool ExoneracionIndividual, int OperacionesEnElPeriodo, decimal CobrableSinBajarDelMinimo);

    private sealed record EntradaJson(CargoJson Cargo, List<ContextoJson> Casos);

    private sealed record ResultadoJson(ResultadoDeCargo Resultado, decimal Valor, string? Exoneracion);

    private sealed record EsperadoJson(List<ResultadoJson> Casos);

    public static TheoryData<string> Casos()
    {
        var datos = new TheoryData<string>();
        foreach (var archivo in CasoDoradoDeCaptaciones.Archivos().Where(a => Path.GetFileName(a).StartsWith("cargo-", StringComparison.Ordinal)))
            datos.Add(Path.GetFileName(archivo));
        return datos;
    }

    [Fact]
    public void Hay_casos_de_cargos_con_gratuitos_y_exoneracion()
    {
        var nombres = CasoDoradoDeCaptaciones.Archivos().Select(Path.GetFileName).ToList();
        nombres.Should().Contain(["cargo-01-gratuitos.json", "cargo-03-exoneracion-saldo-promedio.json", "cargo-05-saldo-insuficiente.json"]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_coincide_con_el_caso_dorado(string archivo)
    {
        var caso = CasoDoradoDeCaptaciones.Cargar(Path.Combine(CasoDoradoDeCaptaciones.DirectorioDeCasos, archivo));
        var entrada = CasoDoradoDeCaptaciones.Leer<EntradaJson>(caso.Entrada);
        var esperado = CasoDoradoDeCaptaciones.Leer<EsperadoJson>(caso.Esperado);
        var c = entrada.Cargo;
        var definicion = new DefinicionDeCargo(c.Codigo, c.Clase, c.Disparador, c.Modo, c.ValorFijo, c.Porcentaje, c.BaseDelPorcentaje, c.Minimo, c.Maximo,
            c.Gratuitos, c.ExentoSiSaldoPromedio, c.ExentoSiSaldoMinimo, c.ExentoSiAntiguedadMeses, c.ExentoEmpleados, c.ExentoCuentaExentaGmf,
            c.ExentoMenores, c.OficinasExentas, c.PoliticaSiNoAlcanza ?? ChargeShortfallPolicy.KeepPending);

        entrada.Casos.Should().HaveSameCount(esperado.Casos);
        for (var i = 0; i < entrada.Casos.Count; i++)
        {
            var x = entrada.Casos[i];
            var calculado = CargosDeCuenta.Calcular(definicion, new ContextoDeCargo(x.MontoDeLaOperacion, x.SaldoPromedio, x.SaldoMinimo,
                x.MesesDeAntiguedad, x.TitularEsEmpleado, x.CuentaExentaDeGmf, x.TitularMenor, x.Oficina, x.ExoneracionIndividual, x.OperacionesEnElPeriodo,
                x.CobrableSinBajarDelMinimo));
            new ResultadoJson(calculado.Resultado, calculado.Valor, calculado.Exoneracion)
                .Should().BeEquivalentTo(esperado.Casos[i], $"{archivo}, caso {i}");
            calculado.Explicacion.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void El_disponible_descuenta_bloqueos_embargos_canje_y_minimo_y_nunca_es_negativo()
    {
        var d = DisponibleDeCuenta.Calcular(LineNature.Deposit, 1_000_000m, 100_000m, 50_000m, 200_000m, 10_000m);
        d.Disponible.Should().Be(640_000m);
        DisponibleDeCuenta.Alcanza(d, 640_000m).Should().BeTrue();
        DisponibleDeCuenta.Alcanza(d, 640_000.01m).Should().BeFalse();

        DisponibleDeCuenta.Calcular(LineNature.Deposit, -5_000m, 0m, 0m, 0m, 0m).Disponible.Should().Be(0m, "un sobregiro técnico no deja disponible");
        DisponibleDeCuenta.Calcular(LineNature.Contribution, 1_000_000m, 0m, 0m, 0m, 0m).Disponible.Should().Be(0m, "los aportes no se retiran");
    }
}
