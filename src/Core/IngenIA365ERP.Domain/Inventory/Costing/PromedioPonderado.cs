using System.Globalization;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Inventory.Costing;

/// <summary>
/// El promedio ponderado (feature 012, T280; FR-042; research R10). Reglas:
/// <list type="bullet">
/// <item>la entrada suma su costo y mueve el promedio (<c>Value / Quantity</c> a 6 decimales) y el último costo;</item>
/// <item>la salida sale al promedio; con existencia cero, al último costo, que se conserva;</item>
/// <item>con el negativo permitido, la parte que deja el ámbito bajo cero sale al último costo en su propia línea, y la
/// entrada que la cubre agrega la diferencia como <c>NegativeRegularization</c> sobre esa salida;</item>
/// <item>la devolución a proveedor y la anulación de una entrada salen al costo con que entró y la diferencia contra el
/// promedio vigente es <c>VoidDifference</c> sobre esa entrada: el promedio no cambia;</item>
/// <item>lo que viaja al costo de la línea de origen (devolución de cliente, anulación de una salida, tránsito) entra o
/// sale a ese costo, sin diferencia;</item>
/// <item>cuando la cantidad llega a cero el valor queda en cero: el residuo va en una línea <c>RoundingResidue</c>.</item>
/// </list>
/// Todo monto se redondea con <see cref="Redondeo.Monto"/>; en <c>Entry</c>/<c>Exit</c>,
/// <c>TotalCost = round(QuantityBase × UnitCost)</c>.
/// </summary>
public static class PromedioPonderado
{
    public static ResultadoDeCosteo Aplicar(EstadoDeCosto estado, MovimientoDeCosto movimiento, ParametrosDeCosteo parametros) =>
        movimiento.EsEntrada ? Entrada(estado, movimiento, parametros) : Salida(estado, movimiento, parametros);

    private static ResultadoDeCosteo Entrada(EstadoDeCosto estado, MovimientoDeCosto mov, ParametrosDeCosteo p)
    {
        var cantidad = mov.QuantityBase;
        var costo = mov.Valoracion == ValoracionDelMovimiento.AlCostoVigente
            ? estado.CostoVigente
            : CostoQueTrae(mov);
        costo = Redondeo.CostoUnitario(costo);

        var explicacion = Inicio(estado, "Entrada", mov)
            .Paso("Costo unitario de la entrada", costo);
        explicacion.Nota("Regla", mov.Valoracion switch
        {
            ValoracionDelMovimiento.AlCostoVigente => estado.Quantity > 0m
                ? "Entra al costo vigente: el promedio del ámbito."
                : "Entra al costo vigente: sin existencia, el último costo.",
            ValoracionDelMovimiento.AlCostoDeOrigen => "Entra al costo de la línea de origen.",
            _ => "Entra al costo indicado.",
        });

        var linea = new LineaDeKardexPropuesta
        {
            Kind = KardexEntryKind.Entry,
            Reason = KardexReason.Normal,
            QuantityBase = cantidad,
            UnitCost = costo,
            TotalCost = Redondeo.Monto(cantidad * costo, p.Montos),
            ReversesEntry = mov.EsAnulacion ? mov.Origen : null,
        };
        var lineas = new List<LineaDeKardexPropuesta> { linea };
        var valor = estado.Value + linea.TotalCost;
        var pendientes = estado.SalidasEnNegativo.ToList();

        if (estado.Quantity < 0m)
            valor += Regularizar(estado, cantidad, costo, pendientes, lineas, explicacion, p);

        var nuevaCantidad = estado.Quantity + cantidad;
        valor += Residuo(nuevaCantidad, valor, linea, lineas, explicacion);

        var nuevo = EstadoDeCosto.Con(nuevaCantidad, valor, costo, pendientes);
        return Fin(nuevo, lineas, explicacion);
    }

