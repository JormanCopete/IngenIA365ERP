using System.Globalization;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Inventory.Costing;

/// <summary>
/// PEPS, primeras en entrar, primeras en salir (feature 012, I5, T828; FR-043; data-model §3.5; research R10). Puro, sobre el
/// mismo <see cref="EstadoDeCosto"/> que el promedio, con sus capas en <see cref="EstadoDeCosto.Capas"/>. Reglas:
/// <list type="bullet">
/// <item>toda entrada crea su capa a su costo —la devolución de cliente, al costo con que salió—, detrás de las vivas;</item>
/// <item>la salida consume las capas en orden <c>(OperationDate, EntryKardexEntryId)</c> y es UNA línea con el costo unitario
/// ponderado de lo consumido (<c>Σ consumido / cantidad</c>, a 6 decimales); si <c>round(cantidad × costo)</c> no da
/// exactamente lo consumido, la diferencia va en una línea <c>RoundingResidue</c> sobre la salida;</item>
/// <item>la devolución a proveedor y la anulación de una entrada salen al costo con que entró y consumen primero la capa de esa
/// entrada; lo que viaja al costo de su línea de origen (la salida del tránsito) consume primero la capa creada desde esa misma
/// línea; si esa capa no alcanza, siguen en orden, y la diferencia entre la línea y lo consumido es <c>VoidDifference</c>;</item>
/// <item>la anulación de una salida entra al costo de la línea anulada y DEVUELVE sus consumos —en orden inverso— a las mismas
/// capas, con consumos de cantidad negativa bajo la línea de la anulación; la diferencia, si la hay, es <c>VoidDifference</c>;</item>
/// <item>con el negativo permitido, lo que no encuentra capa sale al último costo en su propia línea y queda pendiente; la
/// entrada que lo cubre le anota el consumo de su capa y la diferencia como <c>NegativeRegularization</c>.</item>
/// </list>
/// El valor de una capa es <c>round(RemainingQuantity × UnitCost)</c> y un consumo toma la diferencia entre el valor antes y
/// después: así, con existencia no negativa, Σ valor de las capas vivas = <c>CostState.Value</c> al centavo, y un consumo que
/// agota la capa se lleva todo lo que le quedaba. Los retroactivos no llegan aquí (D6: sólo con promedio ponderado).
/// </summary>
public static class Peps
{
    public static ResultadoDeCosteo Aplicar(EstadoDeCosto estado, MovimientoDeCosto movimiento, ParametrosDeCosteo parametros)
    {
        if (!movimiento.EsEntrada) return Salida(estado, movimiento, parametros);
        return movimiento.EsAnulacion && movimiento.ConsumosDelOrigen.Count > 0 && estado.Quantity >= 0m
            ? DevolverConsumos(estado, movimiento, parametros)
            : Entrada(estado, movimiento, parametros);
    }

