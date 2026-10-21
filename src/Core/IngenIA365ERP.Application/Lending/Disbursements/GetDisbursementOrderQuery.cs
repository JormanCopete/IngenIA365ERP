using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>
/// Una orden de giro con la cuenta completa (feature 013, US3, T0445; api.md §9.2, <c>GET /api/lending/disbursement-orders/{id}</c>,
/// <c>Lending.Loans.Read</c>): fuera del alcance de oficina y línea de su crédito, el mismo 404 que si no existiera (<c>AlcanceDeCartera</c>,
/// <c>Lending.Access.OutOfScope</c>). (nuevo)
/// </summary>
public sealed record GetDisbursementOrderQuery(Guid OrderPublicId) : IRequest<Result<DisbursementOrderDto>>;

public sealed class GetDisbursementOrderQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<GetDisbursementOrderQuery, Result<DisbursementOrderDto>>
{
    public async Task<Result<DisbursementOrderDto>> Handle(GetDisbursementOrderQuery request, CancellationToken ct)
    {
        var orden = await db.DisbursementOrders.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == request.OrderPublicId && !o.IsDeleted, ct);
        if (orden is null) return Result.Failure<DisbursementOrderDto>(ErroresDeDesembolso.OrdenInexistente());
        var credito = await db.Loans.AsNoTracking().Where(l => l.Id == orden.LoanId).Select(l => new { l.BranchId, l.CreditLineId }).FirstAsync(ct);
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeDesembolso.OrdenInexistente(), auditoria,
            "DisbursementOrder", orden.PublicId, ct);
        if (dentro.IsFailure) return Result.Failure<DisbursementOrderDto>(dentro.Error);
        return Result.Success((await ConsultasDeOrdenesDeGiro.DtosAsync(db, [orden], cuentaCompleta: true, ct))[0]);
    }
}
