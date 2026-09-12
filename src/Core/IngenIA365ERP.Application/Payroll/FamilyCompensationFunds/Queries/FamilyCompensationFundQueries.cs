using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.FamilyCompensationFunds.Queries;

// DTO
public record FamilyCompensationFundDto
{
    public Guid PublicId { get; init; }
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public int CheckDigit { get; init; }
}

// List Query
public record ListFamilyCompensationFundsQuery : IRequest<Result<PagedList<FamilyCompensationFundDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListFamilyCompensationFundsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListFamilyCompensationFundsQuery, Result<PagedList<FamilyCompensationFundDto>>>
{
    public async Task<Result<PagedList<FamilyCompensationFundDto>>> Handle(
        ListFamilyCompensationFundsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.FamilyCompensationFunds
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
            .Select(e => new FamilyCompensationFundDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                TaxId = e.TaxId,
                CheckDigit = e.CheckDigit
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<FamilyCompensationFundDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetFamilyCompensationFundByIdQuery(Guid PublicId) : IRequest<Result<FamilyCompensationFundDto>>;

public class GetFamilyCompensationFundByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetFamilyCompensationFundByIdQuery, Result<FamilyCompensationFundDto>>
{
    public async Task<Result<FamilyCompensationFundDto>> Handle(
        GetFamilyCompensationFundByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.FamilyCompensationFunds
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new FamilyCompensationFundDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                TaxId = e.TaxId,
                CheckDigit = e.CheckDigit
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<FamilyCompensationFundDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