    /// <summary>
    /// El cambio de método del ámbito (I5, T821; FR-043; data-model §3.1, §3.5): una línea <c>CostAdjustment</c>
    /// <c>MethodChange</c> sin cantidad. A PEPS abre UNA capa con toda la existencia al promedio vigente, cuya entrada es esa
    /// línea, y la línea lleva la diferencia de redondeo entre la capa y el valor del ámbito (normalmente 0); a promedio
    /// ponderado, la línea va en 0 y las capas se cierran.
    /// </summary>
    public static ResultadoDeCosteo CambioDeMetodo(EstadoDeCosto estado, CostMethod nuevo, RedondeoDeMontos montos, DateOnly? fecha)
    {
        ArgumentNullException.ThrowIfNull(estado);
        var explicacion = new ExplicacionDeCosto { Resumen = $"Cambio de método de costeo a {Nombre(nuevo)}." }
            .Paso("Existencia", estado.Quantity)
            .Paso("Valor", estado.Value)
            .Paso("Promedio vigente", estado.AverageCost);

        if (nuevo == CostMethod.Fifo && estado.Quantity > 0m)
        {
            var costo = estado.AverageCost;
            var valorDeLaCapa = Redondeo.Monto(estado.Quantity * costo, montos);
            var linea = Ajuste(KardexReason.MethodChange, costo, valorDeLaCapa - estado.Value, null);
            var capa = new CapaDeCosto(ReferenciaDeKardex.A(linea), fecha, estado.Quantity, estado.Quantity, costo);
            explicacion.Paso("Capa única al promedio", valorDeLaCapa)
                .Paso("Diferencia de redondeo (MethodChange)", linea.TotalCost)
                .Nota("Regla", "Desde el cambio, PEPS parte de una sola capa con toda la existencia al promedio vigente.");
            var nuevoEstado = EstadoDeCosto.Con(estado.Quantity, valorDeLaCapa, estado.LastUnitCost, estado.SalidasEnNegativo) with { Capas = [capa] };
            return new ResultadoDeCosteo([linea], nuevoEstado, explicacion) { CapasNuevas = [capa] };
        }

        explicacion.Nota("Regla", nuevo == CostMethod.Fifo
            ? "Sin existencia no hay capa: la próxima entrada abre la primera."
            : "Desde el cambio, el costo es el promedio del ámbito; las capas se cierran.");
        var cero = Ajuste(KardexReason.MethodChange, estado.CostoVigente, 0m, null);
        var sinCapas = EstadoDeCosto.Con(estado.Quantity, estado.Value, estado.LastUnitCost, estado.SalidasEnNegativo);
        return new ResultadoDeCosteo([cero], sinCapas, explicacion);
    }

    // --------------------------------------------------------------- ajuste sobre una entrada (T843, D5) --

    /// <summary>
    /// La porción de una entrada que sigue en existencia en PEPS (I5, T843; D5): lo que queda de <b>su</b> capa,
    /// <c>RemainingQuantity / OriginalQuantity</c> (a lo sumo 1). Sin capa viva (se agotó o nunca la tuvo), cero: todo ya se vendió o
    /// consumió. La usan la diferencia de precio y los costos adicionales en lugar de la existencia del ámbito del promedio.
    /// </summary>
    public static decimal ProporcionEnExistencia(EstadoDeCosto estado, ReferenciaDeKardex entrada)
    {
        ArgumentNullException.ThrowIfNull(estado);
        var capa = estado.Capas.FirstOrDefault(c => c.Es(entrada));
        return capa is null || capa.OriginalQuantity <= 0m ? 0m : Math.Min(1m, capa.RemainingQuantity / capa.OriginalQuantity);
    }

    /// <summary>La capa viva de una entrada (nula si ya se agotó).</summary>
    public static CapaDeCosto? CapaDe(EstadoDeCosto estado, ReferenciaDeKardex entrada)
    {
        ArgumentNullException.ThrowIfNull(estado);
        return estado.Capas.FirstOrDefault(c => c.Es(entrada));
    }

    /// <summary>
    /// Un ajuste de costo sobre una entrada con PEPS (I5, T843; D5, D6; decisiones-transversales T42b): la diferencia de precio o los
    /// costos adicionales. <paramref name="total"/> es lo que costó de más (o de menos) esa entrada; <paramref name="enExistencia"/>, la
    /// porción que sigue en <b>su</b> capa (<see cref="ProporcionEnExistencia"/>); lo demás ya salió y va a lo vendido. Deja las mismas
    /// líneas que el promedio —el total sobre la entrada (<see cref="PorcionDelAjuste.EnExistencia"/>) y, si algo ya salió, lo vendido con
    /// signo contrario (<see cref="PorcionDelAjuste.Vendida"/>)— y <b>suma lo que quedó a la capa</b>: su costo unitario pasa a
    /// <c>round6((valor de la capa + en existencia) / restante)</c>. Si ese costo no da el valor exacto al centavo, la diferencia va en una
    /// tercera línea del mismo motivo, también en existencia, para que Σ valor de las capas siga siendo el valor del ámbito. Las otras capas
    /// no cambian. (nuevo)
    /// </summary>
    public static ResultadoDeCosteo AjusteSobreEntrada(
        EstadoDeCosto estado, ReferenciaDeKardex entrada, KardexReason motivo, decimal total, decimal enExistencia, RedondeoDeMontos montos,
        ExplicacionDeCosto explicacion)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(explicacion);
        if (total == 0m) return new ResultadoDeCosteo([], estado, explicacion.Nota("Regla", "Nada que ajustar: no hay línea."));

