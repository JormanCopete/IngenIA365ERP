using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Sales.Pricing;

namespace IngenIA365ERP.Domain.Tests.Sales.Pricing;

/// <summary>
/// Feature 012, I3, T546 (FR-053, T51; data-model §14 <c>INV_PriceLists</c>; contracts/api.md §19.2): el resolutor puro de
/// listas de precios coincide con los casos resueltos a mano de <c>Casos/</c> —gana la vigente que coincide en más dimensiones,
/// en empate cliente &gt; segmento &gt; canal &gt; sucursal y la general al final; otro asociado del mismo segmento toma la del
/// segmento; una lista vencida o inactiva no participa; si la ganadora no trae el producto se toma de la siguiente con
/// respaldo (F8); si ninguna lo trae no hay precio; una lista con impuestos incluidos devuelve su precio tal cual y lo marca—,
/// más el <c>ScopeKey</c> y el número de dimensiones del ámbito.
/// </summary>
public class ResolutorDeListaDePreciosCasosTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Sales", "Pricing", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_minimos_de_T546()
    {
        var nombres = Directory.EnumerateFiles(Directorio, "*.json").Select(Path.GetFileName).ToList();
        foreach (var prefijo in new[] { "01-", "02-", "03-", "04-", "05-", "06-", "07-" })
            nombres.Should().Contain(n => n!.StartsWith(prefijo, StringComparison.Ordinal), $"falta el caso {prefijo}*");
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_resolutor_coincide_con_el_calculo_manual(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        caso.Consultas.Should().NotBeEmpty($"{archivo} no tiene consultas");

        var listas = caso.Listas.Select(l => new ListaDePreciosCandidata(
            l.Id, l.Codigo, l.Persona, l.Segmento, l.Canal, l.Sucursal, DateOnly.Parse(l.Desde),
            l.Hasta is null ? null : DateOnly.Parse(l.Hasta), l.Activa, l.IncluyeImpuestos,
            l.Items.Select(i => new PrecioDeLista(i.Producto, i.Unidad, i.Precio)).ToList())).ToList();
        var codigos = caso.Listas.ToDictionary(l => l.Id, l => l.Codigo);

        var n = 0;
        foreach (var consulta in caso.Consultas)
        {
            n++;
            using var _ = new AssertionScope($"{archivo} — {caso.Nombre} — consulta {n}");
            var ctx = consulta.Contexto;
            var r = ResolutorDeListaDePrecios.Resolver(listas,
                new ContextoDePrecio(ctx.Persona, ctx.Segmento, ctx.Canal, ctx.Sucursal, DateOnly.Parse(ctx.Fecha)),
                consulta.Producto, consulta.Unidad);
            var e = consulta.Esperado;

            r.Found.Should().Be(e.Encontrado);
            r.Price.Should().Be(e.Precio);
            (r.PriceListId is { } id ? codigos[id] : null).Should().Be(e.Lista);
            r.PriceListCode.Should().Be(e.Lista);
            r.MatchedDimensions.Select(d => d.ToString()).Should().Equal(e.Dimensiones);
            r.FallbackUsed.Should().Be(e.Respaldo);
            r.IncludesTaxes.Should().Be(e.IncluyeImpuestos);
            r.Candidates.Select(c => c.Code).Should().Equal(e.Candidatas, "las candidatas van en el orden de la resolución");
            r.Candidates.Where(c => c.HasProduct).Select(c => c.Code).Should()
                .BeSubsetOf(e.Candidatas!, "sólo las candidatas dicen si traen el producto");
        }
    }

    [Theory]
    [InlineData(null, null, null, null, "P:-|S:-|C:-|B:-", 0)]
    [InlineData(5, null, null, null, "P:5|S:-|C:-|B:-", 1)]
    [InlineData(null, " a1 ", 2, null, "P:-|S:A1|C:2|B:-", 2)]
    [InlineData(5, "B", 2, 3, "P:5|S:B|C:2|B:3", 4)]
    public void El_ambito_se_normaliza_en_ScopeKey_y_cuenta_sus_dimensiones(
        int? persona, string? segmento, int? canal, int? sucursal, string clave, int dimensiones)
    {
        AmbitoDeLista.Clave(persona, segmento, canal, sucursal).Should().Be(clave);
        AmbitoDeLista.Dimensiones(persona, segmento, canal, sucursal).Should().Be((byte)dimensiones);
    }

    [Fact]
    public void Un_segmento_en_blanco_no_es_dimension()
    {
        AmbitoDeLista.NormalizarSegmento("   ").Should().BeNull();
        AmbitoDeLista.Clave(null, " ", null, null).Should().Be("P:-|S:-|C:-|B:-");
    }

    private sealed class Caso
    {
        public string Nombre { get; set; } = string.Empty;
        public List<ListaJson> Listas { get; set; } = [];
        public List<ConsultaJson> Consultas { get; set; } = [];
    }

    private sealed class ListaJson
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public int? Persona { get; set; }
        public string? Segmento { get; set; }
        public int? Canal { get; set; }
        public int? Sucursal { get; set; }
        public string Desde { get; set; } = string.Empty;
        public string? Hasta { get; set; }
        public bool Activa { get; set; }
        public bool IncluyeImpuestos { get; set; }
        public List<ItemJson> Items { get; set; } = [];
    }

    private sealed class ItemJson
    {
        public int Producto { get; set; }
        public int Unidad { get; set; }
        public decimal Precio { get; set; }
    }

    private sealed class ConsultaJson
    {
        public ContextoJson Contexto { get; set; } = new();
        public int Producto { get; set; }
        public int Unidad { get; set; }
        public EsperadoJson Esperado { get; set; } = new();
    }

    private sealed class ContextoJson
    {
        public int? Persona { get; set; }
        public string? Segmento { get; set; }
        public int? Canal { get; set; }
        public int? Sucursal { get; set; }
        public string Fecha { get; set; } = string.Empty;
    }

    private sealed class EsperadoJson
    {
        public bool Encontrado { get; set; }
        public decimal? Precio { get; set; }
        public string? Lista { get; set; }
        public List<string> Dimensiones { get; set; } = [];
        public bool Respaldo { get; set; }
        public bool IncluyeImpuestos { get; set; }
        public List<string>? Candidatas { get; set; }
    }
}
