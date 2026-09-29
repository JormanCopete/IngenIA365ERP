using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Inventory.Costing;

/// <summary>
/// Una línea de recepción sobre la que se reparte un costo adicional: su Id y su producto, la cantidad recibida en unidad base,
/// su valor neto (el costo con que entró), el peso y el volumen unitarios del producto (por unidad base), lo digitado a mano y la
/// existencia actual del producto en su ámbito de costo (para la regla D5). (nuevo)
/// </summary>
public sealed record LineaAProrratear(
    int ReceiptLineId,
    int ProductId,
    decimal Cantidad,
    decimal Valor,
    decimal? PesoUnitario,
    decimal? VolumenUnitario,
    decimal? Manual,
    decimal Existencia);

/// <summary>Lo que se reparte: el monto (el <c>Subtotal</c> del <c>LandedCost</c>), el método, las líneas y el redondeo vigente. (nuevo)</summary>
public sealed record PedidoDeProrrateo(
    decimal Monto,
    LandedCostAllocationMethod Metodo,
    IReadOnlyList<LineaAProrratear> Lineas,
    RedondeoDeMontos Montos,
    ResiduoDeRedondeo Residuo);

/// <summary>
/// El reparto de una línea (una fila de <c>INV_LandedCostAllocations</c>, data-model §9.7): la base según el método, lo asignado,
/// el residuo que recibió (0 salvo en una), la proporción en existencia y las porciones a inventario y a costo de venta. (nuevo)
/// </summary>
public sealed record RepartoDeLinea(
    int ReceiptLineId,
    int ProductId,
    decimal Basis,
    decimal AllocatedAmount,
    decimal RoundingResidue,
    decimal ExistingRatio,
    decimal ExistingAmount,
    decimal SoldAmount);

/// <summary>
/// Por qué no se puede repartir: <c>Inventory.LandedCost.BasisMissing</c> con los productos sin base, o
/// <c>Inventory.LandedCost.ManualNotBalanced</c> con el monto y lo digitado (api.md §14.9). (nuevo)
/// </summary>
public sealed record RechazoDeProrrateo(string Codigo, string Mensaje, IReadOnlyList<int> Productos, decimal? Monto = null, decimal? Repartido = null);

/// <summary>Las líneas repartidas, el residuo total visible y la explicación, o el rechazo. (nuevo)</summary>
public sealed record ResultadoDeProrrateo(
    IReadOnlyList<RepartoDeLinea> Lineas,
    decimal RoundingResidue,
    ExplicacionDeCosto Explicacion,
    RechazoDeProrrateo? Rechazo = null)
{
    public bool Admitido => Rechazo is null;
}

/// <summary>
/// El reparto de costos adicionales —flete, seguro— entre lo recibido (feature 012, US13, T780; FR-046, US13-3; data-model §9.7;
/// decisión D5, a validar por la contadora). Puro, sin IO ni valores legales.
///
/// <para>
/// La base de cada línea es su valor neto (<see cref="LandedCostAllocationMethod.Value"/>), su cantidad en unidad base
/// (<see cref="LandedCostAllocationMethod.Quantity"/>), cantidad × peso o × volumen unitario del producto
/// (<see cref="LandedCostAllocationMethod.Weight"/>, <see cref="LandedCostAllocationMethod.Volume"/>; un producto sin peso o
/// volumen rechaza con <see cref="CodigoBaseFaltante"/> y la lista) o lo digitado (<see cref="LandedCostAllocationMethod.Manual"/>,
/// que tiene que sumar el monto exacto: <see cref="CodigoManualNoCuadra"/>). El monto se reparte en proporción a la base con
/// <see cref="Redondeo.Repartir"/>: Σ <c>AllocatedAmount</c> es el monto exacto y el residuo del redondeo va a la línea de mayor
/// valor o a la última según <c>Redondeo.Residuo</c>, visible en su <c>RoundingResidue</c>.
/// </para>
///
/// <para>
/// Cada porción se parte entre lo que sigue en existencia y lo ya vendido o consumido (D5):
/// <c>ExistingRatio = mín(1, existencia actual / cantidad recibida)</c> a seis decimales, <c>ExistingAmount = round(asignado ×
/// ExistingRatio)</c> y <c>SoldAmount = asignado − ExistingAmount</c>. Con PEPS la porción en existencia será la capa restante
/// de la recepción (T843, fase 17).
/// </para>
///
/// <para>
/// Al kardex entra por <see cref="MotorDeCosteo.CostoAdicional"/>: dos líneas <c>CostAdjustment</c> <c>LandedCost</c> sobre la
/// entrada de la recepción, en el molde de <see cref="DiferenciaDePrecio"/> —lo asignado completo (lo que costó la entrada) y,
/// si algo ya salió, lo vendido con signo contrario (lo que pasó a costo de venta)—; Σ = lo que queda en el ámbito, que mueve el
/// promedio.
/// </para>
/// </summary>
public static class Prorrateo
{
    public const string CodigoBaseFaltante = "Inventory.LandedCost.BasisMissing";
    public const string CodigoManualNoCuadra = "Inventory.LandedCost.ManualNotBalanced";

