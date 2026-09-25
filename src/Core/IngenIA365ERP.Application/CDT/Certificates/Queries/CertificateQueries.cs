using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.CDT.Certificates.Queries;

// --- DTOs ---

public record CertificateDto(
    Guid PublicId,
    string CertificateNumber,
    string PersonName,
    decimal Amount,
    decimal InterestRate,
    int Term,
    DateOnly IssueDate,
    DateOnly? MaturityDate,
    string Status,
    string StatusText,
    string? RenewalType);

public record CertificateDetailDto(
    Guid PublicId,
    string CertificateNumber,
    int PersonId,
    string PersonName,
    string PersonCode,
    decimal Amount,
    decimal InterestRate,
    int Term,
    DateOnly IssueDate,
    DateOnly? MaturityDate,
    string Status,
    string StatusText,
    string? RenewalType,
    bool IsCapitalized,
    decimal ProjectedInterest,
    int? PreviousCertificateId,
    List<CertificateEntryDto> Entries);

public record CertificateEntryDto(
    Guid PublicId,
    DateOnly EntryDate,
    string EntryType,
    string EntryTypeText,
    decimal Amount,
    decimal CurrentRate,
    string? Description);

// --- Status helper ---

public static class CertificateStatusHelper
{
    public static string GetStatusText(string code) => code switch
    {
        "V" => "Vigente",
        "C" => "Cancelado",
        "R" => "Renovado",
        "E" => "Vencido",
        _ => code
    };

    public static string GetEntryTypeText(string code) => code switch
    {
        "AP" => "Apertura",
        "RN" => "Renovacion",
        "CN" => "Cancelacion",
        "IN" => "Pago intereses",
        "AJ" => "Ajuste",
        _ => code
    };
}

// --- List Query ---

public record ListCertificatesQuery : IRequest<Result<PagedList<CertificateDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? PersonPublicId { get; init; }
    public string? Status { get; init; }
    public bool? NearExpiry { get; init; }
    public int NearExpiryDays { get; init; } = 30;
}

public class ListCertificatesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListCertificatesQuery, Result<PagedList<CertificateDto>>>
{
    public async Task<Result<PagedList<CertificateDto>>> Handle(
        ListCertificatesQuery request, CancellationToken ct)
    {
        var query = context.Certificates.AsNoTracking()
            .Where(c => !c.IsDeleted);

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(c => c.Status == request.Status);

        if (request.PersonPublicId.HasValue)
        {
            var person = await context.People.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId.Value && !p.IsDeleted, ct);
            if (person is not null)
                query = query.Where(c => c.PersonId == person.Id);
        }

        if (request.NearExpiry == true)
        {
            var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(request.NearExpiryDays);
            query = query.Where(c => c.Status == "V" && c.MaturityDate.HasValue && c.MaturityDate.Value <= cutoff);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.IssueDate)
            .ThenByDescending(c => c.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new
            {
                c.PublicId,
                c.CertificateNumber,
                c.PersonId,
                c.Amount,
                c.InterestRate,
                c.Term,
                c.IssueDate,
                c.MaturityDate,
                c.Status,
                c.RenewalType
            })
            .ToListAsync(ct);

        // Batch load person names
        var personIds = items.Select(i => i.PersonId).Distinct().ToList();
        var personNames = await context.People.AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName), ct);

        var dtos = items.Select(c => new CertificateDto(
            c.PublicId,
            c.CertificateNumber,
            personNames.TryGetValue(c.PersonId, out var name) ? name : $"ID:{c.PersonId}",
            c.Amount,
            c.InterestRate,
            c.Term,
            c.IssueDate,
            c.MaturityDate,
            c.Status,
            CertificateStatusHelper.GetStatusText(c.Status),
            c.RenewalType
        )).ToList();

        return Result.Success(new PagedList<CertificateDto>(dtos, totalCount, request.PageNumber, request.PageSize));
    }
}

