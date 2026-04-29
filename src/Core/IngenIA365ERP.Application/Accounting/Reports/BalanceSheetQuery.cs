using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Reports;

// --- DTOs ---

public record BalanceSheetLineDto(
    string AccountCode,
    string AccountName,
    int Level,
    string Nature,
    decimal Balance,
    decimal? PriorYearBalance);

public record BalanceSheetDto(
    int Year,
    int Month,
    List<BalanceSheetLineDto> Assets,
    List<BalanceSheetLineDto> Liabilities,
    List<BalanceSheetLineDto> Equity,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalEquity);

// --- Query ---

public record GetBalanceSheetQuery(
    int Year,
    int Month,
    Guid? BranchPublicId,
    int? ComparisonYear) : IRequest<Result<BalanceSheetDto>>;

public class GetBalanceSheetQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetBalanceSheetQuery, Result<BalanceSheetDto>>
{
    public async Task<Result<BalanceSheetDto>> Handle(GetBalanceSheetQuery request, CancellationToken ct)
    {
        // Load account balances for the period, grouped by account
        var balances = await context.AccountBalances.AsNoTracking()
            .Where(b => b.PeriodYear == (short)request.Year && b.PeriodMonth <= (short)request.Month)
            .GroupBy(b => b.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Debit = g.Sum(x => x.DebitAmount),
                Credit = g.Sum(x => x.CreditAmount)
            })
            .ToListAsync(ct);

        // Load prior year balances if comparison requested
        Dictionary<int, decimal>? priorMap = null;
        if (request.ComparisonYear.HasValue)
        {
            var priorBalances = await context.AccountBalances.AsNoTracking()
                .Where(b => b.PeriodYear == (short)request.ComparisonYear.Value && b.PeriodMonth <= (short)request.Month)
                .GroupBy(b => b.AccountId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    Debit = g.Sum(x => x.DebitAmount),
                    Credit = g.Sum(x => x.CreditAmount)
                })
                .ToListAsync(ct);

            priorMap = priorBalances.ToDictionary(
                b => b.AccountId,
                b => b.Debit - b.Credit);
        }

        var accounts = await context.ChartOfAccounts.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Level <= 4)
            .OrderBy(a => a.AccountCode)
            .ToListAsync(ct);

        var balanceMap = balances.ToDictionary(b => b.AccountId, b => b);

        var assets = new List<BalanceSheetLineDto>();
        var liabilities = new List<BalanceSheetLineDto>();
        var equity = new List<BalanceSheetLineDto>();

        foreach (var acct in accounts)
        {
            balanceMap.TryGetValue(acct.Id, out var bal);
            var balance = (bal?.Debit ?? 0) - (bal?.Credit ?? 0);
            if (acct.Nature == "C") balance = -balance;

            decimal? priorBalance = null;
            if (priorMap is not null)
            {
                priorMap.TryGetValue(acct.Id, out var pb);
                priorBalance = acct.Nature == "C" ? -pb : pb;
            }

            var line = new BalanceSheetLineDto(
                acct.AccountCode, acct.Name, acct.Level, acct.Nature, balance, priorBalance);

            if (acct.AccountCode.StartsWith("1")) assets.Add(line);
            else if (acct.AccountCode.StartsWith("2")) liabilities.Add(line);
            else if (acct.AccountCode.StartsWith("3")) equity.Add(line);
        }

        return Result.Success(new BalanceSheetDto(
            request.Year,
            request.Month,
            assets,
            liabilities,
            equity,
            assets.Where(a => a.Level == 1).Sum(a => a.Balance),
            liabilities.Where(a => a.Level == 1).Sum(a => a.Balance),
            equity.Where(a => a.Level == 1).Sum(a => a.Balance)));
    }
}
