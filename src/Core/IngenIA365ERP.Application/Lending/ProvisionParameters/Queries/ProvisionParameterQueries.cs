using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.ProvisionParameters.Queries;

// DTO
public record ProvisionParameterDto
{
    public Guid PublicId { get; init; }
    public int Period { get; init; }
    public int Code { get; init; }
    public decimal RateB { get; init; }
    public decimal RateC { get; init; }
    public decimal RateD { get; init; }
    public decimal RateE { get; init; }
}

// List Query
public record ListProvisionParametersQuery : IRequest<Result<PagedList<ProvisionParameterDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListProvisionParametersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListProvisionParametersQuery, Result<PagedList<ProvisionParameterDto>>>
{
    public async Task<Result<PagedList<ProvisionParameterDto>>> Handle(
        ListProvisionParametersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.ProvisionParameters
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            if (int.TryParse(term, out var codeValue))
            {
                query = query.Where(e => e.Code == codeValue || e.Period == codeValue);
            }
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "code" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Code)
                : query.OrderBy(e => e.Code),
            "period" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Period)
                : query.OrderBy(e => e.Period),
            _ => query.OrderByDescending(e => e.Period)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new ProvisionParameterDto
            {
                PublicId = e.PublicId,
                Period = e.Period,
                Code = e.Code,
                RateB = e.RateB,
                RateC = e.RateC,
                RateD = e.RateD,
                RateE = e.RateE
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ProvisionParameterDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetProvisionParameterByIdQuery(Guid PublicId) : IRequest<Result<ProvisionParameterDto>>;

public class GetProvisionParameterByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetProvisionParameterByIdQuery, Result<ProvisionParameterDto>>
{
    public async Task<Result<ProvisionParameterDto>> Handle(
        GetProvisionParameterByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.ProvisionParameters
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new ProvisionParameterDto
            {
                PublicId = e.PublicId,
                Period = e.Period,
                Code = e.Code,
                RateB = e.RateB,
                RateC = e.RateC,
                RateD = e.RateD,
                RateE = e.RateE
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<ProvisionParameterDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
