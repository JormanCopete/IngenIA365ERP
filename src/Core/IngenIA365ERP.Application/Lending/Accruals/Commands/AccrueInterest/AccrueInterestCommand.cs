using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Accruals.Commands.AccrueInterest;

// DTOs
public record AccrualResultDto
{
    public int PortfoliosProcessed { get; init; }
    public decimal TotalInterest { get; init; }
    public decimal TotalDefaultInterest { get; init; }
    public Guid? AccountingDocumentId { get; init; }
    public DateOnly AccrualDate { get; init; }
}

// Command
public record AccrueInterestCommand(DateOnly AccrualDate, Guid? CreditLinePublicId) : IRequest<Result<AccrualResultDto>>;

// Handler
public class AccrueInterestCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<AccrueInterestCommand, Result<AccrualResultDto>>
{
    public async Task<Result<AccrualResultDto>> Handle(
        AccrueInterestCommand request,
        CancellationToken cancellationToken)
    {
        // Load active portfolios
        var query = context.LoanPortfolios
            .Where(p => p.CurrentBalance > 0 && !p.IsDeleted && p.ClosingDate == null);

        // Filter by credit line if specified
        if (request.CreditLinePublicId.HasValue)
        {
            var creditLine = await context.CreditLineParameters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.PublicId == request.CreditLinePublicId.Value && !c.IsDeleted,
                    cancellationToken);

            if (creditLine is null)
                return Result.Failure<AccrualResultDto>(new Error("Accrual.CreditLineNotFound", "Linea de credito no encontrada."));

            query = query.Where(p => p.CreditLineId == creditLine.Id);
        }

        var portfolios = await query.ToListAsync(cancellationToken);

        if (portfolios.Count == 0)
            return Result.Failure<AccrualResultDto>(new Error("Accrual.NoPortfolios", "No se encontraron obligaciones activas para causacion."));

        var totalInterest = 0m;
        var totalDefault = 0m;
        var processedCount = 0;
        var accrualPeriod = request.AccrualDate.Year * 100 + request.AccrualDate.Month;

        foreach (var portfolio in portfolios)
        {
            var lastAccrual = portfolio.LastAccrualDate ?? portfolio.DisbursementDate;
            var days = request.AccrualDate.DayNumber - lastAccrual.DayNumber;

            if (days <= 0) continue;

            // Regular interest
            var interest = portfolio.CurrentBalance * portfolio.InterestRate / 100m / 360m * days;

            // Default interest if overdue
            var defaultInterest = 0m;
            if (portfolio.DaysOverdue > 0)
            {
                var defaultRate = portfolio.InterestRate * 1.5m; // Typical SFC rule: 1.5x regular rate
                var overdueBalance = portfolio.CapitalBalanceCurrent + portfolio.InterestBalanceCurrent;
                defaultInterest = overdueBalance * defaultRate / 100m / 360m * days;
            }

            // Create accrual entry
            var entry = new Domain.Entities.Lending.AccrualEntry
            {
                PersonCode = portfolio.IdentificationNumber,
                CreditLineId = portfolio.CreditLineId,
                PortfolioNumber = portfolio.PortfolioNumber,
                AccrualPeriod = accrualPeriod,
                EntryType = "CA",
                Reason = "IN",
                EntryDate = request.AccrualDate,
                SystemDate = request.AccrualDate,
                UserId = currentUser.UserName ?? "SYSTEM",
                UserFullName = currentUser.UserName ?? "SYSTEM",
                Status = "A",
                ExpirationDate = request.AccrualDate,
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.AccrualEntries.Add(entry);

            // Update portfolio
            portfolio.InterestAccrued += interest;
            portfolio.DefaultInterest += defaultInterest;
            portfolio.LastAccrualDate = request.AccrualDate;
            portfolio.InterestBalanceCurrent += interest;
            portfolio.DefaultBalanceCurrent += defaultInterest;
            portfolio.UpdatedAt = dateTime.UtcNow;
            portfolio.UpdatedBy = currentUser.UserName;

            totalInterest += interest;
            totalDefault += defaultInterest;
            processedCount++;
        }

        // Create accounting document
        Guid? docId = null;
        // E3 (feature 009): contabilización por AccountingPoster pendiente

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new AccrualResultDto
        {
            PortfoliosProcessed = processedCount,
            TotalInterest = totalInterest,
            TotalDefaultInterest = totalDefault,
            AccountingDocumentId = docId,
            AccrualDate = request.AccrualDate
        });
    }
}
