using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.LoanPortfolios.Queries;

// --- DTOs ---

public record LoanPortfolioDto(
    Guid PublicId,
    long PortfolioNumber,
    string PersonName,
    string IdentificationNumber,
    string CreditLineName,
    DateOnly DisbursementDate,
    decimal CurrentBalance,
    decimal ApprovedAmount,
    int PaidInstallments,
    decimal PendingInstallmentCount,
    int DaysOverdue,
    string Category,
    decimal InterestRate,
    decimal InstallmentAmount,
    DateOnly? MaturityDate,
    string StatusText);

public record InstallmentDto(
    Guid PublicId,
    int Period,
    DateOnly? ProcessDate,
    decimal AccruedCapital,
    decimal AccruedInterest,
    decimal TotalAmount,
    decimal PaidCapital,
    decimal PaidInterest,
    decimal BalanceCapital,
    decimal BalanceInterest,
    decimal DefaultInterest,
    string Status);

public record PortfolioTransactionDto(
    Guid PublicId,
    string VoucherType,
    long DocumentNumber,
    DateOnly TransactionDate,
    string TransactionCode,
    decimal DebitAmount,
    decimal CreditAmount,
    string Description);

public record LoanPortfolioDetailDto(
    Guid PublicId,
    long PortfolioNumber,
    string PersonName,
    string IdentificationNumber,
    string CreditLineName,
    DateOnly DisbursementDate,
    DateOnly? MaturityDate,
    DateOnly? LastPaymentDate,
    decimal CurrentBalance,
    decimal ApprovedAmount,
    decimal InterestRate,
    int TermMonths,
    decimal InstallmentAmount,
    int PaidInstallments,
    decimal PendingInstallmentCount,
    int DaysOverdue,
    string Category,
    string GuaranteeType,
    string DeductionType,
    string PaymentPeriodicity,
    List<InstallmentDto> Installments,
    List<PortfolioTransactionDto> RecentTransactions);

// --- List Portfolios ---

public record ListLoanPortfoliosQuery : IRequest<Result<PagedList<LoanPortfolioDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? PersonPublicId { get; init; }
    public Guid? CreditLinePublicId { get; init; }
    public string? Status { get; init; } // "A"=active, "C"=closed
    public int? DaysOverdueFrom { get; init; }
    public string? SearchTerm { get; init; }
}

public class ListLoanPortfoliosQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListLoanPortfoliosQuery, Result<PagedList<LoanPortfolioDto>>>
{
    public async Task<Result<PagedList<LoanPortfolioDto>>> Handle(
        ListLoanPortfoliosQuery request, CancellationToken ct)
    {
        var query = context.LoanPortfolios
            .AsNoTracking()
            .Include(lp => lp.CreditLine)
            .Where(lp => !lp.IsDeleted);

        // Filters
        if (request.PersonPublicId.HasValue)
        {
            var person = await context.People.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId.Value && !p.IsDeleted, ct);
            if (person is not null)
                query = query.Where(lp => lp.PersonId == person.Id);
        }

        if (request.CreditLinePublicId.HasValue)
        {
            var cl = await context.CreditLineParameters.AsNoTracking()
                .FirstOrDefaultAsync(c => c.PublicId == request.CreditLinePublicId.Value && !c.IsDeleted, ct);
            if (cl is not null)
                query = query.Where(lp => lp.CreditLineId == cl.Id);
        }

        if (request.Status == "A")
            query = query.Where(lp => lp.CurrentBalance > 0);
        else if (request.Status == "C")
            query = query.Where(lp => lp.CurrentBalance == 0);

        if (request.DaysOverdueFrom.HasValue)
            query = query.Where(lp => lp.DaysOverdue >= request.DaysOverdueFrom.Value);

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var term = request.SearchTerm;
            query = query.Where(lp =>
                lp.IdentificationNumber.Contains(term) ||
                lp.PortfolioNumber.ToString().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(lp => lp.DisbursementDate)
            .ThenByDescending(lp => lp.PortfolioNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(lp => new
            {
                lp.PublicId,
                lp.PortfolioNumber,
                lp.PersonId,
                lp.IdentificationNumber,
                CreditLineName = lp.CreditLine != null ? lp.CreditLine.Description : "",
                lp.DisbursementDate,
                lp.CurrentBalance,
                lp.ApprovedAmount,
                lp.PaidInstallments,
                lp.PendingInstallmentCount,
                lp.DaysOverdue,
                lp.Category,
                lp.InterestRate,
                lp.InstallmentAmount,
                lp.MaturityDate
            })
            .ToListAsync(ct);

        // Batch load person names
        var personIds = items.Select(i => i.PersonId).Distinct().ToList();
        var personNames = await context.People.AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => NombreDePersona.Completo(p), ct);

        var dtos = items.Select(lp => new LoanPortfolioDto(
            lp.PublicId,
            lp.PortfolioNumber,
            personNames.TryGetValue(lp.PersonId, out var name) ? name : lp.IdentificationNumber,
            lp.IdentificationNumber,
            lp.CreditLineName,
            lp.DisbursementDate,
            lp.CurrentBalance,
            lp.ApprovedAmount,
            lp.PaidInstallments,
            lp.PendingInstallmentCount,
            lp.DaysOverdue,
            lp.Category,
            lp.InterestRate,
            lp.InstallmentAmount,
            lp.MaturityDate,
            lp.CurrentBalance > 0
                ? (lp.DaysOverdue > 0 ? $"En mora ({lp.DaysOverdue} dias)" : "Vigente")
                : "Cancelado"
        )).ToList();

        return Result.Success(new PagedList<LoanPortfolioDto>(dtos, totalCount, request.PageNumber, request.PageSize));
    }
}

