using FluentValidation;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Contributions;

// Feature 014, US3 (T093; api.md §7): las consultas de aportes —el resumen de una persona y las cuotas exigibles—. El resumen es la persona (como
// ListContributionBalancesQuery): su saldo sale de IConsultaDeAportes, el mismo de la Cartera, de la constancia y del extracto (SC-010); las cuotas se
// filtran por la oficina y la línea de la cuenta en el alcance de quien pregunta. (nuevo)

/// <summary>El saldo de aportes por clase. (nuevo)</summary>
public sealed record ContributionsByClassDto(decimal Minimum, decimal Ordinary, decimal Extraordinary, decimal Revalued);

/// <summary>Una cuota vencida con saldo. (nuevo)</summary>
public sealed record ContributionInstallmentInArrearsDto(string AccountNumber, string Period, DateOnly DueDate, decimal Pending);

/// <summary>El resumen de aportes de una persona (api.md §7, <c>GET /summary</c>). (nuevo)</summary>
public sealed record ContributionsSummaryDto(
    MemberAccountPersonDto Person,
    DateOnly Date,
    bool Available,
    ContributionsByClassDto ByClass,
    decimal Total,
    decimal MinimumNonReducible,
    decimal PermanentSavings,
    IReadOnlyList<ContributionInstallmentInArrearsDto> InstallmentsInArrears,
    IReadOnlyList<Guid> AccountPublicIds);

/// <summary>
/// El resumen de aportes de una persona a una fecha (api.md §7, <c>GET /api/lending/contributions/summary</c>, <c>Lending.Accounts.Read</c>): el
/// total, el ahorro permanente y el mínimo irreducible de <see cref="IConsultaDeAportes"/> (el mismo saldo de todo consumidor), el desglose por clase
/// de la suma del libro y las cuotas vencidas con saldo. Sin aportes activados responde <c>Available = false</c> y ceros. Lo que respalda créditos
/// (<c>asCollateralOf</c>) llega con la garantía de aportes (K8). (nuevo)
/// </summary>
public sealed record GetContributionsSummaryQuery(Guid PersonPublicId, DateOnly? Date = null) : IRequest<Result<ContributionsSummaryDto>>;

public sealed class GetContributionsSummaryQueryValidator : AbstractValidator<GetContributionsSummaryQuery>
{
    public GetContributionsSummaryQueryValidator() => RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Elija la persona.");
}

public sealed class GetContributionsSummaryQueryHandler(IApplicationDbContext db, IConsultaDeAportes consulta, IDateTimeService reloj)
    : IRequestHandler<GetContributionsSummaryQuery, Result<ContributionsSummaryDto>>
{
    public async Task<Result<ContributionsSummaryDto>> Handle(GetContributionsSummaryQuery request, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (persona is null) return Result.Failure<ContributionsSummaryDto>(new Error("Lending.Person.NotFound", "La persona no existe."));
        var fecha = request.Date ?? reloj.HoyLocal;
        var nombre = (await LectorDeCuentas.PersonasAsync(db, [persona.Id], ct))[persona.Id];

        var saldos = await consulta.SaldosAsync(new SolicitudDeSaldoDeAportes([persona.PublicId], fecha), ct);
        var saldo = saldos.Balances.FirstOrDefault(b => b.PersonPublicId == persona.PublicId);
        var cuentas = await db.MemberAccounts.AsNoTracking()
            .Where(a => a.HolderPersonId == persona.Id && !a.IsDeleted
                        && (a.Nature == LineNature.Contribution || (a.Nature == LineNature.Deposit && (a.DepositClass == DepositClass.Permanent
                            || a.DepositClass == DepositClass.Contractual))))
            .Select(a => new { a.Id, a.PublicId, a.Number, a.Nature }).ToListAsync(ct);
        var deAportes = cuentas.Where(c => c.Nature == LineNature.Contribution).Select(c => c.Id).ToList();
        var conceptos = MemberAccountTransaction.ConceptosDeAportes.ToArray();
        var porClase = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => deAportes.Contains(t.MemberAccountId) && !t.IsDeleted && t.ValueDate <= fecha && conceptos.Contains(t.Concept))
            .GroupBy(t => t.Concept).Select(g => new { Concepto = g.Key, Total = g.Sum(t => t.Amount) }).ToDictionaryAsync(x => x.Concepto, x => x.Total, ct);
        var numeros = cuentas.ToDictionary(c => c.Id, c => c.Number);
        var enMora = (await LectorDeCuotas.DeCuentasAsync(db, cuentas.Select(c => c.Id).ToList(), ct)).Where(q => q.EnMoraEl(fecha))
            .OrderBy(q => q.Cuota.DueDate).ThenBy(q => numeros[q.Cuota.MemberAccountId], StringComparer.Ordinal)
            .Select(q => new ContributionInstallmentInArrearsDto(numeros[q.Cuota.MemberAccountId], q.Cuota.Periodo, q.Cuota.DueDate, q.Pendiente)).ToList();

        return Result.Success(new ContributionsSummaryDto(nombre, fecha, saldos.Available,
            new ContributionsByClassDto(porClase.GetValueOrDefault(MemberAccountConcept.ContributionMinimum), porClase.GetValueOrDefault(MemberAccountConcept.ContributionOrdinary),
                porClase.GetValueOrDefault(MemberAccountConcept.ContributionExtraordinary), porClase.GetValueOrDefault(MemberAccountConcept.ContributionRevaluation)),
            saldo?.Contributions ?? 0m, saldo?.Minimum ?? 0m, saldo?.PermanentSavings ?? 0m, enMora, cuentas.Select(c => c.PublicId).ToList()));
    }
}

