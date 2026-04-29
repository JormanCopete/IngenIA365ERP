using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Shifts.Queries;

// DTO
public record ShiftDto
{
    public Guid PublicId { get; init; }
    public int ShiftCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }
}

// List Query
public record ListShiftsQuery : IRequest<Result<PagedList<ShiftDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListShiftsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListShiftsQuery, Result<PagedList<ShiftDto>>>
{
    public async Task<Result<PagedList<ShiftDto>>> Handle(
        ListShiftsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Shifts
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
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new ShiftDto
            {
                PublicId = e.PublicId,
                ShiftCode = e.ShiftCode,
                Name = e.Name,
                StartTime = e.StartTime,
                EndTime = e.EndTime
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ShiftDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetShiftByIdQuery(Guid PublicId) : IRequest<Result<ShiftDto>>;

public class GetShiftByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetShiftByIdQuery, Result<ShiftDto>>
{
    public async Task<Result<ShiftDto>> Handle(
        GetShiftByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Shifts
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new ShiftDto
            {
                PublicId = e.PublicId,
                ShiftCode = e.ShiftCode,
                Name = e.Name,
                StartTime = e.StartTime,
                EndTime = e.EndTime
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<ShiftDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
