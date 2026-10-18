using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Common.Layouts;

namespace IngenIA365ERP.Domain.Tests.Common.Layouts;

/// <summary>
/// T0987 (feature 013, K5, US16; adaptadores-y-archivos.md §7.1–§7.2; SC-019): el motor genérico de layouts. Cada caso de
/// <c>Casos/*.json</c> trae su layout, sus filas y lo esperado —las líneas exactas con su fin de línea, o las inconsistencias con
/// severidad, código, registro, campo, crédito, ruta y fila—, derivado a mano en <c>derivacion</c>. Las pruebas sueltas fijan lo que no
/// cabe en un caso: bytes en la codificación del layout, SHA-256 determinista y la revisión de la definición.
/// </summary>
public class LayoutsDeArchivoTests
{
    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Common", "Layouts", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.GetFiles(DirectorioDeCasos, "*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Hay_casos_de_ancho_fijo_delimitado_y_bloqueantes()
    {
        Casos().Should().HaveCountGreaterThanOrEqualTo(8);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_caso_sale_como_se_derivo_a_mano(string archivo)
    {
        var caso = Caso.Cargar(Path.Combine(DirectorioDeCasos, archivo));
        using var _ = new AssertionScope($"{archivo} — {caso.Nombre}");
        caso.Derivacion.Should().NotBeNullOrWhiteSpace("todo caso lleva su derivación a mano");

        var r = EscritorPorLayout.Escribir(caso.Layout, caso.Filas);

        r.Inconsistencias.Select(Clave).Should().BeEquivalentTo(caso.Inconsistencias, "las inconsistencias son exactamente las esperadas");
        r.Inconsistencias.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Mensaje), "cada inconsistencia lleva un mensaje para la persona");
        r.Escrito.Should().Be(caso.Escrito);
        if (!caso.Escrito)
        {
            r.Contenido.Should().BeNull("con una bloqueante no se escribe ninguna fila");
            r.Sha256.Should().BeNull();
            r.Texto.Should().BeNull();
            r.Bloqueantes.Should().BePositive();
            return;
        }

