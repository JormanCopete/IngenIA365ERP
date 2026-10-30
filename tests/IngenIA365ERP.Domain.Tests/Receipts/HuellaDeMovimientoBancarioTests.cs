using System.Text;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Receipts.Huellas;

namespace IngenIA365ERP.Domain.Tests.Receipts;

/// <summary>
/// Feature 015, T0185 (R-06; FR-066, FR-197): la huella de un movimiento bancario coincide con los casos de <c>Casos/huella-*.json</c>. Con la secuencia del banco, el
/// movimiento ya cargado se omite; sin ella, la huella es banco, cuenta, fecha, valor y referencia, y dos iguales quedan como posibles duplicados para que una persona
/// decida. El archivo entero tiene su propia huella. Prueba escrita, sin correr (estrategia del dueño).
/// </summary>
public class HuellaDeMovimientoBancarioTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Receipts", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "huella-*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_minimos_de_T0185()
    {
        Directory.EnumerateFiles(Directorio, "huella-*.json").Should().HaveCountGreaterThanOrEqualTo(6, "≈6 casos dorados de la huella (T0185)");
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void La_clasificacion_coincide_con_el_caso(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var cargadas = caso.YaCargadas.Select(m => HuellaDeMovimientoBancario.Calcular(Movimiento(m)).Huella).ToHashSet(StringComparer.Ordinal);

        var r = HuellaDeMovimientoBancario.Clasificar(caso.Movimientos.Select(Movimiento).ToList(), cargadas);

        using (new AssertionScope($"{archivo} — {caso.Nombre}"))
        {
            r.Select(x => (x.Linea, x.Destino.ToString(), x.Huella.ConIdentificadorDelBanco, x.DuplicaLineaDelArchivo))
                .Should().Equal(caso.Esperado.Select(e => (e.Linea, e.Destino, e.ConIdentificador, e.Duplica)));
            r.Should().OnlyContain(x => x.Huella.Huella.Length == 64, "SHA-256 en hexadecimal");
        }
    }

    [Fact]
    public void La_huella_del_archivo_es_su_sha256_y_cambia_con_un_byte()
    {
        var a = Encoding.UTF8.GetBytes("fecha;valor\n2026-11-02;150000\n");
        var b = Encoding.UTF8.GetBytes("fecha;valor\n2026-11-02;150001\n");
        HuellaDeMovimientoBancario.DelArchivo(a).Should().Be(HuellaDeMovimientoBancario.DelArchivo(a.ToArray()));
        HuellaDeMovimientoBancario.DelArchivo(a).Should().NotBe(HuellaDeMovimientoBancario.DelArchivo(b));
        HuellaDeMovimientoBancario.DelArchivo(a).Should().HaveLength(64);
    }

    [Fact]
    public void La_referencia_se_normaliza_y_el_valor_se_redondea_a_dos_decimales()
    {
        var x = HuellaDeMovimientoBancario.Calcular(new MovimientoParaHuella(1, "51", "001", new DateOnly(2026, 11, 2), 1000.004m, "  ab   c ", null));
        var y = HuellaDeMovimientoBancario.Calcular(new MovimientoParaHuella(2, "51", "001", new DateOnly(2026, 11, 2), 1000m, "AB C", null));
        x.Huella.Should().Be(y.Huella);
        x.ConIdentificadorDelBanco.Should().BeFalse();
    }

    private static MovimientoParaHuella Movimiento(Mov m) =>
        new(m.Linea, m.Banco, m.Cuenta, DateOnly.Parse(m.Fecha, System.Globalization.CultureInfo.InvariantCulture), m.Valor, m.Referencia, m.Secuencia);

    private sealed record Caso(string Nombre, string Descripcion, List<Mov> YaCargadas, List<Mov> Movimientos, List<Esperada> Esperado);

    private sealed record Mov(int Linea, string Banco, string Cuenta, string Fecha, decimal Valor, string? Referencia, string? Secuencia);

    private sealed record Esperada(int Linea, string Destino, bool ConIdentificador, int? Duplica);
}
