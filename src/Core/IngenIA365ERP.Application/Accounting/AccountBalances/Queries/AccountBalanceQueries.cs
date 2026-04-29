using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.AccountBalances.Queries;

// --- DTOs ---

public record BalanceDto(
    Guid AccountPublicId,
    string AccountCode,
    string AccountName,
    byte Level,
    string Nature,
    decimal PreviousBalance,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal NewBalance);

// --- Get Account Balances ---

public record GetAccountBalancesQuery : IRequest<Result<List<BalanceDto>>>
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string? AccountCodeFrom { get; init; }
    public string? AccountCodeTo { get; init; }
    public Guid? BranchPublicId { get; init; }
    public Guid? CostCenterPublicId { get; init; }
}

public class GetAccountBalancesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAccountBalancesQuery, Result<List<BalanceDto>>>
{
    public async Task<Result<List<BalanceDto>>> Handle(
        GetAccountBalancesQuery request, CancellationToken ct)
    {
        // Resolve optional FK filters
        int? branchId = null;
        int? costCenterId = null;

        if (request.BranchPublicId.HasValue)
        {
            var branch = await context.Branches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.PublicId == request.BranchPublicId.Value && !b.IsDeleted, ct);
            if (branch is not null) branchId = branch.Id;
        }
        if (request.CostCenterPublicId.HasValue)
        {
            var cc = await context.CostCenters.AsNoTracking()
                .FirstOrDefaultAsync(c => c.PublicId == request.CostCenterPublicId.Value && !c.IsDeleted, ct);
            if (cc is not null) costCenterId = cc.Id;
        }

        // Get accounts within range
        var accountsQuery = context.ChartOfAccounts
            .AsNoTracking()
            .Where(a => !a.IsDeleted);

        if (!string.IsNullOrEmpty(request.AccountCodeFrom))
            accountsQuery = accountsQuery.Where(a => string.Compare(a.AccountCode, request.AccountCodeFrom) >= 0);
        if (!string.IsNullOrEmpty(request.AccountCodeTo))
            accountsQuery = accountsQuery.Where(a => string.Compare(a.AccountCode, request.AccountCodeTo) <= 0);

        var accounts = await accountsQuery
            .OrderBy(a => a.AccountCode)
            .ToListAsync(ct);

        if (accounts.Count == 0)
            return Result.Success(new List<BalanceDto>());

        var accountIds = accounts.Select(a => a.Id).ToList();

        // Get current month balances (grouped by account)
        var currentBalancesQuery = context.AccountBalances.AsNoTracking()
            .Where(b => accountIds.Contains(b.AccountId)
                     && b.PeriodYear == request.Year
                     && b.PeriodMonth == (byte)request.Month);

        if (branchId.HasValue)
            currentBalancesQuery = currentBalancesQuery.Where(b => b.BranchId == branchId.Value);
        if (costCenterId.HasValue)
            currentBalancesQuery = currentBalancesQuery.Where(b => b.CostCenterId == costCenterId.Value);

        var currentBalances = await currentBalancesQuery
            .GroupBy(b => b.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                DebitAmount = g.Sum(b => b.DebitAmount),
                CreditAmount = g.Sum(b => b.CreditAmount)
            })
            .ToDictionaryAsync(x => x.AccountId, ct);

        // Get previous months balances (sum all months before current in same year + all prior years)
        var previousBalancesQuery = context.AccountBalances.AsNoTracking()
            .Where(b => accountIds.Contains(b.AccountId)
                     && ((b.PeriodYear == request.Year && b.PeriodMonth < (byte)request.Month)
                         || b.PeriodYear < request.Year));

        if (branchId.HasValue)
            previousBalancesQuery = previousBalancesQuery.Where(b => b.BranchId == branchId.Value);
        if (costCenterId.HasValue)
            previousBalancesQuery = previousBalancesQuery.Where(b => b.CostCenterId == costCenterId.Value);

        var previousBalances = await previousBalancesQuery
            .GroupBy(b => b.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                DebitAmount = g.Sum(b => b.DebitAmount),
                CreditAmount = g.Sum(b => b.CreditAmount)
            })
            .ToDictionaryAsync(x => x.AccountId, ct);

        // Build result
        var result = new List<BalanceDto>();
        foreach (var account in accounts)
        {
            // Calculate previous balance based on account nature
            decimal prevDebit = 0, prevCredit = 0;
            if (previousBalances.TryGetValue(account.Id, out var prev))
            {
                prevDebit = prev.DebitAmount;
                prevCredit = prev.CreditAmount;
            }
            var previousBalance = account.Nature == "D"
                ? prevDebit - prevCredit
                : prevCredit - prevDebit;

            // Current month amounts
            decimal curDebit = 0, curCredit = 0;
            if (currentBalances.TryGetValue(account.Id, out var cur))
            {
                curDebit = cur.DebitAmount;
                curCredit = cur.CreditAmount;
            }

            // New balance = previous + current movement
            var currentMovement = account.Nature == "D"
                ? curDebit - curCredit
                : curCredit - curDebit;
            var newBalance = previousBalance + currentMovement;

            // Only include accounts that have any balance activity
            if (prevDebit != 0 || prevCredit != 0 || curDebit != 0 || curCredit != 0)
            {
                result.Add(new BalanceDto(
                    account.PublicId,
                    account.AccountCode,
                    account.Name,
                    account.Level,
                    account.Nature,
                    previousBalance,
                    curDebit,
                    curCredit,
                    newBalance));
            }
        }

        return Result.Success(result);
    }
}
