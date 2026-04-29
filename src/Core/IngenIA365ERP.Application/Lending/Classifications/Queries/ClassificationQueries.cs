using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Classifications.Queries;

// DTOs
public record ClassificationSummaryDto
{
    public string Classification { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal TotalBalance { get; init; }
    public decimal ProvisionRate { get; init; }
    public decimal ProvisionAmount { get; init; }
}

public record ClassificationHistoryDto
{
    public int Period { get; init; }
    public string PeriodLabel { get; init; } = string.Empty;
    public int CountA { get; init; }
    public int CountB { get; init; }
    public int CountC { get; init; }
    public int CountD { get; init; }
    public int CountE { get; init; }
    public decimal AmountA { get; init; }
    public decimal AmountB { get; init; }
    public decimal AmountC { get; init; }
    public decimal AmountD { get; init; }
    public decimal AmountE { get; init; }
}

// Query: Current classification distribution
public record GetPortfolioClassificationQuery : IRequest<Result<List<ClassificationSummaryDto>>>;

public class GetPortfolioClassificationQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPortfolioClassificationQuery, Result<List<ClassificationSummaryDto>>>
{
    public async Task<Result<List<ClassificationSummaryDto>>> Handle(
        GetPortfolioClassificationQuery request,
        CancellationToken cancellationToken)
    {
        // Get latest classification period
        var latestPeriod = await context.PortfolioClassifications
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .MaxAsync(c => (int?)c.AccountingPeriod, cancellationToken);

        if (latestPeriod is null)
            return Result.Success(new List<ClassificationSummaryDto>());

        var summary = await context.PortfolioClassifications
            .AsNoTracking()
            .Where(c => c.AccountingPeriod == latestPeriod.Value && !c.IsDeleted)
            .GroupBy(c => c.Category)
            .Select(g => new ClassificationSummaryDto
            {
                Classification = g.Key,
                Count = g.Count(),
                TotalBalance = g.Sum(c => c.TotalBalance),
                ProvisionRate = g.Average(c => c.Rate),
                ProvisionAmount = g.Sum(c => c.ProvisionBalance)
            })
            .OrderBy(s => s.Classification)
            .ToListAsync(cancellationToken);

        return Result.Success(summary);
    }
}

// Query: Classification history (last 12 months)
public record GetClassificationHistoryQuery : IRequest<Result<List<ClassificationHistoryDto>>>;

public class GetClassificationHistoryQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetClassificationHistoryQuery, Result<List<ClassificationHistoryDto>>>
{
    public async Task<Result<List<ClassificationHistoryDto>>> Handle(
        GetClassificationHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var periods = await context.PortfolioClassifications
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .GroupBy(c => c.AccountingPeriod)
            .Select(g => new
            {
                Period = g.Key,
                CountA = g.Count(c => c.Category == "A"),
                CountB = g.Count(c => c.Category == "B"),
                CountC = g.Count(c => c.Category == "C"),
                CountD = g.Count(c => c.Category == "D"),
                CountE = g.Count(c => c.Category == "E"),
                AmountA = g.Where(c => c.Category == "A").Sum(c => c.TotalBalance),
                AmountB = g.Where(c => c.Category == "B").Sum(c => c.TotalBalance),
                AmountC = g.Where(c => c.Category == "C").Sum(c => c.TotalBalance),
                AmountD = g.Where(c => c.Category == "D").Sum(c => c.TotalBalance),
                AmountE = g.Where(c => c.Category == "E").Sum(c => c.TotalBalance)
            })
            .OrderByDescending(g => g.Period)
            .Take(12)
            .ToListAsync(cancellationToken);

        var result = periods
            .OrderBy(p => p.Period)
            .Select(p => new ClassificationHistoryDto
            {
                Period = p.Period,
                PeriodLabel = $"{p.Period / 100}-{p.Period % 100:D2}",
                CountA = p.CountA,
                CountB = p.CountB,
                CountC = p.CountC,
                CountD = p.CountD,
                CountE = p.CountE,
                AmountA = p.AmountA,
                AmountB = p.AmountB,
                AmountC = p.AmountC,
                AmountD = p.AmountD,
                AmountE = p.AmountE
            }).ToList();

        return Result.Success(result);
    }
}
