using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payments.Queries;

/// <summary>Lo que el reverso restituye. (nuevo)</summary>
public sealed record ReversalByConceptDto(IReadOnlyDictionary<string, decimal> ByConcept);

/// <summary>Un pago posterior redistribuido: lo que cubría antes y después. (nuevo)</summary>
public sealed record ReversalReallocationDto(Guid OperationPublicId, string Number, IReadOnlyList<PaymentDistributionLineDto> Before,
    IReadOnlyList<PaymentDistributionLineDto> After);

/// <summary>Un documento que el reverso anularía (vacío hasta US13). (nuevo)</summary>
public sealed record DocumentToVoidDto(Guid PublicId, string Kind, string Number);

/// <summary>La vista previa del reverso (api.md §10). (nuevo)</summary>
public sealed record ReversalPreviewDto(
    string PreviewSha256,
    ReversalByConceptDto Reversal,
    IReadOnlyList<ReversalReallocationDto> Reallocations,
    IReadOnlyList<DocumentToVoidDto> DocumentsToVoid,
    string? RatingNow,
    string? RatingAfter,
    bool NotifiesCollector,
    bool Reopens,
    IReadOnlyList<PaymentDistributionLineDto> Adjustments);

/// <summary>
/// <c>POST /api/lending/loans/{id}/operations/{operationId}/reversal-preview</c> (feature 013, US5, T0553; api.md §10; <c>Lending.Payments.Reverse</c>,
/// sin clave): lo que haría el reverso del pago, sin escribir —su huella (<c>previewSha256</c>), lo que restituye por concepto, las reasignaciones
/// antes/después de cada pago posterior (FR-081), los documentos que se anularían (vacío hasta US13), la calificación informativa (nula hasta US9) y
/// si se avisa al recaudador—. (nuevo)
/// </summary>
public sealed record PreviewLoanPaymentReversalQuery(Guid LoanPublicId, Guid OperationPublicId, string? Reason = null) : IRequest<Result<ReversalPreviewDto>>;

public sealed class PreviewLoanPaymentReversalQueryHandler(
    IApplicationDbContext db,
    ReversionDePago reversion,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria) : IRequestHandler<PreviewLoanPaymentReversalQuery, Result<ReversalPreviewDto>>
{
    public async Task<Result<ReversalPreviewDto>> Handle(PreviewLoanPaymentReversalQuery request, CancellationToken ct)
    {
        var pago = await PagoDeLaPantalla.LeerAsync(db, alcance, auditoria, request.LoanPublicId, request.OperationPublicId, ct);
        if (pago.IsFailure) return Result.Failure<ReversalPreviewDto>(pago.Error);
        var vista = await reversion.PrevisualizarAsync(pago.Value.Pago, ct);
        if (vista.IsFailure) return Result.Failure<ReversalPreviewDto>(vista.Error);
        return Result.Success(Dto(vista.Value));
    }

    public static ReversalPreviewDto Dto(VistaDelReverso v) => new(
        v.Huella,
        new ReversalByConceptDto(v.PorConcepto.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)),
        v.Redistribuidos.Select(r => new ReversalReallocationDto(r.OperationPublicId, r.Number, Lineas(r.Antes), Lineas(r.Despues))).ToList(),
        v.DocumentosAAnular.Select(d => new DocumentToVoidDto(d.PublicId, d.Kind, d.Number)).ToList(),
        null,
        null,
        v.AvisaAlRecaudador,
        v.Reabre,
        Lineas(v.Ajustes));

    private static IReadOnlyList<PaymentDistributionLineDto> Lineas(IReadOnlyList<LineaDeDistribucionDto> lineas) =>
        lineas.Select(l => new PaymentDistributionLineDto(l.InstallmentNumber, l.Concept.ToString(), l.Amount)).ToList();
}

/// <summary>
/// El pago que la pantalla de Cartera reversa (T0553): el crédito en el alcance, la operación del crédito, un pago y no un reverso
/// (<c>Lending.Reversal.OfReversal</c>, <c>Lending.LoanOperation.NotPayment</c>) y que no sea de un recaudador que reversa lo suyo: Nómina, Aportes
/// (hasta que declare su destino, C37) y la recogida de un desembolso (<c>Lending.Reversal.OwnedByCollector</c> con <c>data.collectorClass</c>). (nuevo)
/// </summary>
public static class PagoDeLaPantalla
{
    public static async Task<Result<(Loan Credito, LoanOperation Pago)>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        Guid loanPublicId, Guid operationPublicId, CancellationToken ct)
    {
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, loanPublicId, ct);
        if (leido.IsFailure) return Result.Failure<(Loan, LoanOperation)>(leido.Error);
        var pago = await db.LoanOperations.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == operationPublicId && o.LoanId == leido.Value.Id, ct);
        if (pago is null) return Result.Failure<(Loan, LoanOperation)>(new Error(GetLoanOperationQueryHandler.CodigoInexistente, "La operación no existe en este crédito."));
        if (ReversionDePago.Reversable(pago) is { } error) return Result.Failure<(Loan, LoanOperation)>(error);
        // E244 (US10-4, FR-077): el pago de un cruce al retiro o de un débito automático es de la clase OwnLoanPayoff y se reversa desde el crédito —el reverso
        // arrastra el débito de la cuenta (IArrastreDeReversoDePago)—; la recogida de un desembolso, que es de la misma clase, no.
        var deCuentas = pago.CollectorClass == nameof(ClaseDeRecaudador.OwnLoanPayoff)
                        && await db.MemberAccountOperations.AsNoTracking().AnyAsync(o => o.RelatedLoanOperationId == pago.Id && o.ReversesOperationId == null
                            && ((o.MembershipWithdrawalId != null && o.Kind == MemberAccountOperationKind.InternalOffset) || o.Kind == MemberAccountOperationKind.DirectDebit), ct);
        if (!deCuentas && pago.CollectorClass is nameof(ClaseDeRecaudador.Payroll) or nameof(ClaseDeRecaudador.Contributions) or nameof(ClaseDeRecaudador.OwnLoanPayoff))
            return Result.Failure<(Loan, LoanOperation)>(ErroresDePagos.DelRecaudador(pago.CollectorClass));
        return Result.Success((leido.Value, pago));
    }
}
