using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Classifications.Commands.ClassifyPortfolio;

// DTOs
public record ClassificationResultDto
{
    public int PortfoliosProcessed { get; init; }
    public List<ClassificationBucketDto> Distribution { get; init; } = [];
    public decimal TotalProvision { get; init; }
    public Guid? AccountingDocumentId { get; init; }
}

public record ClassificationBucketDto
{
    public string Category { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal TotalBalance { get; init; }
    public decimal ProvisionRate { get; init; }
    public decimal ProvisionAmount { get; init; }
}

// Command
public record ClassifyPortfolioCommand(DateOnly ClassificationDate) : IRequest<Result<ClassificationResultDto>>;

// Handler
public class ClassifyPortfolioCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<ClassifyPortfolioCommand, Result<ClassificationResultDto>>
{
    // SFC provision rates by category
    private static readonly Dictionary<string, decimal> ProvisionRates = new()
    {
        ["A"] = 1m,
        ["B"] = 3.2m,
        ["C"] = 20m,
        ["D"] = 50m,
        ["E"] = 100m
    };

    public async Task<Result<ClassificationResultDto>> Handle(
        ClassifyPortfolioCommand request,
        CancellationToken cancellationToken)
    {
        var portfolios = await context.LoanPortfolios
            .Where(p => p.CurrentBalance > 0 && !p.IsDeleted && p.ClosingDate == null)
            .Include(p => p.Person)
            .Include(p => p.CreditLine)
            .ToListAsync(cancellationToken);

        if (portfolios.Count == 0)
            return Result.Failure<ClassificationResultDto>(
                new Error("Classification.NoPortfolios", "No se encontraron obligaciones activas para calificar."));

        var acctPeriod = request.ClassificationDate.Year * 100 + request.ClassificationDate.Month;
        var buckets = new Dictionary<string, (int Count, decimal Balance, decimal Provision)>
        {
            ["A"] = (0, 0, 0),
            ["B"] = (0, 0, 0),
            ["C"] = (0, 0, 0),
            ["D"] = (0, 0, 0),
            ["E"] = (0, 0, 0)
        };

        foreach (var p in portfolios)
        {
            // SFC classification rules based on days overdue
            var category = p.DaysOverdue switch
            {
                <= 30 => "A",
                <= 60 => "B",
                <= 90 => "C",
                <= 120 => "D",
                _ => "E"
            };

            var rate = ProvisionRates[category];
            var provision = p.CurrentBalance * rate / 100m;

            // Upsert classification record
            var existing = await context.PortfolioClassifications
                .FirstOrDefaultAsync(c =>
                    c.PersonCode == p.IdentificationNumber &&
                    c.PortfolioNumber == (int)p.PortfolioNumber &&
                    c.AccountingPeriod == acctPeriod &&
                    !c.IsDeleted,
                    cancellationToken);

            if (existing is not null)
            {
                existing.Category = category;
                existing.TotalBalance = p.CurrentBalance;
                existing.InterestBalance = p.InterestBalanceCurrent;
                existing.DefaultBalance = p.DefaultBalanceCurrent;
                existing.ProvisionBalance = provision;
                existing.Rate = rate;
                existing.DaysOverdue = p.DaysOverdue;
                existing.UpdatedAt = dateTime.UtcNow;
                existing.UpdatedBy = currentUser.UserName;
            }
            else
            {
                var classification = new PortfolioClassification
                {
                    PersonCode = p.IdentificationNumber,
                    CreditLineCode = p.CreditLine?.CreditLineId.ToString() ?? p.CreditLineId.ToString(),
                    PortfolioNumber = (int)p.PortfolioNumber,
                    AccountingPeriod = acctPeriod,
                    Category = category,
                    TotalBalance = p.CurrentBalance,
                    InterestBalance = p.InterestBalanceCurrent,
                    DefaultBalance = p.DefaultBalanceCurrent,
                    ProvisionBalance = provision,
                    Rate = rate,
                    DaysOverdue = p.DaysOverdue,
                    PersonName = p.Person != null
                        ? $"{p.Person.FirstName} {p.Person.LastName}"
                        : p.IdentificationNumber,
                    CreatedAt = dateTime.UtcNow,
                    CreatedBy = currentUser.UserName
                };
                context.PortfolioClassifications.Add(classification);
            }

            // Update portfolio provision
            p.ProvisionRate = rate;
            p.ProvisionAmount = provision;
            p.UpdatedAt = dateTime.UtcNow;
            p.UpdatedBy = currentUser.UserName;

            var bucket = buckets[category];
            buckets[category] = (bucket.Count + 1, bucket.Balance + p.CurrentBalance, bucket.Provision + provision);
        }

        // Create accounting document for provisions
        var totalProvision = buckets.Values.Sum(b => b.Provision);
        Guid? docId = null;

        // E3 (feature 009): contabilización por AccountingPoster pendiente

        await context.SaveChangesAsync(cancellationToken);

        var distribution = buckets.Select(b => new ClassificationBucketDto
        {
            Category = b.Key,
            Count = b.Value.Count,
            TotalBalance = b.Value.Balance,
            ProvisionRate = ProvisionRates[b.Key],
            ProvisionAmount = b.Value.Provision
        }).ToList();

        return Result.Success(new ClassificationResultDto
        {
            PortfoliosProcessed = portfolios.Count,
            Distribution = distribution,
            TotalProvision = totalProvision,
            AccountingDocumentId = docId
        });
    }
}