/// <summary>Una cuota exigible de aportes, ahorro permanente o contractual (api.md §7, <c>GET /installments</c>). (nuevo)</summary>
public sealed record AccountInstallmentDto(
    Guid PublicId,
    Guid AccountPublicId,
    string AccountNumber,
    string Nature,
    string Period,
    DateOnly DueDate,
    decimal Amount,
    decimal Paid,
    decimal Pending,
    string Status,
    decimal? ContributionShare,
    decimal? PermanentSavingsShare,
    string? NotDeductedReason);

/// <summary>
/// Las cuotas exigibles (api.md §7, <c>GET /api/lending/contributions/installments</c>, <c>Lending.Accounts.Read</c>) de las cuentas en el alcance de
/// quien pregunta, por persona, estado (<c>Pending</c>, <c>PartiallyPaid</c>, <c>Paid</c>, <c>NotDeducted</c>, <c>Cancelled</c>), período
/// (<c>yyyy-MM</c>) y línea; las anuladas sólo si se piden por estado. En orden de vencimiento. (nuevo)
/// </summary>
public sealed record ListAccountInstallmentsQuery(Guid? PersonPublicId = null, EstadoDeCuotaDeCuenta? Status = null, string? Period = null, string? Line = null,
    int Page = 1, int PageSize = 50) : IRequest<Result<PagedResult<AccountInstallmentDto>>>;

public sealed class ListAccountInstallmentsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance)
    : IRequestHandler<ListAccountInstallmentsQuery, Result<PagedResult<AccountInstallmentDto>>>
{
    public async Task<Result<PagedResult<AccountInstallmentDto>>> Handle(ListAccountInstallmentsQuery request, CancellationToken ct)
    {
        var vigente = await alcance.ObtenerAsync(ct);
        var consulta = db.MemberAccounts.AsNoTracking().Where(a => !a.IsDeleted && (a.Nature == LineNature.Contribution
                || (a.Nature == LineNature.Deposit && (a.DepositClass == DepositClass.Permanent || a.DepositClass == DepositClass.Contractual))))
            .PorOficinaYLinea(vigente, a => a.BranchId, a => a.CreditLineId);
        if (request.PersonPublicId is { } persona) consulta = consulta.Where(a => db.People.Any(p => p.Id == a.HolderPersonId && p.PublicId == persona));
        if (!string.IsNullOrWhiteSpace(request.Line))
        {
            var linea = request.Line.Trim();
            consulta = consulta.Where(a => db.CreditLines.Any(l => l.Id == a.CreditLineId && l.Code == linea));
        }
        var cuentas = await consulta.Select(a => new { a.Id, a.PublicId, a.Number, a.Nature }).ToDictionaryAsync(a => a.Id, ct);

        var cuotas = await LectorDeCuotas.DeCuentasAsync(db, cuentas.Keys.ToList(), ct, incluirAnuladas: request.Status == EstadoDeCuotaDeCuenta.Cancelled);
        var filtradas = cuotas
            .Where(q => request.Status is null || q.Estado == request.Status)
            .Where(q => string.IsNullOrWhiteSpace(request.Period) || q.Cuota.Periodo == request.Period.Trim())
            .OrderBy(q => q.Cuota.DueDate).ThenBy(q => cuentas[q.Cuota.MemberAccountId].Number, StringComparer.Ordinal).ThenBy(q => q.Cuota.Id)
            .ToList();
        var pagina = new PageRequest(request.Page, request.PageSize);
        var items = filtradas.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).Select(q =>
        {
            var c = cuentas[q.Cuota.MemberAccountId];
            return new AccountInstallmentDto(q.Cuota.PublicId, c.PublicId, c.Number, c.Nature.ToString(), q.Cuota.Periodo, q.Cuota.DueDate, q.Cuota.Amount, q.Pagado,
                q.Pendiente, q.Estado.ToString(), q.Cuota.ContributionShare, q.Cuota.PermanentSavingsShare, q.MotivoDeNoDescuento?.ToString());
        }).ToList();
        return Result.Success(new PagedResult<AccountInstallmentDto>(items, pagina.SafePage, pagina.SafePageSize, filtradas.Count));
    }
}
