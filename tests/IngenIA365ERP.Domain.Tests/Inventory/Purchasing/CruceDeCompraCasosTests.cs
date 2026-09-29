using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Purchasing;

namespace IngenIA365ERP.Domain.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, US13, T766 (FR-049, FR-050, SC-007; data-model §9.6): el motor puro <see cref="CruceDeCompra"/> coincide con los
/// casos calculados a mano de <c>Casos/</c> —legibles por la contadora, molde de <c>Payroll/Calculation/CasoDorado</c>—: por línea
/// de factura, la diferencia de cantidad contra lo recibido no facturado, la diferencia de precio contra la orden (o contra la
/// recepción sin orden), si excede la tolerancia vigente según <c>Compras.ReglaDeTolerancia</c>, sus razones y el
/// <c>ToleranceJson</c> con los valores usados; y por recepción contra orden, la regla de lo recibido de más (T782).
/// </summary>
public class CruceDeCompraCasosTests
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Inventory", "Purchasing", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_de_US13()
    {
        var nombres = Directory.EnumerateFiles(Directorio, "*.json").Select(Path.GetFileName).ToList();
        foreach (var caso in new[]
                 {
                     "US13-1-factura-sobre-lo-recibido.json", "US13-2-precio-sobre-tolerancia.json", "US13-regla-ambas-condiciones.json",
                     "US13-regla-cualquiera-de-las.json", "US13-dos-vias-sin-orden.json", "US13-recibido-de-mas.json",
                 })
            nombres.Should().Contain(caso, $"falta el caso {caso} (T766)");
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_cruce_coincide_con_el_calculo_manual(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var tolerancias = caso.Tolerancias.Motor();
        (caso.Lineas.Count + caso.Recepciones.Count).Should().BePositive($"{archivo} no tiene nada que cruzar");

        foreach (var l in caso.Lineas)
        {
            using var _ = new AssertionScope($"{archivo} — {caso.Nombre} — línea {l.Id}");
            var r = CruceDeCompra.Cruzar(
                new LineaAlCruce(l.Ordenado, l.RecibidoNoFacturado, l.Facturado, l.PrecioOrdenado, l.CostoRecibido, l.PrecioFacturado),
                tolerancias, RedondeoDeMontos.Centavo);
            var e = l.Esperado;

            if (e.ConOrden is { } conOrden) r.ConOrden.Should().Be(conOrden, "con orden o dos vías");
            r.QuantityDifference.Should().Be(e.QuantityDifference, "QuantityDifference");
            r.PriceDifferenceAmount.Should().Be(e.PriceDifferenceAmount, "PriceDifferenceAmount");
            r.PriceDifferenceRate.Should().Be(e.PriceDifferenceRate, "PriceDifferenceRate");
            r.ExceedsTolerance.Should().Be(e.ExceedsTolerance, $"ExceedsTolerance. Explicación: {r.Explicacion.Texto()}");
            r.Razones.Should().Equal(e.Razones, "las razones de la retención, en orden");
            r.Reasons.Should().Be(e.Reasons, "la columna Reasons");
            r.Status.Should().Be(e.Status, "Held si excede; nulo si no");
            r.Aprobable.Should().Be(e.Aprobable, "facturar más de lo recibido no se aprueba (T796)");
            r.FacturaSobreLoRecibido.Should().Be(e.FacturaSobreLoRecibido, "facturado > recibido no facturado");
            if (e.ToleranciaDePrecio is { } tp) r.ToleranciaDePrecio.Should().Be(tp, "tolerancia de precio por unidad según la regla");
            if (e.ToleranceJson is { } json) r.ToleranceJson.Should().Be(json, "los valores de tolerancia que se usaron (FR-050, SC-007)");
            r.ToleranceJson.Length.Should().BeLessThanOrEqualTo(CruceDeCompra.LargoMaximoDeToleranceJson);

            r.Explicacion.Pasos.Should().NotBeEmpty("todo cruce lleva explicación");
            var texto = r.Explicacion.Texto();
            foreach (var f in e.Explicacion) texto.Should().Contain(f, "la explicación debe decirlo");
        }

        foreach (var rec in caso.Recepciones)
        {
            using var _ = new AssertionScope($"{archivo} — {caso.Nombre} — recepción {rec.Id}");
            var t = rec.SinTolerancia
                ? ToleranciasDelCruce.Ninguna
                : rec.Regla is { } regla ? tolerancias with { Regla = regla } : tolerancias;
            var r = CruceDeCompra.Recepcion(new PedidoDeRecepcionContraOrden(rec.Ordenado, rec.YaRecibido, rec.EstaRecepcion, t));
            var e = rec.Esperado;

            r.Admitida.Should().Be(e.Admitida, $"admitida. Explicación: {r.Explicacion.Texto()}");
            r.Recibido.Should().Be(e.Recibido, "Σ recibido por la línea de orden con esta recepción");
            r.Excedente.Should().Be(e.Excedente, "lo recibido de más");
            r.Tolerancia.Should().Be(e.Tolerancia, "la tolerancia de cantidad según la regla");
            r.Pendiente.Should().Be(e.Pendiente, "lo pendiente de recibir antes de esta recepción");
            if (e.Rechazo is { } codigo) r.Codigo.Should().Be(codigo);
            else r.Codigo.Should().BeNull();
        }
    }

    // ------------------------------------------------------------------------------------ forma del caso --

    private sealed class Caso
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public ToleranciasJson Tolerancias { get; set; } = new();
        public List<RecepcionJson> Recepciones { get; set; } = [];
        public List<LineaJson> Lineas { get; set; } = [];
    }

    private sealed class ToleranciasJson
    {
        public decimal CantidadPorcentaje { get; set; }
        public decimal CantidadValor { get; set; }
        public decimal PrecioPorcentaje { get; set; }
        public decimal PrecioValor { get; set; }
        public ReglaDeTolerancia Regla { get; set; } = ReglaDeTolerancia.AmbasCondiciones;

        public ToleranciasDelCruce Motor() => new(CantidadPorcentaje, CantidadValor, PrecioPorcentaje, PrecioValor, Regla);
    }

    private sealed class RecepcionJson
    {
        public string Id { get; set; } = string.Empty;
        public ReglaDeTolerancia? Regla { get; set; }
        public bool SinTolerancia { get; set; }
        public decimal Ordenado { get; set; }
        public decimal YaRecibido { get; set; }
        public decimal EstaRecepcion { get; set; }
        public RecepcionEsperadaJson Esperado { get; set; } = new();
    }

    private sealed class RecepcionEsperadaJson
    {
        public bool Admitida { get; set; }
        public decimal Recibido { get; set; }
        public decimal Excedente { get; set; }
        public decimal Tolerancia { get; set; }
        public decimal Pendiente { get; set; }
        public string? Rechazo { get; set; }
    }

    private sealed class LineaJson
    {
        public string Id { get; set; } = string.Empty;
        public decimal? Ordenado { get; set; }
        public decimal RecibidoNoFacturado { get; set; }
        public decimal Facturado { get; set; }
        public decimal? PrecioOrdenado { get; set; }
        public decimal? CostoRecibido { get; set; }
        public decimal PrecioFacturado { get; set; }
        public EsperadoJson Esperado { get; set; } = new();
    }

    private sealed class EsperadoJson
    {
        public bool? ConOrden { get; set; }
        public decimal QuantityDifference { get; set; }
        public decimal PriceDifferenceAmount { get; set; }
        public decimal PriceDifferenceRate { get; set; }
        public bool ExceedsTolerance { get; set; }
        public List<string> Razones { get; set; } = [];
        public string? Reasons { get; set; }
        public PurchaseMatchStatus? Status { get; set; }
        public bool Aprobable { get; set; }
        public bool FacturaSobreLoRecibido { get; set; }
        public decimal? ToleranciaDePrecio { get; set; }
        public string? ToleranceJson { get; set; }
        public List<string> Explicacion { get; set; } = [];
    }
}