        var capas = estado.Capas.ToList();
        var i = capas.FindIndex(c => c.Es(entrada));
        if (i < 0 && enExistencia != 0m)
        {
            // Con PEPS lo que queda en existencia de una entrada es su capa: si ya se agotó (la anulación de unos costos adicionales
            // después de venderla), todo va a lo vendido.
            explicacion.Nota("Capa agotada", "La capa de la entrada ya no tiene existencia: todo el ajuste va al costo de lo vendido.");
            enExistencia = 0m;
        }

        var vendida = total - enExistencia;
        var lineas = new List<LineaDeKardexPropuesta>
        {
            new() { Kind = KardexEntryKind.CostAdjustment, Reason = motivo, QuantityBase = 0m, UnitCost = 0m, TotalCost = total, AffectsEntry = entrada, Porcion = PorcionDelAjuste.EnExistencia },
        };
        if (vendida != 0m)
            lineas.Add(new LineaDeKardexPropuesta { Kind = KardexEntryKind.CostAdjustment, Reason = motivo, QuantityBase = 0m, UnitCost = 0m, TotalCost = -vendida, AffectsEntry = entrada, Porcion = PorcionDelAjuste.Vendida });

        var residuo = 0m;
        if (i >= 0 && enExistencia != 0m)
        {
            var capa = capas[i];
            var objetivo = capa.Valor(montos) + enExistencia;
            var costo = Math.Max(0m, Redondeo.CostoUnitario(objetivo / capa.RemainingQuantity));
            var nueva = capa with { UnitCost = costo };
            residuo = nueva.Valor(montos) - objetivo;
            capas[i] = nueva;
            explicacion.Paso(Etiqueta(capa), capa.RemainingQuantity).Paso("Costo unitario nuevo de la capa", costo);
            if (residuo != 0m)
            {
                lineas.Add(new LineaDeKardexPropuesta { Kind = KardexEntryKind.CostAdjustment, Reason = motivo, QuantityBase = 0m, UnitCost = costo, TotalCost = residuo, AffectsEntry = entrada, Porcion = PorcionDelAjuste.EnExistencia });
                explicacion.Paso("Residuo contra el valor de la capa", residuo);
            }
        }