// --- Get By Id Query ---

public record GetCertificateByIdQuery(Guid PublicId) : IRequest<Result<CertificateDetailDto>>;

public class GetCertificateByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCertificateByIdQuery, Result<CertificateDetailDto>>
{
    public async Task<Result<CertificateDetailDto>> Handle(
        GetCertificateByIdQuery request, CancellationToken ct)
    {
        var cert = await context.Certificates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == request.PublicId && !c.IsDeleted, ct);

        if (cert is null)
            return Result.Failure<CertificateDetailDto>(new Error("Certificate.NotFound",
                "Certificado no encontrado."));

        var person = await context.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == cert.PersonId, ct);
        var personName = person is not null ? PersonFactory.NombreVisible(person.FirstName, person.OtherNames, person.LastName, person.SecondLastName, person.BusinessName) : $"ID:{cert.PersonId}";
        var personCode = person?.LegacyCode ?? person?.TaxId ?? "";

        // Entries
        var entries = await context.CertificateEntries.AsNoTracking()
            .Where(e => e.CertificateId == cert.Id && !e.IsDeleted)
            .OrderByDescending(e => e.EntryDate)
            .ThenByDescending(e => e.Id)
            .Select(e => new CertificateEntryDto(
                e.PublicId,
                e.EntryDate,
                e.EntryType,
                CertificateStatusHelper.GetEntryTypeText(e.EntryType),
                e.Amount,
                e.CurrentRate,
                e.Description))
            .ToListAsync(ct);

        // Calculate projected interest
        var daysRemaining = cert.MaturityDate.HasValue
            ? Math.Max(0, cert.MaturityDate.Value.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber)
            : 0;
        var projectedInterest = Math.Round(cert.Amount * cert.InterestRate / 100m * daysRemaining / 365m, 0);

        return Result.Success(new CertificateDetailDto(
            cert.PublicId,
            cert.CertificateNumber,
            cert.PersonId,
            personName,
            personCode,
            cert.Amount,
            cert.InterestRate,
            cert.Term,
            cert.IssueDate,
            cert.MaturityDate,
            cert.Status,
            CertificateStatusHelper.GetStatusText(cert.Status),
            cert.RenewalType,
            cert.IsCapitalized,
            projectedInterest,
            cert.PreviousCertificateId,
            entries));
    }
}

// --- Near Expiry Query ---

public record GetCertificatesNearExpiryQuery(int DaysAhead = 30) : IRequest<Result<List<CertificateDto>>>;

public class GetCertificatesNearExpiryQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCertificatesNearExpiryQuery, Result<List<CertificateDto>>>
{
    public async Task<Result<List<CertificateDto>>> Handle(
        GetCertificatesNearExpiryQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddDays(request.DaysAhead);

        var items = await context.Certificates.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == "V"
                        && c.MaturityDate.HasValue && c.MaturityDate.Value <= cutoff
                        && c.MaturityDate.Value >= today)
            .OrderBy(c => c.MaturityDate)
            .Take(100)
            .Select(c => new
            {
                c.PublicId,
                c.CertificateNumber,
                c.PersonId,
                c.Amount,
                c.InterestRate,
                c.Term,
                c.IssueDate,
                c.MaturityDate,
                c.Status,
                c.RenewalType
            })
            .ToListAsync(ct);

        var personIds = items.Select(i => i.PersonId).Distinct().ToList();
        var personNames = await context.People.AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName), ct);

        var dtos = items.Select(c => new CertificateDto(
            c.PublicId,
            c.CertificateNumber,
            personNames.TryGetValue(c.PersonId, out var name) ? name : $"ID:{c.PersonId}",
            c.Amount,
            c.InterestRate,
            c.Term,
            c.IssueDate,
            c.MaturityDate,
            c.Status,
            CertificateStatusHelper.GetStatusText(c.Status),
            c.RenewalType
        )).ToList();

        return Result.Success(dtos);
    }
}
