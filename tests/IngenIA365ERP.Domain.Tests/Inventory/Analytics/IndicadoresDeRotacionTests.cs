using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Inventory.Analytics;

namespace IngenIA365ERP.Domain.Tests.Inventory.Analytics;

/// <summary>
/// Feature 012, US17, T943 (FR-086; contracts/api.md §27 vista <c>turnover</c>, §28 fichas <c>turnover</c> e <c>inventoryDays</c>): el
/// cálculo puro <see cref="IndicadoresDeRotacion"/> coincide con los casos calculados a mano de <c>Casos/rotacion-*.json</c> —inventario
/// promedio = promedio de los saldos observados; rotación = costo de venta del período ÷ inventario promedio; días = días del período ÷
/// rotación (con la rotación exacta); inventario promedio 0 → «sin dato», nunca una división por cero—, más las reglas sueltas. Los de
/// <c>ClasificacionAbc</c> son de US15 (<see cref="ClasificacionAbcTests"/>).
/// </summary>
public class IndicadoresDeRotacionTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Inventory", "Analytics", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "rotacion-*.json").OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_de_rotacion()
    {
        var nombres = Directory.EnumerateFiles(Directorio, "rotacion-*.json").Select(Path.GetFileName).ToList();
        nombres.Should().HaveCountGreaterThanOrEqualTo(4, "T943: anual, con cierres, promedio cero y sin ventas");
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_calculo_coincide_con_el_calculo_manual(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;

        var promedio = IndicadoresDeRotacion.InventarioPromedio(caso.Saldos);
        var r = IndicadoresDeRotacion.Calcular(caso.CostoDeVenta, promedio, caso.DiasDelPeriodo);

        using var _ = new AssertionScope($"{archivo} — {caso.Nombre}");
        promedio.Should().Be(caso.Esperado.InventarioPromedio, "inventario promedio");
        r.InventarioPromedio.Should().Be(caso.Esperado.InventarioPromedio);
        r.CostoDeVenta.Should().Be(caso.CostoDeVenta);
        r.DiasDelPeriodo.Should().Be(caso.DiasDelPeriodo);
        r.Rotacion.Should().Be(caso.Esperado.Rotacion, "rotación");
        r.DiasDeInventario.Should().Be(caso.Esperado.DiasDeInventario, "días de inventario");
        r.SinDato.Should().Be(caso.Esperado.SinDato, "sin dato");
    }

    [Fact]
    public void Sin_saldos_el_promedio_es_cero_y_no_hay_dato()
    {
        IndicadoresDeRotacion.InventarioPromedio([]).Should().Be(0m);
        IndicadoresDeRotacion.Calcular(1000m, 0m, 30).SinDato.Should().BeTrue();
    }

    [Fact]
    public void Un_promedio_negativo_tampoco_se_divide()
    {
        var r = IndicadoresDeRotacion.Calcular(1000m, -500m, 30);

        r.SinDato.Should().BeTrue("un inventario promedio negativo no da una rotación con sentido");
        r.Rotacion.Should().BeNull();
        r.DiasDeInventario.Should().BeNull();
    }

    [Fact]
    public void Los_dias_del_periodo_se_cuentan_de_punta_a_punta()
    {
        IndicadoresDeRotacion.DiasDelPeriodo(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)).Should().Be(30);
        IndicadoresDeRotacion.DiasDelPeriodo(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)).Should().Be(365);
        IndicadoresDeRotacion.DiasDelPeriodo(new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 25)).Should().Be(1);
    }

    [Fact]
    public void Un_periodo_sin_dias_es_un_error_de_quien_llama()
    {
        var act = () => IndicadoresDeRotacion.Calcular(1000m, 500m, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private sealed record Caso(string Nombre, decimal CostoDeVenta, decimal[] Saldos, int DiasDelPeriodo, Esperado Esperado);

    private sealed record Esperado(decimal InventarioPromedio, decimal? Rotacion, decimal? DiasDeInventario, bool SinDato);
}
