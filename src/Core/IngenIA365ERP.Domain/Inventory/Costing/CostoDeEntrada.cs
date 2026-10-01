using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Taxes;

namespace IngenIA365ERP.Domain.Inventory.Costing;

/// <summary>
/// Un impuesto de la línea de compra, ya calculado por <c>MotorTributario</c>: clase y valor. Si es descontable o va al
/// costo lo decide <see cref="IvaDescontable"/>. (nuevo)
/// </summary>
public sealed record ImpuestoDeCompra(TaxKind Clase, decimal Valor, string? Codigo = null);

/// <summary>
/// La línea de compra que se valora (FR-044): cantidad en unidad base, valor bruto (cantidad × precio), descuentos no
/// condicionados (los condicionados no bajan el costo), impuestos y las tres respuestas del orden de FR-044: la
/// cooperativa es responsable de IVA a la fecha, el tipo de compra marca el IVA no descontable y cómo se vende el
/// producto. (nuevo)
/// </summary>
public sealed record PedidoDeCostoDeCompra(
    decimal QuantityBase,
    decimal ValorBruto,
    decimal DescuentosNoCondicionados,
    IReadOnlyList<ImpuestoDeCompra> Impuestos,
    bool CooperativaResponsableIva,
    bool TipoIvaNoDescontable,
    VatSaleTreatment TratamientoDeVenta);

/// <summary>
/// El costo de una entrada: costo unitario (6 decimales), total de la línea, lo que los impuestos sumaron al costo y la
/// explicación. <see cref="Movimiento"/> es el movimiento listo para <see cref="MotorDeCosteo"/>. (nuevo)
/// </summary>
public sealed record CostoDeEntradaCalculado(
    decimal CostoUnitario,
    decimal CostoTotal,
    decimal ImpuestosAlCosto,
    ValoracionDelMovimiento Valoracion,
    ExplicacionDeCosto Explicacion)
{
    public MovimientoDeCosto Movimiento(decimal cantidadBase, ReferenciaDeKardex? origen = null, bool esAnulacion = false) =>
        new(cantidadBase, Valoracion, Valoracion == ValoracionDelMovimiento.AlCostoVigente ? null : CostoUnitario, origen, esAnulacion);
}

/// <summary>
/// Las reglas de costo de las entradas y devoluciones (feature 012, T281; FR-044; research R10, R21; NIC 2 párr. 11):
/// <list type="bullet">
/// <item><b>compras</b>: al costo neto de descuentos no condicionados, más los impuestos que van al costo —el IVA según
/// <see cref="IvaDescontable.Determinar"/> (responsable de IVA → tipo de compra → tratamiento del producto), el INC
/// siempre—. Los costos adicionales prorrateados son de I5;</item>
/// <item><b>devolución de cliente</b>: al costo con que salió;</item>
/// <item><b>devolución a proveedor</b>: al costo con que entró (la diferencia contra el promedio la pone el motor);</item>
/// <item><b>ajuste positivo</b>: al costo vigente, o al indicado (con <c>Inventory.Adjustments.SetUnitCost</c>);</item>
/// <item><b>saldo inicial</b>: al costo cargado.</item>
/// </list>
/// </summary>
public static class CostoDeEntrada
{
    public static CostoDeEntradaCalculado DeCompra(PedidoDeCostoDeCompra pedido, RedondeoDeMontos montos)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        if (pedido.QuantityBase <= 0m) throw new ArgumentException("La cantidad de una compra es positiva.", nameof(pedido));
        if (pedido.DescuentosNoCondicionados < 0m || pedido.DescuentosNoCondicionados > pedido.ValorBruto)
            throw new ArgumentException("Los descuentos no pueden ser negativos ni mayores que el valor bruto.", nameof(pedido));

        var explicacion = new ExplicacionDeCosto { Resumen = "Costo de entrada de una compra (FR-044)." }
            .Paso("Valor bruto", pedido.ValorBruto)
            .Paso("Descuentos no condicionados", pedido.DescuentosNoCondicionados);

