using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Receipts.Cumplimiento;

namespace IngenIA365ERP.Domain.Tests.Receipts;

/// <summary>
/// Feature 015, US14, T0232 (R-24; FR-095, FR-202): las señales de efectivo coinciden con los casos de <c>Casos/senales-*.json</c>: acumulado del mes calendario contra el
/// umbral de múltiples (la anulación resta), fraccionamiento por ventana, cantidad mínima y umbral de declaración, pagador tercero frecuente y consignante no identificado
/// repetido. Los parámetros llegan con su versión. Prueba escrita, sin correr.
/// </summary>
public class SenalesDeEfectivoTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Receipts", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "senales-*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_minimos_de_T0232()
    {
        Directory.EnumerateFiles(Directorio, "senales-*.json").Should().HaveCountGreaterThanOrEqualTo(8, "≈8 casos dorados de las señales (T0232)");
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void Las_senales_coinciden_con_el_caso(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var hoy = Fecha(caso.Hoy);
        var p = new ParametrosDeSenales(caso.Parametros.UmbralIndividual, caso.Parametros.UmbralMultiples, caso.Parametros.UmbralDeclaracion,
            caso.Parametros.FraccionamientoOperaciones, caso.Parametros.FraccionamientoDias, caso.Parametros.TercerosFrecuentes, caso.Parametros.ConsignanteRepetido,
            caso.Parametros.Version);
        var senales = SenalesDeEfectivo.Evaluar(caso.Persona, caso.Transacciones.Select(t => new TransaccionDeEfectivo(t.Persona, t.Pagador, Fecha(t.Fecha), t.Efectivo)).ToList(),
            hoy, p).ToList();
        if (caso.Consignante is { } c && SenalesDeEfectivo.Consignante(c, caso.Partidas.Select(x => (Fecha(x.Fecha), x.Valor)).ToList(), hoy, p) is { } s) senales.Add(s);

        using (new AssertionScope($"{archivo} — {caso.Descripcion}"))
        {
            senales.Select(x => (x.Senal.ToString(), x.Cantidad, x.Total)).Should().BeEquivalentTo(caso.Esperado.Select(e => (e.Senal, e.Cantidad, e.Total)));
            p.Version.Should().Be(caso.Parametros.Version, "la señal deja el rastro de la versión del parámetro (FR-097)");
        }
    }

    private static DateOnly Fecha(string texto) => DateOnly.Parse(texto, CultureInfo.InvariantCulture);

    private sealed record Caso(string Nombre, string Descripcion, int Persona, string Hoy, Parametros Parametros, List<Tx> Transacciones, string? Consignante,
        List<Partida> Partidas, List<Esperada> Esperado);

    private sealed record Parametros(decimal UmbralIndividual, decimal UmbralMultiples, decimal UmbralDeclaracion, int FraccionamientoOperaciones, int FraccionamientoDias,
        int TercerosFrecuentes, int ConsignanteRepetido, string? Version);

    private sealed record Tx(int Persona, int Pagador, string Fecha, decimal Efectivo);

    private sealed record Partida(string Fecha, decimal Valor);

    private sealed record Esperada(string Senal, int Cantidad, decimal Total);
}
