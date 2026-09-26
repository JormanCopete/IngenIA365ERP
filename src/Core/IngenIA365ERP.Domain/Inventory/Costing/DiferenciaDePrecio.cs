using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Inventory.Costing;

/// <summary>
/// Lo que pide una factura (o una nota con <c>affectsCost</c>) del proveedor cuyo precio difiere del de la recepción
/// (FR-050, E6): la fila del kardex de la entrada que corrige, la cantidad facturada en unidad base y la diferencia de
/// costo total (positiva si la factura cuesta más). (nuevo)
/// </summary>
public sealed record PedidoDeDiferenciaDePrecio(ReferenciaDeKardex Entrada, decimal CantidadFacturada, decimal Diferencia);

/// <summary>
/// La diferencia de precio entre la factura del proveedor y su recepción (feature 012, T341; data-model §9.2; FR-050; E6):
/// en I1 no se retiene, se reconoce. Se reparte entre lo que sigue en existencia en el ámbito y lo que ya salió
/// (vendido o consumido), en proporción a la existencia del ámbito frente a la cantidad facturada:
/// <c>enExistencia = round(diferencia × min(1, existencia / facturada))</c>, <c>vendida = diferencia − enExistencia</c>.
/// Deja dos líneas <c>CostAdjustment</c> <c>PriceDifference</c> sobre la entrada: la diferencia completa (la entrada costó
/// eso) y, si algo ya salió, lo vendido con signo contrario (el costo que pasó a lo vendido); Σ = lo que queda en el ámbito,
/// que mueve el promedio. La cantidad no cambia. Puro. (nuevo)
/// </summary>
public static class DiferenciaDePrecio
{
    public static ResultadoDeCosteo Aplicar(EstadoDeCosto estado, PedidoDeDiferenciaDePrecio pedido, RedondeoDeMontos montos)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(pedido);
        if (pedido.CantidadFacturada <= 0m) throw new ArgumentException("La cantidad facturada es positiva.", nameof(pedido));

        var explicacion = new ExplicacionDeCosto { Resumen = "Diferencia de precio entre la factura del proveedor y la recepción (E6)." }
            .Paso("Diferencia total", pedido.Diferencia)
            .Paso("Cantidad facturada", pedido.CantidadFacturada)
            .Paso("Existencia del ámbito", estado.Quantity);
        if (pedido.Diferencia == 0m) return new ResultadoDeCosteo([], estado, explicacion.Nota("Regla", "Sin diferencia: no hay ajuste."));

        var proporcion = estado.Quantity <= 0m ? 0m : Math.Min(1m, estado.Quantity / pedido.CantidadFacturada);
        var enExistencia = Redondeo.Monto(pedido.Diferencia * proporcion, montos);
        var vendida = pedido.Diferencia - enExistencia;
        explicacion.Paso("En existencia", enExistencia).Paso("Vendida o consumida", vendida)
            .Nota("Regla", "Lo que sigue en existencia mueve el promedio; lo que ya salió va al costo de lo vendido.");

        var lineas = new List<LineaDeKardexPropuesta>
        {
            new()
            {
                Kind = KardexEntryKind.CostAdjustment,
                Reason = KardexReason.PriceDifference,
                QuantityBase = 0m,
                UnitCost = 0m,
                TotalCost = pedido.Diferencia,
                AffectsEntry = pedido.Entrada,
                Porcion = PorcionDelAjuste.EnExistencia,
            },
        };
        if (vendida != 0m)
        {
            lineas.Add(new LineaDeKardexPropuesta
            {
                Kind = KardexEntryKind.CostAdjustment,
                Reason = KardexReason.PriceDifference,
                QuantityBase = 0m,
                UnitCost = 0m,
                TotalCost = -vendida,
                AffectsEntry = pedido.Entrada,
                Porcion = PorcionDelAjuste.Vendida,
            });
        }

        var nuevo = EstadoDeCosto.Con(estado.Quantity, estado.Value + enExistencia, estado.LastUnitCost, estado.SalidasEnNegativo);
        return new ResultadoDeCosteo(lineas, nuevo, explicacion);
    }

    /// <summary>Lo que quedó en existencia (Σ de las líneas: la completa menos lo vendido).</summary>
    public static decimal EnExistencia(ResultadoDeCosteo resultado) => resultado.Valor;

    /// <summary>Lo que pasó a lo vendido (positivo si la factura costó más).</summary>
    public static decimal Vendida(ResultadoDeCosteo resultado) =>
        -resultado.Lineas.Where(l => l.Porcion == PorcionDelAjuste.Vendida).Sum(l => l.TotalCost);
}
