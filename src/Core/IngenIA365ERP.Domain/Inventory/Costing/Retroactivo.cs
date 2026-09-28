using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Inventory.Costing;

/// <summary>
/// Un movimiento ya registrado en el ámbito, como lo necesita el recálculo: su línea principal
/// (<see cref="EntryId"/>, que con <see cref="OperationDate"/> da el orden del kardex), su documento, cómo se valoró, su
/// cantidad y su valor registrados (Σ <c>TotalCost</c> de sus líneas y de los ajustes que ya lo afectan), su costo
/// unitario y si la salida deja el inventario (venta, consumo, baja: la diferencia es «vendida») o se mueve dentro de
/// él (traslado: «en existencia»). (nuevo)
/// </summary>
public sealed record MovimientoRegistrado(
    long EntryId,
    long DocumentId,
    DateOnly OperationDate,
    MovimientoDeCosto Movimiento,
    decimal ValorRegistrado,
    decimal CostoUnitarioRegistrado,
    bool SaleDelInventario);

/// <summary>El movimiento que entra con fecha anterior: su fecha, su documento y cómo se valora. (nuevo)</summary>
public sealed record MovimientoRetroactivo(DateOnly OperationDate, long DocumentId, MovimientoDeCosto Movimiento);

/// <summary>
/// Lo que se recalcula: el estado del ámbito antes del primer movimiento de <see cref="Historia"/> (normalmente vacío),
/// la historia del ámbito en cualquier orden y los movimientos retroactivos de UN documento. (nuevo)
/// </summary>
public sealed record PedidoRetroactivo(
    EstadoDeCosto EstadoInicial,
    IReadOnlyList<MovimientoRegistrado> Historia,
    IReadOnlyList<MovimientoRetroactivo> Nuevos,
    ParametrosDeCosteo Parametros);

/// <summary>Los ajustes retroactivos de un documento afectado: uno <c>AjusteDeCostoReconocido</c> por cada uno. (nuevo)</summary>
public sealed record AjusteRetroactivoPorDocumento(
    long DocumentId,
    decimal EnExistencia,
    decimal Vendida,
    IReadOnlyList<LineaDeKardexPropuesta> Lineas);

/// <summary>
/// El resultado: las líneas de los movimientos nuevos (en el orden de <see cref="PedidoRetroactivo.Nuevos"/>), los
/// ajustes <c>Retroactive</c> sobre los movimientos posteriores, agrupados por documento afectado, el estado final del
/// ámbito y la explicación; o el rechazo que nombra el movimiento donde el ámbito quedaría en negativo. (nuevo)
/// </summary>
public sealed record ResultadoRetroactivo(
    IReadOnlyList<ResultadoDeCosteo> Nuevos,
    IReadOnlyList<LineaDeKardexPropuesta> Ajustes,
    IReadOnlyList<AjusteRetroactivoPorDocumento> PorDocumento,
    EstadoDeCosto EstadoFinal,
    ExplicacionDeCosto Explicacion,
    RechazoDeCosteo? Rechazo = null)
{
    public bool Admitido => Rechazo is null;
}

/// <summary>
/// Lo que muestra <c>POST /api/inventory/documents/{id}/cost-impact</c> antes de confirmar (US16, T830; FR-045; api.md §9.3): si
/// el documento es retroactivo (deja un movimiento con fecha anterior a otro ya registrado del ámbito), los documentos afectados
/// —uno <c>AjusteDeCostoReconocido</c> por cada uno, con su porción en existencia y vendida—, el total y el resultado del mismo
/// cálculo que hará la confirmación; o el rechazo. (nuevo)
/// </summary>
public sealed record ImpactoEnCostos(
    bool EsRetroactivo,
    IReadOnlyList<AjusteRetroactivoPorDocumento> Afectados,
    decimal Total,
    ResultadoRetroactivo Resultado,
    RechazoDeCosteo? Rechazo);

