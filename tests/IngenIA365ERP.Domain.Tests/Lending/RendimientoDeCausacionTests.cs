using System.Diagnostics;
using FluentAssertions;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Tests.Lending.Replay;
using Xunit.Abstractions;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// Feature 013, US4 (T0468; nombre fijo de DT §2.25; §7.6, SC-007, R43): la cota del motor solo. 50.000 créditos sintéticos (semilla
/// fija; desembolsados hasta 180 días antes, así que varios ya tienen cuotas vencidas y mora, con tramos y reajustes al azar) se causan
/// de d − 1 a d en menos de 60 segundos, después de un calentamiento. <b>Corre siempre</b>, sin bandera, como <c>RendimientoTests</c> de
/// Nómina: la medición con base y tandas es <c>CausacionDiariaPerformanceTests</c> (<c>RUN_PERF_TESTS=1</c>).
/// </summary>
public class RendimientoDeCausacionTests(ITestOutputHelper salida)
{
    private const int Creditos = 50_000;
    private static readonly TimeSpan Cota = TimeSpan.FromSeconds(60);

    [Fact]
    public void Cincuenta_mil_creditos_se_causan_en_menos_de_un_minuto()
    {
        var d = new DateOnly(2027, 9, 15);
        var azar = new Random(20261013);
        var entradas = new EntradaDeReconstruccion[Creditos];
        for (var i = 0; i < Creditos; i++) entradas[i] = CreditosDePrueba.AlAzar(azar, d.AddDays(-azar.Next(1, 181)));

        // Calentamiento: el JIT y el memo de tasas.
        for (var i = 0; i < 500; i++) MotorDeCartera.Causar(entradas[i], d.AddDays(-1), d);

        var reloj = Stopwatch.StartNew();
        var filas = 0;
        for (var i = 0; i < Creditos; i++) filas += MotorDeCartera.Causar(entradas[i], d.AddDays(-1), d).Filas.Count;
        reloj.Stop();

        salida.WriteLine($"{Creditos} créditos causados en {reloj.Elapsed.TotalSeconds:F1} s ({reloj.Elapsed.TotalMilliseconds * 1000 / Creditos:F0} µs por crédito), {filas} filas.");
        filas.Should().BeGreaterThan(Creditos, "cada crédito vivo causa al menos su corriente");
        reloj.Elapsed.Should().BeLessThan(Cota);
    }
}
