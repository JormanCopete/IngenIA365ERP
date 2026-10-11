using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Loans;

/// <summary>
/// <c>GET /api/lending/loans/{id}/operations/{operationId}</c> (feature 013, US4, T0504; api.md §8.1, <c>Lending.Loans.Read</c>; FR-077): el
/// <b>comprobante de aplicación</b> de una operación: la operación, su distribución por cuota y concepto (las filas del libro que escribió) y sus
/// mensajes —los que la tienen por origen y los de las partes de la puesta al día que pidió— con el estado de su entrega.
/// <c>Lending.LoanOperation.NotFound</c> si no es del crédito. (nuevo)
/// </summary>
public sealed record GetLoanOperationQuery(Guid LoanPublicId, Guid OperationPublicId) : IRequest<Result<LoanOperationDetailDto>>;

public sealed class GetLoanOperationQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<GetLoanOperationQuery, Result<LoanOperationDetailDto>>
{
    public const string CodigoInexistente = "Lending.LoanOperation.NotFound";

    public async Task<Result<LoanOperationDetailDto>> Handle(GetLoanOperationQuery request, CancellationToken ct)
    {
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Result.Failure<LoanOperationDetailDto>(leido.Error);
        var op = await db.LoanOperations.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == request.OperationPublicId && o.LoanId == leido.Value.Id, ct);
        if (op is null) return Result.Failure<LoanOperationDetailDto>(new Error(CodigoInexistente, "La operación no existe en este crédito."));

        var distribucion = await db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == op.Id && t.LoanId == op.LoanId)
            .GroupBy(t => new { t.InstallmentNumber, t.Concept })
            .Select(g => new { g.Key.InstallmentNumber, g.Key.Concept, Monto = g.Sum(t => t.Amount) })
            .ToListAsync(ct);
        var partes = await db.AccountingFactParts.AsNoTracking().Where(p => p.TriggerOperationId == op.Id).Select(p => p.PublicId).ToListAsync(ct);
        var origenes = partes.Append(op.PublicId).ToList();
        var mensajes = await db.IntegrationMessages.AsNoTracking().Where(m => origenes.Contains(m.OriginPublicId))
            .OrderBy(m => m.Id)
            .Select(m => new
            {
                m.Type,
                m.PublicId,
                Estado = db.IntegrationMessageDeliveries.Where(d => d.MessageId == m.Id).OrderBy(d => d.Id).Select(d => d.Status.ToString()).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var dto = (await OperacionesDelCredito.DtosAsync(db, [op], ct))[0];
        return Result.Success(new LoanOperationDetailDto(dto,
            distribucion.OrderBy(d => d.InstallmentNumber ?? 0).ThenBy(d => (int)d.Concept)
                .Select(d => new OperationDistributionDto(d.InstallmentNumber, d.Concept.ToString(), d.Monto)).ToList(),
            mensajes.Select(m => new OperationMessageDto(m.Type, m.PublicId, m.Estado ?? string.Empty)).ToList()));
    }
}
