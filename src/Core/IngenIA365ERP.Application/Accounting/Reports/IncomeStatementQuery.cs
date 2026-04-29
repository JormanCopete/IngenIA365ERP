using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Reports;

// --- DTOs ---

public record IncomeStatementLineDto(
    string AccountCode,
    string AccountName,
    int Level,
    decimal Amount,
    decimal? PriorYearAmount);

public record IncomeStatementDto(
    int Year,
    int Month,
    List<IncomeStatementLineDto> Income,
    List<IncomeStatementLineDto> Expenses,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetProfit);

// --- Query ---

public record GetIncomeStatementQuery(
    int Year,
    int Month,
    Guid? BranchPublicId,
    int? ComparisonYear) : IRequest<Result<IncomeStatementDto>>;

public class GetIncomeStatementQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetIncomeStatementQuery, Result<IncomeStatementDto>>
{
    public async Task<Result<IncomeStatementDto>> Handle(GetIncomeStatementQuery request, CancellationToken ct)
    {
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

        Dictionary<int, (decimal Debit, decimal Credit)>? priorMap = null;
        if (request.ComparisonYear.HasValue)
        {
            var priorBal = await context.AccountBalances.AsNoTracking()
                .Where(b => b.PeriodYear == (short)request.ComparisonYear.Value && b.PeriodMonth <= (short)request.Month)
                .GroupBy(b => b.AccountId)
                .Select(g => new
                {
                    AccountId = g.Key,
                    Debit = g.Sum(x => x.DebitAmount),
                    Credit = g.Sum(x => x.CreditAmount)
                })
                .ToListAsync(ct);
            priorMap = priorBal.ToDictionary(b => b.AccountId, b => (b.Debit, b.Credit));
        }

        // Income = class 4, Expenses = classes 5, 6, 7
        var accounts = await context.ChartOfAccounts.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Level <= 4 &&
                        (a.AccountCode.StartsWith("4") || a.AccountCode.StartsWith("5") ||
                         a.AccountCode.StartsWith("6") || a.AccountCode.StartsWith("7")))
            .OrderBy(a => a.AccountCode)
            .ToListAsync(ct);

        var balanceMap = balances.ToDictionary(b => b.AccountId, b => b);

        var income = new List<IncomeStatementLineDto>();
        var expenses = new List<IncomeStatementLineDto>();

        foreach (var acct in accounts)
        {
            balanceMap.TryGetValue(acct.Id, out var bal);
            var amount = (bal?.Credit ?? 0) - (bal?.Debit ?? 0);
            if (acct.AccountCode.StartsWith("5") || acct.AccountCode.StartsWith("6") || acct.AccountCode.StartsWith("7"))
                amount = (bal?.Debit ?? 0) - (bal?.Credit ?? 0);

            decimal? priorAmount = null;
            if (priorMap is not null && priorMap.TryGetValue(acct.Id, out var pb))
            {
                priorAmount = acct.AccountCode.StartsWith("4")
                    ? pb.Credit - pb.Debit
                    : pb.Debit - pb.Credit;
            }

            var line = new IncomeStatementLineDto(acct.AccountCode, acct.Name, acct.Level, amount, priorAmount);

            if (acct.AccountCode.StartsWith("4")) income.Add(line);
            else expenses.Add(line);
        }

        var totalIncome = income.Where(i => i.Level == 1).Sum(i => i.Amount);
        var totalExpenses = expenses.Where(e => e.Level == 1).Sum(e => e.Amount);

        return Result.Success(new IncomeStatementDto(
            request.Year, request.Month, income, expenses,
            totalIncome, totalExpenses, totalIncome - totalExpenses));
    }
}