/// <summary>
/// El retroactivo (feature 012; T282 en I1, generalizado en I5 por T829; FR-045; decisiones-transversales T18; research R10,
/// preguntas D6, D8 y D9): cualquier documento que deja un movimiento con fecha anterior a otro ya registrado del mismo producto
/// y ámbito. En I1 lo usaban sólo el saldo inicial de una bodega <c>NotActivated</c> (y su anulación) y los ajustes de un conteo
/// aprobado, que no dependen de <c>Costeo.RetroactivosPermitidos</c>; desde I5 cualquier clase, con el parámetro y sus días
/// máximos, que mira la aplicación antes de llamarlo (la puerta no es del motor). <b>Sólo con promedio ponderado</b> (D6): con
/// PEPS responde <see cref="CodigoRequierePromedioPonderado"/> sin calcular nada, también para las dos clases de I1.
///
/// <para>
/// Inserta los movimientos nuevos en el orden del kardex <c>(OperationDate, Id)</c> —los nuevos reciben Ids mayores, así
/// que dentro de su fecha van después de lo ya registrado—, vuelve a pasar por <see cref="MotorDeCosteo"/> cada
/// movimiento posterior y, donde el valor recalculado difiere del registrado, propone una línea
/// <c>CostAdjustment</c>/<c>Retroactive</c> con <c>AffectsEntryId</c>, fechada en ese movimiento, marcada «en
/// existencia» o «vendida» y agrupada por documento. Nada se reescribe. Lo que se valoró al costo de una línea de origen
/// recalculada (la entrada al tránsito, la devolución de cliente) sigue al costo nuevo de esa línea. Con el negativo
/// prohibido, un saldo intermedio bajo cero rechaza todo nombrando el movimiento.
/// </para>
/// </summary>
public static class Retroactivo
{
    /// <summary>Con PEPS no se admiten retroactivos (D6): <c>Inventory.Costing.RetroactiveRequiresWeightedAverage</c>. (nuevo)</summary>
    public const string CodigoRequierePromedioPonderado = "Inventory.Costing.RetroactiveRequiresWeightedAverage";

    /// <summary>
    /// El primer movimiento registrado con fecha posterior a <paramref name="fecha"/> en el orden del kardex
    /// <c>(OperationDate, EntryId)</c>: el que hace retroactivo al documento y el que nombra su rechazo cuando el parámetro no lo
    /// permite. A igual fecha lo nuevo va después, así que no cuenta. Nulo = el documento no es retroactivo.
    /// </summary>
    public static MovimientoRegistrado? PrimerMovimientoPosterior(IEnumerable<MovimientoRegistrado> historia, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(historia);
        return historia.Where(h => h.OperationDate > fecha).OrderBy(h => h.OperationDate).ThenBy(h => h.EntryId).FirstOrDefault();
    }

