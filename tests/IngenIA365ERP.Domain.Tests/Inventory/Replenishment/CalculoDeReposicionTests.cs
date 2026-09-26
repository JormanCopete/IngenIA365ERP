using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Inventory.Replenishment;

namespace IngenIA365ERP.Domain.Tests.Inventory.Replenishment;

/// <summary>
/// Feature 012, US17, T942 (FR-035, US17-2; contracts/api.md §4.4, §27): el cálculo puro <see cref="CalculoDeReposicion"/>
/// coincide con los casos calculados a mano de <c>Casos/</c> —posición = disponible + en tránsito + por recibir; reorden cuando
/// la posición es <b>igual o menor</b> que el punto; sugerido = máximo − posición sólo si pide reorden; quiebre cuando el
/// disponible es <b>estrictamente</b> menor que el mínimo; por recibir 0 antes de I5—, más las reglas sueltas.
/// </summary>
public class CalculoDeReposicionTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Inventory", "Replenishment", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_de_US17()
    {
        var nombres = Directory.EnumerateFiles(Directorio, "*.json").Select(Path.GetFileName).ToList();
        foreach (var prefijo in new[] { "01-", "02-", "03-", "04-", "05-" })
            nombres.Should().Contain(n => n!.StartsWith(prefijo, StringComparison.Ordinal), $"falta el caso {prefijo}* (T942)");
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_calculo_coincide_con_el_calculo_manual(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;

        var r = CalculoDeReposicion.Calcular(new EntradaDeReposicion(
            caso.Disponible, caso.EnTransito, caso.PorRecibir, caso.Minimo, caso.Maximo, caso.PuntoDeReorden));

        using var _ = new AssertionScope($"{archivo} — {caso.Nombre}");
        r.Posicion.Should().Be(caso.Esperado.Posicion, "posición");
        r.RequiereReorden.Should().Be(caso.Esperado.RequiereReorden, "requiere reorden");
        r.Sugerido.Should().Be(caso.Esperado.Sugerido, "sugerido");
        r.Quiebre.Should().Be(caso.Esperado.Quiebre, "quiebre");
        r.Alerta.Should().Be(caso.Esperado.RequiereReorden || caso.Esperado.Quiebre, "la vista reorder-alerts muestra la fila si pide reorden o está en quiebre");
    }

    [Fact]
    public void El_sugerido_se_redondea_a_cuatro_decimales_de_cantidad()
    {
        var r = CalculoDeReposicion.Calcular(new EntradaDeReposicion(1.123456m, 0m, 0m, 0m, 10m, 5m));

        r.Posicion.Should().Be(1.1235m);
        r.Sugerido.Should().Be(8.8765m);
    }

    [Fact]
    public void Sin_pedir_reorden_el_sugerido_es_cero_aunque_la_posicion_quede_bajo_el_maximo()
    {
        var r = CalculoDeReposicion.Calcular(new EntradaDeReposicion(20m, 0m, 0m, 10m, 50m, 15m));

        r.RequiereReorden.Should().BeFalse();
        r.Sugerido.Should().Be(0m, "el sugerido es máximo − posición sólo cuando la posición es igual o menor que el punto");
        r.Alerta.Should().BeFalse();
    }

    [Fact]
    public void Una_politica_en_cero_no_pide_nada_con_existencia_positiva()
    {
        var r = CalculoDeReposicion.Calcular(new EntradaDeReposicion(1m, 0m, 0m, 0m, 0m, 0m));

        r.RequiereReorden.Should().BeFalse();
        r.Quiebre.Should().BeFalse();
    }

    private sealed record Caso(
        string Nombre, decimal Disponible, decimal EnTransito, decimal PorRecibir, decimal Minimo, decimal Maximo, decimal PuntoDeReorden, Esperado Esperado);

    private sealed record Esperado(decimal Posicion, bool RequiereReorden, decimal Sugerido, bool Quiebre);
}
