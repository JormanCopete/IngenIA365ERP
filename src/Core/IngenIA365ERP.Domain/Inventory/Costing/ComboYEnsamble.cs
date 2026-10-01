using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Inventory.Costing;

/// <summary>
/// El movimiento de un componente de un combo o de un kit (feature 012, I6, T917): el componente (su clave, para nombrarlo si no
/// alcanza), el estado de costo de su ámbito y su movimiento ya en unidad base —cantidad por unidad × unidades—. (nuevo)
/// </summary>
public sealed record MovimientoDeComponente<TClave>(TClave Componente, EstadoDeCosto Estado, MovimientoDeCosto Movimiento);

/// <summary>Lo que el motor devolvió para un componente: su resultado completo (líneas, estado nuevo, explicación). (nuevo)</summary>
public sealed record ResultadoDeComponente<TClave>(TClave Componente, ResultadoDeCosteo Resultado);

/// <summary>
/// El resultado de mover los componentes de un combo o de un ensamble (I6, T917): uno por componente y, en total,
/// <see cref="Costo"/> —el valor absoluto de Σ <c>TotalCost</c>: el costo de venta del combo, lo reingresado por su devolución o lo
/// consumido por el ensamble—. Si un componente no alcanza, <see cref="Rechazo"/> y <see cref="ComponenteRechazado"/>, y ningún
/// componente se mueve (<see cref="Componentes"/> vacío). (nuevo)
/// </summary>
public sealed record ResultadoDeCompuesto<TClave>(
    IReadOnlyList<ResultadoDeComponente<TClave>> Componentes,
    decimal Costo,
    ExplicacionDeCosto Explicacion,
    RechazoDeCosteo? Rechazo = null)
{
    public TClave? ComponenteRechazado { get; init; }

    public bool Admitido => Rechazo is null;
}

/// <summary>
/// El resultado de un ensamble (I6, T917; US15-3): las salidas de los componentes, la entrada del kit al costo de lo consumido y lo
/// consumido. Rechazado, <see cref="Kit"/> es nulo y nada se mueve. (nuevo)
/// </summary>
public sealed record ResultadoDeEnsamble<TClave>(
    ResultadoDeCompuesto<TClave> Componentes,
    ResultadoDeCosteo? Kit,
    decimal CostoConsumido,
    ExplicacionDeCosto Explicacion)
{
    public RechazoDeCosteo? Rechazo => Componentes.Rechazo;

    public bool Admitido => Rechazo is null;
}

/// <summary>
/// El combo y el ensamble sobre el motor (feature 012, I6, T917; US15-2, US15-3; data-model §1.11). Cada componente mueve su propio
/// ámbito por <see cref="MotorDeCosteo.Aplicar"/> —la salida al costo vigente de su ámbito; la devolución de un combo, al costo con que
/// salió—; el combo no tiene kardex propio y el kit entra por <see cref="EntradaDeEnsamble"/>. Todo o nada: el primer componente que
/// no alcanza rechaza el conjunto. Puro; lo invoca <see cref="MotorDeCosteo"/>.
/// </summary>
internal static class ComboYEnsamble
{
    public static ResultadoDeCompuesto<TClave> Mover<TClave>(IReadOnlyList<MovimientoDeComponente<TClave>> componentes,
        ParametrosDeCosteo parametros, string que)
    {
        ArgumentNullException.ThrowIfNull(componentes);
        ArgumentNullException.ThrowIfNull(parametros);
        if (componentes.Count == 0) throw new ArgumentException($"Un {que} tiene al menos un componente.", nameof(componentes));
        if (componentes.Select(c => c.Movimiento.EsEntrada).Distinct().Count() > 1)
            throw new ArgumentException($"Los componentes de un {que} se mueven todos en el mismo sentido.", nameof(componentes));

        var explicacion = new ExplicacionDeCosto { Resumen = $"Movimiento de los componentes de un {que}: uno por componente, al costo de su ámbito." };
        var resultados = new List<ResultadoDeComponente<TClave>>(componentes.Count);
        foreach (var c in componentes)
        {
            var r = MotorDeCosteo.Aplicar(c.Estado, c.Movimiento, parametros);
            if (r.Rechazo is { } rechazo)
            {
                explicacion.Nota("Rechazo", $"El componente {c.Componente} no alcanza: {rechazo.Mensaje}");
                return new ResultadoDeCompuesto<TClave>([], 0m, explicacion,
                    rechazo with { Mensaje = $"Componente {c.Componente}: {rechazo.Mensaje}" }) { ComponenteRechazado = c.Componente };
            }

            resultados.Add(new ResultadoDeComponente<TClave>(c.Componente, r));
            explicacion.Paso($"Componente {c.Componente}", r.Valor).Agregar(r.Explicacion);
        }

        var costo = Math.Abs(resultados.Sum(r => r.Resultado.Valor));
        explicacion.Paso($"Costo del {que} (Σ de sus componentes)", costo);
        return new ResultadoDeCompuesto<TClave>(resultados, costo, explicacion);
    }

