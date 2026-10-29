using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Receipts.Cumplimiento;

/// <summary>
/// El medio de la transacción en el reporte a la UIAF (Anexo 2 TRSO, tabla 7.3): el valor del enum es el código del archivo. (nuevo; segunda ronda del 2026-10-27)
/// </summary>
public enum MedioDeTransaccionReportable
{
    /// <summary>Efectivo (billetes y monedas de curso legal).</summary>
    Efectivo = 0,

    /// <summary>Pago o consignación de cheque en ventanilla, sea cual sea el tipo de cheque.</summary>
    Cheque = 1,

    /// <summary>Plataforma virtual (sin intervención del personal de la organización).</summary>
    PlataformaVirtual = 2,
}

/// <summary>
/// Una transacción que puede ir al reporte (feature 015; segunda ronda del 2026-10-27, Anexo 2 TRSO): el titular del producto, quien la realiza, la fecha, el valor
/// —siempre positivo— con su medio y su sentido (el retiro es <see cref="CashTransactionDirection.Out"/>, el depósito <see cref="CashTransactionDirection.In"/>), el
/// documento, de dónde viene, el producto que la vincula con el titular (la clave con que se busca su código, <c>Loan</c>, <c>Account</c>…, y su número) y la oficina.
/// (nuevo)
/// </summary>
public sealed record TransaccionReportable(
    int PersonaId,
    DateOnly Fecha,
    decimal Valor,
    MedioDeTransaccionReportable Medio,
    CashTransactionDirection Sentido,
    string Documento,
    string Origen,
    int? RealizaId = null,
    string? ClaveDeProducto = null,
    string? NumeroDeProducto = null,
    int? SucursalId = null)
{
    /// <summary>Quien realiza la transacción: el titular si no se registró otra persona.</summary>
    public int Realiza => RealizaId ?? PersonaId;
}

/// <summary>
/// Una línea del reporte: <b>una transacción</b>. <see cref="Clase"/> dice por qué entró —por llegar sola al umbral individual o por ser parte de las múltiples del mes
/// de su titular—; el archivo no lo lleva. (nuevo)
/// </summary>
public sealed record LineaDelReporte(CashTransactionReportLineKind Clase, TransaccionReportable Transaccion)
{
    public int PersonaId => Transaccion.PersonaId;

    public decimal Valor => Transaccion.Valor;
}

/// <summary>
/// El armado puro del reporte de transacciones (feature 015, R1, T0390; FR-096, FR-204; R-24; reescrito en la segunda ronda del 2026-10-27 al Anexo 2 TRSO de la
/// Supersolidaria): primero las <b>múltiples</b> —las transacciones del mes de un mismo titular que en su conjunto llegan al umbral de múltiples—, y de ésas se reporta
/// <b>cada operación por separado</b>; después las <b>individuales</b> que llegan al umbral y no entraron como múltiples. Ninguna transacción aparece dos veces. Sólo
/// cuentan los medios del alcance (por defecto efectivo, cheque y plataforma virtual). Los umbrales llegan de los parámetros <c>Sarlaft.*</c> con su vigencia (nunca un
/// literal). Las anuladas no llegan (las descarta quien llama). Con <c>Suspendido</c> el reporte no lleva líneas. (nuevo)
/// </summary>
public static class ReporteDeTransacciones
{
    /// <summary>Los medios que entran con cada alcance.</summary>
    public static IReadOnlySet<MedioDeTransaccionReportable> Medios(CashTransactionReportScope alcance) => alcance switch
    {
        CashTransactionReportScope.CashOnly => new HashSet<MedioDeTransaccionReportable> { MedioDeTransaccionReportable.Efectivo },
        CashTransactionReportScope.CashAndCheck => new HashSet<MedioDeTransaccionReportable> { MedioDeTransaccionReportable.Efectivo, MedioDeTransaccionReportable.Cheque },
        _ => new HashSet<MedioDeTransaccionReportable>
            { MedioDeTransaccionReportable.Efectivo, MedioDeTransaccionReportable.Cheque, MedioDeTransaccionReportable.PlataformaVirtual },
    };

    public static IReadOnlyList<LineaDelReporte> Armar(IReadOnlyList<TransaccionReportable> transacciones, CashTransactionReportScope alcance, decimal umbralIndividual,
        decimal umbralMultiples, CashTransactionReportApplication estado)
    {
        ArgumentNullException.ThrowIfNull(transacciones);
        if (estado == CashTransactionReportApplication.Suspended) return [];
        var medios = Medios(alcance);
        var candidatas = transacciones.Where(t => t.Valor > 0m && medios.Contains(t.Medio)).ToList();
        var lineas = new List<LineaDelReporte>();
        var enMultiples = new HashSet<int>();
        if (umbralMultiples > 0m)
            foreach (var g in candidatas.GroupBy(t => t.PersonaId).Where(g => g.Sum(t => t.Valor) >= umbralMultiples))
            {
                enMultiples.Add(g.Key);
                lineas.AddRange(g.Select(t => new LineaDelReporte(CashTransactionReportLineKind.Multiple, t)));
            }
        if (umbralIndividual > 0m)
            lineas.AddRange(candidatas.Where(t => !enMultiples.Contains(t.PersonaId) && t.Valor >= umbralIndividual)
                .Select(t => new LineaDelReporte(CashTransactionReportLineKind.Individual, t)));
        return lineas.OrderBy(l => l.Transaccion.Fecha).ThenBy(l => l.Transaccion.Documento, StringComparer.Ordinal).ThenBy(l => l.Transaccion.Medio)
            .ThenBy(l => l.Transaccion.Sentido).ThenBy(l => l.PersonaId).ToList();
    }
}
