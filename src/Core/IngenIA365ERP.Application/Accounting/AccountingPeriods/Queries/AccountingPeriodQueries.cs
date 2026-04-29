using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.AccountingPeriods.Queries;

// DTO
public record AccountingPeriodDto
{
    public Guid PublicId { get; init; }
    public string ModuleCode { get; init; } = string.Empty;
    public int Year { get; init; }
    public byte PeriodNumber { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string Status { get; init; } = string.Empty;
}

// List Query
public record ListAccountingPeriodsQuery : IRequest<Result<PagedList<AccountingPeriodDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListAccountingPeriodsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListAccountingPeriodsQuery, Result<PagedList<AccountingPeriodDto>>>
{
    public async Task<Result<PagedList<AccountingPeriodDto>>> Handle(
        ListAccountingPeriodsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.AccountingPeriods
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            if (int.TryParse(term, out var year))
            {
                query = query.Where(e => e.Year == year);
            }
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "modulecode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.ModuleCode)
                : query.OrderBy(e => e.ModuleCode),
            _ => query.OrderByDescending(e => e.Year).ThenByDescending(e => e.PeriodNumber)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new AccountingPeriodDto
            {
                PublicId = e.PublicId,
                ModuleCode = e.ModuleCode,
                Year = e.Year,
                PeriodNumber = e.PeriodNumber,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Status = e.Status
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<AccountingPeriodDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetAccountingPeriodByIdQuery(Guid PublicId) : IRequest<Result<AccountingPeriodDto>>;

public class GetAccountingPeriodByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAccountingPeriodByIdQuery, Result<AccountingPeriodDto>>
{
    public async Task<Result<AccountingPeriodDto>> Handle(
        GetAccountingPeriodByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.AccountingPeriods
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new AccountingPeriodDto
            {
                PublicId = e.PublicId,
                ModuleCode = e.ModuleCode,
                Year = e.Year,
                PeriodNumber = e.PeriodNumber,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Status = e.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<AccountingPeriodDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
