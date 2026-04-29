using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.VatAccounts.Queries;

// DTO
public record VatAccountDto
{
    public Guid PublicId { get; init; }
    public int ProductGroupId { get; init; }
    public int TransactionTypeId { get; init; }
    public int WarehouseId { get; init; }
    public int LocationId { get; init; }
    public decimal VatRate { get; init; }
    public string? AccountType { get; init; }
    public string? AccountCode { get; init; }
}

// List Query
public record ListVatAccountsQuery : IRequest<Result<PagedList<VatAccountDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListVatAccountsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListVatAccountsQuery, Result<PagedList<VatAccountDto>>>
{
    public async Task<Result<PagedList<VatAccountDto>>> Handle(
        ListVatAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.VatAccounts
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e =>
                (e.AccountCode != null && e.AccountCode.ToLower().Contains(term)) ||
                (e.AccountType != null && e.AccountType.ToLower().Contains(term)));
        }

        query = query.OrderBy(e => e.ProductGroupId).ThenBy(e => e.TransactionTypeId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new VatAccountDto
            {
                PublicId = e.PublicId,
                ProductGroupId = e.ProductGroupId,
                TransactionTypeId = e.TransactionTypeId,
                WarehouseId = e.WarehouseId,
                LocationId = e.LocationId,
                VatRate = e.VatRate,
                AccountType = e.AccountType,
                AccountCode = e.AccountCode
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<VatAccountDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetVatAccountByIdQuery(Guid PublicId) : IRequest<Result<VatAccountDto>>;

public class GetVatAccountByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetVatAccountByIdQuery, Result<VatAccountDto>>
{
    public async Task<Result<VatAccountDto>> Handle(
        GetVatAccountByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.VatAccounts
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new VatAccountDto
            {
                PublicId = e.PublicId,
                ProductGroupId = e.ProductGroupId,
                TransactionTypeId = e.TransactionTypeId,
                WarehouseId = e.WarehouseId,
                LocationId = e.LocationId,
                VatRate = e.VatRate,
                AccountType = e.AccountType,
                AccountCode = e.AccountCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<VatAccountDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
