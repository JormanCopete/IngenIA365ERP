using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.LoanApplications.Queries;

// --- DTOs ---

public record LoanApplicationDto(
    Guid PublicId,
    int ApplicationNumber,
    string PersonCode,
    string PersonName,
    string CreditLineName,
    decimal RequestedAmount,
    decimal ApprovedAmount,
    int Term,
    decimal InterestRate,
    decimal InstallmentAmount,
    string Status,
    string StatusText,
    DateOnly ApplicationDate,
    DateOnly? ApprovalDate,
    DateOnly? DisbursementDate);

public record LoanApplicationDetailDto(
    Guid PublicId,
    int ApplicationNumber,
    string PersonCode,
    string PersonName,
    string IdentificationNumber,
    string CreditLineName,
    int CreditLineId,
    decimal RequestedAmount,
    decimal ApprovedAmount,
    int Term,
    decimal InterestRate,
    decimal InstallmentAmount,
    string Status,
    string StatusText,
    DateOnly ApplicationDate,
    DateOnly? ApprovalDate,
    DateOnly? DisbursementDate,
    string GuaranteeType,
    string Periodicity,
    string? Remarks,
    decimal Salary,
    decimal OtherIncome,
    decimal MonthlyDeductions,
    decimal PaymentCapacity,
    string Codeudor1,
    string Codeudor2,
    string EntryUserId,
    string AuthorizingUserId);

public record AmortizationLineDto(
    int Period,
    DateOnly PaymentDate,
    decimal Capital,
    decimal Interest,
    decimal Total,
    decimal RemainingBalance);

// --- Status helper ---

public static class LoanApplicationStatusHelper
{
    public static string GetStatusText(string code) => code switch
    {
        "R" => "Radicada",
        "A" => "Aprobada",
        "X" => "Rechazada",
        "D" => "Desembolsada",
        "C" => "Cancelada",
        _ => code
    };
}

// --- List Query ---

public record ListLoanApplicationsQuery : IRequest<Result<PagedList<LoanApplicationDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Status { get; init; }
    public Guid? PersonPublicId { get; init; }
    public Guid? CreditLinePublicId { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
}

public class ListLoanApplicationsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListLoanApplicationsQuery, Result<PagedList<LoanApplicationDto>>>
{
    public async Task<Result<PagedList<LoanApplicationDto>>> Handle(
        ListLoanApplicationsQuery request, CancellationToken ct)
    {
        var query = context.LoanApplications
            .AsNoTracking()
            .Include(a => a.CreditLine)
            .Where(a => !a.IsDeleted);

        // Filters
        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(a => a.Status == request.Status);

        if (request.PersonPublicId.HasValue)
        {
            var person = await context.People.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId.Value && !p.IsDeleted, ct);
            if (person is not null)
            {
                var personCode = person.LegacyCode ?? person.TaxId;
                query = query.Where(a => a.PersonCode == personCode);
            }
        }

        if (request.CreditLinePublicId.HasValue)
        {
            var cl = await context.CreditLineParameters.AsNoTracking()
                .FirstOrDefaultAsync(c => c.PublicId == request.CreditLinePublicId.Value && !c.IsDeleted, ct);
            if (cl is not null)
                query = query.Where(a => a.CreditLineId == cl.Id);
        }

        if (request.DateFrom.HasValue)
            query = query.Where(a => a.ApplicationDate >= request.DateFrom.Value);
        if (request.DateTo.HasValue)
            query = query.Where(a => a.ApplicationDate <= request.DateTo.Value);

        var totalCount = await query.CountAsync(ct);

