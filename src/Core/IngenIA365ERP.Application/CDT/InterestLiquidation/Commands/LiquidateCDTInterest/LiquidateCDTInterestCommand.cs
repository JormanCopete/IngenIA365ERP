using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.CDT;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.CDT.InterestLiquidation.Commands.LiquidateCDTInterest;

// DTOs
public record CDTLiquidationResultDto
{
    public int CertificatesProcessed { get; init; }
    public decimal TotalInterest { get; init; }
    public Guid? AccountingDocumentId { get; init; }
    public DateOnly LiquidationDate { get; init; }
}

public record CDTLiquidationPreviewLineDto
{
    public string CertificateNumber { get; init; } = string.Empty;
    public string PersonName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal Rate { get; init; }
    public int Days { get; init; }
    public decimal ProjectedInterest { get; init; }
}

// Command
public record LiquidateCDTInterestCommand(DateOnly LiquidationDate) : IRequest<Result<CDTLiquidationResultDto>>;

// Handler
public class LiquidateCDTInterestCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<LiquidateCDTInterestCommand, Result<CDTLiquidationResultDto>>
{
    public async Task<Result<CDTLiquidationResultDto>> Handle(
        LiquidateCDTInterestCommand request,
        CancellationToken cancellationToken)
    {
        var certificates = await context.Certificates
            .Where(c => c.Status != "C" && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        if (certificates.Count == 0)
            return Result.Failure<CDTLiquidationResultDto>(
                new Error("CDTLiquidation.NoCertificates", "No se encontraron CDT activos para liquidar."));

        var totalInterest = 0m;
        var processedCount = 0;

        foreach (var cdt in certificates)
        {
            var lastLiq = cdt.AccrualDate ?? cdt.IssueDate;
            var days = request.LiquidationDate.DayNumber - lastLiq.DayNumber;

            if (days <= 0) continue;

            var interest = cdt.Amount * cdt.InterestRate / 100m / 360m * days;

            // Create certificate entry
            var entry = new CertificateEntry
            {
                CertificateId = cdt.Id,
                PersonId = cdt.PersonId,
                CreditLineId = cdt.CreditLineId,
                EntryDate = request.LiquidationDate,
                OpeningDate = cdt.IssueDate,
                EntryType = "INT",
                PreviousRate = cdt.InterestRate,
                CurrentRate = cdt.InterestRate,
                Amount = Math.Round(interest, 2),
                Description = $"Liquidacion intereses CDT {cdt.CertificateNumber} - {days} dias",
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.CertificateEntries.Add(entry);

            // Update certificate accrual date
            cdt.AccrualDate = request.LiquidationDate;
            cdt.UpdatedAt = dateTime.UtcNow;
            cdt.UpdatedBy = currentUser.UserName;

            totalInterest += Math.Round(interest, 2);
            processedCount++;
        }

        // Create accounting document
        Guid? docId = null;
        if (totalInterest > 0)
        {
            var periodCode = request.LiquidationDate.Year * 100 + request.LiquidationDate.Month;
            var doc = new AccountingDocument
            {
                VoucherTypeCode = "CDT",
                DocumentNumber = 0,
                Detail = $"Liquidacion intereses CDT {request.LiquidationDate:yyyy-MM-dd}",
                TotalDebit = totalInterest,
                TotalCredit = totalInterest,
                DocumentDate = request.LiquidationDate,
                IsClosed = true,
                PeriodCode = periodCode,
                ModuleCode = "CDT",
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.AccountingDocuments.Add(doc);
            docId = doc.PublicId;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new CDTLiquidationResultDto
        {
            CertificatesProcessed = processedCount,
            TotalInterest = totalInterest,
            AccountingDocumentId = docId,
            LiquidationDate = request.LiquidationDate
        });
    }
}

// Preview Query
public record GetCDTLiquidationPreviewQuery(DateOnly LiquidationDate)
    : IRequest<Result<List<CDTLiquidationPreviewLineDto>>>;

public class GetCDTLiquidationPreviewQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCDTLiquidationPreviewQuery, Result<List<CDTLiquidationPreviewLineDto>>>
{
    public async Task<Result<List<CDTLiquidationPreviewLineDto>>> Handle(
        GetCDTLiquidationPreviewQuery request,
        CancellationToken cancellationToken)
    {
        var certificates = await context.Certificates
            .AsNoTracking()
            .Where(c => c.Status != "C" && !c.IsDeleted)
            .OrderBy(c => c.CertificateNumber)
            .ToListAsync(cancellationToken);

        // Load person names
        var personIds = certificates.Select(c => c.PersonId).Distinct().ToList();
        var people = await context.People
            .AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var result = certificates.Select(cdt =>
        {
            var lastLiq = cdt.AccrualDate ?? cdt.IssueDate;
            var days = request.LiquidationDate.DayNumber - lastLiq.DayNumber;
            if (days < 0) days = 0;
            var interest = cdt.Amount * cdt.InterestRate / 100m / 360m * days;

            var personName = people.TryGetValue(cdt.PersonId, out var person)
                ? $"{person.FirstName} {person.LastName}"
                : cdt.PersonId.ToString();

            return new CDTLiquidationPreviewLineDto
            {
                CertificateNumber = cdt.CertificateNumber,
                PersonName = personName,
                Amount = cdt.Amount,
                Rate = cdt.InterestRate,
                Days = days,
                ProjectedInterest = Math.Round(interest, 2)
            };
        }).ToList();

        return Result.Success(result);
    }
}