// --- Get Portfolio By Id ---

public record GetLoanPortfolioByIdQuery(Guid PublicId) : IRequest<Result<LoanPortfolioDetailDto>>;

public class GetLoanPortfolioByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetLoanPortfolioByIdQuery, Result<LoanPortfolioDetailDto>>
{
    public async Task<Result<LoanPortfolioDetailDto>> Handle(
        GetLoanPortfolioByIdQuery request, CancellationToken ct)
    {
        var portfolio = await context.LoanPortfolios
            .AsNoTracking()
            .Include(lp => lp.Person)
            .Include(lp => lp.CreditLine)
            .FirstOrDefaultAsync(lp => lp.PublicId == request.PublicId && !lp.IsDeleted, ct);

        if (portfolio is null)
            return Result.Failure<LoanPortfolioDetailDto>(new Error("Portfolio.NotFound",
                "Credito no encontrado."));

        var personName = portfolio.Person is not null
            ? NombreDePersona.Completo(portfolio.Person)
            : portfolio.IdentificationNumber;

        // Load installments
        var personCode = portfolio.Person?.LegacyCode ?? portfolio.Person?.TaxId ?? "";
        var installments = await context.PendingInstallments
            .AsNoTracking()
            .Where(pi => pi.PortfolioNumber == portfolio.PortfolioNumber
                      && pi.CreditLineId == portfolio.CreditLineId
                      && pi.PersonCode == personCode
                      && !pi.IsDeleted)
            .OrderBy(pi => pi.AccrualPeriod)
            .Select(pi => new InstallmentDto(
                pi.PublicId,
                pi.Cycle,
                pi.ProcessDate,
                pi.AccruedCapital,
                pi.AccruedInterest,
                pi.TotalAmount,
                pi.PaidCapital,
                pi.PaidInterest,
                pi.BalanceCapital,
                pi.BalanceInterest,
                pi.DefaultInterest,
                pi.BalanceCapital == 0 && pi.BalanceInterest == 0 ? "Pagada"
                    : pi.BalanceCapital < pi.AccruedCapital ? "Parcial"
                    : "Pendiente"))
            .ToListAsync(ct);

        // Load recent transactions
        var transactions = await context.LendingTransactions
            .AsNoTracking()
            .Where(t => t.PortfolioNumber == portfolio.PortfolioNumber
                     && t.CreditLineId == portfolio.CreditLineId
                     && !t.IsDeleted)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.DocumentNumber)
            .Take(50)
            .Select(t => new PortfolioTransactionDto(
                t.PublicId,
                t.VoucherType,
                t.DocumentNumber,
                t.TransactionDate,
                t.TransactionCode,
                t.DebitAmount,
                t.CreditAmount,
                t.Description))
            .ToListAsync(ct);

        var dto = new LoanPortfolioDetailDto(
            portfolio.PublicId,
            portfolio.PortfolioNumber,
            personName,
            portfolio.IdentificationNumber,
            portfolio.CreditLine?.Description ?? "",
            portfolio.DisbursementDate,
            portfolio.MaturityDate,
            portfolio.LastPaymentDate,
            portfolio.CurrentBalance,
            portfolio.ApprovedAmount,
            portfolio.InterestRate,
            portfolio.TermMonths,
            portfolio.InstallmentAmount,
            portfolio.PaidInstallments,
            portfolio.PendingInstallmentCount,
            portfolio.DaysOverdue,
            portfolio.Category,
            portfolio.GuaranteeType,
            portfolio.DeductionType,
            portfolio.PaymentPeriodicity,
            installments,
            transactions);

        return Result.Success(dto);
    }
}

// --- Get Statement Query ---

public record GetLoanStatementQuery(Guid PublicId) : IRequest<Result<List<PortfolioTransactionDto>>>;

public class GetLoanStatementQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetLoanStatementQuery, Result<List<PortfolioTransactionDto>>>
{
    public async Task<Result<List<PortfolioTransactionDto>>> Handle(
        GetLoanStatementQuery request, CancellationToken ct)
    {
        var portfolio = await context.LoanPortfolios.AsNoTracking()
            .FirstOrDefaultAsync(lp => lp.PublicId == request.PublicId && !lp.IsDeleted, ct);

        if (portfolio is null)
            return Result.Failure<List<PortfolioTransactionDto>>(new Error("Portfolio.NotFound",
                "Credito no encontrado."));

        var transactions = await context.LendingTransactions
            .AsNoTracking()
            .Where(t => t.PortfolioNumber == portfolio.PortfolioNumber
                     && t.CreditLineId == portfolio.CreditLineId
                     && !t.IsDeleted)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.DocumentNumber)
            .Select(t => new PortfolioTransactionDto(
                t.PublicId,
                t.VoucherType,
                t.DocumentNumber,
                t.TransactionDate,
                t.TransactionCode,
                t.DebitAmount,
                t.CreditAmount,
                t.Description))
            .ToListAsync(ct);

        return Result.Success(transactions);
    }
}