    private static ResultadoDeCosteo Salida(EstadoDeCosto estado, MovimientoDeCosto mov, ParametrosDeCosteo p)
    {
        var cantidad = -mov.QuantityBase;
        if (estado.Quantity < cantidad && !p.NegativoPermitido)
        {
            var disponible = Math.Max(estado.Quantity, 0m);
            return new ResultadoDeCosteo([], estado, Inicio(estado, "Salida rechazada", mov),
                new RechazoDeCosteo(MotorDeCosteo.CodigoExistenciaInsuficiente,
                    $"La existencia del ámbito no alcanza: hay {F(disponible)} y la salida pide {F(cantidad)}.",
                    disponible, cantidad));
        }

        return mov.Valoracion switch
        {
            ValoracionDelMovimiento.AlCostoVigente => SalidaAlPromedio(estado, mov, cantidad, p),
            ValoracionDelMovimiento.DevolucionDeEntrada => SalidaDeUnaEntrada(estado, mov, cantidad, p),
            _ => SalidaAlCostoDeOrigen(estado, mov, cantidad, p),
        };
    }

    private static ResultadoDeCosteo SalidaAlPromedio(EstadoDeCosto estado, MovimientoDeCosto mov, decimal cantidad, ParametrosDeCosteo p)
    {
        var explicacion = Inicio(estado, "Salida al costo vigente", mov);
        var lineas = new List<LineaDeKardexPropuesta>();
        var pendientes = estado.SalidasEnNegativo.ToList();
        var valor = estado.Value;
        var existencia = estado.Quantity;

        if (existencia >= cantidad)
        {
            var costo = estado.CostoVigente;
            var linea = Salir(cantidad, costo, p, mov);
            lineas.Add(linea);
            valor += linea.TotalCost;
            existencia -= cantidad;
            explicacion.Paso("Costo unitario de la salida", costo)
                .Nota("Regla", estado.Quantity > 0m ? "Sale al promedio del ámbito." : "Sin existencia, sale al último costo.");
            valor += Residuo(existencia, valor, linea, lineas, explicacion);
            return Fin(EstadoDeCosto.Con(existencia, valor, estado.LastUnitCost, pendientes), lineas, explicacion);
        }

        var enNegativo = cantidad;
        if (existencia > 0m)
        {
            var linea = Salir(existencia, estado.AverageCost, p, mov);
            lineas.Add(linea);
            valor += linea.TotalCost;
            explicacion.Paso("Sale al promedio lo que había", existencia).Paso("Promedio", estado.AverageCost);
            enNegativo = cantidad - existencia;
            existencia = 0m;
            valor += Residuo(existencia, valor, linea, lineas, explicacion);
        }

        var negativa = Salir(enNegativo, estado.LastUnitCost, p, mov);
        lineas.Add(negativa);
        valor += negativa.TotalCost;
        existencia -= enNegativo;
        pendientes.Add(new SalidaEnNegativo(ReferenciaDeKardex.A(negativa), enNegativo, estado.LastUnitCost));
        explicacion.Paso("Sale en negativo al último costo", enNegativo)
            .Paso("Último costo", estado.LastUnitCost)
            .Nota("Regla", "Con el negativo permitido, lo que deja el ámbito bajo cero sale al último costo y se regulariza cuando llegue la entrada.");

        return Fin(EstadoDeCosto.Con(existencia, valor, estado.LastUnitCost, pendientes), lineas, explicacion);
    }

