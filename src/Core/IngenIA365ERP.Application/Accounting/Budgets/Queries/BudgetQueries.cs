using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Budgets.Queries;

// --- DTOs ---

public record BudgetDto(
    Guid PublicId,
    string AccountCode,
    string AccountName,
    int Year,
    string? BranchName,
    string? CostCenterName,
    decimal JanBudget,
    decimal FebBudget,
    decimal MarBudget,
    decimal AprBudget,
    decimal MayBudget,
    decimal JunBudget,
    decimal JulBudget,
    decimal AugBudget,
    decimal SepBudget,
    decimal OctBudget,
    decimal NovBudget,
    decimal DecBudget,
    decimal TotalBudget);

public record BudgetExecutionDto(
    string AccountCode,
    string AccountName,
    int Month,
    decimal BudgetAmount,
    decimal ActualAmount,
    decimal Variance,
    decimal VariancePercent);

// --- List Budgets ---

public record ListBudgetsQuery : IRequest<Result<PagedList<BudgetDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public int Year { get; init; }
    public string? AccountCodeFrom { get; init; }
    public string? AccountCodeTo { get; init; }
}

public class ListBudgetsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListBudgetsQuery, Result<PagedList<BudgetDto>>>
{
    public async Task<Result<PagedList<BudgetDto>>> Handle(
        ListBudgetsQuery request, CancellationToken ct)
    {
        var query = context.Budgets
            .AsNoTracking()
            .Where(b => !b.IsDeleted && b.PeriodYear == request.Year);

        // Join with account for code filtering
        var joined = query.Join(
            context.ChartOfAccounts.AsNoTracking().Where(a => !a.IsDeleted),
            b => b.AccountId, a => a.Id,
            (b, a) => new { Budget = b, Account = a });

        if (!string.IsNullOrEmpty(request.AccountCodeFrom))
            joined = joined.Where(x => string.Compare(x.Account.AccountCode, request.AccountCodeFrom) >= 0);
        if (!string.IsNullOrEmpty(request.AccountCodeTo))
            joined = joined.Where(x => string.Compare(x.Account.AccountCode, request.AccountCodeTo) <= 0);

        var totalCount = await joined.CountAsync(ct);

        var rawItems = await joined
            .OrderBy(x => x.Account.AccountCode)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new
            {
                x.Budget.PublicId,
                x.Account.AccountCode,
                AccountName = x.Account.Name,
                x.Budget.PeriodYear,
                x.Budget.BranchId,
                x.Budget.CostCenterId,
                x.Budget.JanBudget,
                x.Budget.FebBudget,
                x.Budget.MarBudget,
                x.Budget.AprBudget,
                x.Budget.MayBudget,
                x.Budget.JunBudget,
                x.Budget.JulBudget,
                x.Budget.AugBudget,
                x.Budget.SepBudget,
                x.Budget.OctBudget,
                x.Budget.NovBudget,
                x.Budget.DecBudget,
                x.Budget.TotalBudget
            })
            .ToListAsync(ct);

        // Batch-load branch and cost center names
        var branchIds = rawItems.Select(r => r.BranchId).Distinct().ToList();
        var ccIds = rawItems.Select(r => r.CostCenterId).Distinct().ToList();

        var branchNames = branchIds.Count > 0
            ? await context.Branches.AsNoTracking()
                .Where(b => branchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, b => b.Name, ct)
            : new Dictionary<int, string>();

        var ccNames = ccIds.Count > 0
            ? await context.CostCenters.AsNoTracking()
                .Where(c => ccIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct)
            : new Dictionary<int, string>();

        var items = rawItems.Select(r => new BudgetDto(
            r.PublicId,
            r.AccountCode,
            r.AccountName,
            r.PeriodYear,
            branchNames.TryGetValue(r.BranchId, out var bn) ? bn : null,
            ccNames.TryGetValue(r.CostCenterId, out var cn) ? cn : null,
            r.JanBudget ?? 0,
            r.FebBudget ?? 0,
            r.MarBudget ?? 0,
            r.AprBudget ?? 0,
            r.MayBudget ?? 0,
            r.JunBudget ?? 0,
            r.JulBudget ?? 0,
            r.AugBudget ?? 0,
            r.SepBudget ?? 0,
            r.OctBudget ?? 0,
            r.NovBudget ?? 0,
            r.DecBudget ?? 0,
            r.TotalBudget ?? 0
        )).ToList();