    public static ResultadoRetroactivo Insertar(PedidoRetroactivo pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        if (pedido.Nuevos.Count == 0) throw new ArgumentException("El retroactivo necesita al menos un movimiento.", nameof(pedido));
        var p = pedido.Parametros;
        var explicacion = new ExplicacionDeCosto { Resumen = "Retroactivo: movimientos insertados en su fecha y posteriores recalculados, sin reescribir nada." };

        if (p.Metodo != CostMethod.WeightedAverage)
        {
            const string mensaje = "Con PEPS no se admiten movimientos con fecha anterior a otros ya registrados: sólo con promedio ponderado (D6).";
            explicacion.Nota("Rechazo", mensaje);
            return new ResultadoRetroactivo([], [], [], pedido.EstadoInicial, explicacion,
                new RechazoDeCosteo(CodigoRequierePromedioPonderado, mensaje, 0m, 0m));
        }

        var historia = pedido.Historia.OrderBy(h => h.OperationDate).ThenBy(h => h.EntryId).ToList();
        var primeraFecha = pedido.Nuevos.Min(n => n.OperationDate);

        // Lo anterior a la fecha de inserción (y lo de esa misma fecha: los nuevos van después) no cambia.
        var estado = pedido.EstadoInicial;
        var indice = 0;
        for (; indice < historia.Count && historia[indice].OperationDate <= primeraFecha; indice++)
            estado = Sumar(estado, historia[indice]);
        explicacion.Paso("Existencia en la fecha de inserción", estado.Quantity).Paso("Valor en la fecha de inserción", estado.Value);

        // Los nuevos y los posteriores, en orden: a igual fecha, lo registrado antes que lo nuevo.
        var pendientes = historia.Skip(indice).Select(h => (h.OperationDate, Orden: 0, h.EntryId, Registrado: h, Nuevo: (MovimientoRetroactivo?)null))
            .Concat(pedido.Nuevos.Select((n, i) => (n.OperationDate, Orden: 1, EntryId: (long)i, Registrado: (MovimientoRegistrado?)null, Nuevo: (MovimientoRetroactivo?)n)))
            .OrderBy(x => x.OperationDate).ThenBy(x => x.Orden).ThenBy(x => x.EntryId)
            .ToList();

        var resultadosNuevos = new ResultadoDeCosteo?[pedido.Nuevos.Count];
        var costosRecalculados = new Dictionary<long, decimal>();
        var ajustes = new List<LineaDeKardexPropuesta>();

        foreach (var paso in pendientes)
        {
            if (paso.Nuevo is { } nuevo)
            {
                var resultado = MotorDeCosteo.Aplicar(estado, nuevo.Movimiento, p);
                if (!resultado.Admitido) return Rechazo(pedido, estado, explicacion, resultado.Rechazo!, null);
                resultadosNuevos[(int)paso.EntryId] = resultado;
                estado = resultado.Estado;
                explicacion.Paso($"Insertado el {nuevo.OperationDate:yyyy-MM-dd}, documento {nuevo.DocumentId}", resultado.Valor);
                continue;
            }

            var registrado = paso.Registrado!;
            var movimiento = ConOrigenRecalculado(registrado.Movimiento, costosRecalculados);
            var recalculado = MotorDeCosteo.Aplicar(estado, movimiento, p);
            if (!recalculado.Admitido) return Rechazo(pedido, estado, explicacion, recalculado.Rechazo!, registrado.EntryId);

            if (recalculado.Principal is { } principal) costosRecalculados[registrado.EntryId] = principal.UnitCost;
            var diferencia = recalculado.Valor - registrado.ValorRegistrado;
            if (diferencia != 0m)
            {
                var porcion = registrado.SaleDelInventario && !registrado.Movimiento.EsEntrada ? PorcionDelAjuste.Vendida : PorcionDelAjuste.EnExistencia;
                ajustes.Add(new LineaDeKardexPropuesta
                {
                    Kind = KardexEntryKind.CostAdjustment,
                    Reason = KardexReason.Retroactive,
                    QuantityBase = 0m,
                    UnitCost = recalculado.Principal?.UnitCost ?? 0m,
                    TotalCost = diferencia,
                    AffectsEntry = ReferenciaDeKardex.A(registrado.EntryId),
                    OperationDate = registrado.OperationDate,
                    Porcion = porcion,
                    AffectedDocumentId = registrado.DocumentId,
                });
                explicacion.Paso($"Recalculado el movimiento {registrado.EntryId} del {registrado.OperationDate:yyyy-MM-dd} ({porcion})", diferencia);
            }

            estado = recalculado.Estado;
        }

        var porDocumento = ajustes
            .GroupBy(a => a.AffectedDocumentId!.Value)
            .Select(g => new AjusteRetroactivoPorDocumento(
                g.Key,
                g.Where(a => a.Porcion == PorcionDelAjuste.EnExistencia).Sum(a => a.TotalCost),
                g.Where(a => a.Porcion == PorcionDelAjuste.Vendida).Sum(a => a.TotalCost),
                g.ToList()))
            .OrderBy(d => d.Lineas.Min(l => l.OperationDate))
            .ThenBy(d => d.DocumentId)
            .ToList();

        explicacion.Paso("Existencia final", estado.Quantity).Paso("Valor final", estado.Value).Paso("Promedio final", estado.AverageCost);
        return new ResultadoRetroactivo(resultadosNuevos.Select(r => r!).ToList(), ajustes, porDocumento, estado, explicacion);
    }

    /// <summary>Lo registrado antes de la inserción entra con su cantidad y su valor, sin recalcular.</summary>
    private static EstadoDeCosto Sumar(EstadoDeCosto estado, MovimientoRegistrado h)
    {
        var cantidad = estado.Quantity + h.Movimiento.QuantityBase;
        var valor = estado.Value + h.ValorRegistrado;
        var ultimo = h.Movimiento.EsEntrada ? h.CostoUnitarioRegistrado : estado.LastUnitCost;
        return EstadoDeCosto.Con(cantidad, valor, ultimo, estado.SalidasEnNegativo);
    }

    /// <summary>Lo valorado al costo de una línea de origen recalculada sigue al costo nuevo de esa línea.</summary>
    private static MovimientoDeCosto ConOrigenRecalculado(MovimientoDeCosto movimiento, Dictionary<long, decimal> recalculados) =>
        movimiento.Valoracion == ValoracionDelMovimiento.AlCostoDeOrigen
        && movimiento.Origen?.Id is { } origen
        && recalculados.TryGetValue(origen, out var costo)
            ? movimiento with { CostoUnitario = costo }
            : movimiento;

    private static ResultadoRetroactivo Rechazo(
        PedidoRetroactivo pedido, EstadoDeCosto estado, ExplicacionDeCosto explicacion, RechazoDeCosteo rechazo, long? movimiento)
    {
        var mensaje = movimiento is { } id
            ? $"{rechazo.Mensaje} Al insertar el retroactivo, el ámbito queda en negativo en el movimiento {id}."
            : rechazo.Mensaje;
        explicacion.Nota("Rechazo", mensaje);
        return new ResultadoRetroactivo([], [], [], pedido.EstadoInicial, explicacion,
            rechazo with { Mensaje = mensaje, MovimientoEntryId = movimiento });
    }
}