    private static ResultadoDeCosteo SalidaDeUnaEntrada(EstadoDeCosto estado, MovimientoDeCosto mov, decimal cantidad, ParametrosDeCosteo p)
    {
        var costoDeEntrada = Redondeo.CostoUnitario(CostoQueTrae(mov));
        var vigente = estado.CostoVigente;
        var explicacion = Inicio(estado, mov.EsAnulacion ? "Anulación de una entrada" : "Devolución a proveedor", mov)
            .Paso("Costo con que entró", costoDeEntrada)
            .Paso("Costo vigente", vigente);

        var linea = Salir(cantidad, costoDeEntrada, p, mov);
        var lineas = new List<LineaDeKardexPropuesta> { linea };
        var diferencia = Redondeo.Monto(cantidad * costoDeEntrada, p.Montos) - Redondeo.Monto(cantidad * vigente, p.Montos);
        if (diferencia != 0m)
        {
            lineas.Add(Ajuste(KardexReason.VoidDifference, vigente, diferencia, mov.Origen));
            explicacion.Paso("Diferencia contra el promedio (VoidDifference)", diferencia)
                .Nota("Regla", "Sale al costo con que entró; la diferencia contra el promedio vigente es ajuste de costo y el promedio no cambia.");
        }
        else
        {
            explicacion.Nota("Regla", "Sale al costo con que entró, que es el promedio vigente: no hay diferencia.");
        }

        var valor = estado.Value + linea.TotalCost + diferencia;
        var existencia = estado.Quantity - cantidad;
        var pendientes = estado.SalidasEnNegativo.ToList();
        if (existencia < 0m)
            pendientes.Add(new SalidaEnNegativo(ReferenciaDeKardex.A(linea), Math.Min(cantidad, -existencia), vigente));
        valor += Residuo(existencia, valor, linea, lineas, explicacion);

        return Fin(EstadoDeCosto.Con(existencia, valor, estado.LastUnitCost, pendientes), lineas, explicacion);
    }

    private static ResultadoDeCosteo SalidaAlCostoDeOrigen(EstadoDeCosto estado, MovimientoDeCosto mov, decimal cantidad, ParametrosDeCosteo p)
    {
        var costo = Redondeo.CostoUnitario(CostoQueTrae(mov));
        var explicacion = Inicio(estado, "Salida al costo de la línea de origen", mov)
            .Paso("Costo unitario de la salida", costo)
            .Nota("Regla", "Sale al costo de su línea de origen (el tránsito, al de la línea de despacho), no al promedio.");

        var linea = Salir(cantidad, costo, p, mov);
        var lineas = new List<LineaDeKardexPropuesta> { linea };
        var valor = estado.Value + linea.TotalCost;
        var existencia = estado.Quantity - cantidad;
        var pendientes = estado.SalidasEnNegativo.ToList();
        if (existencia < 0m)
            pendientes.Add(new SalidaEnNegativo(ReferenciaDeKardex.A(linea), Math.Min(cantidad, -existencia), costo));
        valor += Residuo(existencia, valor, linea, lineas, explicacion);

        return Fin(EstadoDeCosto.Con(existencia, valor, estado.LastUnitCost, pendientes), lineas, explicacion);
    }

    /// <summary>La entrada que cubre un negativo: la diferencia entre el costo con que salió y el de la entrada.</summary>
    private static decimal Regularizar(
        EstadoDeCosto estado, decimal cantidad, decimal costo, List<SalidaEnNegativo> pendientes,
        List<LineaDeKardexPropuesta> lineas, ExplicacionDeCosto explicacion, ParametrosDeCosteo p)
    {
        var restante = Math.Min(cantidad, -estado.Quantity);
        var total = 0m;

        while (restante > 0m && pendientes.Count > 0)
        {
            var pendiente = pendientes[0];
            var tomada = Math.Min(restante, pendiente.Cantidad);
            total += RegularizarUna(tomada, pendiente.CostoUnitario, costo, pendiente.Salida, lineas, p);
            if (tomada == pendiente.Cantidad) pendientes.RemoveAt(0);
            else pendientes[0] = pendiente with { Cantidad = pendiente.Cantidad - tomada };
            restante -= tomada;
        }

        if (restante > 0m)
        {
            var costoDelNegativo = Redondeo.CostoUnitario(estado.Value / estado.Quantity);
            total += RegularizarUna(restante, costoDelNegativo, costo, null, lineas, p);
        }

        if (total != 0m)
            explicacion.Paso("Regularización del negativo (NegativeRegularization)", total)
                .Nota("Negativo", "La entrada cubre lo que salió en negativo al último costo; la diferencia va al costo de lo que salió.");
        return total;
    }