    public static ResultadoDeEnsamble<TClave> Ensamblar<TClave>(IReadOnlyList<MovimientoDeComponente<TClave>> componentes,
        EstadoDeCosto estadoDelKit, decimal cantidadDelKit, ParametrosDeCosteo parametros, DateOnly? fecha)
    {
        ArgumentNullException.ThrowIfNull(componentes);
        if (componentes.Any(c => c.Movimiento.EsEntrada))
            throw new ArgumentException("En un ensamble los componentes salen: su cantidad es negativa.", nameof(componentes));
        if (cantidadDelKit <= 0m) throw new ArgumentException("Se ensambla una cantidad positiva de kits.", nameof(cantidadDelKit));

        var salidas = Mover(componentes, parametros, "ensamble");
        if (!salidas.Admitido) return new ResultadoDeEnsamble<TClave>(salidas, null, 0m, salidas.Explicacion);

        var kit = EntradaDeEnsamble(estadoDelKit, cantidadDelKit, salidas.Costo, parametros, fecha);
        var explicacion = new ExplicacionDeCosto { Resumen = "Ensamble: salen los componentes y entra el kit al costo de lo consumido." }
            .Agregar(salidas.Explicacion)
            .Agregar(kit.Explicacion);
        return new ResultadoDeEnsamble<TClave>(salidas, kit, salidas.Costo, explicacion);
    }

    /// <summary>
    /// La entrada del kit: costo unitario = consumido ÷ cantidad, a 6 decimales, al costo indicado. Si <c>round(cantidad × costo)</c> no
    /// da exactamente lo consumido, la diferencia va en una línea <c>RoundingResidue</c> sobre esa entrada (FR-017: el residuo no se
    /// pierde y queda visible), de modo que el kit entra por exactamente lo que salió de sus componentes.
    /// </summary>
    public static ResultadoDeCosteo EntradaDeEnsamble(EstadoDeCosto estadoDelKit, decimal cantidad, decimal costoConsumido,
        ParametrosDeCosteo parametros, DateOnly? fecha)
    {
        ArgumentNullException.ThrowIfNull(estadoDelKit);
        ArgumentNullException.ThrowIfNull(parametros);
        if (cantidad <= 0m) throw new ArgumentException("Se ensambla una cantidad positiva de kits.", nameof(cantidad));
        if (costoConsumido < 0m) throw new ArgumentException("Lo consumido por un ensamble no es negativo.", nameof(costoConsumido));

        var unitario = Redondeo.CostoUnitario(costoConsumido / cantidad);
        var entrada = MotorDeCosteo.Aplicar(estadoDelKit,
            new MovimientoDeCosto(cantidad, ValoracionDelMovimiento.AlCostoIndicado, unitario) { OperationDate = fecha }, parametros);

        var explicacion = new ExplicacionDeCosto { Resumen = "Entrada del kit por ensamble" }
            .Paso("Costo consumido de los componentes", costoConsumido)
            .Paso("Cantidad de kits", cantidad)
            .Paso("Costo unitario del kit", unitario)
            .Nota("Regla", "El kit entra al costo de lo consumido: Σ de las salidas de sus componentes ÷ la cantidad, a 6 decimales.")
            .Agregar(entrada.Explicacion);

        var principal = entrada.Principal!;
        var residuo = Redondeo.Monto(costoConsumido, parametros.Montos) - principal.TotalCost;
        if (residuo == 0m) return entrada with { Explicacion = explicacion };

        var lineas = entrada.Lineas.ToList();
        lineas.Add(new LineaDeKardexPropuesta
        {
            Kind = KardexEntryKind.CostAdjustment,
            Reason = KardexReason.RoundingResidue,
            QuantityBase = 0m,
            UnitCost = unitario,
            TotalCost = residuo,
            AffectsEntry = ReferenciaDeKardex.A(principal),
        });
        explicacion.Paso("Residuo de redondeo del ensamble (RoundingResidue)", residuo)
            .Nota("Residuo", "Cantidad × costo unitario no da exactamente lo consumido: la diferencia entra al kit en su propia línea.");
        var e = entrada.Estado;
        var estado = EstadoDeCosto.Con(e.Quantity, e.Value + residuo, e.LastUnitCost, e.SalidasEnNegativo) with { Capas = e.Capas };
        return entrada with { Lineas = lineas, Estado = estado, Explicacion = explicacion };
    }
}
