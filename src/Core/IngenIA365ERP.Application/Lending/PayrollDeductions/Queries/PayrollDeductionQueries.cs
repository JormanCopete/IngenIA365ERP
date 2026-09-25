using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.PayrollDeductions.Queries;

// DTOs
public record DeductionPreviewLineDto
{
    public string EmployeeName { get; init; } = string.Empty;
    public string IdentificationNumber { get; init; } = string.Empty;
    public long PortfolioNumber { get; init; }
    public string CreditLine { get; init; } = string.Empty;
    public decimal InstallmentAmount { get; init; }
    public decimal CurrentBalance { get; init; }
}

// Query
public record GetPayrollDeductionPreviewQuery(Guid PayPeriodPublicId)
    : IRequest<Result<List<DeductionPreviewLineDto>>>;

public class GetPayrollDeductionPreviewQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPayrollDeductionPreviewQuery, Result<List<DeductionPreviewLineDto>>>
{
    public async Task<Result<List<DeductionPreviewLineDto>>> Handle(
        GetPayrollDeductionPreviewQuery request,
        CancellationToken cancellationToken)
    {
        var payPeriod = await context.PayPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(pp => pp.PublicId == request.PayPeriodPublicId && !pp.IsDeleted,
                cancellationToken);

        if (payPeriod is null)
            return Result.Failure<List<DeductionPreviewLineDto>>(
                new Error("PayrollDeduction.PeriodNotFound", "Periodo de pago no encontrado."));

        var portfolios = await context.LoanPortfolios
            .AsNoTracking()
            .Where(p => p.CurrentBalance > 0 &&
                        !p.IsDeleted &&
                        p.ClosingDate == null &&
                        p.DeductionType == "NM")
            .Include(p => p.Person)
            .Include(p => p.CreditLine)
            .OrderBy(p => p.IdentificationNumber)
            .ThenBy(p => p.PortfolioNumber)
            .ToListAsync(cancellationToken);

        var result = portfolios.Select(p => new DeductionPreviewLineDto
        {
            EmployeeName = p.Person != null
                ? NombreDePersona.Completo(p.Person)
                : p.IdentificationNumber,
            IdentificationNumber = p.IdentificationNumber,
            PortfolioNumber = p.PortfolioNumber,
            CreditLine = p.CreditLine?.Description ?? p.CreditLineId.ToString(),
            InstallmentAmount = p.InstallmentAmount,
            CurrentBalance = p.CurrentBalance
        }).ToList();

        return Result.Success(result);
    }
}
