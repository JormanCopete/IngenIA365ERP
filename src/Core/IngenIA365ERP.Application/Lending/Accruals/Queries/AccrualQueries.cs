using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Accruals.Queries;

// DTOs
public record AccrualPreviewLineDto
{
    public long PortfolioNumber { get; init; }
    public string PersonName { get; init; } = string.Empty;
    public string CreditLine { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public decimal Rate { get; init; }
    public int Days { get; init; }
    public decimal ProjectedInterest { get; init; }
    public decimal ProjectedDefault { get; init; }
}

// Query
public record GetAccrualPreviewQuery(DateOnly AccrualDate, Guid? CreditLinePublicId)
    : IRequest<Result<List<AccrualPreviewLineDto>>>;

public class GetAccrualPreviewQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAccrualPreviewQuery, Result<List<AccrualPreviewLineDto>>>
{
    public async Task<Result<List<AccrualPreviewLineDto>>> Handle(
        GetAccrualPreviewQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.LoanPortfolios
            .AsNoTracking()
            .Where(p => p.CurrentBalance > 0 && !p.IsDeleted && p.ClosingDate == null);

        if (request.CreditLinePublicId.HasValue)
        {
            var creditLine = await context.CreditLineParameters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.PublicId == request.CreditLinePublicId.Value && !c.IsDeleted,
                    cancellationToken);

            if (creditLine is null)
                return Result.Failure<List<AccrualPreviewLineDto>>(
                    new Error("Accrual.CreditLineNotFound", "Linea de credito no encontrada."));

            query = query.Where(p => p.CreditLineId == creditLine.Id);
        }

        var portfolios = await query
            .Include(p => p.Person)
            .Include(p => p.CreditLine)
            .OrderBy(p => p.PortfolioNumber)
            .ToListAsync(cancellationToken);

        var result = portfolios.Select(p =>
        {
            var lastAccrual = p.LastAccrualDate ?? p.DisbursementDate;
            var days = request.AccrualDate.DayNumber - lastAccrual.DayNumber;
            if (days < 0) days = 0;

            var interest = p.CurrentBalance * p.InterestRate / 100m / 360m * days;
            var defaultInterest = 0m;
            if (p.DaysOverdue > 0)
            {
                var defaultRate = p.InterestRate * 1.5m;
                var overdueBalance = p.CapitalBalanceCurrent + p.InterestBalanceCurrent;
                defaultInterest = overdueBalance * defaultRate / 100m / 360m * days;
            }

            return new AccrualPreviewLineDto
            {
                PortfolioNumber = p.PortfolioNumber,
                PersonName = p.Person != null
                    ? NombreDePersona.Completo(p.Person)
                    : p.IdentificationNumber,
                CreditLine = p.CreditLine?.Description ?? p.CreditLineId.ToString(),
                Balance = p.CurrentBalance,
                Rate = p.InterestRate,
                Days = days,
                ProjectedInterest = Math.Round(interest, 2),
                ProjectedDefault = Math.Round(defaultInterest, 2)
            };
        }).ToList();

        return Result.Success(result);
    }
}
