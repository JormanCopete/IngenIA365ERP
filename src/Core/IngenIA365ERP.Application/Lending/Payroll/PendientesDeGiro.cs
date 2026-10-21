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

/// <summary>
/// Lo «descontado pendiente de giro» de las cuotas de cuentas (feature 014, T091; decisión del dueño del 2026-10-18, D36; contrato-de-cuentas §6.4): un
/// resultado de cuenta con algo descontado, que ningún resultado posterior reemplaza y que ninguna consignación vigente —no reversada— gira
/// (<c>LND_MemberAccountOperations.PayrollDeductionAccountResultId</c>). Derivado, como el de los créditos. (nuevo)
/// </summary>
public static class PendientesDeGiroDeCuentas
{
    public static IQueryable<PayrollDeductionAccountResult> Consulta(IApplicationDbContext db) =>
        db.PayrollDeductionAccountResults.Where(r => !r.IsDeleted
            && (r.Outcome == PayrollResultOutcome.Deducted || r.Outcome == PayrollResultOutcome.PartiallyDeducted) && r.DeductedAmount > 0m
            && !db.PayrollDeductionAccountResults.Any(x => x.SupersedesResultId == r.Id && !x.IsDeleted)
            && !db.MemberAccountOperations.Any(o => o.PayrollDeductionAccountResultId == r.Id && o.ReversedByOperationId == null));

    /// <summary>El pendiente de giro de la línea <paramref name="lineaPublicId"/> del archivo, si es de la cuenta <paramref name="cuentaId"/>.</summary>
    public static async Task<PayrollDeductionAccountResult?> DeLineaAsync(IApplicationDbContext db, int cuentaId, Guid lineaPublicId, CancellationToken ct)
    {
        var linea = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            db.PayrollDeductionFileAccountLines.Where(l => l.PublicId == lineaPublicId && l.MemberAccountId == cuentaId).Select(l => (int?)l.Id), ct);
        if (linea is null) return null;
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            Consulta(db).Where(r => r.PayrollDeductionFileAccountLineId == linea.Value).OrderByDescending(r => r.Id), ct);
    }
}