    /// <summary>Decimales de <c>ExistingRatio</c> (<c>PrecisionDeInventario.Tarifa</c>, 9,6).</summary>
    public const int DecimalesDeLaProporcion = 6;

    public static ResultadoDeProrrateo Repartir(PedidoDeProrrateo pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        if (pedido.Lineas is null || pedido.Lineas.Count == 0) throw new ArgumentException("Un reparto necesita al menos una línea recibida.", nameof(pedido));
        if (pedido.Monto < 0m) throw new ArgumentException("El monto a repartir no es negativo.", nameof(pedido));
        if (pedido.Lineas.Any(l => l.Cantidad <= 0m)) throw new ArgumentException("Cada línea recibida tiene cantidad positiva.", nameof(pedido));

        var monto = Redondeo.Monto(pedido.Monto, pedido.Montos);
        var explicacion = new ExplicacionDeCosto { Resumen = $"Costos adicionales repartidos por {Metodo(pedido.Metodo)} (FR-046)." }
            .Paso("Monto a repartir", monto)
            .Paso("Líneas recibidas", pedido.Lineas.Count);

        // ---- base ----
        var bases = new decimal[pedido.Lineas.Count];
        var sinBase = new List<int>();
        for (var i = 0; i < pedido.Lineas.Count; i++)
        {
            var l = pedido.Lineas[i];
            decimal? baseDeLinea = pedido.Metodo switch
            {
                LandedCostAllocationMethod.Value => l.Valor,
                LandedCostAllocationMethod.Quantity => l.Cantidad,
                LandedCostAllocationMethod.Weight => l.PesoUnitario is > 0m and var p ? l.Cantidad * p : null,
                LandedCostAllocationMethod.Volume => l.VolumenUnitario is > 0m and var v ? l.Cantidad * v : null,
                LandedCostAllocationMethod.Manual => Redondeo.Monto(l.Manual ?? 0m, pedido.Montos),
                _ => throw new ArgumentOutOfRangeException(nameof(pedido), pedido.Metodo, "Método de reparto desconocido."),
            };
            if (baseDeLinea is null) sinBase.Add(l.ProductId);
            bases[i] = baseDeLinea ?? 0m;
        }

        if (sinBase.Count > 0)
            return Rechazo(explicacion, new RechazoDeProrrateo(CodigoBaseFaltante,
                $"Sin {(pedido.Metodo == LandedCostAllocationMethod.Weight ? "peso" : "volumen")} en {sinBase.Distinct().Count()} producto(s): no se puede repartir por {Metodo(pedido.Metodo)}.",
                sinBase.Distinct().ToList()));

        var totalBase = bases.Sum();
        if (pedido.Metodo == LandedCostAllocationMethod.Manual)
        {
            if (totalBase != monto)
                return Rechazo(explicacion.Paso("Digitado", totalBase), new RechazoDeProrrateo(CodigoManualNoCuadra,
                    "Lo digitado a mano no suma el monto a repartir.", [], monto, totalBase));
        }
        else if (totalBase <= 0m)
        {
            return Rechazo(explicacion, new RechazoDeProrrateo(CodigoBaseFaltante,
                $"La base del reparto por {Metodo(pedido.Metodo)} suma cero.", pedido.Lineas.Select(l => l.ProductId).Distinct().ToList()));
        }

        explicacion.Paso("Base total", totalBase);

        // ---- reparto con residuo ----
        var sinRedondear = bases.Select(b => pedido.Metodo == LandedCostAllocationMethod.Manual ? b : monto * b / totalBase).ToList();
        var asignados = Redondeo.Repartir(monto, sinRedondear, pedido.Montos, pedido.Residuo);

        var lineas = new List<RepartoDeLinea>(pedido.Lineas.Count);
        for (var i = 0; i < pedido.Lineas.Count; i++)
        {
            var l = pedido.Lineas[i];
            var asignado = asignados[i];
            var residuo = asignado - Redondeo.Monto(sinRedondear[i], pedido.Montos);
            var proporcion = Math.Round(Math.Clamp(l.Existencia / l.Cantidad, 0m, 1m), DecimalesDeLaProporcion, MidpointRounding.AwayFromZero);
            var enExistencia = Redondeo.Monto(asignado * proporcion, pedido.Montos);
            lineas.Add(new RepartoDeLinea(l.ReceiptLineId, l.ProductId, bases[i], asignado, residuo, proporcion, enExistencia, asignado - enExistencia));
        }

        var residuoTotal = lineas.Sum(l => l.RoundingResidue);
        explicacion.Paso("Residuo del redondeo", residuoTotal)
            .Nota("Residuo", pedido.Residuo == ResiduoDeRedondeo.UltimaLinea ? "A la última línea." : "A la línea de mayor valor.")
            .Paso("En existencia", lineas.Sum(l => l.ExistingAmount))
            .Paso("Vendida o consumida", lineas.Sum(l => l.SoldAmount))
            .Nota("Regla D5", "Proporción en existencia = mín(1, existencia actual / cantidad recibida); lo demás va a costo de venta.");

        return new ResultadoDeProrrateo(lineas, residuoTotal, explicacion);
    }