        // Get person names in batch
        var items = await query
            .OrderByDescending(a => a.ApplicationDate)
            .ThenByDescending(a => a.ApplicationNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new
            {
                a.PublicId,
                a.ApplicationNumber,
                a.PersonCode,
                a.IdentificationNumber,
                CreditLineName = a.CreditLine != null ? a.CreditLine.Description : "",
                a.RequestedAmount,
                a.ApprovedAmount,
                a.Term,
                a.InterestRate,
                a.InstallmentAmount,
                a.Status,
                a.ApplicationDate,
                a.ApprovalDate,
                a.DisbursementDate
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

        var dtos = items.Select(a => new LoanApplicationDto(
            a.PublicId,
            a.ApplicationNumber,
            a.PersonCode,
            personNames.TryGetValue(a.PersonCode, out var name) ? name : a.PersonCode,
            a.CreditLineName,
            a.RequestedAmount,
            a.ApprovedAmount,
            a.Term,
            a.InterestRate,
            a.InstallmentAmount,
            a.Status,
            LoanApplicationStatusHelper.GetStatusText(a.Status),
            a.ApplicationDate,
            a.ApprovalDate,
            a.DisbursementDate
        )).ToList();

        return Result.Success(new PagedList<LoanApplicationDto>(dtos, totalCount, request.PageNumber, request.PageSize));
    }
}

// --- Get By Id Query ---

public record GetLoanApplicationByIdQuery(Guid PublicId) : IRequest<Result<LoanApplicationDetailDto>>;

public class GetLoanApplicationByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetLoanApplicationByIdQuery, Result<LoanApplicationDetailDto>>
{
    public async Task<Result<LoanApplicationDetailDto>> Handle(
        GetLoanApplicationByIdQuery request, CancellationToken ct)
    {
        var app = await context.LoanApplications
            .AsNoTracking()
            .Include(a => a.CreditLine)
            .FirstOrDefaultAsync(a => a.PublicId == request.PublicId && !a.IsDeleted, ct);

        if (app is null)
            return Result.Failure<LoanApplicationDetailDto>(new Error("LoanApplication.NotFound",
                "Solicitud de credito no encontrada."));

        var person = await context.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LegacyCode == app.PersonCode || p.TaxId == app.PersonCode, ct);
        var personName = person is not null ? $"{person.FirstName} {person.LastName}" : app.PersonCode;

        var dto = new LoanApplicationDetailDto(
            app.PublicId,
            app.ApplicationNumber,
            app.PersonCode,
            personName,
            app.IdentificationNumber,
            app.CreditLine?.Description ?? "",
            app.CreditLineId,
            app.RequestedAmount,
            app.ApprovedAmount,
            app.Term,
            app.InterestRate,
            app.InstallmentAmount,
            app.Status,
            LoanApplicationStatusHelper.GetStatusText(app.Status),
            app.ApplicationDate,
            app.ApprovalDate,
            app.DisbursementDate,
            app.GuaranteeType,
            app.Periodicity,
            app.Remarks,
            app.Salary,
            app.OtherIncome,
            app.MonthlyDeductions,
            app.PaymentCapacity,
            app.Codeudor1,
            app.Codeudor2,
            app.EntryUserId,
            app.AuthorizingUserId);

        return Result.Success(dto);
    }
}

// --- Amortization Preview Query ---

public record GetAmortizationPreviewQuery(
    decimal Amount,
    decimal AnnualRate,
    int TermMonths) : IRequest<Result<List<AmortizationLineDto>>>;

public class GetAmortizationPreviewQueryHandler
    : IRequestHandler<GetAmortizationPreviewQuery, Result<List<AmortizationLineDto>>>
{
    public Task<Result<List<AmortizationLineDto>>> Handle(
        GetAmortizationPreviewQuery request, CancellationToken ct)
    {
        var monthlyRate = request.AnnualRate / 100m / 12m;
        decimal installment;
        if (monthlyRate > 0)
        {
            var factor = (double)monthlyRate * Math.Pow(1 + (double)monthlyRate, request.TermMonths)
                         / (Math.Pow(1 + (double)monthlyRate, request.TermMonths) - 1);
            installment = request.Amount * (decimal)factor;
        }
        else
        {
            installment = request.Amount / request.TermMonths;
        }
        installment = Math.Round(installment, 0);

        var lines = new List<AmortizationLineDto>();
        var balance = request.Amount;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (int period = 1; period <= request.TermMonths; period++)
        {
            var interest = Math.Round(balance * monthlyRate, 0);
            var capital = installment - interest;

            if (period == request.TermMonths)
            {
                capital = balance;
            }
            if (capital > balance) capital = balance;

            lines.Add(new AmortizationLineDto(
                period,
                today.AddMonths(period),
                capital,
                interest,
                capital + interest,
                balance - capital));

            balance -= capital;
            if (balance < 0) balance = 0;
        }

        return Task.FromResult(Result.Success(lines));
    }
}
