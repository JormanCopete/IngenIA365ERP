using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Lotes;

namespace IngenIA365ERP.Domain.Tests.Treasury;

/// <summary>
/// Feature 015, T0126 (FR-110, FR-116; SC-011, SC-013): el armado puro de un lote coincide con los casos de <c>Casos/lote-*.json</c>: selección por forma, pendientes
/// con su motivo, las cuatro marcas antifraude, totales y cantidad al peso y una orden a lo sumo una vez. Prueba escrita, sin correr.
/// </summary>
public class ArmadoDeLoteTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Treasury", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "lote-*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_minimos_de_T0126() =>
        Directory.EnumerateFiles(Directorio, "lote-*.json").Should().HaveCountGreaterThanOrEqualTo(6, "≈6 casos dorados del armado de lotes (T0126)");

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_armado_coincide_con_el_calculo_manual(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var ordenes = caso.Ordenes.Select(o => new OrdenCandidata(o.Id, Enum.Parse<PaymentOrderState>(o.Estado), Enum.Parse<PaymentForm>(o.Forma), o.Neto,
            o.PagadaPorArchivo, o.EnOtroLote, o.AnulacionPedida, o.Motivos, new VerificacionDeDestino(o.Cambiada, o.Compartida, o.UsuarioInterno, o.Importada))).ToList();

        var r = ArmadoDeLote.Armar(ordenes, Enum.Parse<PaymentBatchForm>(caso.Forma));

        using (new AssertionScope($"{archivo} — {caso.Nombre}"))
        {
            r.Incluidas.Select(i => (i.OrdenId, i.Valor, (int)i.Marcas)).Should().Equal(caso.Esperado.Incluidas.Select(i => (i.Id, i.Valor, i.Marcas)));
            r.Pendientes.Select(p => (p.OrdenId, p.Motivo)).Should().Equal(caso.Esperado.Pendientes.Select(p => (p.Id, p.Motivo)));
            r.Total.Should().Be(caso.Esperado.Total);
            r.Cantidad.Should().Be(caso.Esperado.Cantidad);
        }
    }

    [Fact]
    public void Solo_la_cuenta_cambiada_exige_revision() =>
        new OrdenIncluida(1, 10m, PaymentBatchItemFlags.SharedDestination | PaymentBatchItemFlags.RecentAccountChange).ExigeRevision.Should().BeTrue();

    private sealed record Caso(string Nombre, string Descripcion, string Forma, List<Orden> Ordenes, Esperado Esperado);

    private sealed record Orden(int Id, string Estado, string Forma, decimal Neto, bool PagadaPorArchivo, bool EnOtroLote, bool AnulacionPedida, List<string> Motivos,
        bool Cambiada, bool Compartida, bool UsuarioInterno, bool Importada);

    private sealed record Esperado(List<Incluida> Incluidas, List<Pendiente> Pendientes, decimal Total, int Cantidad);

    private sealed record Incluida(int Id, decimal Valor, int Marcas);

    private sealed record Pendiente(int Id, string Motivo);
}
