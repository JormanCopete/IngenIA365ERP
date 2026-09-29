using System.Globalization;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Inventory.Costing;

/// <summary>Qué es, para el valorizado, una línea del kardex. (nuevo)</summary>
public enum ClaseAValorizar
{
    /// <summary>Línea <c>Entry</c>: crea capa en PEPS y suma al promedio.</summary>
    Entrada = 1,

    /// <summary>Línea <c>Exit</c>: consume capas en PEPS y sale al promedio.</summary>
    Salida = 2,

    /// <summary>
    /// El ajuste completo sobre una entrada (<c>PriceDifference</c>, <c>LandedCost</c>): lo que costó de más o de menos esa
    /// compra. La aplicación manda UNA por documento y entrada, con el monto completo (la primera línea que escribió el motor);
    /// la de lo vendido va como <see cref="OtroAjuste"/>.
    /// </summary>
    AjusteSobreEntrada = 3,

    /// <summary>Cualquier otro ajuste (retroactivo, regularización, anulación, residuo, cambio de método, la porción vendida):
    /// cuenta en el valor del libro, pero la reconstrucción del otro método lo recalcula por sí misma.</summary>
    OtroAjuste = 4,
}

/// <summary>Una línea del kardex de un producto y ámbito, como la necesita el valorizado. (nuevo)</summary>
public sealed record MovimientoAValorizar(
    long EntryId,
    DateOnly OperationDate,
    ClaseAValorizar Clase,
    decimal QuantityBase,
    decimal UnitCost,
    decimal TotalCost,
    CostMethod MetodoRegistrado,
    long? AffectsEntryId = null);

/// <summary>
/// La historia de un producto en un ámbito: su grupo contable (el vigente a la fecha que se valora lo resuelve la aplicación),
/// el corte del sistema anterior —la fecha de su saldo inicial; nula si el producto nació en este sistema— y su kardex. (nuevo)
/// </summary>
public sealed record HistoriaParaValorizar(
    int ProductId,
    int ScopeWarehouseId,
    int AccountingGroupId,
    DateOnly? CorteDelSistemaAnterior,
    IReadOnlyList<MovimientoAValorizar> Movimientos);

/// <summary>El valor de un producto y ámbito a una fecha por los dos métodos, o la nota de por qué no se pudo calcular. (nuevo)</summary>
public sealed record ValorDeProducto(
    int ProductId,
    int ScopeWarehouseId,
    int AccountingGroupId,
    DateOnly Fecha,
    decimal? PromedioPonderado,
    decimal? Peps,
    string? Nota)
{
    public bool Calculado => Nota is null;
}

/// <summary>
/// Una fila del informe <c>method-change-valuation</c> (api.md §27): grupo contable, fecha, los dos valorizados de los productos
/// que se pudieron calcular, la diferencia (PEPS − promedio) y los que no, con su nota. (nuevo)
/// </summary>
public sealed record ValorizacionDeGrupo(
    int AccountingGroupId,
    DateOnly Fecha,
    decimal PromedioPonderado,
    decimal Peps,
    decimal Diferencia,
    IReadOnlyList<ValorDeProducto> SinCalcular)
{
    /// <summary>La nota de la fila: vacía si todo se calculó; si no, cuántos productos quedaron fuera y por qué.</summary>
    public string? Nota => SinCalcular.Count == 0
        ? null
        : string.Join(" ", SinCalcular.Select(p => p.Nota).Distinct(StringComparer.Ordinal));
}

/// <summary>El valorizado por grupo y fecha y el detalle por producto. (nuevo)</summary>
public sealed record ResultadoDeValorizacion(IReadOnlyList<ValorizacionDeGrupo> Grupos, IReadOnlyList<ValorDeProducto> Productos);

