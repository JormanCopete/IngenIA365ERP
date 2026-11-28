using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Payroll;

/// <summary>
/// Lo «descontado pendiente de giro» (feature 013, K2; data-model §24.8; FR-114): un resultado <c>Deducted</c> o <c>PartiallyDeducted</c> con algo
/// descontado, que ningún resultado posterior reemplaza (<c>SupersedesResultId</c>) y que ningún pago vigente —no reversado— gira
/// (<c>LND_LoanOperations.PayrollDeductionResultId</c>). Es derivado: no hay columna de estado. Lo usan el contrato (exige y valida la referencia),
/// la reconstrucción (la cuota no cuenta para la mora dentro del umbral), la tarea de giros, las consultas y el cuadre. (nuevo)
/// <para>
/// 2026-11-27 (feature 028, E2, T068; R7, FR-022, H1): lo descontado de un <b>crédito cerrado</b> (<see cref="CreditoCerrado"/>: cancelado —por ejemplo, lo
/// recogió el desembolso de otro—, anulado o novado) ya no está pendiente de giro para Cartera: no exige referencia, no alerta giro atrasado, no avisa al
/// deudor y no cuenta para centrales. Lo que la pagaduría gire por él lo trata Recaudo con la regla que le da el rechazo del contrato
/// (<c>Recogida.GiroPosteriorDeCreditoRecogido</c>). Se mira el estado del crédito al consultar —no sólo <see cref="PayrollDeductionResult.LoanClosedAtResult"/>,
/// la marca que sella el consumo—: así salen también los resultados de antes de la marca, y si el crédito se reabre (se anula el crédito nuevo que lo recogió
/// y se reversa la recogida) su pendiente vuelve.
/// </para>
/// </summary>
public static class PendientesDeGiro
{
    /// <summary>Los resultados pendientes de giro, como consulta (se compone con el filtro de quien la usa). Sin los de créditos cerrados (T068).</summary>
    public static IQueryable<PayrollDeductionResult> Consulta(IApplicationDbContext db) =>
        db.PayrollDeductionResults.Where(r => !r.IsDeleted
            && (r.Outcome == PayrollResultOutcome.Deducted || r.Outcome == PayrollResultOutcome.PartiallyDeducted) && r.DeductedAmount > 0m
            && !db.PayrollDeductionResults.Any(x => x.SupersedesResultId == r.Id && !x.IsDeleted)
            && !db.LoanOperations.Any(o => o.PayrollDeductionResultId == r.Id && o.ReversedByOperationId == null)
            && !db.Loans.Any(l => l.Id == r.LoanId
                                  && (l.Status == LoanStatus.PaidOff || l.Status == LoanStatus.Voided || l.Status == LoanStatus.Novated)));

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T067, T068; R7): ¿el crédito ya no está vigente para la libranza? Cancelado, anulado o novado —lo que el contrato de
    /// obligaciones no deja pagar—. El castigado no: sigue recibiendo pagos (recuperación) y su descuento se trata como siempre. (nuevo)
    /// </summary>
    public static bool CreditoCerrado(LoanStatus estado) => estado is LoanStatus.PaidOff or LoanStatus.Voided or LoanStatus.Novated;

    /// <summary>
    /// Los días de mora de la pagaduría a <paramref name="fecha"/>: desde el vencimiento de la ventana de giro; cero dentro de ella. Sólo tiene sentido sobre
    /// <see cref="Consulta"/>: lo descontado de un crédito cerrado no tiene mora de la pagaduría (T068).
    /// </summary>
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
