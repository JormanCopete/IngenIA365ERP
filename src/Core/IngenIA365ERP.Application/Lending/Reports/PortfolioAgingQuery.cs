using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports;

// --- DTOs ---

public record PortfolioAgingBucketDto(
    string BucketLabel,
    int MinDays,
    int MaxDays,
    int LoanCount,
    decimal TotalBalance,
    decimal TotalOverdue,
    decimal PercentOfTotal);

public record PortfolioAgingDto(
    DateTime AsOfDate,
    List<PortfolioAgingBucketDto> Buckets,
    decimal GrandTotalBalance,
    decimal GrandTotalOverdue,
    int TotalLoans);

// --- Query ---

public record GetPortfolioAgingQuery(
    DateTime AsOfDate,
    Guid? BranchPublicId,
    int? CreditLineId) : IRequest<Result<PortfolioAgingDto>>;

public class GetPortfolioAgingQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPortfolioAgingQuery, Result<PortfolioAgingDto>>
{
    private static readonly (string Label, int Min, int Max)[] BucketDefs =
    [
        ("Al dia", 0, 0),
        ("1-30 dias", 1, 30),
        ("31-60 dias", 31, 60),
        ("61-90 dias", 61, 90),
        ("91-180 dias", 91, 180),
        ("Mas de 180 dias", 181, int.MaxValue)
    ];

    public async Task<Result<PortfolioAgingDto>> Handle(GetPortfolioAgingQuery request, CancellationToken ct)
    {
        // Active loans: not deleted, not written off
        var query = context.LoanPortfolios.AsNoTracking()
            .Where(lp => !lp.IsDeleted && lp.IsWrittenOff != "S");

        if (request.CreditLineId.HasValue)
            query = query.Where(lp => lp.CreditLineId == request.CreditLineId.Value);

        var loans = await query.ToListAsync(ct);

        // Classify by DaysOverdue which is already on LoanPortfolio
        var buckets = BucketDefs.Select(bd =>
        {
            var matching = loans.Where(l =>
                l.DaysOverdue >= bd.Min && l.DaysOverdue <= bd.Max).ToList();

            var totalBalance = matching.Sum(l => l.CurrentBalance);
            // Overdue amount: sum of interest + default balances for overdue loans
            var totalOverdue = matching.Sum(l =>
                l.InterestBalanceCurrent + l.DefaultBalanceCurrent);

            return new PortfolioAgingBucketDto(
                bd.Label, bd.Min, bd.Max, matching.Count,
                totalBalance, totalOverdue, 0);
        }).ToList();

        var grandTotal = buckets.Sum(b => b.TotalBalance);
        if (grandTotal > 0)
        {
            buckets = buckets.Select(b => b with
            {
                PercentOfTotal = Math.Round(b.TotalBalance / grandTotal * 100, 2)
            }).ToList();
        }

        return Result.Success(new PortfolioAgingDto(
            request.AsOfDate, buckets, grandTotal,
            buckets.Sum(b => b.TotalOverdue), loans.Count));
    }
}
