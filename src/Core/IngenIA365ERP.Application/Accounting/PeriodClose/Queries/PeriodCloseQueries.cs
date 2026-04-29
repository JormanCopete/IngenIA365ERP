using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.PeriodClose.Queries;

// DTOs
public record PeriodClosePreviewDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string Status { get; init; } = string.Empty;
    public int DraftDocumentCount { get; init; }
    public decimal TotalDebit { get; init; }
    public decimal TotalCredit { get; init; }
    public decimal Difference { get; init; }
    public bool CanClose { get; init; }
}

public record TrialBalanceLineDto
{
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public byte Level { get; init; }
    public string Nature { get; init; } = string.Empty;
    public decimal PreviousBalance { get; init; }
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public decimal FinalBalance { get; init; }
}

// Query: Period Close Preview
public record GetPeriodClosePreviewQuery(int Year, int Month) : IRequest<Result<PeriodClosePreviewDto>>;

public class GetPeriodClosePreviewQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPeriodClosePreviewQuery, Result<PeriodClosePreviewDto>>
{
    public async Task<Result<PeriodClosePreviewDto>> Handle(
        GetPeriodClosePreviewQuery request,
        CancellationToken cancellationToken)
    {
        var periodCode = request.Year * 100 + request.Month;

        var period = await context.AccountingPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.ModuleCode == "CNT" &&
                p.Year == request.Year &&
                p.PeriodNumber == request.Month &&
                !p.IsDeleted,
                cancellationToken);

        if (period is null)
            return Result.Failure<PeriodClosePreviewDto>(new Error("PeriodClose.NotFound", "Periodo contable no encontrado."));

        var draftCount = await context.AccountingDocuments
            .AsNoTracking()
            .CountAsync(d =>
                d.PeriodCode == periodCode &&
                !d.IsClosed &&
                !d.IsVoided &&
                !d.IsDeleted,
                cancellationToken);

        var sums = await context.JournalEntries
            .AsNoTracking()
            .Where(j => j.PeriodCode == periodCode.ToString() && !j.IsDeleted)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalDebit = g.Sum(j => j.DebitAmount),
                TotalCredit = g.Sum(j => j.CreditAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalDebit = sums?.TotalDebit ?? 0m;
        var totalCredit = sums?.TotalCredit ?? 0m;
        var difference = totalDebit - totalCredit;

        return Result.Success(new PeriodClosePreviewDto
        {
            Year = request.Year,
            Month = request.Month,
            Status = period.Status,
            DraftDocumentCount = draftCount,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            Difference = difference,
            CanClose = draftCount == 0 && difference == 0 && period.Status != "C"
        });
    }
}

// Query: Trial Balance
public record GetTrialBalanceQuery(int Year, int Month) : IRequest<Result<List<TrialBalanceLineDto>>>;

public class GetTrialBalanceQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetTrialBalanceQuery, Result<List<TrialBalanceLineDto>>>
{
    public async Task<Result<List<TrialBalanceLineDto>>> Handle(
        GetTrialBalanceQuery request,
        CancellationToken cancellationToken)
    {
        // Previous month balance
        var prevMonth = request.Month == 1 ? 12 : request.Month - 1;
        var prevYear = request.Month == 1 ? request.Year - 1 : request.Year;

        var accounts = await context.ChartOfAccounts
            .AsNoTracking()
            .Where(a => !a.IsDeleted && a.Status == 1)
            .OrderBy(a => a.AccountCode)
            .Select(a => new
            {
                a.Id,
                a.AccountCode,
                a.Name,
                a.Level,
                a.Nature
            })
            .ToListAsync(cancellationToken);

        // Previous balances
        var prevBalances = await context.AccountBalances
            .AsNoTracking()
            .Where(b => b.PeriodYear == prevYear &&
                        b.PeriodMonth == (byte)prevMonth &&
                        !b.IsDeleted)
            .GroupBy(b => b.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Debit = g.Sum(b => b.DebitAmount),
                Credit = g.Sum(b => b.CreditAmount)
            })
            .ToDictionaryAsync(x => x.AccountId, cancellationToken);

        // Current period movements
        var currentBalances = await context.AccountBalances
            .AsNoTracking()
            .Where(b => b.PeriodYear == request.Year &&
                        b.PeriodMonth == (byte)request.Month &&
                        !b.IsDeleted)
            .GroupBy(b => b.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Debit = g.Sum(b => b.DebitAmount),
                Credit = g.Sum(b => b.CreditAmount)
            })
            .ToDictionaryAsync(x => x.AccountId, cancellationToken);

        var result = accounts.Select(a =>
        {
            var prev = prevBalances.GetValueOrDefault(a.Id);
            var curr = currentBalances.GetValueOrDefault(a.Id);

            var prevBalance = (prev?.Debit ?? 0) - (prev?.Credit ?? 0);
            if (a.Nature == "C") prevBalance = (prev?.Credit ?? 0) - (prev?.Debit ?? 0);

            var debit = curr?.Debit ?? 0;
            var credit = curr?.Credit ?? 0;
            var final_ = a.Nature == "C"
                ? prevBalance + credit - debit
                : prevBalance + debit - credit;

            return new TrialBalanceLineDto
            {
                AccountCode = a.AccountCode,
                AccountName = a.Name,
                Level = a.Level,
                Nature = a.Nature,
                PreviousBalance = prevBalance,
                Debit = debit,
                Credit = credit,
                FinalBalance = final_
            };
        }).ToList();

        return Result.Success(result);
    }
}
