using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Defaults.Queries;

// --- DTOs ---

public record DefaultPortfolioDto(
    Guid PortfolioPublicId,
    long PortfolioNumber,
    string PersonName,
    string IdentificationNumber,
    string CreditLineName,
    decimal CurrentBalance,
    int DaysOverdue,
    string Classification,
    string ClassificationText,
    decimal CapitalBalance,
    decimal InterestBalance,
    decimal DefaultBalance,
    decimal TotalOverdue,
    DateOnly DisbursementDate,
    DateOnly? LastPaymentDate);

public record DefaultSummaryDto(
    decimal TotalOverdueAmount,
    decimal TotalPortfolioBalance,
    decimal PortfolioAtRiskPct,
    int TotalOverduePortfolios,
    int ClassA,
    int ClassB,
    int ClassC,
    int ClassD,
    int ClassE,
    decimal AmountA,
    decimal AmountB,
    decimal AmountC,
    decimal AmountD,
    decimal AmountE);

// --- List Defaults ---

public record ListDefaultPortfoliosQuery : IRequest<Result<PagedList<DefaultPortfolioDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int? DaysOverdueFrom { get; init; }
    public int? DaysOverdueTo { get; init; }
    public string? Classification { get; init; }
    public Guid? CreditLinePublicId { get; init; }
    public string? SearchTerm { get; init; }
}

public class ListDefaultPortfoliosQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListDefaultPortfoliosQuery, Result<PagedList<DefaultPortfolioDto>>>
{
    public async Task<Result<PagedList<DefaultPortfolioDto>>> Handle(
        ListDefaultPortfoliosQuery request, CancellationToken ct)
    {
        var query = context.LoanPortfolios
            .AsNoTracking()
            .Include(lp => lp.CreditLine)
            .Where(lp => !lp.IsDeleted && lp.CurrentBalance > 0 && lp.DaysOverdue > 0);

        if (request.DaysOverdueFrom.HasValue)
            query = query.Where(lp => lp.DaysOverdue >= request.DaysOverdueFrom.Value);
        if (request.DaysOverdueTo.HasValue)
            query = query.Where(lp => lp.DaysOverdue <= request.DaysOverdueTo.Value);
        if (!string.IsNullOrEmpty(request.Classification))
            query = query.Where(lp => lp.Category == request.Classification);
        if (request.CreditLinePublicId.HasValue)
        {
            var cl = await context.CreditLineParameters.AsNoTracking()
                .FirstOrDefaultAsync(c => c.PublicId == request.CreditLinePublicId.Value && !c.IsDeleted, ct);
            if (cl is not null)
                query = query.Where(lp => lp.CreditLineId == cl.Id);
        }
        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var term = request.SearchTerm;
            query = query.Where(lp => lp.IdentificationNumber.Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(lp => lp.DaysOverdue)
            .ThenByDescending(lp => lp.CurrentBalance)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(lp => new
            {
                lp.PublicId,
                lp.PortfolioNumber,
                lp.PersonId,
                lp.IdentificationNumber,
                CreditLineName = lp.CreditLine != null ? lp.CreditLine.Description : "",
                lp.CurrentBalance,
                lp.DaysOverdue,
                lp.Category,
                lp.DefaultBalanceCurrent,
                lp.InterestBalanceCurrent,
                lp.CapitalBalanceCurrent,
                lp.DisbursementDate,
                lp.LastPaymentDate
            })
            .ToListAsync(ct);

        // Batch load person names
        var personIds = items.Select(i => i.PersonId).Distinct().ToList();
        var personNames = await context.People.AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => NombreDePersona.Completo(p), ct);

        var classificationText = new Dictionary<string, string>
        {
            { "A", "Normal" }, { "B", "Aceptable" }, { "C", "Apreciable" },
            { "D", "Significativo" }, { "E", "Incobrable" }
        };

        var dtos = items.Select(lp => new DefaultPortfolioDto(
            lp.PublicId,
            lp.PortfolioNumber,
            personNames.TryGetValue(lp.PersonId, out var name) ? name : lp.IdentificationNumber,
            lp.IdentificationNumber,
            lp.CreditLineName,
            lp.CurrentBalance,
            lp.DaysOverdue,
            lp.Category,
            classificationText.TryGetValue(lp.Category, out var text) ? text : lp.Category,
            lp.CapitalBalanceCurrent,
            lp.InterestBalanceCurrent,
            lp.DefaultBalanceCurrent,
            lp.CapitalBalanceCurrent + lp.InterestBalanceCurrent + lp.DefaultBalanceCurrent,
            lp.DisbursementDate,
            lp.LastPaymentDate
        )).ToList();

        return Result.Success(new PagedList<DefaultPortfolioDto>(dtos, totalCount, request.PageNumber, request.PageSize));
    }
}

// --- Get Default Summary ---

public record GetDefaultSummaryQuery : IRequest<Result<DefaultSummaryDto>>;

public class GetDefaultSummaryQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDefaultSummaryQuery, Result<DefaultSummaryDto>>
{
    public async Task<Result<DefaultSummaryDto>> Handle(GetDefaultSummaryQuery request, CancellationToken ct)
    {
        var activePortfolios = context.LoanPortfolios.AsNoTracking()
            .Where(lp => !lp.IsDeleted && lp.CurrentBalance > 0);

        var totalBalance = await activePortfolios.SumAsync(lp => lp.CurrentBalance, ct);

        var overduePortfolios = activePortfolios.Where(lp => lp.DaysOverdue > 0);
        var totalOverdue = await overduePortfolios.SumAsync(lp =>
            lp.CapitalBalanceCurrent + lp.InterestBalanceCurrent + lp.DefaultBalanceCurrent, ct);
        var totalOverdueCount = await overduePortfolios.CountAsync(ct);

        var classGroups = await overduePortfolios
            .GroupBy(lp => lp.Category)
            .Select(g => new { Category = g.Key, Count = g.Count(), Total = g.Sum(lp => lp.CurrentBalance) })
            .ToListAsync(ct);

        var byClass = classGroups.ToDictionary(g => g.Category, g => (g.Count, g.Total));

        var riskPct = totalBalance > 0 ? totalOverdue / totalBalance * 100 : 0;

        return Result.Success(new DefaultSummaryDto(
            totalOverdue,
            totalBalance,
            Math.Round(riskPct, 2),
            totalOverdueCount,
            byClass.TryGetValue("A", out var a) ? a.Count : 0,
            byClass.TryGetValue("B", out var b) ? b.Count : 0,
            byClass.TryGetValue("C", out var c) ? c.Count : 0,
            byClass.TryGetValue("D", out var d) ? d.Count : 0,
            byClass.TryGetValue("E", out var e) ? e.Count : 0,
            a.Total,
            b.Total,
            c.Total,
            d.Total,
            e.Total));
    }
}