        explicacion.Paso("En existencia", enExistencia).Paso("Vendida o consumida", vendida)
            .Nota("Regla PEPS", "Lo que queda de la capa de esa entrada suma a su costo; lo que ya salió de ella va al costo de lo vendido.");
        var nuevo = EstadoDeCosto.Con(estado.Quantity, estado.Value + enExistencia + residuo, estado.LastUnitCost, estado.SalidasEnNegativo) with { Capas = capas };
        return new ResultadoDeCosteo(lineas, nuevo, explicacion);
    }

    // ------------------------------------------------------------------------------------------------ entrada --

    private static ResultadoDeCosteo Entrada(EstadoDeCosto estado, MovimientoDeCosto mov, ParametrosDeCosteo p)
    {
        var cantidad = mov.QuantityBase;
        var costo = Redondeo.CostoUnitario(mov.Valoracion == ValoracionDelMovimiento.AlCostoVigente ? estado.CostoVigente : CostoQueTrae(mov));
        var explicacion = Inicio(estado, "Entrada (PEPS)", mov).Paso("Costo unitario de la entrada", costo);
        explicacion.Nota("Regla", mov.Valoracion switch
        {
            ValoracionDelMovimiento.AlCostoVigente => "Entra al costo vigente del ámbito y crea su capa.",
            ValoracionDelMovimiento.AlCostoDeOrigen => "Entra al costo de la línea de origen y crea su capa a ese costo.",
            _ => "Entra al costo indicado y crea su capa.",
        });

        var linea = Entrar(cantidad, costo, p, mov.EsAnulacion ? mov.Origen : null);
        var lineas = new List<LineaDeKardexPropuesta> { linea };
        var creada = new CapaDeCosto(ReferenciaDeKardex.A(linea), mov.OperationDate, cantidad, cantidad, costo) { Origen = mov.Origen };
        var capa = creada;
        var consumos = new List<ConsumoDeCapa>();
        var pendientes = estado.SalidasEnNegativo.ToList();
        var valor = estado.Value + linea.TotalCost;

        if (estado.Quantity < 0m)
        {
            var regularizaciones = new List<(ReferenciaDeKardex? Salida, decimal Diferencia)>();
            var restante = Math.Min(cantidad, -estado.Quantity);
            while (restante > 0m && pendientes.Count > 0)
            {
                var pendiente = pendientes[0];
                var tomada = Math.Min(restante, pendiente.Cantidad);
                (capa, var tomado) = Tomar(capa, tomada, p.Montos);
                consumos.Add(new ConsumoDeCapa(pendiente.Salida, creada, tomada, costo, tomado));
                regularizaciones.Add((pendiente.Salida, Redondeo.Monto(tomada * pendiente.CostoUnitario, p.Montos) - tomado));
                if (tomada == pendiente.Cantidad) pendientes.RemoveAt(0);
                else pendientes[0] = pendiente with { Cantidad = pendiente.Cantidad - tomada };
                restante -= tomada;
            }

            if (restante > 0m)
            {
                // Sin la salida pendiente (la aplicación no la trajo): al costo del negativo, sin consumo que la nombre.
                var costoDelNegativo = Math.Max(0m, Redondeo.CostoUnitario(estado.Value / estado.Quantity));
                (capa, var tomado) = Tomar(capa, restante, p.Montos);
                regularizaciones.Add((null, Redondeo.Monto(restante * costoDelNegativo, p.Montos) - tomado));
            }

            // Cubierto el negativo, el valor del ámbito es exactamente el de las capas: el centavo que falte va a la última.
            if (estado.Quantity + cantidad >= 0m && regularizaciones.Count > 0)
            {
                var calculado = valor + regularizaciones.Sum(r => r.Diferencia);
                var enCapas = estado.Capas.Sum(c => c.Valor(p.Montos)) + capa.Valor(p.Montos);
                var ultima = regularizaciones[^1];
                regularizaciones[^1] = ultima with { Diferencia = ultima.Diferencia + enCapas - calculado };
            }

            var total = 0m;
            foreach (var (salida, diferencia) in regularizaciones.Where(r => r.Diferencia != 0m))
            {
                lineas.Add(Ajuste(KardexReason.NegativeRegularization, costo, diferencia, salida));
                total += diferencia;
            }
            valor += total;
            if (total != 0m)
                explicacion.Paso("Regularización del negativo (NegativeRegularization)", total)
                    .Nota("Negativo", "La entrada cubre con su capa lo que salió en negativo al último costo; la diferencia va al costo de lo que salió.");
        }

        var capas = estado.Capas.ToList();
        if (capa.RemainingQuantity > 0m) capas.Add(capa); // la más nueva: sin retroactivos en PEPS (D6), va detrás de todas
        var nuevaCantidad = estado.Quantity + cantidad;
        valor += Residuo(nuevaCantidad, valor, linea, lineas, explicacion);

        var nuevo = EstadoDeCosto.Con(nuevaCantidad, valor, costo, pendientes) with { Capas = capas };
        return Fin(nuevo, lineas, explicacion) with { Consumos = consumos, CapasNuevas = [capa] };
    }

    /// <summary>La anulación de una salida: entra al costo de la línea anulada y devuelve sus consumos a las mismas capas.</summary>
    private static ResultadoDeCosteo DevolverConsumos(EstadoDeCosto estado, MovimientoDeCosto mov, ParametrosDeCosteo p)
    {
        var cantidad = mov.QuantityBase;
        var costo = Redondeo.CostoUnitario(CostoQueTrae(mov));
        var explicacion = Inicio(estado, "Anulación de una salida (PEPS)", mov)
            .Paso("Costo de la línea anulada", costo)
            .Nota("Regla", "Devuelve los consumos de la salida anulada a sus mismas capas, en orden inverso.");

        var linea = Entrar(cantidad, costo, p, mov.Origen);
        var lineas = new List<LineaDeKardexPropuesta> { linea };
        var capas = estado.Capas.ToList();
        var consumos = new List<ConsumoDeCapa>();
        var nuevas = new List<CapaDeCosto>();
        var porDevolver = cantidad;
        var devuelto = 0m;

        foreach (var consumo in mov.ConsumosDelOrigen.Where(c => c.Quantity > 0m).Reverse())
        {
            if (porDevolver <= 0m) break;
            var q = Math.Min(porDevolver, consumo.Quantity);
            var i = capas.FindIndex(c => c.Es(consumo.Capa.Entrada));
            CapaDeCosto antes;
            CapaDeCosto despues;
            if (i >= 0)
            {
                antes = capas[i];
                despues = antes with { RemainingQuantity = antes.RemainingQuantity + q };
                if (despues.RemainingQuantity > despues.OriginalQuantity)
                    throw new InvalidOperationException("La anulación devolvería a una capa más de lo que entró en ella.");
                capas[i] = despues;
            }
            else
            {
                antes = consumo.Capa with { RemainingQuantity = 0m };
                despues = consumo.Capa with { RemainingQuantity = q };
                Insertar(capas, despues);
            }

            var valorDevuelto = despues.Valor(p.Montos) - antes.Valor(p.Montos);
            devuelto += valorDevuelto;
            consumos.Add(new ConsumoDeCapa(ReferenciaDeKardex.A(linea), consumo.Capa, -q, consumo.Capa.UnitCost, -valorDevuelto));
            explicacion.Paso(Etiqueta(despues), -q);
            porDevolver -= q;
        }

        if (porDevolver > 0m)
        {
            var sobrante = new CapaDeCosto(ReferenciaDeKardex.A(linea), mov.OperationDate, porDevolver, porDevolver, costo) { Origen = mov.Origen };
            Insertar(capas, sobrante);
            nuevas.Add(sobrante);
            devuelto += sobrante.Valor(p.Montos);
            explicacion.Paso("Sin consumo que devolver: capa nueva al costo de la línea", porDevolver);
        }

        var diferencia = devuelto - linea.TotalCost;
        if (diferencia != 0m)
        {
            lineas.Add(Ajuste(KardexReason.VoidDifference, costo, diferencia, mov.Origen));
            explicacion.Paso("Diferencia contra las capas devueltas (VoidDifference)", diferencia);
        }

        var nuevo = EstadoDeCosto.Con(estado.Quantity + cantidad, estado.Value + devuelto, costo, estado.SalidasEnNegativo) with { Capas = capas };
        return Fin(nuevo, lineas, explicacion) with { Consumos = consumos, CapasNuevas = nuevas };
    }

    // ------------------------------------------------------------------------------------------------- salida --

    private static ResultadoDeCosteo Salida(EstadoDeCosto estado, MovimientoDeCosto mov, ParametrosDeCosteo p)
    {
        var cantidad = -mov.QuantityBase;
        if (estado.Quantity < cantidad && !p.NegativoPermitido)
        {
            var disponible = Math.Max(estado.Quantity, 0m);
            return new ResultadoDeCosteo([], estado, Inicio(estado, "Salida rechazada (PEPS)", mov),
                new RechazoDeCosteo(MotorDeCosteo.CodigoExistenciaInsuficiente,
                    $"La existencia del ámbito no alcanza: hay {F(disponible)} y la salida pide {F(cantidad)}.",
                    disponible, cantidad));
        }

        var especifica = mov.Valoracion is ValoracionDelMovimiento.DevolucionDeEntrada or ValoracionDelMovimiento.AlCostoDeOrigen;
        var explicacion = Inicio(estado, especifica ? "Salida al costo de su línea de origen (PEPS)" : "Salida por capas (PEPS)", mov);
        var capas = estado.Capas.ToList();
        var tomas = new List<(CapaDeCosto Capa, decimal Cantidad, decimal Valor)>();
        var porCubrir = cantidad;

        if (especifica && mov.Origen is { } origen)
        {
            var i = capas.FindIndex(c => c.Es(origen, oSuOrigen: true));
            if (i >= 0) porCubrir = Consumir(capas, i, porCubrir, tomas, p.Montos);
        }
        while (porCubrir > 0m && capas.Count > 0) porCubrir = Consumir(capas, 0, porCubrir, tomas, p.Montos);

        var cubierta = cantidad - porCubrir;
        var negativo = porCubrir;
        var tomado = tomas.Sum(t => t.Valor);
        foreach (var t in tomas) explicacion.Paso(Etiqueta(t.Capa), t.Cantidad);

        var lineas = new List<LineaDeKardexPropuesta>();
        var pendientes = estado.SalidasEnNegativo.ToList();
        var valor = estado.Value;
        LineaDeKardexPropuesta principal;

        if (especifica)
        {
            var costo = Redondeo.CostoUnitario(CostoQueTrae(mov));
            principal = Salir(cantidad, costo, p, mov);
            lineas.Add(principal);
            var objetivo = -tomado - Redondeo.Monto(negativo * costo, p.Montos);
            var diferencia = objetivo - principal.TotalCost;
            explicacion.Paso("Costo de la línea de origen", costo);
            if (diferencia != 0m)
            {
                lineas.Add(Ajuste(KardexReason.VoidDifference, costo, diferencia, mov.Origen));
                explicacion.Paso("Diferencia contra las capas consumidas (VoidDifference)", diferencia)
                    .Nota("Regla", "Sale al costo de su línea de origen; la diferencia contra el costo de las capas que consumió es ajuste de costo.");
            }
            else
            {
                explicacion.Nota("Regla", "Sale al costo de su línea de origen, que es el de las capas que consumió: no hay diferencia.");
            }
            valor += objetivo;
            if (negativo > 0m) pendientes.Add(new SalidaEnNegativo(ReferenciaDeKardex.A(principal), negativo, costo));
        }
        else
        {
            explicacion.Nota("Regla", "PEPS: consume las capas en orden, la más antigua primero.");
            if (cubierta > 0m)
            {
                var costo = Redondeo.CostoUnitario(tomado / cubierta);
                principal = Salir(cubierta, costo, p, mov);
                lineas.Add(principal);
                var residuo = -tomado - principal.TotalCost;
                if (residuo != 0m)
                {
                    lineas.Add(Ajuste(KardexReason.RoundingResidue, costo, residuo, ReferenciaDeKardex.A(principal)));
                    explicacion.Paso("Residuo contra el valor de las capas (RoundingResidue)", residuo);
                }
                valor -= tomado;
                explicacion.Paso("Costo unitario de la salida", costo);
            }
            else
            {
                principal = null!;
            }

            if (negativo > 0m)
            {
                var negativa = Salir(negativo, estado.LastUnitCost, p, mov);
                lineas.Add(negativa);
                principal ??= negativa;
                valor += negativa.TotalCost;
                pendientes.Add(new SalidaEnNegativo(ReferenciaDeKardex.A(negativa), negativo, estado.LastUnitCost));
                explicacion.Paso("Sale en negativo al último costo", negativo)
                    .Paso("Último costo", estado.LastUnitCost)
                    .Nota("Regla", "Con el negativo permitido, lo que no encuentra capa sale al último costo y lo cubre la próxima entrada.");
            }
        }

        var consumos = tomas.Select(t => new ConsumoDeCapa(ReferenciaDeKardex.A(principal), t.Capa, t.Cantidad, t.Capa.UnitCost, t.Valor)).ToList();
        var existencia = estado.Quantity - cantidad;
        valor += Residuo(existencia, valor, principal, lineas, explicacion);

        var nuevo = EstadoDeCosto.Con(existencia, valor, estado.LastUnitCost, pendientes) with { Capas = capas };
        return Fin(nuevo, lineas, explicacion) with { Consumos = consumos };
    }

    // ------------------------------------------------------------------------------------------------- apoyo --

    /// <summary>Consume de la capa <paramref name="i"/> lo que pueda y devuelve lo que falta por cubrir.</summary>
    private static decimal Consumir(List<CapaDeCosto> capas, int i, decimal porCubrir, List<(CapaDeCosto, decimal, decimal)> tomas, RedondeoDeMontos montos)
    {
        var capa = capas[i];
        var tomada = Math.Min(porCubrir, capa.RemainingQuantity);
        var (resto, valor) = Tomar(capa, tomada, montos);
        tomas.Add((capa, tomada, valor));
        if (resto.RemainingQuantity == 0m) capas.RemoveAt(i);
        else capas[i] = resto;
        return porCubrir - tomada;
    }

    /// <summary>Lo que queda de la capa y el valor que se lleva el consumo: el valor antes menos el valor después.</summary>
    private static (CapaDeCosto Resto, decimal Valor) Tomar(CapaDeCosto capa, decimal cantidad, RedondeoDeMontos montos)
    {
        if (cantidad < 0m || cantidad > capa.RemainingQuantity)
            throw new InvalidOperationException("Un consumo no puede tomar más de lo que queda en la capa.");
        var resto = capa with { RemainingQuantity = capa.RemainingQuantity - cantidad };
        return (resto, capa.Valor(montos) - resto.Valor(montos));
    }

    /// <summary>Inserta la capa en el orden PEPS: <c>(OperationDate, EntryKardexEntryId)</c>; sin Id todavía, al final de su fecha.</summary>
    private static void Insertar(List<CapaDeCosto> capas, CapaDeCosto capa)
    {
        var clave = Clave(capa);
        var i = capas.FindIndex(c => Clave(c).CompareTo(clave) > 0);
        if (i < 0) capas.Add(capa);
        else capas.Insert(i, capa);
    }

    private static (DateOnly, long) Clave(CapaDeCosto c) => (c.OperationDate ?? DateOnly.MinValue, c.Entrada.Id ?? long.MaxValue);

    private static decimal Residuo(
        decimal cantidad, decimal valor, LineaDeKardexPropuesta causa, List<LineaDeKardexPropuesta> lineas, ExplicacionDeCosto explicacion)
    {
        if (cantidad != 0m || valor == 0m) return 0m;
        lineas.Add(Ajuste(KardexReason.RoundingResidue, causa.UnitCost, -valor, ReferenciaDeKardex.A(causa)));
        explicacion.Paso("Residuo de redondeo (RoundingResidue)", -valor).Nota("Residuo", "La cantidad llegó a cero: el valor queda en cero.");
        return -valor;
    }

    private static LineaDeKardexPropuesta Entrar(decimal cantidad, decimal costo, ParametrosDeCosteo p, ReferenciaDeKardex? revierte) => new()
    {
        Kind = KardexEntryKind.Entry,
        Reason = KardexReason.Normal,
        QuantityBase = cantidad,
        UnitCost = costo,
        TotalCost = Redondeo.Monto(cantidad * costo, p.Montos),
        ReversesEntry = revierte,
    };

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

    /// <summary>Cómo nombra la explicación una capa: su fecha y su costo (la contadora la ubica en el kardex).</summary>
    private static string Etiqueta(CapaDeCosto capa) =>
        capa.OperationDate is { } fecha
            ? $"Capa del {fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} a {F(capa.UnitCost)}"
            : $"Capa de la entrada {capa.Entrada.Id?.ToString(CultureInfo.InvariantCulture) ?? "nueva"} a {F(capa.UnitCost)}";

    private static string Nombre(CostMethod metodo) => metodo == CostMethod.Fifo ? "PEPS" : "promedio ponderado";

    private static ExplicacionDeCosto Inicio(EstadoDeCosto estado, string que, MovimientoDeCosto mov) =>
        new ExplicacionDeCosto { Resumen = que }
            .Paso("Cantidad del movimiento", mov.QuantityBase)
            .Paso("Existencia anterior", estado.Quantity)
            .Paso("Valor anterior", estado.Value)
            .Paso("Capas vivas", estado.Capas.Count);

    private static ResultadoDeCosteo Fin(EstadoDeCosto nuevo, List<LineaDeKardexPropuesta> lineas, ExplicacionDeCosto explicacion)
    {
        explicacion.Paso("Existencia nueva", nuevo.Quantity)
            .Paso("Valor nuevo", nuevo.Value)
            .Paso("Capas vivas después", nuevo.Capas.Count);
        return new ResultadoDeCosteo(lineas, nuevo, explicacion);
    }

    private static string F(decimal valor) => valor.ToString("0.######", CultureInfo.InvariantCulture);
}