        var esperado = string.Concat(caso.Lineas.Select(l => l + caso.FinDeLinea));
        r.Texto.Should().Be(esperado, "las líneas salen exactas, con el fin de línea del layout también tras la última");
        r.Lineas.Should().Be(caso.Filas.Count);
        r.Contenido.Should().Equal(caso.Layout.Codificacion().GetBytes(esperado), "los bytes van en la codificación del layout");
        r.Sha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(r.Contenido!)));
        if (caso.Layout.FileKind == TipoDeArchivoDeLayout.FixedWidth)
            foreach (var (linea, fila) in caso.Lineas.Zip(caso.Filas))
                linea.Length.Should().Be(caso.Layout.Registro(fila.Registro)!.Length, "en ancho fijo cada línea mide lo que su registro");
    }

    [Fact]
    public void El_escritor_es_determinista()
    {
        var caso = Caso.Cargar(Path.Combine(DirectorioDeCasos, "01-ancho-fijo-cabecera-detalle-totales.json"));
        var a = EscritorPorLayout.Escribir(caso.Layout, caso.Filas);
        var b = EscritorPorLayout.Escribir(caso.Layout, caso.Filas);
        a.Contenido.Should().Equal(b.Contenido);
        a.Sha256.Should().Be(b.Sha256).And.HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Utf8_va_sin_marca_de_orden_y_latin1_escribe_un_byte_por_letra()
    {
        var utf8 = Layout("utf-8");
        var r = EscritorPorLayout.Escribir(utf8, [Fila("Ñandú")]);
        r.Contenido!.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF });
        r.Contenido.Should().Equal(Encoding.UTF8.GetBytes("Ñandú\r\n"));

        var latin1 = Layout("iso-8859-1");
        var l = EscritorPorLayout.Escribir(latin1, [Fila("Ñandú")]);
        l.Contenido.Should().Equal(new byte[] { 0xD1, (byte)'a', (byte)'n', (byte)'d', 0xFA, 0x0D, 0x0A });
        l.Advertencias.Should().Be(0, "latin1 representa la Ñ y la ú");
    }

    [Fact]
    public void Una_sola_bloqueante_impide_escribir_las_filas_buenas()
    {
        var layout = Layout("utf-8");
        var r = EscritorPorLayout.Escribir(layout, [Fila("Bien"), Fila(null), Fila("Bien")]);
        r.Escrito.Should().BeFalse();
        r.Contenido.Should().BeNull();
        r.Lineas.Should().Be(0);
        r.Inconsistencias.Should().ContainSingle().Which.Fila.Should().Be(1);
    }

    [Fact]
    public void Cuenta_los_campos_sin_cotejar()
    {
        var caso = Caso.Cargar(Path.Combine(DirectorioDeCasos, "01-ancho-fijo-cabecera-detalle-totales.json"));
        caso.Layout.TotalFields.Should().Be(14);
        caso.Layout.VerifiedFields.Should().Be(4);
        caso.Layout.UnverifiedFields.Should().Be(10);
        caso.Layout.HasUnverifiedFields.Should().BeTrue();
    }

    [Theory]
    [InlineData("""{ "number": 1, "name": "A", "order": 1, "length": 5, "kind": "A", "format": "Text", "path": "X", "constant": "Y" }""", "a la vez")]
    [InlineData("""{ "number": 1, "name": "A", "order": 1, "length": 5, "kind": "A", "format": "Text", "required": true }""", "no dice de dónde sale")]
    [InlineData("""{ "number": 1, "name": "A", "order": 1, "length": 5, "kind": "N", "format": "Rate", "path": "X" }""", "decimals")]
    [InlineData("""{ "number": 1, "name": "A", "order": 1, "length": 5, "kind": "X", "format": "Text", "path": "X" }""", "tipo «X»")]
    [InlineData("""{ "number": 1, "name": "A", "order": 1, "length": 0, "kind": "A", "format": "Text", "path": "X" }""", "largo inválido")]
    [InlineData("""{ "number": 1, "name": "A", "order": 1, "length": 2, "kind": "A", "format": "Text", "constant": "ZZ", "allowedValues": ["CC"] }""", "valores admitidos")]
    [InlineData("""{ "number": 1, "name": "A", "order": 1, "length": 5, "kind": "A", "format": "Text", "path": "X", "requerido": true }""", "no es un JSON válido")]
    [InlineData("""{ "number": 2, "name": "A", "order": 1, "length": 5, "kind": "A", "format": "Text", "path": "X" }""", "consecutivos")]
    public void Una_definicion_invalida_no_se_lee(string campo, string motivo)
    {
        var json = $$"""
            { "code": "MALO", "version": "1", "source": "prueba", "validFrom": "2026-01-01", "fileKind": "Delimited", "delimiter": "|",
              "records": [ { "code": "Detail", "fields": [ {{campo}} ] } ] }
            """;
        var leer = () => LectorDeLayout.Leer(json, "malo.json");
        leer.Should().Throw<FormatException>().WithMessage($"*malo.json*{motivo}*");
    }

    [Fact]
    public void En_ancho_fijo_los_campos_son_contiguos_y_suman_el_largo_del_registro()
    {
        const string json = """
            { "code": "HUECO", "version": "1", "source": "prueba", "validFrom": "2026-01-01", "fileKind": "FixedWidth",
              "records": [ { "code": "Detail", "length": 10, "fields": [
                { "number": 1, "name": "A", "start": 1, "length": 3, "kind": "A", "format": "Text", "path": "X" },
                { "number": 2, "name": "B", "start": 5, "length": 3, "kind": "A", "format": "Text", "path": "Y" } ] } ] }
            """;
        var leer = () => LectorDeLayout.Leer(json, "hueco.json");
        leer.Should().Throw<FormatException>().WithMessage("*empieza en 5 y debía empezar en 4*").WithMessage("*suman 6 posiciones*");
    }

    [Fact]
    public void Un_delimitado_sin_delimitador_o_con_codificacion_desconocida_no_se_lee()
    {
        const string json = """
            { "code": "SIN", "version": "1", "source": "prueba", "validFrom": "2026-01-01", "fileKind": "Delimited", "encoding": "no-existe",
              "records": [ { "code": "Detail", "fields": [ { "number": 1, "name": "A", "order": 1, "length": 3, "kind": "A", "format": "Text", "path": "X" } ] } ] }
            """;
        var leer = () => LectorDeLayout.Leer(json, "sin.json");
        leer.Should().Throw<FormatException>().WithMessage("*delimitador*").WithMessage("*codificación «no-existe»*");
    }

    [Fact]
    public void La_vigencia_cubre_desde_y_hasta_inclusive()
    {
        var l = Layout("utf-8") with { ValidFrom = new DateOnly(2026, 12, 1), ValidTo = new DateOnly(2027, 6, 30) };
        l.IsValidAt(new DateOnly(2026, 11, 30)).Should().BeFalse();
        l.IsValidAt(new DateOnly(2026, 12, 1)).Should().BeTrue();
        l.IsValidAt(new DateOnly(2027, 6, 30)).Should().BeTrue();
        l.IsValidAt(new DateOnly(2027, 7, 1)).Should().BeFalse();
    }

    private static LayoutDeArchivo Layout(string codificacion) => LectorDeLayout.Leer($$"""
        { "code": "UNO", "version": "1", "source": "prueba", "validFrom": "2026-01-01", "fileKind": "Delimited", "delimiter": "|", "encoding": "{{codificacion}}",
          "records": [ { "code": "Detail", "fields": [ { "number": 1, "name": "Nombre", "order": 1, "length": 20, "kind": "A", "format": "Text", "required": true, "path": "Deudor.Nombre" } ] } ] }
        """, "uno.json");

    private static FilaDeLayout Fila(string? nombre) => new("Detail", "CR-1", new Dictionary<string, object?> { ["Deudor.Nombre"] = nombre });

    private static InconsistenciaEsperada Clave(InconsistenciaDeLayout i) => new(i.Severidad, i.Codigo, i.Registro, i.Campo, i.Credito, i.Ruta, i.Fila);

    public sealed record InconsistenciaEsperada(SeveridadDeInconsistencia Severidad, string Codigo, string Registro, int? Campo, string? Credito, string? Ruta, int Fila);

    private sealed class Caso
    {
        private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

        public string Nombre { get; private init; } = string.Empty;
        public string Derivacion { get; private init; } = string.Empty;
        public LayoutDeArchivo Layout { get; private init; } = null!;
        public IReadOnlyList<FilaDeLayout> Filas { get; private init; } = [];
        public bool Escrito { get; private init; }
        public string FinDeLinea { get; private init; } = "\r\n";
        public IReadOnlyList<string> Lineas { get; private init; } = [];
        public IReadOnlyList<InconsistenciaEsperada> Inconsistencias { get; private init; } = [];

        public static Caso Cargar(string ruta)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(ruta), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var raiz = doc.RootElement;
            var esperado = raiz.GetProperty("esperado");
            return new Caso
            {
                Nombre = raiz.GetProperty("nombre").GetString()!,
                Derivacion = raiz.TryGetProperty("derivacion", out var d) ? d.GetString()! : string.Empty,
                Layout = LectorDeLayout.Leer(raiz.GetProperty("layout").GetRawText(), Path.GetFileName(ruta)),
                Filas = raiz.GetProperty("filas").EnumerateArray().Select(f => new FilaDeLayout(
                    f.GetProperty("registro").GetString()!,
                    f.TryGetProperty("credito", out var c) ? c.GetString() : null,
                    f.GetProperty("valores").EnumerateObject().ToDictionary(p => p.Name, p => Valor(p.Value)))).ToList(),
                Escrito = esperado.GetProperty("escrito").GetBoolean(),
                FinDeLinea = esperado.TryGetProperty("finDeLinea", out var fin) ? fin.GetString()! : "\r\n",
                Lineas = esperado.TryGetProperty("lineas", out var l) ? l.EnumerateArray().Select(x => x.GetString()!).ToList() : [],
                Inconsistencias = esperado.GetProperty("inconsistencias").Deserialize<List<InconsistenciaEsperada>>(Opciones)!,
            };
        }

        private static object? Valor(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Number => e.GetDecimal(),
            JsonValueKind.String => e.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}