        var alCosto = 0m;
        foreach (var impuesto in pedido.Impuestos)
        {
            var tratamiento = IvaDescontable.Determinar(impuesto.Clase, pedido.CooperativaResponsableIva, pedido.TipoIvaNoDescontable, pedido.TratamientoDeVenta);
            var razon = IvaDescontable.Explicar(impuesto.Clase, pedido.CooperativaResponsableIva, pedido.TipoIvaNoDescontable, pedido.TratamientoDeVenta);
            var nombre = impuesto.Codigo ?? impuesto.Clase.ToString();
            if (tratamiento == TaxTreatment.AddedToCost)
            {
                alCosto += impuesto.Valor;
                explicacion.Paso($"{nombre} al costo", impuesto.Valor).Nota(nombre, razon);
            }
            else
            {
                explicacion.Paso($"{nombre} descontable (fuera del costo)", impuesto.Valor).Nota(nombre, razon);
            }
        }

        var total = Redondeo.Monto(pedido.ValorBruto - pedido.DescuentosNoCondicionados + alCosto, montos);
        var unitario = Redondeo.CostoUnitario(total / pedido.QuantityBase);
        explicacion.Paso("Costo neto de la línea", total).Paso("Cantidad en unidad base", pedido.QuantityBase).Paso("Costo unitario", unitario);
        return new CostoDeEntradaCalculado(unitario, Redondeo.Monto(pedido.QuantityBase * unitario, montos), alCosto,
            ValoracionDelMovimiento.AlCostoIndicado, explicacion);
    }

    /// <summary>Devolución de cliente: entra al costo con que salió la línea de venta enlazada.</summary>
    public static CostoDeEntradaCalculado DeDevolucionDeCliente(decimal costoConQueSalio) =>
        AlCosto(costoConQueSalio, ValoracionDelMovimiento.AlCostoDeOrigen, "Devolución de cliente: entra al costo con que salió.");

    /// <summary>Devolución a proveedor: sale al costo con que entró la recepción enlazada; el motor pone la diferencia.</summary>
    public static CostoDeEntradaCalculado DeDevolucionAProveedor(decimal costoConQueEntro) =>
        AlCosto(costoConQueEntro, ValoracionDelMovimiento.DevolucionDeEntrada,
            "Devolución a proveedor: sale al costo con que entró; la diferencia con el promedio es ajuste de costo.");

    /// <summary>Ajuste positivo: al costo indicado si lo trae (con permiso), si no al costo vigente del ámbito.</summary>
    public static CostoDeEntradaCalculado DeAjustePositivo(EstadoDeCosto estado, decimal? costoIndicado)
    {
        ArgumentNullException.ThrowIfNull(estado);
        return costoIndicado is { } indicado
            ? AlCosto(indicado, ValoracionDelMovimiento.AlCostoIndicado, "Ajuste positivo al costo indicado.")
            : AlCosto(estado.CostoVigente, ValoracionDelMovimiento.AlCostoVigente, "Ajuste positivo al costo vigente del ámbito.");
    }

    /// <summary>Saldo inicial: entra al costo cargado.</summary>
    public static CostoDeEntradaCalculado DeSaldoInicial(decimal costoCargado) =>
        AlCosto(costoCargado, ValoracionDelMovimiento.AlCostoIndicado, "Saldo inicial al costo cargado.");

    private static CostoDeEntradaCalculado AlCosto(decimal costo, ValoracionDelMovimiento valoracion, string regla)
    {
        if (costo < 0m) throw new ArgumentOutOfRangeException(nameof(costo), costo, "El costo unitario no puede ser negativo.");
        var unitario = Redondeo.CostoUnitario(costo);
        var explicacion = new ExplicacionDeCosto { Resumen = regla }.Paso("Costo unitario", unitario);
        return new CostoDeEntradaCalculado(unitario, 0m, 0m, valoracion, explicacion);
    }
}
