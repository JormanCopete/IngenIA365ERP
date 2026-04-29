using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.PeriodicityParameters.Queries;

// DTO
public record PeriodicityParameterDto
{
    public Guid PublicId { get; init; }
    public string CompanyCode { get; init; } = string.Empty;
    public string DeductionClass { get; init; } = string.Empty;
    public string Periodicity { get; init; } = string.Empty;
    public int StartDay { get; init; }
    public int EndDay { get; init; }
    public string DayCount { get; init; } = string.Empty;
}

// List Query
public record ListPeriodicityParametersQuery : IRequest<Result<PagedList<PeriodicityParameterDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListPeriodicityParametersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListPeriodicityParametersQuery, Result<PagedList<PeriodicityParameterDto>>>
{
    public async Task<Result<PagedList<PeriodicityParameterDto>>> Handle(
        ListPeriodicityParametersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.PeriodicityParameters
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.CompanyCode.ToLower().Contains(term)
                || e.DeductionClass.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "companycode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.CompanyCode)
                : query.OrderBy(e => e.CompanyCode),
            "periodicity" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Periodicity)
                : query.OrderBy(e => e.Periodicity),
            _ => query.OrderBy(e => e.CompanyCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new PeriodicityParameterDto
            {
                PublicId = e.PublicId,
                CompanyCode = e.CompanyCode,
                DeductionClass = e.DeductionClass,
                Periodicity = e.Periodicity,
                StartDay = e.StartDay,
                EndDay = e.EndDay,
                DayCount = e.DayCount
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<PeriodicityParameterDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetPeriodicityParameterByIdQuery(Guid PublicId) : IRequest<Result<PeriodicityParameterDto>>;

public class GetPeriodicityParameterByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPeriodicityParameterByIdQuery, Result<PeriodicityParameterDto>>
{
    public async Task<Result<PeriodicityParameterDto>> Handle(
        GetPeriodicityParameterByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.PeriodicityParameters
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new PeriodicityParameterDto
            {
                PublicId = e.PublicId,
                CompanyCode = e.CompanyCode,
                DeductionClass = e.DeductionClass,
                Periodicity = e.Periodicity,
                StartDay = e.StartDay,
                EndDay = e.EndDay,
                DayCount = e.DayCount
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<PeriodicityParameterDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
