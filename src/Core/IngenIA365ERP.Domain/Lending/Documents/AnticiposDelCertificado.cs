using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Lending.Documents;

/// <summary>Lo que el anticipo cambia en los intereses pagados de un año: lo que suma (aplicado) y lo que resta (usado en un pago), por clase.</summary>
public sealed record AjustePorAnticipo(decimal CorrienteAplicado, decimal MoraAplicado, decimal CorrienteExcluido, decimal MoraExcluido)
{
    public static AjustePorAnticipo Cero { get; } = new(0m, 0m, 0m, 0m);

    public decimal Aplicado => CorrienteAplicado + MoraAplicado;

    public decimal Excluido => CorrienteExcluido + MoraExcluido;

    /// <summary>
    /// Decisión del dueño del 2026-10-18: el anticipo que <b>ingresó en el año</b> y al 31 de diciembre sigue sin aplicar —el que se aplicará a
    /// intereses (y a lo demás de la cuota) el año siguiente—. El certificado del año lo muestra como renglón «anticipo recibido»; en el del año
    /// siguiente el interés que cubra no cuenta, porque no ingresó en ese año.
    /// </summary>
    public decimal RecibidoPendiente { get; init; }
}

/// <summary>
/// El anticipo en el certificado tributario (decisión del dueño del 2026-10-16): el interés que cubre un anticipo cuenta como pagado en el año
/// <b>sólo si el anticipo ingresó en ese año</b> —la fecha valor del pago que lo dejó—; si ingresó antes, no cuenta aunque se aplique dentro.
/// <list type="bullet">
/// <item>el anticipo aplicado a una cuota en su vencimiento (<see cref="EstadoDelCredito.AnticiposAplicados"/>, filas <c>AdvanceApplied</c>) con
/// fecha en el año <b>suma</b> el interés que cubrió el dinero ingresado en el año;</item>
/// <item>el anticipo que usa un pago (la cancelación, D7) ya está dentro de las líneas <c>PaymentAllocation</c> del pago, que el certificado suma
/// enteras; de ellas se <b>resta</b> el interés que cubrió el anticipo ingresado antes del año.</item>
/// </list>
/// El pago que dejó el anticipo no suma ese interés: sus líneas <c>PaymentAllocation</c> no llevan el anticipo (va en <c>AdvanceReceived</c>), así
/// que nada se cuenta dos veces. Para saber de qué ingreso sale cada peso, el anticipo se sigue por lotes con su fecha de ingreso, por cubeta (el de
/// cada cuota y el general), y se consume del más antiguo al más nuevo; dentro de una aplicación el dinero cubre los conceptos en el orden de la
/// prelación (las líneas, en su orden), primero el anticipo y luego el efectivo del pago. Lo que pasa del anticipo de una cuota al general conserva su
/// fecha. Un anticipo que entra por otra vía (saldo migrado, traslado de una novación) toma la fecha de su fila. Puro. (nuevo)
/// </summary>
public static class AnticiposDelCertificado
{
    private const int General = 0;

    private sealed class Lote(DateOnly? ingreso, decimal monto)
    {
        public DateOnly? Ingreso { get; } = ingreso;

        public decimal Monto { get; set; } = monto;
    }

    private readonly record struct Porcion(DateOnly? Ingreso, decimal Monto, bool EsAnticipo);

    private readonly record struct Evento(DateOnly Dia, int Prioridad, int Orden, Action Hacer);

    /// <summary>El ajuste del año <paramref name="año"/> para el crédito reconstruido al 31 de diciembre.</summary>
    public static AjustePorAnticipo Calcular(EstadoDelCredito estado, short año, IReadOnlySet<LoanConcept> corrientes, IReadOnlySet<LoanConcept> deMora)
    {
        ArgumentNullException.ThrowIfNull(estado);
        var movimientos = estado.Libro.Movimientos;
        if (estado.AnticiposAplicados.Count == 0 && !movimientos.Any(m => m.Concepto == LoanConcept.Advance)) return AjustePorAnticipo.Cero;

        var cubetas = new Dictionary<int, List<Lote>>();
        decimal corrienteAplicado = 0m, moraAplicado = 0m, corrienteExcluido = 0m, moraExcluido = 0m;

        var primeraFila = new Dictionary<string, int>(StringComparer.Ordinal);
        var conPagoContado = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < movimientos.Count; i++)
        {
            if (movimientos[i].OperacionId is not { } op) continue;
            primeraFila.TryAdd(op, i);
            if (CertificadoTributario.ClasesDePago.Contains(movimientos[i].Clase)) conPagoContado.Add(op);
        }

