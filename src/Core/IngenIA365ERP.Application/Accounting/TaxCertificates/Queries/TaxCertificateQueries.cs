using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.TaxCertificates.Queries;

// DTOs
public record TaxCertificateDto
{
    public Guid PublicId { get; init; }
    public string PersonName { get; init; } = string.Empty;
    public string TaxId { get; init; } = string.Empty;
    public int Year { get; init; }
    public decimal TotalBase { get; init; }
    public decimal TotalWithholding { get; init; }
    public decimal Rate { get; init; }
    public DateTime IssueDate { get; init; }
    public string PayerName { get; init; } = string.Empty;
}

// Query: Individual certificate
public record GetTaxCertificateQuery(Guid PersonPublicId, int Year)
    : IRequest<Result<TaxCertificateDto>>;

public class GetTaxCertificateQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetTaxCertificateQuery, Result<TaxCertificateDto>>
{
    public async Task<Result<TaxCertificateDto>> Handle(
        GetTaxCertificateQuery request,
        CancellationToken cancellationToken)
    {
        var person = await context.People
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted,
                cancellationToken);

        if (person is null)
            return Result.Failure<TaxCertificateDto>(new Error("TaxCert.PersonNotFound", "Persona no encontrada."));

        var periodCode = request.Year.ToString();

        var cert = await context.TaxCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(tc =>
                tc.EmployeeId == person.Id &&
                tc.PeriodCode == periodCode &&
                !tc.IsDeleted,
                cancellationToken);

        if (cert is null)
            return Result.Failure<TaxCertificateDto>(
                new Error("TaxCert.NotFound", $"No se encontro certificado de retencion para el ano {request.Year}."));

        return Result.Success(new TaxCertificateDto
        {
            PublicId = cert.PublicId,
            PersonName = $"{person.FirstName} {person.LastName}",
            TaxId = person.TaxId,
            Year = request.Year,
            TotalBase = cert.Value34,
            TotalWithholding = cert.Value35,
            Rate = cert.Rate,
            IssueDate = cert.IssueDate,
            PayerName = cert.PayerName
        });
    }
}

// Query: List certificates for a year
public record ListTaxCertificatesQuery(int Year) : IRequest<Result<List<TaxCertificateDto>>>;

public class ListTaxCertificatesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListTaxCertificatesQuery, Result<List<TaxCertificateDto>>>
{
    public async Task<Result<List<TaxCertificateDto>>> Handle(
        ListTaxCertificatesQuery request,
        CancellationToken cancellationToken)
    {
        var periodCode = request.Year.ToString();

        var certs = await context.TaxCertificates
            .AsNoTracking()
            .Where(tc => tc.PeriodCode == periodCode && !tc.IsDeleted)
            .Include(tc => tc.Employee)
            .OrderBy(tc => tc.EmployeeId)
            .ToListAsync(cancellationToken);

        // Load person names
        var employeeIds = certs.Select(c => c.EmployeeId).Distinct().ToList();
        var people = await context.People
            .AsNoTracking()
            .Where(p => employeeIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var result = certs.Select(c =>
        {
            var person = people.GetValueOrDefault(c.EmployeeId);
            return new TaxCertificateDto
            {
                PublicId = c.PublicId,
                PersonName = person != null ? $"{person.FirstName} {person.LastName}" : c.EmployeeId.ToString(),
                TaxId = person?.TaxId ?? "",
                Year = request.Year,
                TotalBase = c.Value34,
                TotalWithholding = c.Value35,
                Rate = c.Rate,
                IssueDate = c.IssueDate,
                PayerName = c.PayerName
            };
        }).ToList();

        return Result.Success(result);
    }
}
