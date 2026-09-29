using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Sales.Promotions;

namespace IngenIA365ERP.Domain.Tests.Sales.Promotions;

/// <summary>
/// Feature 012, I6, T862 (FR-055, US14-4; data-model §14 «Promociones»; contracts/api.md §19.4): el motor puro de promociones
/// coincide con los casos calculados a mano de <c>Casos/</c> —3×2 con el residuo por <c>Redondeo.Residuo</c>, porcentaje por
/// categoría con sus descendientes y segmento (Y entre clases, O dentro de una), precio por cantidad con dos tramos, precio de
/// paquete, dos no acumulables (gana la de mayor descuento), una acumulable que se suma, fuera de vigencia o de canal no aplica—
/// y la salida es siempre un descuento por línea con su promoción, nunca una línea a precio cero.
/// </summary>
public class MotorDePromocionesTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Sales", "Promotions", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_minimos_de_T862()
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
        caso.Consultas.Should().NotBeEmpty($"{archivo} no tiene consultas");
        var promociones = caso.Promociones.Select(Promocion).ToList();
        var montos = Redondeo.MontosDesde(caso.Montos);
        var residuo = Redondeo.ResiduoDesde(caso.Residuo);

        var n = 0;
        foreach (var consulta in caso.Consultas)
        {
            n++;
            using var _ = new AssertionScope($"{archivo} — {caso.Nombre} — consulta {n}");
            var lineas = consulta.Lineas.Select(l => new LineaDePromocion(l.Linea, l.Producto, l.Padre, l.Categorias, l.Cantidad, l.Precio)).ToList();
            var r = MotorDePromociones.Aplicar(lineas, new ContextoDePromocion(DateOnly.Parse(consulta.Fecha), consulta.Canal, consulta.Segmento, montos, residuo),
                promociones);

            r.Descuentos.Select(d => (d.LineNumber, d.PromotionId, d.Amount)).Should()
                .Equal(consulta.Esperado.Select(e => (e.Linea, e.Promocion, e.Valor)), "descuentos por línea y promoción, en orden de línea");
            r.Descartadas.Select(d => d.PromotionId).Should().BeEquivalentTo(consulta.Descartadas ?? []);

            // Siempre un descuento por línea con su promoción, con explicación, y la línea nunca queda en cero ni negativa.
            foreach (var d in r.Descuentos)
            {
                d.PromotionId.Should().BePositive();
                d.Amount.Should().BePositive();
                d.Explicacion.Should().NotBeNullOrWhiteSpace();
            }
            foreach (var linea in lineas)
            {
                var descontado = r.Descuentos.Where(d => d.LineNumber == linea.LineNumber).Sum(d => d.Amount);
                (linea.Bruto(montos) - descontado).Should().BePositive($"la línea {linea.LineNumber} no queda a precio cero");
            }
        }
    }

    [Fact]
    public void Sin_promociones_no_hay_descuentos()
    {
        var r = MotorDePromociones.Aplicar([new LineaDePromocion(1, 1, null, [1], 3m, 1000m)],
            new ContextoDePromocion(new DateOnly(2026, 10, 1), null, null, RedondeoDeMontos.Centavo, ResiduoDeRedondeo.MayorValor), []);

        r.Descuentos.Should().BeEmpty();
        r.Descartadas.Should().BeEmpty();
    }

    [Fact]
    public void El_residuo_del_reparto_queda_visible_en_la_linea_que_lo_recibe()
    {
        var promocion = new PromocionVigente(1, "3X2", "Lleve 3 pague 2", PromotionKind.BuyNPayM, null, null, 3m, 2m, null, false,
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), true, [new AmbitoDePromocion(PromotionScopeKind.Product, ProductId: 10)], []);
        var lineas = Enumerable.Range(1, 3).Select(i => new LineaDePromocion(i, 10, null, [1], 1m, 10000m)).ToList();

        var r = MotorDePromociones.Aplicar(lineas,
            new ContextoDePromocion(new DateOnly(2026, 10, 15), null, null, RedondeoDeMontos.Centavo, ResiduoDeRedondeo.MayorValor), [promocion]);

        r.Descuentos.Select(d => d.Amount).Should().Equal([3333.34m, 3333.33m, 3333.33m], "con MayorValor en empate, la primera");
        r.Descuentos.Select(d => d.Residuo).Should().Equal(0.01m, 0m, 0m);
    }

    private static PromocionVigente Promocion(PromocionJson p) => new(
        p.Id, p.Codigo, p.Nombre ?? p.Codigo, Enum.Parse<PromotionKind>(p.Clase), p.Tasa, p.Valor, p.Lleva, p.Paga, p.Paquete, p.Acumulable,
        DateOnly.Parse(p.Desde), DateOnly.Parse(p.Hasta), p.Activa,
        p.Ambitos.Select(a => new AmbitoDePromocion(Enum.Parse<PromotionScopeKind>(a.Clase), a.Producto, a.Categoria, a.Segmento, a.Canal, a.Cantidad)).ToList(),
        (p.Tramos ?? []).Select(t => new TramoDePromocion(t.Desde, t.Precio)).ToList());

    private sealed class Caso
    {
        public string Nombre { get; set; } = string.Empty;
        public string Montos { get; set; } = "Centavo";
        public string Residuo { get; set; } = "MayorValor";
        public List<PromocionJson> Promociones { get; set; } = [];
        public List<ConsultaJson> Consultas { get; set; } = [];
    }

    private sealed class PromocionJson
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string? Nombre { get; set; }
        public string Clase { get; set; } = string.Empty;
        public decimal? Tasa { get; set; }
        public decimal? Valor { get; set; }
        public decimal? Lleva { get; set; }
        public decimal? Paga { get; set; }
        public decimal? Paquete { get; set; }
        public bool Acumulable { get; set; }
        public string Desde { get; set; } = string.Empty;
        public string Hasta { get; set; } = string.Empty;
        public bool Activa { get; set; }
        public List<AmbitoJson> Ambitos { get; set; } = [];
        public List<TramoJson>? Tramos { get; set; }
    }

    private sealed class AmbitoJson
    {
        public string Clase { get; set; } = string.Empty;
        public int? Producto { get; set; }
        public int? Categoria { get; set; }
        public string? Segmento { get; set; }
        public int? Canal { get; set; }
        public decimal? Cantidad { get; set; }
    }

    private sealed class TramoJson
    {
        public decimal Desde { get; set; }
        public decimal Precio { get; set; }
    }

    private sealed class ConsultaJson
    {
        public string Fecha { get; set; } = string.Empty;
        public int? Canal { get; set; }
        public string? Segmento { get; set; }
        public List<LineaJson> Lineas { get; set; } = [];
        public List<EsperadoJson> Esperado { get; set; } = [];
        public List<int>? Descartadas { get; set; }
    }

    private sealed class LineaJson
    {
        public int Linea { get; set; }
        public int Producto { get; set; }
        public int? Padre { get; set; }
        public List<int> Categorias { get; set; } = [];
        public decimal Cantidad { get; set; }
        public decimal Precio { get; set; }
    }

    private sealed class EsperadoJson
    {
        public int Linea { get; set; }
        public int Promocion { get; set; }
        public decimal Valor { get; set; }
    }
}
