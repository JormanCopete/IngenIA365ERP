using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.VoucherTypes.Queries;

// DTO
public record VoucherTypeDto
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? DocumentType { get; init; }
    public bool ControlSequential { get; init; }
}

// List Query
public record ListVoucherTypesQuery : IRequest<Result<PagedList<VoucherTypeDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListVoucherTypesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListVoucherTypesQuery, Result<PagedList<VoucherTypeDto>>>
{
    public async Task<Result<PagedList<VoucherTypeDto>>> Handle(
        ListVoucherTypesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.VoucherTypes
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Code.ToLower().Contains(term)
                || e.Name.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "code" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Code)
                : query.OrderBy(e => e.Code),
            _ => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new VoucherTypeDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                DocumentType = e.DocumentType,
                ControlSequential = e.ControlSequential
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<VoucherTypeDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetVoucherTypeByIdQuery(Guid PublicId) : IRequest<Result<VoucherTypeDto>>;

public class GetVoucherTypeByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetVoucherTypeByIdQuery, Result<VoucherTypeDto>>
{
    public async Task<Result<VoucherTypeDto>> Handle(
        GetVoucherTypeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.VoucherTypes
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new VoucherTypeDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                DocumentType = e.DocumentType,
                ControlSequential = e.ControlSequential
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<VoucherTypeDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