/// <summary>
/// El valorizado por los dos métodos (feature 012, I5, T831; FR-043; US16-4; api.md §27): lo que se entrega a Contabilidad para
/// la aplicación retroactiva o la revelación de la NIC 8 o la Sección 10 cuando se cambia de método. Puro. Para cada producto y
/// ámbito, desde su kardex, el valor de la existencia <b>al inicio</b> de cada fecha (todo lo anterior a ella: la fecha del cambio
/// es el primer día de un período y el período comparativo empieza también un primer día) por promedio ponderado y por PEPS,
/// agrupado por grupo contable.
///
/// <para>
/// El método con que se registró la historia se toma del libro (Σ <c>TotalCost</c>, lo que Contabilidad ya tiene); el otro se
/// reconstruye: el promedio, con las entradas a su costo registrado, las salidas al promedio a 6 decimales y el ajuste completo
/// sobre una entrada en proporción <c>min(1, existencia / cantidad de la entrada)</c> (la regla de <see cref="DiferenciaDePrecio"/>);
/// PEPS, con una capa por entrada, las salidas consumiéndolas en orden (<c>round(valor × cantidad / restante)</c>, la que agota
/// se lleva lo que queda) y el ajuste sobre una entrada en proporción a lo que queda de su capa (lo demás ya se vendió). Si la
/// historia mezcla métodos, se reconstruyen los dos.
/// </para>
///
/// <para>
/// No se puede calcular —y la nota lo dice— un producto cuya historia empieza en el corte del sistema anterior en esa fecha o
/// después (su existencia de antes no está en el kardex), ni por PEPS uno cuya historia deja la existencia en negativo (no hay
/// capas que consumir). Esos productos quedan fuera de las sumas de su grupo y se listan.
/// </para>
/// </summary>
public static class ValorizacionPorDosMetodos
{
    public static ResultadoDeValorizacion Calcular(
        IReadOnlyList<HistoriaParaValorizar> historias, IReadOnlyList<DateOnly> fechas, RedondeoDeMontos montos)
    {
        ArgumentNullException.ThrowIfNull(historias);
        ArgumentNullException.ThrowIfNull(fechas);

        var productos = new List<ValorDeProducto>();
        foreach (var fecha in fechas.Distinct().OrderBy(f => f))
            foreach (var historia in historias)
                productos.Add(Valorar(historia, fecha, montos));

        var grupos = productos
            .GroupBy(p => (p.AccountingGroupId, p.Fecha))
            .OrderBy(g => g.Key.Fecha).ThenBy(g => g.Key.AccountingGroupId)
            .Select(g =>
            {
                var calculados = g.Where(p => p.Calculado).ToList();
                var promedio = calculados.Sum(p => p.PromedioPonderado!.Value);
                var peps = calculados.Sum(p => p.Peps!.Value);
                return new ValorizacionDeGrupo(g.Key.AccountingGroupId, g.Key.Fecha, promedio, peps, peps - promedio,
                    g.Where(p => !p.Calculado).ToList());
            })
            .ToList();

        return new ResultadoDeValorizacion(grupos, productos);
    }

    private static ValorDeProducto Valorar(HistoriaParaValorizar h, DateOnly fecha, RedondeoDeMontos montos)
    {
        if (h.CorteDelSistemaAnterior is { } corte && fecha <= corte)
            return SinCalcular(h, fecha,
                $"No se puede calcular: la historia de este producto empieza el {corte.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} " +
                "con su saldo inicial (corte del sistema anterior) y lo de antes no está en el kardex.");

        var lineas = h.Movimientos.Where(m => m.OperationDate < fecha)
            .OrderBy(m => m.OperationDate).ThenBy(m => m.EntryId).ToList();
        var libro = lineas.Sum(m => m.TotalCost);
        var todasEnPromedio = lineas.All(m => m.MetodoRegistrado == CostMethod.WeightedAverage);
        var todasEnPeps = lineas.All(m => m.MetodoRegistrado == CostMethod.Fifo);

        var promedio = todasEnPromedio ? libro : ReconstruirPromedio(lineas, montos);
        decimal peps;
        if (todasEnPeps)
        {
            peps = libro;
        }
        else
        {
            var reconstruido = ReconstruirPeps(lineas, montos);
            if (reconstruido.Negativo is { } dia)
                return SinCalcular(h, fecha,
                    $"No se puede calcular por PEPS: el {dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} la existencia quedó negativa y no hay capas que consumir.");
            peps = reconstruido.Valor;
        }

        return new ValorDeProducto(h.ProductId, h.ScopeWarehouseId, h.AccountingGroupId, fecha, promedio, peps, null);
    }

