using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Conciliacion;

namespace IngenIA365ERP.Domain.Tests.Treasury;

/// <summary>
/// Feature 015, US25, T0272 (FR-216): el cruce automático de la conciliación mínima coincide con <c>Casos/cruce-*.json</c>: valor exacto, fecha dentro de
/// <c>Conciliacion.DiasDeHolgura</c>, la referencia desempata, el vínculo con la línea de origen, uno a uno antes que uno a varios, nada se cruza dos veces y un empate no
/// cruza solo. Prueba escrita, sin correr.
/// </summary>
public class CruceDeConciliacionTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Treasury", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "cruce-*.json").Where(f => !Path.GetFileName(f).StartsWith("cruce-reglas-", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_minimos_de_T0272() =>
        Directory.EnumerateFiles(Directorio, "cruce-*.json").Should().HaveCountGreaterThanOrEqualTo(10, "≈10 casos dorados del cruce (T0272)");

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_cruce_coincide_con_el_caso(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var lineas = caso.Lineas.Select(l => new LineaACruzar(l.Id, l.PublicId, l.Fecha, Enum.Parse<StatementLineDirection>(l.Sentido), l.Valor, l.Referencia)).ToList();
        var registros = caso.Registros.Select(r => new RegistroACruzar(r.Clave, r.Fecha, Enum.Parse<StatementLineDirection>(r.Sentido), r.Valor, r.Referencia, r.LineaDeOrigen))
            .ToList();

        var cruces = CruceDeConciliacion.Cruzar(lineas, registros, caso.DiasDeHolgura);

        using (new AssertionScope($"{archivo} — {caso.Nombre}"))
        {
            cruces.Select(c => (c.LineaId, string.Join(",", c.Registros), c.Regla))
                .Should().BeEquivalentTo(caso.Esperado.Select(e => (e.Linea, string.Join(",", e.Registros), e.Regla)), o => o.WithStrictOrdering());
            cruces.SelectMany(c => c.Registros).Should().OnlyHaveUniqueItems("nada se cruza dos veces");
            cruces.Select(c => c.LineaId).Should().OnlyHaveUniqueItems();
        }
    }

    [Theory]
    [InlineData("123", "0123", true)]
    [InlineData(" ab 12 ", "AB12", true)]
    [InlineData(null, null, false)]
    [InlineData("", "", false)]
    [InlineData("12", "13", false)]
    public void La_referencia_se_compara_sin_espacios_ceros_ni_mayusculas(string? a, string? b, bool igual) =>
        CruceDeConciliacion.MismaReferencia(a, b).Should().Be(igual);

    private sealed record Caso(string Nombre, int DiasDeHolgura, List<LineaCaso> Lineas, List<RegistroCaso> Registros, List<EsperadoCaso> Esperado);

    private sealed record LineaCaso(long Id, Guid PublicId, DateOnly Fecha, string Sentido, decimal Valor, string? Referencia);

    private sealed record RegistroCaso(string Clave, DateOnly Fecha, string Sentido, decimal Valor, string? Referencia, Guid? LineaDeOrigen);

    private sealed record EsperadoCaso(long Linea, List<string> Registros, string Regla);
}
