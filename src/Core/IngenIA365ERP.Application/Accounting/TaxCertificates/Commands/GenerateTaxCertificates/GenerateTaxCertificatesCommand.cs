using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.TaxCertificates.Commands.GenerateTaxCertificates;

// DTOs
public record TaxCertificateGenerationResultDto
{
    public int CertificatesGenerated { get; init; }
    public int Year { get; init; }
    public decimal TotalWithholding { get; init; }
}

// Command
public record GenerateTaxCertificatesCommand(int Year) : IRequest<Result<TaxCertificateGenerationResultDto>>;

// Handler
public class GenerateTaxCertificatesCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<GenerateTaxCertificatesCommand, Result<TaxCertificateGenerationResultDto>>
{
    public async Task<Result<TaxCertificateGenerationResultDto>> Handle(
        GenerateTaxCertificatesCommand request,
        CancellationToken cancellationToken)
    {
        var periodStart = $"{request.Year}01";
        var periodEnd = $"{request.Year}12";

        // Query withholding journal entries grouped by person
        // Withholding accounts typically start with "2365" (Retencion en la Fuente)
        var withholdingAccounts = await context.ChartOfAccounts
            .AsNoTracking()
            .Where(a => a.AccountCode.StartsWith("2365") && !a.IsDeleted)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (withholdingAccounts.Count == 0)
            return Result.Failure<TaxCertificateGenerationResultDto>(
                new Error("TaxCert.NoWithholdingAccounts", "No se encontraron cuentas de retencion en la fuente."));

        var withholdingByPerson = await context.JournalEntries
            .AsNoTracking()
            .Where(j => withholdingAccounts.Contains(j.AccountId) &&
                        j.PeriodCode != null &&
                        j.PeriodCode.CompareTo(periodStart) >= 0 &&
                        j.PeriodCode.CompareTo(periodEnd) <= 0 &&
                        j.PersonId != null &&
                        !j.IsDeleted)
            .GroupBy(j => j.PersonId!.Value)
            .Select(g => new
            {
                PersonId = g.Key,
                TotalWithholding = g.Sum(j => j.CreditAmount - j.DebitAmount),
                TotalBase = g.Sum(j => j.BaseAmount)
            })
            .Where(x => x.TotalWithholding > 0)
            .ToListAsync(cancellationToken);

        if (withholdingByPerson.Count == 0)
            return Result.Success(new TaxCertificateGenerationResultDto
            {
                CertificatesGenerated = 0,
                Year = request.Year,
                TotalWithholding = 0
            });

        // Load company info (first company)
        var company = await context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => !c.IsDeleted, cancellationToken);

        var periodCodeStr = request.Year.ToString();
        var totalWithholding = 0m;
        var generated = 0;

        // Delete existing certificates for this year
        var existing = await context.TaxCertificates
            .Where(tc => tc.PeriodCode == periodCodeStr && !tc.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var e in existing)
        {
            e.IsDeleted = true;
            e.DeletedAt = dateTime.UtcNow;
            e.DeletedBy = currentUser.UserName;
        }

        foreach (var w in withholdingByPerson)
        {
            var cert = new TaxCertificate
            {
                PeriodCode = periodCodeStr,
                PayrollCompanyId = 1,
                EmployeeId = w.PersonId,
                Value34 = w.TotalBase,
                Value35 = w.TotalWithholding,
                StartDate = new DateTime(request.Year, 1, 1),
                EndDate = new DateTime(request.Year, 12, 31),
                IssueDate = dateTime.UtcNow,
                IssuedAt = company?.Name ?? "IngenIA365ERP",
                PayerName = company?.Name ?? "",
                PayerTaxId = company?.TaxId ?? "",
                Rate = w.TotalBase > 0 ? w.TotalWithholding / w.TotalBase * 100 : 0,
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.TaxCertificates.Add(cert);

            totalWithholding += w.TotalWithholding;
            generated++;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new TaxCertificateGenerationResultDto
        {
            CertificatesGenerated = generated,
            Year = request.Year,
            TotalWithholding = totalWithholding
        });
    }
}
