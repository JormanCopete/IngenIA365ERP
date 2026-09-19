using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.PayrollDeductions.Commands.ProcessPayrollDeduction;

// DTOs
public record PayrollDeductionResultDto
{
    public int EmployeesProcessed { get; init; }
    public int PortfoliosProcessed { get; init; }
    public decimal TotalDeducted { get; init; }
    public Guid? AccountingDocumentId { get; init; }
}

// Command
public record ProcessPayrollDeductionCommand(Guid PayPeriodPublicId) : IRequest<Result<PayrollDeductionResultDto>>;

// Handler
public class ProcessPayrollDeductionCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<ProcessPayrollDeductionCommand, Result<PayrollDeductionResultDto>>
{
    public async Task<Result<PayrollDeductionResultDto>> Handle(
        ProcessPayrollDeductionCommand request,
        CancellationToken cancellationToken)
    {
        var payPeriod = await context.PayPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(pp => pp.PublicId == request.PayPeriodPublicId && !pp.IsDeleted,
                cancellationToken);

        if (payPeriod is null)
            return Result.Failure<PayrollDeductionResultDto>(
                new Error("PayrollDeduction.PeriodNotFound", "Periodo de pago no encontrado."));

        // Load active portfolios with payroll deduction enabled
        var portfolios = await context.LoanPortfolios
            .Where(p => p.CurrentBalance > 0 &&
                        !p.IsDeleted &&
                        p.ClosingDate == null &&
                        p.DeductionType == "NM") // NM = Nomina (Payroll)
            .Include(p => p.Person)
            .ToListAsync(cancellationToken);

        if (portfolios.Count == 0)
            return Result.Failure<PayrollDeductionResultDto>(
                new Error("PayrollDeduction.NoPortfolios", "No se encontraron obligaciones con descuento por nomina."));

        var totalDeducted = 0m;
        var processedPortfolios = 0;
        var employeeSet = new HashSet<string>();
        var period = payPeriod.PeriodId;

        foreach (var portfolio in portfolios)
        {
            var deductionAmount = portfolio.InstallmentAmount;
            if (deductionAmount <= 0) continue;

            // Cap deduction to current balance
            if (deductionAmount > portfolio.CurrentBalance)
                deductionAmount = portfolio.CurrentBalance;

            // Apply payment distribution: Interest first, then capital
            var interestPayment = Math.Min(deductionAmount, portfolio.InterestBalanceCurrent);
            var capitalPayment = deductionAmount - interestPayment;

            // Update portfolio balances
            portfolio.InterestBalanceCurrent -= interestPayment;
            portfolio.CapitalBalanceCurrent -= capitalPayment;
            portfolio.CurrentBalance -= capitalPayment;
            portfolio.CapitalAppliedPayroll += capitalPayment;
            portfolio.InterestAppliedPayroll += interestPayment;
            portfolio.PaidInstallments += 1;
            portfolio.LastPaymentDate = DateOnly.FromDateTime(payPeriod.EndDate);
            portfolio.UpdatedAt = dateTime.UtcNow;
            portfolio.UpdatedBy = currentUser.UserName;

            // Create payroll deduction entry
            var entry = new PayrollDeductionEntry
            {
                Period = period,
                CompanyCode = portfolio.DiscountCompany,
                PersonCode = portfolio.IdentificationNumber,
                EntryType = "DN",
                ConceptCode = portfolio.PayrollCode,
                StartDate = payPeriod.StartDate.ToString("yyyyMMdd"),
                EndDate = payPeriod.EndDate.ToString("yyyyMMdd"),
                Amount = deductionAmount,
                TotalAmount = portfolio.InstallmentAmount,
                AccumulatedAmount = portfolio.CapitalAppliedPayroll + portfolio.InterestAppliedPayroll,
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.PayrollDeductionEntries.Add(entry);

            totalDeducted += deductionAmount;
            processedPortfolios++;
            employeeSet.Add(portfolio.IdentificationNumber);
        }

        // Create consolidated accounting document
        Guid? docId = null;
        // E3 (feature 009): contabilización por AccountingPoster pendiente

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new PayrollDeductionResultDto
        {
            EmployeesProcessed = employeeSet.Count,
            PortfoliosProcessed = processedPortfolios,
            TotalDeducted = totalDeducted,
            AccountingDocumentId = docId
        });
    }
}
