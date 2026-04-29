using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Withdrawals.Queries;

// --- DTOs ---

public record WithdrawalPreviewDto(
    Guid PersonPublicId,
    string PersonName,
    string PersonCode,
    int ActiveLoansCount,
    decimal SavingsBalance,
    decimal ContributionBalance,
    int CdtCount,
    bool CanWithdraw,
    List<string> BlockingReasons);

public record WithdrawalDto(
    Guid PublicId,
    string PersonCode,
    string PersonName,
    DateOnly EntryDate,
    string PriorStatus,
    string CurrentStatus,
    string ReasonDescription,
    int Period);

// --- Preview Query ---

public record GetWithdrawalPreviewQuery(Guid PersonPublicId) : IRequest<Result<WithdrawalPreviewDto>>;

public class GetWithdrawalPreviewQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetWithdrawalPreviewQuery, Result<WithdrawalPreviewDto>>
{
    public async Task<Result<WithdrawalPreviewDto>> Handle(
        GetWithdrawalPreviewQuery request, CancellationToken ct)
    {
        var person = await context.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);

        if (person is null)
            return Result.Failure<WithdrawalPreviewDto>(new Error("Person.NotFound",
                "Persona no encontrada."));

        var personCode = person.LegacyCode ?? person.TaxId;
        var personName = $"{person.FirstName} {person.LastName}";

        var blockingReasons = new List<string>();

        // Check associate status
        var associate = await context.Associates.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PersonId == person.Id && !a.IsDeleted, ct);

        if (associate is null)
        {
            blockingReasons.Add("La persona no es un asociado.");
        }
        else if (associate.Status == "R")
        {
            blockingReasons.Add("El asociado ya se encuentra retirado.");
        }

        // Active loans count
        var activeLoansCount = await context.LoanPortfolios.AsNoTracking()
            .CountAsync(lp => lp.PersonId == person.Id && lp.CurrentBalance > 0 && !lp.IsDeleted, ct);

        if (activeLoansCount > 0)
            blockingReasons.Add($"Tiene {activeLoansCount} credito(s) vigente(s).");

        // Savings balance
        var savingsBalance = await context.DepositEntries.AsNoTracking()
            .Where(de => de.PersonCode == personCode && !de.IsDeleted)
            .SumAsync(de => de.EntryType == "D" ? de.InstallmentAmount : -de.InstallmentAmount, ct);

        // Contribution balance
        var contributionBalance = await context.ContributionReductions.AsNoTracking()
            .Where(cr => cr.PersonCode == personCode && !cr.IsDeleted)
            .SumAsync(cr => cr.OpeningBalance, ct);

        // CDT count
        var cdtCount = await context.Certificates.AsNoTracking()
            .CountAsync(c => c.PersonId == person.Id && c.Status == "V" && !c.IsDeleted, ct);

        if (cdtCount > 0)
            blockingReasons.Add($"Tiene {cdtCount} CDT(s) vigente(s).");

        var canWithdraw = blockingReasons.Count == 0;

        return Result.Success(new WithdrawalPreviewDto(
            person.PublicId,
            personName,
            personCode,
            activeLoansCount,
            savingsBalance,
            contributionBalance,
            cdtCount,
            canWithdraw,
            blockingReasons));
    }
}

// --- List Withdrawals Query ---

public record ListWithdrawalsQuery : IRequest<Result<PagedList<WithdrawalDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
}

public class ListWithdrawalsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListWithdrawalsQuery, Result<PagedList<WithdrawalDto>>>
{
    public async Task<Result<PagedList<WithdrawalDto>>> Handle(
        ListWithdrawalsQuery request, CancellationToken ct)
    {
        var query = context.AssociateWithdrawals.AsNoTracking()
            .Where(w => !w.IsDeleted);

        if (request.DateFrom.HasValue)
            query = query.Where(w => w.EntryDate >= request.DateFrom.Value);
        if (request.DateTo.HasValue)
            query = query.Where(w => w.EntryDate <= request.DateTo.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(w => w.EntryDate)
            .ThenByDescending(w => w.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(w => new
            {
                w.PublicId,
                w.PersonCode,
                w.EntryDate,
                w.PriorStatus,
                w.CurrentStatus,
                w.WithdrawalReasonId,
                w.Period
            })
            .ToListAsync(ct);

        // Batch load person names
        var personCodes = items.Select(i => i.PersonCode).Distinct().ToList();
        var personNames = await context.People.AsNoTracking()
            .Where(p => personCodes.Contains(p.LegacyCode!) || personCodes.Contains(p.TaxId))
            .ToDictionaryAsync(
                p => p.LegacyCode ?? p.TaxId,
                p => p.FirstName + " " + p.LastName,
                ct);

        // Batch load reason descriptions
        var reasonIds = items.Select(i => i.WithdrawalReasonId).Distinct().ToList();
        var reasonDescs = await context.WithdrawalReasons.AsNoTracking()
            .Where(wr => reasonIds.Contains(wr.Id))
            .ToDictionaryAsync(wr => wr.Id, wr => wr.Name, ct);

        var dtos = items.Select(w => new WithdrawalDto(
            w.PublicId,
            w.PersonCode,
            personNames.TryGetValue(w.PersonCode, out var name) ? name : w.PersonCode,
            w.EntryDate,
            w.PriorStatus,
            w.CurrentStatus,
            reasonDescs.TryGetValue(w.WithdrawalReasonId, out var desc) ? desc : "",
            w.Period
        )).ToList();

        return Result.Success(new PagedList<WithdrawalDto>(dtos, totalCount, request.PageNumber, request.PageSize));
    }
}
