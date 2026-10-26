using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Treasury.Firmas;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Batches;

/// <summary>
/// La combinación de firmas de un lote (feature 015, T2, fase 42; FR-208; pedido del dueño para T2): al completarse la aprobación del lote, los que aprobaron —las
/// decisiones «aprobar» de su solicitud, las guardadas y la de esta unidad de trabajo— se comparan con los firmantes vigentes de la cuenta de origen por
/// <see cref="CombinacionDeFirmas"/>. Sin firmantes registrados en la cuenta no se exige nada. (nuevo)
/// </summary>
public static class FirmasDelLote
{
    public static async Task<VeredictoDeFirmas> EvaluarAsync(IApplicationDbContext db, PaymentBatch lote, ApprovalRequest solicitud, DateOnly hoy, CancellationToken ct)
    {
        var firmantes = (await db.BankAccountSigners.AsNoTracking().Where(s => s.BankAccountId == lote.BankAccountId && s.UserId != null).ToListAsync(ct))
            .Where(s => s.Vigente(hoy))
            .Select(s => new FirmanteVigente(s.UserId!.Value, s.SignatureClass, s.MaxAmountAlone, s.CanSignJointly)).ToList();
        var guardadas = await db.ApprovalDecisions.AsNoTracking().Where(d => d.RequestId == solicitud.Id && d.Decision == ApprovalDecisionKind.Approve)
            .Select(d => d.DecidedByUserId).ToListAsync(ct);
        var enCurso = db.ApprovalDecisions.Local.Where(d => d.RequestId == solicitud.Id && d.Decision == ApprovalDecisionKind.Approve).Select(d => d.DecidedByUserId);
        return CombinacionDeFirmas.Evaluar(lote.Total, firmantes, guardadas.Concat(enCurso).Distinct().ToList());
    }
}

/// <summary>Los errores de los lotes de T2 (feature 015, fase 42; nuevos, DT §2.7). (nuevo)</summary>
public static class ErroresDeTesoreriaT2Lotes
{
    public static Error CombinacionDeFirmas(string motivo) => new("Treasury.Batch.SignatureCombinationNotMet",
        $"{motivo} Pida la aprobación de los firmantes de la cuenta (Tesorería → Firmantes y tokens).");
}
