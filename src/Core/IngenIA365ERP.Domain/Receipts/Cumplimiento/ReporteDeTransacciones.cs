using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Receipts.Cumplimiento;

/// <summary>Una transacción que puede ir al reporte: la persona, la fecha, el efectivo, los otros medios, el documento y de dónde viene. (nuevo)</summary>
public sealed record TransaccionReportable(int PersonaId, DateOnly Fecha, decimal Efectivo, decimal OtrosMedios, string Documento, string Origen);

/// <summary>Una línea del reporte. (nuevo)</summary>
public sealed record LineaDelReporte(CashTransactionReportLineKind Clase, int PersonaId, decimal Valor, int Cantidad, DateOnly? Fecha, string? Documento,
    IReadOnlyList<string> Documentos, string Origen);

/// <summary>
/// El armado puro del reporte de transacciones (feature 015, R1, T0390; FR-096, FR-204; R-24): con el alcance <c>SoloEfectivo</c> cuenta el efectivo y con <c>Todas</c>
/// el efectivo y los otros medios. Una transacción cuyo valor llega al umbral individual es una línea <c>Individual</c>; las de una persona en el mes que suman el umbral
/// de múltiples son una línea <c>Multiple</c> con todas. Los umbrales llegan de los parámetros <c>Sarlaft.*</c> con su vigencia (nunca un literal). Las anuladas no
/// llegan (las descarta quien llama). Con <c>Suspendido</c> el reporte no lleva líneas. (nuevo)
/// </summary>
public static class ReporteDeTransacciones
{
    public static IReadOnlyList<LineaDelReporte> Armar(IReadOnlyList<TransaccionReportable> transacciones, CashTransactionReportScope alcance, decimal umbralIndividual,
        decimal umbralMultiples, CashTransactionReportApplication estado)
    {
        ArgumentNullException.ThrowIfNull(transacciones);
        if (estado == CashTransactionReportApplication.Suspended) return [];
        decimal Valor(TransaccionReportable t) => alcance == CashTransactionReportScope.All ? t.Efectivo + t.OtrosMedios : t.Efectivo;
        var lineas = new List<LineaDelReporte>();
        if (umbralIndividual > 0m)
            lineas.AddRange(transacciones.Where(t => Valor(t) >= umbralIndividual).OrderBy(t => t.Fecha).ThenBy(t => t.Documento, StringComparer.Ordinal)
                .Select(t => new LineaDelReporte(CashTransactionReportLineKind.Individual, t.PersonaId, Valor(t), 1, t.Fecha, t.Documento, [t.Documento], t.Origen)));
        if (umbralMultiples > 0m)
            foreach (var g in transacciones.Where(t => Valor(t) > 0m).GroupBy(t => t.PersonaId).OrderBy(g => g.Key))
            {
                var total = g.Sum(Valor);
                if (total < umbralMultiples) continue;
                var origenes = g.Select(t => t.Origen).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
                lineas.Add(new LineaDelReporte(CashTransactionReportLineKind.Multiple, g.Key, total, g.Count(), null, null,
                    g.OrderBy(t => t.Fecha).Select(t => t.Documento).ToList(), string.Join("+", origenes)));
            }
        return lineas;
    }
}