    private static decimal RegularizarUna(
        decimal cantidad, decimal costoConQueSalio, decimal costoDeLaEntrada, ReferenciaDeKardex? salida,
        List<LineaDeKardexPropuesta> lineas, ParametrosDeCosteo p)
    {
        var diferencia = Redondeo.Monto(cantidad * (costoConQueSalio - costoDeLaEntrada), p.Montos);
        if (diferencia != 0m)
            lineas.Add(Ajuste(KardexReason.NegativeRegularization, costoDeLaEntrada, diferencia, salida));
        return diferencia;
    }

    /// <summary>Con la cantidad en cero el valor queda en cero: el residuo va en su propia línea, visible.</summary>
    private static decimal Residuo(
        decimal cantidad, decimal valor, LineaDeKardexPropuesta causa, List<LineaDeKardexPropuesta> lineas, ExplicacionDeCosto explicacion)
    {
        if (cantidad != 0m || valor == 0m) return 0m;
        lineas.Add(Ajuste(KardexReason.RoundingResidue, causa.UnitCost, -valor, ReferenciaDeKardex.A(causa)));
        explicacion.Paso("Residuo de redondeo (RoundingResidue)", -valor)
            .Nota("Residuo", "La cantidad llegó a cero: el valor queda en cero.");
        return -valor;
    }

    private static LineaDeKardexPropuesta Salir(decimal cantidad, decimal costo, ParametrosDeCosteo p, MovimientoDeCosto mov) => new()
    {
        Kind = KardexEntryKind.Exit,
        Reason = KardexReason.Normal,
        QuantityBase = -cantidad,
        UnitCost = costo,
        TotalCost = -Redondeo.Monto(cantidad * costo, p.Montos),
        ReversesEntry = mov.EsAnulacion ? mov.Origen : null,
    };

    private static LineaDeKardexPropuesta Ajuste(KardexReason motivo, decimal costo, decimal diferencia, ReferenciaDeKardex? afecta) => new()
    {
        Kind = KardexEntryKind.CostAdjustment,
        Reason = motivo,
        QuantityBase = 0m,
        UnitCost = costo,
        TotalCost = diferencia,
        AffectsEntry = afecta,
    };

    private static decimal CostoQueTrae(MovimientoDeCosto mov)
    {
        var costo = mov.CostoUnitario
            ?? throw new ArgumentException($"Un movimiento valorado {mov.Valoracion} trae su costo unitario.", nameof(mov));
        if (costo < 0m) throw new ArgumentException("El costo unitario no puede ser negativo.", nameof(mov));
        return costo;
    }

    private static ExplicacionDeCosto Inicio(EstadoDeCosto estado, string que, MovimientoDeCosto mov) =>
        new ExplicacionDeCosto { Resumen = que }
            .Paso("Cantidad del movimiento", mov.QuantityBase)
            .Paso("Existencia anterior", estado.Quantity)
            .Paso("Valor anterior", estado.Value)
            .Paso("Promedio anterior", estado.AverageCost)
            .Paso("Último costo anterior", estado.LastUnitCost);

    private static ResultadoDeCosteo Fin(EstadoDeCosto nuevo, List<LineaDeKardexPropuesta> lineas, ExplicacionDeCosto explicacion)
    {
        explicacion.Paso("Existencia nueva", nuevo.Quantity)
            .Paso("Valor nuevo", nuevo.Value)
            .Paso("Promedio nuevo", nuevo.AverageCost);
        return new ResultadoDeCosteo(lineas, nuevo, explicacion);
    }

    private static string F(decimal valor) => valor.ToString("0.####", CultureInfo.InvariantCulture);
}
