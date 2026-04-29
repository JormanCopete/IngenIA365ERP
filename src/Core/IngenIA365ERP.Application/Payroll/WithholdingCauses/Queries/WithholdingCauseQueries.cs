using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WithholdingCauses.Queries;

// DTO
public record WithholdingCauseDto
{
    public Guid PublicId { get; init; }
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int IndemnityType { get; init; }
    public int AutoDeductions { get; init; }
}

// List Query
public record ListWithholdingCausesQuery : IRequest<Result<PagedList<WithholdingCauseDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListWithholdingCausesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListWithholdingCausesQuery, Result<PagedList<WithholdingCauseDto>>>
{
    public async Task<Result<PagedList<WithholdingCauseDto>>> Handle(
        ListWithholdingCausesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.WithholdingCauses
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Name.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "name" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name),
            "code" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Code)
                : query.OrderBy(e => e.Code),
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new WithholdingCauseDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                IndemnityType = e.IndemnityType,
                AutoDeductions = e.AutoDeductions
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<WithholdingCauseDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetWithholdingCauseByIdQuery(Guid PublicId) : IRequest<Result<WithholdingCauseDto>>;

public class GetWithholdingCauseByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetWithholdingCauseByIdQuery, Result<WithholdingCauseDto>>
{
    public async Task<Result<WithholdingCauseDto>> Handle(
        GetWithholdingCauseByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.WithholdingCauses
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new WithholdingCauseDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                IndemnityType = e.IndemnityType,
                AutoDeductions = e.AutoDeductions
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<WithholdingCauseDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
