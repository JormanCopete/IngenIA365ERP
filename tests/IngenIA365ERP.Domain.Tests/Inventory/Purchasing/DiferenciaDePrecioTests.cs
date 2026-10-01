using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;

namespace IngenIA365ERP.Domain.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, T341 (FR-050, E6; data-model §9.2): la diferencia de precio de la factura del proveedor frente a su recepción
/// no se retiene: se reparte entre lo que sigue en existencia (mueve el promedio) y lo que ya salió (costo de lo vendido).
/// </summary>
public class DiferenciaDePrecioTests
{
    private static readonly ReferenciaDeKardex Entrada = ReferenciaDeKardex.A(42);

    [Fact]
    public void Todo_en_existencia_mueve_el_promedio()
    {
        // 36 unidades a 1.300; la factura cuesta 36 × 1.350: 1.800 más.
        var estado = EstadoDeCosto.Con(36m, 46_800m, 1_300m);
        var r = MotorDeCosteo.DiferenciaDePrecio(estado, new PedidoDeDiferenciaDePrecio(Entrada, 36m, 1_800m), RedondeoDeMontos.Centavo);

        r.Lineas.Should().ContainSingle();
        r.Lineas[0].Reason.Should().Be(KardexReason.PriceDifference);
        r.Lineas[0].Kind.Should().Be(KardexEntryKind.CostAdjustment);
        r.Lineas[0].TotalCost.Should().Be(1_800m);
        r.Lineas[0].AffectsEntry!.EntryId.Should().Be(42);
        DiferenciaDePrecio.EnExistencia(r).Should().Be(1_800m);
        DiferenciaDePrecio.Vendida(r).Should().Be(0m);
        r.Estado.Quantity.Should().Be(36m);
        r.Estado.Value.Should().Be(48_600m);
        r.Estado.AverageCost.Should().Be(1_350m);
    }

    [Fact]
    public void Con_parte_vendida_se_parte_en_existencia_y_vendido()
    {
        // Quedan 12 de las 36 facturadas: un tercio sigue en existencia.
        var estado = EstadoDeCosto.Con(12m, 15_600m, 1_300m);
        var r = MotorDeCosteo.DiferenciaDePrecio(estado, new PedidoDeDiferenciaDePrecio(Entrada, 36m, 1_800m), RedondeoDeMontos.Centavo);

        DiferenciaDePrecio.EnExistencia(r).Should().Be(600m);
        DiferenciaDePrecio.Vendida(r).Should().Be(1_200m);
        r.Lineas.Should().HaveCount(2);
        r.Lineas.Sum(l => l.TotalCost).Should().Be(600m, "Σ del kardex = lo que cambió el valor del ámbito");
        r.Estado.Value.Should().Be(16_200m);
        r.Estado.AverageCost.Should().Be(1_350m);
    }

    [Fact]
    public void Sin_existencia_todo_va_a_lo_vendido_y_una_rebaja_resta()
    {
        var r = MotorDeCosteo.DiferenciaDePrecio(EstadoDeCosto.Con(0m, 0m, 1_300m), new PedidoDeDiferenciaDePrecio(Entrada, 36m, -360m), RedondeoDeMontos.Centavo);
        DiferenciaDePrecio.EnExistencia(r).Should().Be(0m);
        DiferenciaDePrecio.Vendida(r).Should().Be(-360m);
        r.Estado.Value.Should().Be(0m);
        r.Estado.LastUnitCost.Should().Be(1_300m);
    }

    [Fact]
    public void Sin_diferencia_no_hay_lineas()
    {
        var estado = EstadoDeCosto.Con(36m, 46_800m, 1_300m);
        var r = MotorDeCosteo.DiferenciaDePrecio(estado, new PedidoDeDiferenciaDePrecio(Entrada, 36m, 0m), RedondeoDeMontos.Centavo);
        r.Lineas.Should().BeEmpty();
        r.Estado.Should().Be(estado);
    }
}