    private static ValorDeProducto SinCalcular(HistoriaParaValorizar h, DateOnly fecha, string nota) =>
        new(h.ProductId, h.ScopeWarehouseId, h.AccountingGroupId, fecha, null, null, nota);

    private static decimal ReconstruirPromedio(IReadOnlyList<MovimientoAValorizar> lineas, RedondeoDeMontos montos)
    {
        var cantidad = 0m;
        var valor = 0m;
        var ultimo = 0m;
        var entradas = lineas.Where(l => l.Clase == ClaseAValorizar.Entrada).ToDictionary(l => l.EntryId, l => l.QuantityBase);

        foreach (var l in lineas)
        {
            switch (l.Clase)
            {
                case ClaseAValorizar.Entrada:
                    cantidad += l.QuantityBase;
                    valor += l.TotalCost;
                    ultimo = l.UnitCost;
                    break;
                case ClaseAValorizar.Salida:
                    var sale = -l.QuantityBase;
                    if (cantidad > 0m && sale >= cantidad)
                        valor = -Redondeo.Monto((sale - cantidad) * ultimo, montos);
                    else if (cantidad > 0m)
                        valor -= Redondeo.Monto(sale * Redondeo.CostoUnitario(valor / cantidad), montos);
                    else
                        valor -= Redondeo.Monto(sale * ultimo, montos);
                    cantidad -= sale;
                    break;
                case ClaseAValorizar.AjusteSobreEntrada:
                    var deLaEntrada = l.AffectsEntryId is { } id && entradas.TryGetValue(id, out var q) ? q : 0m;
                    var proporcion = cantidad <= 0m || deLaEntrada <= 0m ? 0m : Math.Min(1m, cantidad / deLaEntrada);
                    valor += Redondeo.Monto(l.TotalCost * proporcion, montos);
                    break;
            }
        }
        return valor;
    }

    private static (decimal Valor, DateOnly? Negativo) ReconstruirPeps(IReadOnlyList<MovimientoAValorizar> lineas, RedondeoDeMontos montos)
    {
        var capas = new List<(long Entrada, decimal Original, decimal Restante, decimal Valor)>();

        foreach (var l in lineas)
        {
            switch (l.Clase)
            {
                case ClaseAValorizar.Entrada:
                    capas.Add((l.EntryId, l.QuantityBase, l.QuantityBase, l.TotalCost));
                    break;
                case ClaseAValorizar.Salida:
                    var porCubrir = -l.QuantityBase;
                    while (porCubrir > 0m && capas.Count > 0)
                    {
                        var capa = capas[0];
                        var tomada = Math.Min(porCubrir, capa.Restante);
                        if (tomada == capa.Restante) capas.RemoveAt(0);
                        else capas[0] = capa with
                        {
                            Restante = capa.Restante - tomada,
                            Valor = capa.Valor - Redondeo.Monto(capa.Valor * tomada / capa.Restante, montos),
                        };
                        porCubrir -= tomada;
                    }
                    if (porCubrir > 0m) return (0m, l.OperationDate);
                    break;
                case ClaseAValorizar.AjusteSobreEntrada:
                    var i = capas.FindIndex(c => c.Entrada == l.AffectsEntryId);
                    if (i >= 0)
                    {
                        var capa = capas[i];
                        capas[i] = capa with { Valor = capa.Valor + Redondeo.Monto(l.TotalCost * capa.Restante / capa.Original, montos) };
                    }
                    break;
            }
        }
        return (capas.Sum(c => c.Valor), null);
    }
}
