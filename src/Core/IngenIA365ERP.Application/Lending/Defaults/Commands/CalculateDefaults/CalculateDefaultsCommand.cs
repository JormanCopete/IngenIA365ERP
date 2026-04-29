using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Defaults.Commands.CalculateDefaults;

public record CalculateDefaultsCommand(DateOnly CalculationDate) : IRequest<Result<CalculateDefaultsResultDto>>;

public record CalculateDefaultsResultDto(
    int PortfoliosProcessed,
    int NewDefaults,
    int UpdatedDefaults,
    int ClearedDefaults);

public class CalculateDefaultsCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CalculateDefaultsCommand, Result<CalculateDefaultsResultDto>>
{
    public async Task<Result<CalculateDefaultsResultDto>> Handle(
        CalculateDefaultsCommand request, CancellationToken ct)
    {
        var calculationDate = request.CalculationDate;
        var accrualPeriod = calculationDate.Year * 100 + calculationDate.Month;

        // Get all active portfolios
        var portfolios = await context.LoanPortfolios
            .Where(lp => !lp.IsDeleted && lp.CurrentBalance > 0)
            .ToListAsync(ct);

        int processed = 0, newDefaults = 0, updatedDefaults = 0, clearedDefaults = 0;

        foreach (var portfolio in portfolios)
        {
            processed++;

            // Find overdue installments
            var overdueInstallments = await context.PendingInstallments
                .Where(pi => pi.PortfolioNumber == portfolio.PortfolioNumber
                          && pi.CreditLineId == portfolio.CreditLineId
                          && !pi.IsDeleted
                          && pi.ProcessDate.HasValue
                          && pi.ProcessDate.Value < calculationDate
                          && (pi.BalanceCapital > 0 || pi.BalanceInterest > 0))
                .ToListAsync(ct);

            if (overdueInstallments.Count == 0)
            {
                // Clear any existing default for this portfolio
                portfolio.DaysOverdue = 0;
                portfolio.Category = "A";
                portfolio.UpdatedAt = dateTime.UtcNow;
                portfolio.UpdatedBy = currentUser.UserName;

                // Check if existing default record should be cleared
                var existingDefault = await context.DefaultRecords
                    .FirstOrDefaultAsync(dr => dr.PortfolioNumber == portfolio.PortfolioNumber
                                            && dr.CreditLineId == portfolio.CreditLineId
                                            && dr.AccrualPeriod == accrualPeriod
                                            && !dr.IsDeleted, ct);
                if (existingDefault is not null)
                {
                    existingDefault.IsDeleted = true;
                    existingDefault.DeletedAt = dateTime.UtcNow;
                    existingDefault.DeletedBy = currentUser.UserName;
                    clearedDefaults++;
                }
                continue;
            }

            // Calculate days overdue from the oldest unpaid installment
            var oldestOverdue = overdueInstallments
                .Where(pi => pi.ProcessDate.HasValue)
                .OrderBy(pi => pi.ProcessDate)
                .First();
            var daysOverdue = calculationDate.DayNumber - oldestOverdue.ProcessDate!.Value.DayNumber;
            if (daysOverdue < 0) daysOverdue = 0;

            // Colombian regulation classification (SFC Circular 100)
            var classification = daysOverdue switch
            {
                <= 30 => "A",    // Normal
                <= 60 => "B",    // Aceptable
                <= 90 => "C",    // Apreciable
                <= 180 => "D",   // Significativo
                _ => "E"         // Incobrable
            };

            // Update portfolio
            portfolio.DaysOverdue = daysOverdue;
            portfolio.Category = classification;
            portfolio.LastDefaultDate = calculationDate;
            portfolio.UpdatedAt = dateTime.UtcNow;
            portfolio.UpdatedBy = currentUser.UserName;

            // Sum balances from overdue installments
            var totalCapitalBalance = overdueInstallments.Sum(pi => pi.BalanceCapital);
            var totalInterestBalance = overdueInstallments.Sum(pi => pi.BalanceInterest);
            var totalExtraBalance = overdueInstallments.Sum(pi => pi.BalanceExtra);
            var totalDefaultBalance = overdueInstallments.Sum(pi => pi.DefaultInterestBalance);

            var personCode = oldestOverdue.PersonCode;

            // Upsert DefaultRecord
            var defaultRecord = await context.DefaultRecords
                .FirstOrDefaultAsync(dr => dr.PortfolioNumber == portfolio.PortfolioNumber
                                        && dr.CreditLineId == portfolio.CreditLineId
                                        && dr.AccrualPeriod == accrualPeriod
                                        && !dr.IsDeleted, ct);

            if (defaultRecord is null)
            {
                defaultRecord = new DefaultRecord
                {
                    PersonCode = personCode,
                    CreditLineId = portfolio.CreditLineId,
                    PortfolioNumber = portfolio.PortfolioNumber,
                    AccrualPeriod = accrualPeriod,
                    AccountingPeriod = accrualPeriod,
                    DaysOverdue = daysOverdue,
                    CapitalBalance = totalCapitalBalance,
                    InterestBalance = totalInterestBalance,
                    ExtraBalance = totalExtraBalance,
                    DefaultBalance = totalDefaultBalance,
                    CreatedAt = dateTime.UtcNow,
                    CreatedBy = currentUser.UserName
                };
                context.DefaultRecords.Add(defaultRecord);
                newDefaults++;
            }
            else
            {
                defaultRecord.DaysOverdue = daysOverdue;
                defaultRecord.CapitalBalance = totalCapitalBalance;
                defaultRecord.InterestBalance = totalInterestBalance;
                defaultRecord.ExtraBalance = totalExtraBalance;
                defaultRecord.DefaultBalance = totalDefaultBalance;
                defaultRecord.UpdatedAt = dateTime.UtcNow;
                defaultRecord.UpdatedBy = currentUser.UserName;
                updatedDefaults++;
            }
        }

        await context.SaveChangesAsync(ct);

        return Result.Success(new CalculateDefaultsResultDto(
            processed, newDefaults, updatedDefaults, clearedDefaults));
    }
}

public class CalculateDefaultsCommandValidator : AbstractValidator<CalculateDefaultsCommand>
{
    public CalculateDefaultsCommandValidator()
    {
        RuleFor(x => x.CalculationDate)
            .NotEmpty().WithMessage("Fecha de calculo requerida.");
    }
}
