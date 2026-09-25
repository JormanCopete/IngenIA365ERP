using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Contributions.Queries;

// --- DTOs ---

public record ContributionBalanceDto(
    Guid PersonPublicId,
    string PersonName,
    string PersonCode,
    decimal TotalContributions,
    decimal TotalReductions,
    decimal CurrentBalance);

public record ContributionEntryDto(
    Guid PublicId,
    int Period,
    decimal Amount,
    string Type,
    string TypeText,
    DateTime CreatedAt);

// --- Balance Query ---

public record GetContributionBalanceQuery(Guid PersonPublicId) : IRequest<Result<ContributionBalanceDto>>;

public class GetContributionBalanceQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetContributionBalanceQuery, Result<ContributionBalanceDto>>
{
    public async Task<Result<ContributionBalanceDto>> Handle(
        GetContributionBalanceQuery request, CancellationToken ct)
    {
        var person = await context.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);

        if (person is null)
            return Result.Failure<ContributionBalanceDto>(new Error("Person.NotFound",
                "Persona no encontrada."));

        var personCode = person.LegacyCode ?? person.TaxId;
        var personName = NombreDePersona.Completo(person);

        var entries = await context.ContributionReductions.AsNoTracking()
            .Where(cr => cr.PersonCode == personCode && !cr.IsDeleted)
            .ToListAsync(ct);

        var totalContributions = entries.Where(e => e.OpeningBalance > 0).Sum(e => e.OpeningBalance);
        var totalReductions = entries.Where(e => e.OpeningBalance < 0).Sum(e => Math.Abs(e.OpeningBalance));
        var currentBalance = entries.Sum(e => e.OpeningBalance);

        return Result.Success(new ContributionBalanceDto(
            person.PublicId,
            personName,
            personCode,
            totalContributions,
            totalReductions,
            currentBalance));
    }
}

// --- List Contributions Query ---

public record ListContributionsQuery : IRequest<Result<List<ContributionEntryDto>>>
{
    public Guid PersonPublicId { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
}

public class ListContributionsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListContributionsQuery, Result<List<ContributionEntryDto>>>
{
    public async Task<Result<List<ContributionEntryDto>>> Handle(
        ListContributionsQuery request, CancellationToken ct)
    {
        var person = await context.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);

        if (person is null)
            return Result.Failure<List<ContributionEntryDto>>(new Error("Person.NotFound",
                "Persona no encontrada."));

        var personCode = person.LegacyCode ?? person.TaxId;

        var query = context.ContributionReductions.AsNoTracking()
            .Where(cr => cr.PersonCode == personCode && !cr.IsDeleted);

        if (request.DateFrom.HasValue)
        {
            var fromPeriod = request.DateFrom.Value.Year * 100 + request.DateFrom.Value.Month;
            query = query.Where(cr => cr.Period >= fromPeriod);
        }
        if (request.DateTo.HasValue)
        {
            var toPeriod = request.DateTo.Value.Year * 100 + request.DateTo.Value.Month;
            query = query.Where(cr => cr.Period <= toPeriod);
        }

        var entries = await query
            .OrderByDescending(cr => cr.Period)
            .ThenByDescending(cr => cr.Id)
            .Take(200)
            .Select(cr => new ContributionEntryDto(
                cr.PublicId,
                cr.Period,
                Math.Abs(cr.OpeningBalance),
                cr.OpeningBalance >= 0 ? "A" : "R",
                cr.OpeningBalance >= 0 ? "Aporte" : "Retiro",
                cr.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(entries);
    }
}
