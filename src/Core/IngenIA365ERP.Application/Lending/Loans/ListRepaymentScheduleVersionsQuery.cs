using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Settlement;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Loans;

/// <summary>
/// <c>GET /api/lending/loans/{id}/schedule/versions</c> (feature 013, US4, T0504; api.md §8.1, <c>Lending.Loans.Read</c>): las versiones del plan con su
/// causa (la de la versión de condiciones), vigencia, versión de reglas y desde qué cuota regeneraron. (nuevo)
/// </summary>
public sealed record ListRepaymentScheduleVersionsQuery(Guid LoanPublicId) : IRequest<Result<IReadOnlyList<StatementScheduleVersionDto>>>;

public sealed class ListRepaymentScheduleVersionsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<ListRepaymentScheduleVersionsQuery, Result<IReadOnlyList<StatementScheduleVersionDto>>>
{
    public async Task<Result<IReadOnlyList<StatementScheduleVersionDto>>> Handle(ListRepaymentScheduleVersionsQuery request, CancellationToken ct)
    {
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Result.Failure<IReadOnlyList<StatementScheduleVersionDto>>(leido.Error);
        var versiones = await (from v in db.RepaymentScheduleVersions.AsNoTracking()
                               join t in db.LoanTermsVersions.AsNoTracking() on v.TermsVersionId equals t.Id
                               where v.LoanId == leido.Value.Id
                               orderby v.VersionNumber
                               select new { v.VersionNumber, v.EffectiveValueDate, t.Cause, v.RulesVersion, v.InstallmentCount, v.FirstRegeneratedInstallment })
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<StatementScheduleVersionDto>>(versiones.Select(v => new StatementScheduleVersionDto(v.VersionNumber,
            v.EffectiveValueDate, v.Cause.ToString(), v.RulesVersion, v.InstallmentCount, v.FirstRegeneratedInstallment)).ToList());
    }
}