        var eventos = new List<Evento>();
        var n = 0;
        foreach (var aplicacion in estado.AnticiposAplicados)
        {
            eventos.Add(new Evento(aplicacion.Fecha, 0, n++, () =>
            {
                var porciones = aplicacion.Usado.SelectMany(u => Consumir(cubetas, u.Cuota, u.Monto)).ToList();
                foreach (var (concepto, porcion) in Repartir(aplicacion.Lineas.Select(l => (l.Concepto, l.Monto)), porciones))
                {
                    if (aplicacion.Fecha.Year != año || porcion.Ingreso?.Year != año) continue;
                    if (corrientes.Contains(concepto)) corrienteAplicado += porcion.Monto;
                    else if (deMora.Contains(concepto)) moraAplicado += porcion.Monto;
                }
                if (aplicacion.Liberado > 0m)
                    foreach (var p in Consumir(cubetas, aplicacion.Cuota, aplicacion.Liberado)) Agregar(cubetas, null, p.Ingreso, p.Monto);
            }));
        }
        foreach (var (id, distribucion) in estado.Pagos)
        {
            var orden = primeraFila.TryGetValue(id, out var f) ? f : int.MaxValue;
            var contado = conPagoContado.Contains(id) && distribucion.FechaValor.Year == año;
            eventos.Add(new Evento(distribucion.FechaValor, 1, orden, () =>
            {
                var porciones = distribucion.AnticipoUsado.SelectMany(u => Consumir(cubetas, u.Cuota, u.Monto)).ToList();
                porciones.Add(new Porcion(distribucion.FechaValor, distribucion.Lineas.Sum(l => l.Monto), false));
                foreach (var (concepto, porcion) in Repartir(distribucion.Lineas.Select(l => (l.Concepto, l.Monto)), porciones))
                {
                    if (!contado || !porcion.EsAnticipo || porcion.Ingreso?.Year == año) continue;
                    if (corrientes.Contains(concepto)) corrienteExcluido += porcion.Monto;
                    else if (deMora.Contains(concepto)) moraExcluido += porcion.Monto;
                }
                if (distribucion.Anticipo > 0m) Agregar(cubetas, distribucion.CuotaDelAnticipo, distribucion.FechaValor, distribucion.Anticipo);
            }));
        }
        for (var i = 0; i < movimientos.Count; i++)
        {
            var m = movimientos[i];
            if (m.Concepto != LoanConcept.Advance || m.Monto == 0m) continue;
            if (m.OperacionId is { } op && estado.Pagos.ContainsKey(op)) continue;
            if (m.OperacionId is null && m.Clase is LoanTransactionKind.AdvanceApplied or LoanTransactionKind.AdvanceReceived) continue;
            eventos.Add(new Evento(m.Dia, 1, i, () =>
            {
                if (m.Monto > 0m) Agregar(cubetas, m.Cuota, m.Dia, m.Monto);
                else _ = Consumir(cubetas, m.Cuota, -m.Monto).ToList();
            }));
        }

        foreach (var e in eventos.OrderBy(e => e.Dia).ThenBy(e => e.Prioridad).ThenBy(e => e.Orden)) e.Hacer();
        // Decisión del dueño del 2026-10-18: lo que ingresó en el año y quedó sin aplicar al cierre.
        var pendiente = cubetas.Values.SelectMany(l => l).Where(l => l.Ingreso?.Year == año).Sum(l => l.Monto);
        return new AjustePorAnticipo(corrienteAplicado, moraAplicado, corrienteExcluido, moraExcluido) { RecibidoPendiente = pendiente };
    }

    private static void Agregar(Dictionary<int, List<Lote>> cubetas, int? cuota, DateOnly? ingreso, decimal monto)
    {
        if (monto <= 0m) return;
        var clave = cuota ?? General;
        if (!cubetas.TryGetValue(clave, out var lotes)) cubetas[clave] = lotes = [];
        var i = lotes.FindIndex(l => (l.Ingreso ?? DateOnly.MinValue) > (ingreso ?? DateOnly.MinValue));
        lotes.Insert(i < 0 ? lotes.Count : i, new Lote(ingreso, monto));
    }

    /// <summary>Saca <paramref name="monto"/> de la cubeta, del lote más antiguo al más nuevo; lo que falte sale sin fecha (no se sabe cuándo ingresó).</summary>
    private static IEnumerable<Porcion> Consumir(Dictionary<int, List<Lote>> cubetas, int? cuota, decimal monto)
    {
        var porciones = new List<Porcion>();
        var falta = monto;
        if (cubetas.TryGetValue(cuota ?? General, out var lotes))
        {
            while (falta > 0m && lotes.Count > 0)
            {
                var lote = lotes[0];
                var t = Math.Min(lote.Monto, falta);
                porciones.Add(new Porcion(lote.Ingreso, t, true));
                lote.Monto -= t;
                falta -= t;
                if (lote.Monto == 0m) lotes.RemoveAt(0);
            }
        }
        if (falta > 0m) porciones.Add(new Porcion(null, falta, true));
        return porciones;
    }

    /// <summary>Cubre las líneas, en su orden, con las porciones, en su orden: qué parte de cada concepto salió de cada porción.</summary>
    private static IEnumerable<(LoanConcept Concepto, Porcion Porcion)> Repartir(IEnumerable<(LoanConcept Concepto, decimal Monto)> lineas, List<Porcion> porciones)
    {
        var i = 0;
        var restoDeLaPorcion = porciones.Count > 0 ? porciones[0].Monto : 0m;
        foreach (var (concepto, monto) in lineas)
        {
            var falta = monto;
            while (falta > 0m && i < porciones.Count)
            {
                var t = Math.Min(falta, restoDeLaPorcion);
                if (t > 0m) yield return (concepto, porciones[i] with { Monto = t });
                falta -= t;
                restoDeLaPorcion -= t;
                if (restoDeLaPorcion == 0m && ++i < porciones.Count) restoDeLaPorcion = porciones[i].Monto;
            }
        }
    }
}
