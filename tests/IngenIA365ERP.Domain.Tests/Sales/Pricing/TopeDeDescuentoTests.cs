using FluentAssertions;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Sales.Pricing;

namespace IngenIA365ERP.Domain.Tests.Sales.Pricing;

/// <summary>
/// Feature 012, I3, T550 (FR-054, FR-017, T51; data-model §14 <c>INV_DocumentLineDiscounts</c> e <c>INV_DiscountCaps</c>): el tope
/// efectivo de un usuario es el mayor de los topes vigentes de sus roles activos (sin fila, 0); cambiar a mano el precio de lista
/// es un descuento <c>IsPriceOverride</c> de (lista − precio) × cantidad; por encima del tope de línea o de documento el
/// descuento no se rechaza: pide aprobación; el descuento por total se prorratea a las líneas con el residuo según
/// <c>Redondeo.Residuo</c> y la suma exacta.
/// </summary>
public class TopeDeDescuentoTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 1);

    [Fact]
    public void El_tope_efectivo_es_el_mayor_de_los_roles_vigentes_y_sin_fila_es_cero()
    {
        var topes = new[]
        {
            new TopeDeRol(1, 0.05m, 0.03m, new DateOnly(2026, 1, 1), null),
            new TopeDeRol(2, 0.10m, 0.02m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
            new TopeDeRol(3, 0.50m, 0.50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 30)), // vencido
            new TopeDeRol(4, 0.40m, 0.40m, new DateOnly(2026, 10, 2), null), // todavía no rige
        };

        var tope = TopeDeDescuento.Efectivo(topes, Hoy);

        tope.MaxLineRate.Should().Be(0.10m);
        tope.MaxDocumentRate.Should().Be(0.03m, "cada tope se toma por separado: el de línea de un rol y el de documento de otro");
        tope.FromRoles.Should().BeEquivalentTo([1, 2]);

        var sinFila = TopeDeDescuento.Efectivo([], Hoy);
        sinFila.MaxLineRate.Should().Be(0m);
        sinFila.MaxDocumentRate.Should().Be(0m);
        sinFila.FromRoles.Should().BeEmpty();
    }

    [Fact]
    public void Cambiar_el_precio_de_lista_es_un_descuento_por_la_diferencia_por_la_cantidad()
    {
        var tope = new TopeDelUsuario(0.05m, 0.05m, [1]);

        var d = TopeDeDescuento.PorPrecioDigitado(listPrice: 10_000m, precioDigitado: 9_700m, cantidad: 3m, tope, RedondeoDeMontos.Peso)!;

        d.IsPriceOverride.Should().BeTrue();
        d.Amount.Should().Be(900m, "(10.000 − 9.700) × 3");
        d.Rate.Should().Be(0.03m, "900 / 30.000");
        d.CapRateApplied.Should().Be(0.05m);
        d.RequiresApproval.Should().BeFalse();

        var porEncima = TopeDeDescuento.PorPrecioDigitado(10_000m, 8_800m, 1m, tope, RedondeoDeMontos.Peso)!;
        porEncima.Amount.Should().Be(1_200m);
        porEncima.Rate.Should().Be(0.12m);
        porEncima.RequiresApproval.Should().BeTrue("12 % supera el tope de línea de 5 %");

        TopeDeDescuento.PorPrecioDigitado(10_000m, 10_000m, 1m, tope, RedondeoDeMontos.Peso).Should().BeNull("sin rebaja no hay descuento");
        TopeDeDescuento.PorPrecioDigitado(10_000m, 10_500m, 1m, tope, RedondeoDeMontos.Peso).Should().BeNull("subir el precio no es descuento");
    }

    [Theory]
    [InlineData(0.05, false)]
    [InlineData(0.0501, true)]
    [InlineData(0.12, true)]
    public void Un_porcentaje_sobre_el_tope_de_linea_pide_aprobacion(double tasa, bool pide)
    {
        var tope = new TopeDelUsuario(0.05m, 0.05m, [1]);
        var d = TopeDeDescuento.PorPorcentaje(bruto: 50_000m, tasa: (decimal)tasa, tope, RedondeoDeMontos.Centavo);

        d.IsPriceOverride.Should().BeFalse();
        d.Amount.Should().Be(Math.Round(50_000m * (decimal)tasa, 2, MidpointRounding.AwayFromZero));
        d.RequiresApproval.Should().Be(pide);
    }

    [Fact]
    public void Un_valor_se_mide_contra_el_bruto_y_sin_tope_todo_descuento_pide_aprobacion()
    {
        var d = TopeDeDescuento.PorValor(bruto: 40_000m, valor: 2_000m, TopeDelUsuario.Ninguno);
        d.Rate.Should().Be(0.05m);
        d.CapRateApplied.Should().Be(0m);
        d.RequiresApproval.Should().BeTrue("sin fila de tope, el tope es 0 (T51)");
    }

    [Fact]
    public void Varios_descuentos_de_una_linea_se_suman_contra_el_tope_de_linea()
    {
        var tope = new TopeDelUsuario(0.10m, 0.05m, [1]);
        TopeDeDescuento.SuperaTopeDeLinea(bruto: 10_000m, [500m, 400m], tope).Should().BeFalse("9 % ≤ 10 %");
        TopeDeDescuento.SuperaTopeDeLinea(bruto: 10_000m, [500m, 600m], tope).Should().BeTrue("11 % > 10 %");
        TopeDeDescuento.SuperaTopeDeLinea(bruto: 0m, [], tope).Should().BeFalse();
    }

    [Fact]
    public void El_descuento_por_total_se_prorratea_con_suma_exacta_y_residuo_visible()
    {
        var tope = new TopeDelUsuario(0.10m, 0.05m, [1]);
        decimal[] netos = [10_000m, 10_000m, 10_000m];

        var mayor = TopeDeDescuento.PorTotal(1_000m, netos, tope, RedondeoDeMontos.Peso, ResiduoDeRedondeo.MayorValor);
        mayor.Parts.Should().Equal(334m, 333m, 333m);
        mayor.Parts.Sum().Should().Be(1_000m);
        mayor.Rate.Should().Be(0.033333m);
        mayor.RequiresApproval.Should().BeFalse();

        var ultima = TopeDeDescuento.PorTotal(1_000m, netos, tope, RedondeoDeMontos.Peso, ResiduoDeRedondeo.UltimaLinea);
        ultima.Parts.Should().Equal(333m, 333m, 334m);

        var desigual = TopeDeDescuento.PorTotal(100m, [30_000m, 10_000m], tope, RedondeoDeMontos.Centavo, ResiduoDeRedondeo.MayorValor);
        desigual.Parts.Should().Equal(75m, 25m);
    }

    [Fact]
    public void El_descuento_por_total_sobre_el_tope_de_documento_pide_aprobacion()
    {
        var tope = new TopeDelUsuario(0.50m, 0.05m, [1]);
        var d = TopeDeDescuento.PorTotal(3_000m, [20_000m, 20_000m], tope, RedondeoDeMontos.Peso, ResiduoDeRedondeo.MayorValor);
        d.Rate.Should().Be(0.075m);
        d.CapRateApplied.Should().Be(0.05m);
        d.RequiresApproval.Should().BeTrue("7,5 % supera el tope por total de 5 % aunque el de línea sea 50 %");
    }
}