    /// <summary>
    /// Las líneas de kardex de la porción de una recepción (T780): <c>CostAdjustment</c> <c>LandedCost</c> con
    /// <c>AffectsEntryId</c> de su entrada, partidas en existencia y vendido. La cantidad del ámbito no cambia; su valor sube lo que
    /// quedó en existencia.
    /// </summary>
    /// <param name="metodo">
    /// Con PEPS (I5, T843; D5) la porción en existencia —que la aplicación calculó con lo que queda de la capa de esa entrada— se suma
    /// al costo de esa capa (<see cref="Peps.AjusteSobreEntrada"/>) con <paramref name="montos"/>.
    /// </param>
    public static ResultadoDeCosteo AlKardex(EstadoDeCosto estado, ReferenciaDeKardex entrada, RepartoDeLinea reparto,
        CostMethod metodo = CostMethod.WeightedAverage, RedondeoDeMontos montos = RedondeoDeMontos.Centavo)
    {
        if (metodo == CostMethod.Fifo)
        {
            ArgumentNullException.ThrowIfNull(reparto);
            if (reparto.ExistingAmount + reparto.SoldAmount != reparto.AllocatedAmount)
                throw new ArgumentException("La porción en existencia más la vendida es lo asignado.", nameof(reparto));
            var explicacionPeps = new ExplicacionDeCosto { Resumen = "Costos adicionales sobre la entrada de la recepción (FR-046), con PEPS." }
                .Paso("Asignado a la línea", reparto.AllocatedAmount)
                .Paso("Proporción en existencia (capa restante / original)", reparto.ExistingRatio);
            return Peps.AjusteSobreEntrada(estado, entrada, KardexReason.LandedCost, reparto.AllocatedAmount, reparto.ExistingAmount, montos, explicacionPeps);
        }

        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(reparto);
        if (reparto.ExistingAmount + reparto.SoldAmount != reparto.AllocatedAmount)
            throw new ArgumentException("La porción en existencia más la vendida es lo asignado.", nameof(reparto));

        var explicacion = new ExplicacionDeCosto { Resumen = "Costos adicionales sobre la entrada de la recepción (FR-046)." }
            .Paso("Asignado a la línea", reparto.AllocatedAmount)
            .Paso("Proporción en existencia", reparto.ExistingRatio)
            .Paso("En existencia", reparto.ExistingAmount)
            .Paso("Vendida o consumida", reparto.SoldAmount);
        if (reparto.AllocatedAmount == 0m) return new ResultadoDeCosteo([], estado, explicacion.Nota("Regla", "Nada que repartir: no hay ajuste."));

        var lineas = new List<LineaDeKardexPropuesta>
        {
            new()
            {
                Kind = KardexEntryKind.CostAdjustment,
                Reason = KardexReason.LandedCost,
                QuantityBase = 0m,
                UnitCost = 0m,
                TotalCost = reparto.AllocatedAmount,
                AffectsEntry = entrada,
                Porcion = PorcionDelAjuste.EnExistencia,
            },
        };
        if (reparto.SoldAmount != 0m)
        {
            lineas.Add(new LineaDeKardexPropuesta
            {
                Kind = KardexEntryKind.CostAdjustment,
                Reason = KardexReason.LandedCost,
                QuantityBase = 0m,
                UnitCost = 0m,
                TotalCost = -reparto.SoldAmount,
                AffectsEntry = entrada,
                Porcion = PorcionDelAjuste.Vendida,
            });
        }
        explicacion.Nota("Regla", "Lo que sigue en existencia mueve el promedio; lo que ya salió va al costo de lo vendido.");

        var nuevo = EstadoDeCosto.Con(estado.Quantity, estado.Value + reparto.ExistingAmount, estado.LastUnitCost, estado.SalidasEnNegativo);
        return new ResultadoDeCosteo(lineas, nuevo, explicacion);
    }

    /// <summary>Lo que quedó en existencia (Σ de las líneas: lo asignado menos lo vendido).</summary>
    public static decimal EnExistencia(ResultadoDeCosteo resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return resultado.Valor;
    }

    /// <summary>Lo que pasó a costo de venta (positivo si el costo adicional es positivo).</summary>
    public static decimal Vendida(ResultadoDeCosteo resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return -resultado.Lineas.Where(l => l.Porcion == PorcionDelAjuste.Vendida).Sum(l => l.TotalCost);
    }

    private static ResultadoDeProrrateo Rechazo(ExplicacionDeCosto explicacion, RechazoDeProrrateo rechazo) =>
        new([], 0m, explicacion.Nota("Rechazo", rechazo.Mensaje), rechazo);

    private static string Metodo(LandedCostAllocationMethod metodo) => metodo switch
    {
        LandedCostAllocationMethod.Value => "valor",
        LandedCostAllocationMethod.Quantity => "cantidad",
        LandedCostAllocationMethod.Weight => "peso",
        LandedCostAllocationMethod.Volume => "volumen",
        LandedCostAllocationMethod.Manual => "digitación a mano",
        _ => metodo.ToString(),
    };
}