        return Result.Success(new PagedList<BudgetDto>(items, totalCount, request.PageNumber, request.PageSize));
    }
}

// --- Budget Execution (Budget vs Actual) ---

public record GetBudgetExecutionQuery : IRequest<Result<List<BudgetExecutionDto>>>
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string? AccountCodeFrom { get; init; }
    public string? AccountCodeTo { get; init; }
}

public class GetBudgetExecutionQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetBudgetExecutionQuery, Result<List<BudgetExecutionDto>>>
{
    public async Task<Result<List<BudgetExecutionDto>>> Handle(
        GetBudgetExecutionQuery request, CancellationToken ct)
    {
        // 1. Get budgets for the year
        var budgetsQuery = context.Budgets.AsNoTracking()
            .Where(b => !b.IsDeleted && b.PeriodYear == request.Year);

        var joined = budgetsQuery.Join(
            context.ChartOfAccounts.AsNoTracking().Where(a => !a.IsDeleted),
            b => b.AccountId, a => a.Id,
            (b, a) => new { Budget = b, Account = a });

        if (!string.IsNullOrEmpty(request.AccountCodeFrom))
            joined = joined.Where(x => string.Compare(x.Account.AccountCode, request.AccountCodeFrom) >= 0);
        if (!string.IsNullOrEmpty(request.AccountCodeTo))
            joined = joined.Where(x => string.Compare(x.Account.AccountCode, request.AccountCodeTo) <= 0);

        var budgets = await joined
            .OrderBy(x => x.Account.AccountCode)
            .ToListAsync(ct);

        if (budgets.Count == 0)
            return Result.Success(new List<BudgetExecutionDto>());

        // 2. Get actual balances for the same accounts/month
        var accountIds = budgets.Select(b => b.Budget.AccountId).Distinct().ToList();
        var actuals = await context.AccountBalances.AsNoTracking()
            .Where(ab => accountIds.Contains(ab.AccountId)
                      && ab.PeriodYear == request.Year
                      && ab.PeriodMonth == (byte)request.Month)
            .GroupBy(ab => ab.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                DebitAmount = g.Sum(ab => ab.DebitAmount),
                CreditAmount = g.Sum(ab => ab.CreditAmount)
            })
            .ToDictionaryAsync(x => x.AccountId, ct);

        // 3. Build execution report
        var result = new List<BudgetExecutionDto>();
        foreach (var item in budgets)
        {
            // Get budget amount for the specific month
            var budgetAmount = request.Month switch
            {
                1 => item.Budget.JanBudget ?? 0,
                2 => item.Budget.FebBudget ?? 0,
                3 => item.Budget.MarBudget ?? 0,
                4 => item.Budget.AprBudget ?? 0,
                5 => item.Budget.MayBudget ?? 0,
                6 => item.Budget.JunBudget ?? 0,
                7 => item.Budget.JulBudget ?? 0,
                8 => item.Budget.AugBudget ?? 0,
                9 => item.Budget.SepBudget ?? 0,
                10 => item.Budget.OctBudget ?? 0,
                11 => item.Budget.NovBudget ?? 0,
                12 => item.Budget.DecBudget ?? 0,
                _ => 0m
            };

            // Calculate actual amount based on account nature
            decimal actualAmount = 0;
            if (actuals.TryGetValue(item.Budget.AccountId, out var actual))
            {
                actualAmount = item.Account.Nature == "D"
                    ? actual.DebitAmount - actual.CreditAmount
                    : actual.CreditAmount - actual.DebitAmount;
            }

            var variance = budgetAmount - actualAmount;
            var variancePercent = budgetAmount != 0
                ? Math.Round((variance / budgetAmount) * 100, 2)
                : 0;

            result.Add(new BudgetExecutionDto(
                item.Account.AccountCode,
                item.Account.Name,
                request.Month,
                budgetAmount,
                actualAmount,
                variance,
                variancePercent));
        }

        return Result.Success(result);
    }
}
