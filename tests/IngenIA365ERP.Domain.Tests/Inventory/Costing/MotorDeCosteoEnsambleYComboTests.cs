using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;

namespace IngenIA365ERP.Domain.Tests.Inventory.Costing;

/// <summary>
/// Feature 012, I6, T904/T917 (FR-017, US15-2, US15-3): las reglas sueltas del ensamble y del combo que los casos dorados 18 y 19 no
/// alcanzan —el residuo del costo del kit, el PEPS, la validación de lo que entra—.
/// </summary>
public class MotorDeCosteoEnsambleYComboTests
{
    private static readonly ParametrosDeCosteo Promedio = new();

    [Fact]
    public void Si_la_cantidad_por_el_costo_unitario_no_da_lo_consumido_el_residuo_va_en_su_propia_linea_sobre_la_entrada_del_kit()
    {
        // 10.000,00 consumidos para 30.000 kits: 0,333333 × 30.000 = 9.999,99 → falta un centavo.
        var r = MotorDeCosteo.EntradaDeEnsamble(EstadoDeCosto.Vacio, 30000m, 10000m, Promedio, new DateOnly(2026, 9, 1));

        r.Lineas.Should().HaveCount(2);
        r.Lineas[0].Should().Match<LineaDeKardexPropuesta>(l =>
            l.Kind == KardexEntryKind.Entry && l.UnitCost == 0.333333m && l.TotalCost == 9999.99m);
        r.Lineas[1].Should().Match<LineaDeKardexPropuesta>(l =>
            l.Kind == KardexEntryKind.CostAdjustment && l.Reason == KardexReason.RoundingResidue && l.QuantityBase == 0m
            && l.TotalCost == 0.01m && l.AffectsEntry!.Linea == r.Lineas[0]);
        r.Valor.Should().Be(10000m, "el kit entra exactamente por lo consumido");
        r.Estado.Value.Should().Be(10000m);
        r.Explicacion.Texto().Should().Contain("Residuo de redondeo del ensamble (RoundingResidue): 0.01");
    }

    [Fact]
    public void Con_PEPS_el_kit_abre_su_capa_al_costo_del_ensamble()
    {
        var r = MotorDeCosteo.EntradaDeEnsamble(EstadoDeCosto.Vacio, 5m, 25000m, new ParametrosDeCosteo(CostMethod.Fifo), new DateOnly(2026, 9, 1));

        r.Estado.Capas.Should().ContainSingle().Which.Should().Match<CapaDeCosto>(c => c.OriginalQuantity == 5m && c.UnitCost == 5000m);
        r.Estado.Value.Should().Be(25000m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void El_kit_entra_con_cantidad_positiva(int cantidad)
    {
        var act = () => MotorDeCosteo.EntradaDeEnsamble(EstadoDeCosto.Vacio, cantidad, 100m, Promedio, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Lo_consumido_no_es_negativo()
    {
        var act = () => MotorDeCosteo.EntradaDeEnsamble(EstadoDeCosto.Vacio, 1m, -1m, Promedio, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Un_ensamble_sale_de_sus_componentes_y_no_admite_entradas_de_componentes()
    {
        var entrada = new MovimientoDeComponente<string>("A", EstadoDeCosto.Vacio, new MovimientoDeCosto(1m, ValoracionDelMovimiento.AlCostoIndicado, 1m));
        var act = () => MotorDeCosteo.Ensamblar([entrada], EstadoDeCosto.Vacio, 1m, Promedio, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Un_combo_o_un_ensamble_sin_componentes_es_un_error_del_llamador()
    {
        var combo = () => MotorDeCosteo.MoverCombo<string>([], Promedio);
        combo.Should().Throw<ArgumentException>();
        var ensamble = () => MotorDeCosteo.Ensamblar<string>([], EstadoDeCosto.Vacio, 1m, Promedio, null);
        ensamble.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Un_combo_con_componentes_en_distinto_sentido_es_un_error_del_llamador()
    {
        var estado = EstadoDeCosto.Con(10m, 1000m, 100m);
        var act = () => MotorDeCosteo.MoverCombo(
        [
            new MovimientoDeComponente<string>("A", estado, new MovimientoDeCosto(-1m, ValoracionDelMovimiento.AlCostoVigente)),
            new MovimientoDeComponente<string>("B", estado, new MovimientoDeCosto(1m, ValoracionDelMovimiento.AlCostoIndicado, 1m)),
        ], Promedio);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Con_el_negativo_permitido_el_combo_sale_aunque_un_componente_no_alcance()
    {
        var a = EstadoDeCosto.Con(10m, 1000m, 100m);
        var b = EstadoDeCosto.Con(1m, 300m, 300m);
        var r = MotorDeCosteo.MoverCombo(
        [
            new MovimientoDeComponente<string>("A", a, new MovimientoDeCosto(-2m, ValoracionDelMovimiento.AlCostoVigente)),
            new MovimientoDeComponente<string>("B", b, new MovimientoDeCosto(-2m, ValoracionDelMovimiento.AlCostoVigente)),
        ], Promedio with { NegativoPermitido = true });

        r.Admitido.Should().BeTrue();
        r.Costo.Should().Be(800m, "2 A × 100 + 2 B × 300 (el que queda en negativo sale al último costo)");
        r.Componentes.Single(c => c.Componente == "B").Resultado.Estado.Quantity.Should().Be(-1m);
    }
}
