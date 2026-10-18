using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Payroll;

/// <summary>
/// Lo «descontado pendiente de giro» (feature 013, K2; data-model §24.8; FR-114): un resultado <c>Deducted</c> o <c>PartiallyDeducted</c> con algo
/// descontado, que ningún resultado posterior reemplaza (<c>SupersedesResultId</c>) y que ningún pago vigente —no reversado— gira
/// (<c>LND_LoanOperations.PayrollDeductionResultId</c>). Es derivado: no hay columna de estado. Lo usan el contrato (exige y valida la referencia),
/// la reconstrucción (la cuota no cuenta para la mora dentro del umbral), la tarea de giros, las consultas y el cuadre. (nuevo)
/// </summary>
public static class PendientesDeGiro
{
    /// <summary>Los resultados pendientes de giro, como consulta (se compone con el filtro de quien la usa).</summary>
    public static IQueryable<PayrollDeductionResult> Consulta(IApplicationDbContext db) =>
        db.PayrollDeductionResults.Where(r => !r.IsDeleted
            && (r.Outcome == PayrollResultOutcome.Deducted || r.Outcome == PayrollResultOutcome.PartiallyDeducted) && r.DeductedAmount > 0m
            && !db.PayrollDeductionResults.Any(x => x.SupersedesResultId == r.Id && !x.IsDeleted)
            && !db.LoanOperations.Any(o => o.PayrollDeductionResultId == r.Id && o.ReversedByOperationId == null));

    /// <summary>Los días de mora de la pagaduría a <paramref name="fecha"/>: desde el vencimiento de la ventana de giro; cero dentro de ella.</summary>
    public static int DiasDeMora(DateOnly? vencimientoDelGiro, DateOnly fecha) =>
        vencimientoDelGiro is { } v && fecha > v ? fecha.DayNumber - v.DayNumber : 0;

    /// <summary>¿La mora de la pagaduría superó el umbral sellado? Sin umbral (nulo) nunca: la mora de la pagaduría no se imputa (C71).</summary>
    public static bool SuperaElUmbral(DateOnly? vencimientoDelGiro, int? umbral, DateOnly fecha) =>
        umbral is { } u && DiasDeMora(vencimientoDelGiro, fecha) > u;
}
