using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Loans;

/// <summary>
/// <c>GET /api/lending/loans/{id}/schedule?version=</c> (feature 013, US4, T0504; api.md §8.1, <c>Lending.Loans.Read</c>): una versión del plan con sus
/// cuotas; sin versión, la vigente (la mayor). <c>Lending.Schedule.NotFound</c> si la versión no existe. (nuevo)
/// </summary>
public sealed record GetRepaymentScheduleQuery(Guid LoanPublicId, int? Version = null) : IRequest<Result<RepaymentScheduleDto>>;

public sealed class GetRepaymentScheduleQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<GetRepaymentScheduleQuery, Result<RepaymentScheduleDto>>
{
    public const string CodigoVersionInexistente = "Lending.Schedule.NotFound";

    public async Task<Result<RepaymentScheduleDto>> Handle(GetRepaymentScheduleQuery request, CancellationToken ct)
    {
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Result.Failure<RepaymentScheduleDto>(leido.Error);
        var versiones = db.RepaymentScheduleVersions.AsNoTracking().Where(v => v.LoanId == leido.Value.Id);
        var v = request.Version is { } numero
            ? await versiones.FirstOrDefaultAsync(x => x.VersionNumber == numero, ct)
            : await versiones.OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
        if (v is null) return Result.Failure<RepaymentScheduleDto>(new Error(CodigoVersionInexistente, "Esa versión del plan no existe."));
        var causa = await db.LoanTermsVersions.AsNoTracking().Where(t => t.Id == v.TermsVersionId).Select(t => t.Cause.ToString()).FirstOrDefaultAsync(ct);
        var cuotas = await db.RepaymentInstallments.AsNoTracking().Where(i => i.ScheduleVersionId == v.Id).OrderBy(i => i.InstallmentNumber)
            .Select(i => new ScheduleInstallmentDto(i.InstallmentNumber, i.PeriodStart, i.DueDate, i.ExigibleDate, i.OpeningPrincipal, i.Principal, i.Interest,
                i.GraceInterest, i.Insurance, i.OtherCharges, i.Total, i.ClosingPrincipal, i.IsExtraordinary, i.IsGraceInstallment))
            .ToListAsync(ct);
        return Result.Success(new RepaymentScheduleDto(v.VersionNumber, v.EffectiveValueDate, causa ?? string.Empty, v.RulesVersion,
            v.FirstRegeneratedInstallment, v.PrincipalBase, cuotas));
    }
}
