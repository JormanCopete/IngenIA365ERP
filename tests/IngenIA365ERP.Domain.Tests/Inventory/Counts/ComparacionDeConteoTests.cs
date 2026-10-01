using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Inventory.Counts;

namespace IngenIA365ERP.Domain.Tests.Inventory.Counts;

/// <summary>
/// Feature 012, US11, T382 (FR-040, US11-2, US11-3; data-model §8.2): el motor puro <see cref="ComparacionDeConteo"/> coincide con
/// los casos calculados a mano de <c>Casos/</c> —lo contado de una ronda es la suma de todos los contadores, una tanda negativa
/// corrige, la tolerancia es por porcentaje <b>o</b> por unidades (con las dos en cero toda diferencia pide reconteo), manda la
/// última ronda, con movimientos admitidos el teórico es la foto más lo movido después, y un producto fuera de la foto entra con
/// teórico 0—, más las reglas sueltas de la tolerancia.
/// </summary>
public class ComparacionDeConteoTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Inventory", "Counts", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_de_US11()
    {
        var nombres = Directory.EnumerateFiles(Directorio, "*.json").Select(Path.GetFileName).ToList();
        foreach (var prefijo in new[] { "01-", "02-", "03-", "04-", "05-", "06-", "07-", "08-" })
            nombres.Should().Contain(n => n!.StartsWith(prefijo, StringComparison.Ordinal), $"falta el caso {prefijo}*");
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_coincide_con_el_calculo_manual(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var resultado = ComparacionDeConteo.Comparar(new PedidoDeComparacion<string>(
            caso.Foto.Select(f => new LineaDeFoto<string>(f.Linea, f.Teorico)).ToList(),
            caso.Capturas.Select(c => new CapturaDeConteo<string>(c.Linea, c.Ronda, c.Contador, c.Cantidad)).ToList(),
            caso.Movimientos.Select(m => new MovimientoPosteriorALaFoto<string>(m.Linea, m.Cantidad)).ToList(),
            new ToleranciaDeReconteo(caso.Tolerancia.Porcentaje, caso.Tolerancia.Unidades),
            caso.SumaMovimientos));

        using var _ = new AssertionScope($"{archivo} — {caso.Nombre}");
        resultado.Select(r => r.Linea).Should().Equal(caso.Esperado.Select(e => e.Linea), "una línea por cada línea de la foto, en su orden");
        foreach (var esperado in caso.Esperado)
        {
            var r = resultado.Single(x => x.Linea == esperado.Linea);
            r.Teorico.Should().Be(esperado.Teorico, $"teórico de {esperado.Linea}");
            r.MovimientosPosteriores.Should().Be(esperado.Movimientos, $"movimientos de {esperado.Linea}");
            r.Contado.Should().Be(esperado.Contado, $"contado de {esperado.Linea}");
            r.Diferencia.Should().Be(esperado.Diferencia, $"diferencia de {esperado.Linea}");
            r.RondaQueManda.Should().Be(esperado.Ronda, $"ronda que manda en {esperado.Linea}");
            r.RequiereReconteo.Should().Be(esperado.Reconteo, $"reconteo de {esperado.Linea}");
        }
    }

    [Theory]
    [InlineData(10, -1, 0, 1, false)]
    [InlineData(10, -2, 0, 1, true)]
    [InlineData(10, -1, 10, 0, false)]
    [InlineData(10, 2, 10, 1, true)]
    [InlineData(10, 2, 10, 2, false)]
    [InlineData(0, 1, 50, 0, true)]
    [InlineData(5, 0, 0, 0, false)]
    public void Una_diferencia_se_tolera_si_cabe_en_las_unidades_o_en_el_porcentaje(
        double teorico, double diferencia, double porcentaje, double unidades, bool fuera) =>
        ComparacionDeConteo.FueraDeTolerancia((decimal)teorico, (decimal)diferencia, new ToleranciaDeReconteo((decimal)porcentaje, (decimal)unidades))
            .Should().Be(fuera);

    [Fact]
    public void Sin_sumar_movimientos_lo_movido_despues_no_cambia_el_teorico()
    {
        var r = ComparacionDeConteo.Comparar(new PedidoDeComparacion<int>(
            [new LineaDeFoto<int>(1, 10m)],
            [new CapturaDeConteo<int>(1, 1, 7, 8m)],
            [new MovimientoPosteriorALaFoto<int>(1, -2m)],
            ToleranciaDeReconteo.Ninguna,
            SumaMovimientos: false)).Single();

        r.MovimientosPosteriores.Should().BeNull("con bloqueo no se admiten movimientos y no se suman");
        r.Teorico.Should().Be(10m);
        r.Diferencia.Should().Be(-2m);
        r.ContadoPorRonda.Should().ContainKey(1).WhoseValue.Should().Be(8m);
    }

    private sealed class Caso
    {
        public string Nombre { get; set; } = string.Empty;
        public ToleranciaJson Tolerancia { get; set; } = new();
        public bool SumaMovimientos { get; set; }
        public List<FotoJson> Foto { get; set; } = [];
        public List<CapturaJson> Capturas { get; set; } = [];
        public List<MovimientoJson> Movimientos { get; set; } = [];
        public List<EsperadoJson> Esperado { get; set; } = [];
    }

    private sealed class ToleranciaJson
    {
        public decimal Porcentaje { get; set; }
        public decimal Unidades { get; set; }
    }

    private sealed class FotoJson
    {
        public string Linea { get; set; } = string.Empty;
        public decimal Teorico { get; set; }
    }

    private sealed class CapturaJson
    {
        public string Linea { get; set; } = string.Empty;
        public byte Ronda { get; set; }
        public int Contador { get; set; }
        public decimal Cantidad { get; set; }
    }

    private sealed class MovimientoJson
    {
        public string Linea { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
    }

    private sealed class EsperadoJson
    {
        public string Linea { get; set; } = string.Empty;
        public decimal Teorico { get; set; }
        public decimal? Movimientos { get; set; }
        public decimal Contado { get; set; }
        public decimal Diferencia { get; set; }
        public byte Ronda { get; set; }
        public bool Reconteo { get; set; }
    }
}
