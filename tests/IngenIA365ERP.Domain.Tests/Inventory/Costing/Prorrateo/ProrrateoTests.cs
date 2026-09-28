using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;

namespace IngenIA365ERP.Domain.Tests.Inventory.Costing;

/// <summary>
/// Feature 012, US13, T767 (FR-046, US13-3; data-model §9.7; decisión D5): el reparto puro de costos adicionales
/// (<see cref="Prorrateo"/>) coincide con los casos hechos a mano de esta carpeta —por valor, cantidad,
/// peso, volumen o a mano; el residuo del redondeo a la línea de mayor valor o a la última según <c>Redondeo.Residuo</c>, con
/// Σ <c>AllocatedAmount</c> = monto exacto y el residuo visible en <c>RoundingResidue</c>; la separación existencia/vendido con
/// <c>ExistingRatio = mín(1, existencia actual / cantidad recibida)</c>— y sus rechazos (<c>BasisMissing</c>,
/// <c>ManualNotBalanced</c>). Además, su paso al kardex por <see cref="MotorDeCosteo.CostoAdicional"/>.
/// </summary>
public class ProrrateoTests
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Inventory", "Costing", "Prorrateo");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_de_US13_3()
    {
        var nombres = Directory.EnumerateFiles(Directorio, "*.json").Select(Path.GetFileName).ToList();
        foreach (var caso in new[]
                 {
                     "US13-3-por-valor.json", "por-cantidad-residuo-al-mayor.json", "por-cantidad-residuo-a-la-ultima.json", "por-peso.json",
                     "por-volumen.json", "por-peso-producto-sin-peso.json", "manual-no-cuadra.json", "manual-cuadra.json",
                     "existencia-y-vendido.json",
                 })
            nombres.Should().Contain(caso, $"falta el caso {caso} (T767)");
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_reparto_coincide_con_el_calculo_manual(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var resultado = Prorrateo.Repartir(new PedidoDeProrrateo(
            caso.Monto, caso.Metodo,
            caso.Lineas.Select(l => new LineaAProrratear(l.Linea, l.Producto, l.Cantidad, l.Valor, l.Peso, l.Volumen, l.Manual, l.Existencia)).ToList(),
            caso.Montos, caso.Residuo));

        using var _ = new AssertionScope($"{archivo} — {caso.Nombre}");
        var e = caso.Esperado;
        resultado.Explicacion.Pasos.Should().NotBeEmpty("todo reparto lleva explicación");

        if (e.Rechazo is { } codigo)
        {
            resultado.Admitido.Should().BeFalse("el reparto debía rechazarse");
            resultado.Rechazo!.Codigo.Should().Be(codigo);
            if (e.Productos is { Count: > 0 }) resultado.Rechazo.Productos.Should().Equal(e.Productos, "los productos sin base");
            if (e.MontoDelRechazo is { } monto) resultado.Rechazo.Monto.Should().Be(monto);
            if (e.RepartidoDelRechazo is { } repartido) resultado.Rechazo.Repartido.Should().Be(repartido);
            resultado.Lineas.Should().BeEmpty("un reparto rechazado no reparte nada");
            return;
        }

        resultado.Admitido.Should().BeTrue($"el reparto no debía rechazarse: {resultado.Rechazo?.Mensaje}");
        resultado.Lineas.Select(l => l.ReceiptLineId).Should().Equal(e.Lineas.Select(l => l.Linea), "una por línea de recepción, en su orden");
        foreach (var esperada in e.Lineas)
        {
            var r = resultado.Lineas.Single(l => l.ReceiptLineId == esperada.Linea);
            r.Basis.Should().Be(esperada.Basis, $"Basis de la línea {esperada.Linea}");
            r.AllocatedAmount.Should().Be(esperada.Allocated, $"AllocatedAmount de la línea {esperada.Linea}");
            r.RoundingResidue.Should().Be(esperada.Residue, $"RoundingResidue de la línea {esperada.Linea}");
            r.ExistingRatio.Should().Be(esperada.ExistingRatio, $"ExistingRatio de la línea {esperada.Linea}");
            r.ExistingAmount.Should().Be(esperada.Existing, $"ExistingAmount de la línea {esperada.Linea}");
            r.SoldAmount.Should().Be(esperada.Sold, $"SoldAmount de la línea {esperada.Linea}");
            (r.ExistingAmount + r.SoldAmount).Should().Be(r.AllocatedAmount, "ExistingAmount + SoldAmount = AllocatedAmount");
        }

        resultado.Lineas.Sum(l => l.AllocatedAmount).Should().Be(caso.Monto, "Σ AllocatedAmount = el monto, exacto");
        resultado.RoundingResidue.Should().Be(e.RoundingResidue, "el residuo queda visible");
    }

    [Fact]
    public void El_costo_adicional_entra_al_kardex_partido_en_existencia_y_vendido()
    {
        // Recepción de 10 a $100 y venta de 4: quedan 6 por $600. Flete de $1.000: 60 % a inventario, 40 % a costo de venta.
        var estado = EstadoDeCosto.Con(6m, 600m, 100m);
        var reparto = Prorrateo.Repartir(new PedidoDeProrrateo(1000m, LandedCostAllocationMethod.Value,
            [new LineaAProrratear(1, 10, 10m, 1000m, null, null, null, 6m)], RedondeoDeMontos.Centavo, ResiduoDeRedondeo.MayorValor));

        var r = MotorDeCosteo.CostoAdicional(estado, ReferenciaDeKardex.A(77), reparto.Lineas.Single());

        r.Lineas.Should().HaveCount(2);
        r.Lineas.Should().OnlyContain(l => l.Kind == KardexEntryKind.CostAdjustment && l.Reason == KardexReason.LandedCost
                                           && l.QuantityBase == 0m && l.AffectsEntry!.Id == 77);
        r.Lineas[0].TotalCost.Should().Be(1000m);
        r.Lineas[0].Porcion.Should().Be(PorcionDelAjuste.EnExistencia);
        r.Lineas[1].TotalCost.Should().Be(-400m);
        r.Lineas[1].Porcion.Should().Be(PorcionDelAjuste.Vendida);
        Prorrateo.EnExistencia(r).Should().Be(600m);
        Prorrateo.Vendida(r).Should().Be(400m);
        r.Estado.Quantity.Should().Be(6m);
        r.Estado.Value.Should().Be(1200m);
        r.Estado.AverageCost.Should().Be(200m);
    }

    [Fact]
    public void Todo_vendido_no_mueve_el_promedio()
    {
        var estado = EstadoDeCosto.Con(0m, 0m, 100m);
        var linea = new RepartoDeLinea(1, 10, 1000m, 500m, 0m, 0m, 0m, 500m);

        var r = MotorDeCosteo.CostoAdicional(estado, ReferenciaDeKardex.A(5), linea);

        r.Valor.Should().Be(0m);
        r.Estado.Value.Should().Be(0m);
        r.Estado.LastUnitCost.Should().Be(100m);
    }

    [Fact]
    public void Un_monto_negativo_o_sin_lineas_es_un_error_de_programa()
    {
        var sinLineas = () => Prorrateo.Repartir(new PedidoDeProrrateo(100m, LandedCostAllocationMethod.Value, [],
            RedondeoDeMontos.Centavo, ResiduoDeRedondeo.MayorValor));
        sinLineas.Should().Throw<ArgumentException>();

        var negativo = () => Prorrateo.Repartir(new PedidoDeProrrateo(-1m, LandedCostAllocationMethod.Value,
            [new LineaAProrratear(1, 10, 1m, 1m, null, null, null, 1m)], RedondeoDeMontos.Centavo, ResiduoDeRedondeo.MayorValor));
        negativo.Should().Throw<ArgumentException>();
    }

    // ------------------------------------------------------------------------------------ forma del caso --

    private sealed class Caso
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Monto { get; set; }
        public LandedCostAllocationMethod Metodo { get; set; }
        public RedondeoDeMontos Montos { get; set; } = RedondeoDeMontos.Centavo;
        public ResiduoDeRedondeo Residuo { get; set; } = ResiduoDeRedondeo.MayorValor;
        public List<LineaJson> Lineas { get; set; } = [];
        public EsperadoJson Esperado { get; set; } = new();
    }

    private sealed class LineaJson
    {
        public int Linea { get; set; }
        public int Producto { get; set; }
        public decimal Cantidad { get; set; }
        public decimal Valor { get; set; }
        public decimal? Peso { get; set; }
        public decimal? Volumen { get; set; }
        public decimal? Manual { get; set; }
        public decimal Existencia { get; set; }
    }

    private sealed class EsperadoJson
    {
        public string? Rechazo { get; set; }
        public List<int>? Productos { get; set; }
        public decimal? MontoDelRechazo { get; set; }
        public decimal? RepartidoDelRechazo { get; set; }
        public decimal RoundingResidue { get; set; }
        public List<LineaEsperadaJson> Lineas { get; set; } = [];
    }

    private sealed class LineaEsperadaJson
    {
        public int Linea { get; set; }
        public decimal Basis { get; set; }
        public decimal Allocated { get; set; }
        public decimal Residue { get; set; }
        public decimal ExistingRatio { get; set; }
        public decimal Existing { get; set; }
        public decimal Sold { get; set; }
    }
}
