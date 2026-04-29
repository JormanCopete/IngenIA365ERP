using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.TaxFormCodes.Queries;

// DTO
public record TaxFormCodeDto
{
    public Guid PublicId { get; init; }
    public string? FormCode { get; init; }
    public string? Description { get; init; }
    public string? AccountCode { get; init; }
    public string? ConceptCode { get; init; }
    public string? TaxType { get; init; }
}

// List Query
public record ListTaxFormCodesQuery : IRequest<Result<PagedList<TaxFormCodeDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListTaxFormCodesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListTaxFormCodesQuery, Result<PagedList<TaxFormCodeDto>>>
{
    public async Task<Result<PagedList<TaxFormCodeDto>>> Handle(
        ListTaxFormCodesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.TaxFormCodes
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => (e.Description != null && e.Description.ToLower().Contains(term))
                || (e.FormCode != null && e.FormCode.ToLower().Contains(term)));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "formcode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.FormCode)
                : query.OrderBy(e => e.FormCode),
            _ => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Description)
                : query.OrderBy(e => e.Description)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new TaxFormCodeDto
            {
                PublicId = e.PublicId,
                FormCode = e.FormCode,
                Description = e.Description,
                AccountCode = e.AccountCode,
                ConceptCode = e.ConceptCode,
                TaxType = e.TaxType
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<TaxFormCodeDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetTaxFormCodeByIdQuery(Guid PublicId) : IRequest<Result<TaxFormCodeDto>>;

public class GetTaxFormCodeByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetTaxFormCodeByIdQuery, Result<TaxFormCodeDto>>
{
    public async Task<Result<TaxFormCodeDto>> Handle(
        GetTaxFormCodeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.TaxFormCodes
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new TaxFormCodeDto
            {
                PublicId = e.PublicId,
                FormCode = e.FormCode,
                Description = e.Description,
                AccountCode = e.AccountCode,
                ConceptCode = e.ConceptCode,
                TaxType = e.TaxType
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<TaxFormCodeDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
