using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Conciliacion;

namespace IngenIA365ERP.Domain.Tests.Treasury;

/// <summary>
/// Feature 015, T2, T0443 (FR-129): el cruce de la conciliación completa con reglas configurables coincide con <c>Casos/cruce-reglas-*.json</c>: cada regla con su holgura,
/// la prioridad manda, el vínculo con la línea de origen va primero, un empate no cruza y nada se cruza dos veces; sin reglas es el cruce de la mínima. Prueba escrita, sin
/// correr. (nuevo)
/// </summary>
public class CruceConReglasTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Treasury", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "cruce-reglas-*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_cruce_con_reglas_coincide_con_el_caso(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var lineas = caso.Lineas.Select(l => new LineaACruzar(l.Id, l.PublicId, l.Fecha, Enum.Parse<StatementLineDirection>(l.Sentido), l.Valor, l.Referencia)).ToList();
        var registros = caso.Registros.Select(r => new RegistroACruzar(r.Clave, r.Fecha, Enum.Parse<StatementLineDirection>(r.Sentido), r.Valor, r.Referencia, r.LineaDeOrigen))
            .ToList();
        var reglas = caso.Reglas.Select(r => new ReglaDeCruce(r.Codigo, Enum.Parse<ReconciliationRuleCriterion>(r.Criterio), r.DiasDeHolgura, r.Prioridad)).ToList();

        var cruces = CruceDeConciliacion.CruzarConReglas(lineas, registros, reglas, caso.DiasDeHolgura);

        using (new AssertionScope($"{archivo} — {caso.Nombre}"))
        {
            cruces.Select(c => (c.LineaId, string.Join(",", c.Registros), c.Regla))
                .Should().BeEquivalentTo(caso.Esperado.Select(e => (e.Linea, string.Join(",", e.Registros), e.Regla)));
            cruces.SelectMany(c => c.Registros).Should().OnlyHaveUniqueItems("nada se cruza dos veces");
        }
    }

    [Fact]
    public void Sin_reglas_es_el_cruce_de_la_minima()
    {
        var linea = new LineaACruzar(1, Guid.NewGuid(), new DateOnly(2026, 11, 5), StatementLineDirection.Credit, 100m, null);
        var registro = new RegistroACruzar("rec:a", new DateOnly(2026, 11, 5), StatementLineDirection.Credit, 100m, null);

        CruceDeConciliacion.CruzarConReglas([linea], [registro], [], 3).Single().Regla.Should().Be(CruceDeConciliacion.ReglaUnoAUno);
    }

    private sealed record Caso(string Nombre, int DiasDeHolgura, List<ReglaCaso> Reglas, List<LineaCaso> Lineas, List<RegistroCaso> Registros, List<EsperadoCaso> Esperado);

    private sealed record ReglaCaso(string Codigo, string Criterio, int DiasDeHolgura, int Prioridad);

    private sealed record LineaCaso(long Id, Guid PublicId, DateOnly Fecha, string Sentido, decimal Valor, string? Referencia);

    private sealed record RegistroCaso(string Clave, DateOnly Fecha, string Sentido, decimal Valor, string? Referencia, Guid? LineaDeOrigen);

    private sealed record EsperadoCaso(long Linea, List<string> Registros, string Regla);
}
