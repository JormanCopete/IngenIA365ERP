using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.WithdrawalStatuses.Queries;

// DTO
public record WithdrawalStatusDto
{
    public Guid PublicId { get; init; }
    public string PersonCode { get; init; } = string.Empty;
    public DateOnly RequestDate { get; init; }
    public string ReasonCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateOnly? EffectiveDate { get; init; }
    public string? Remarks { get; init; }
    public int Period { get; init; }
}

// List Query
public record ListWithdrawalStatusesQuery : IRequest<Result<PagedList<WithdrawalStatusDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListWithdrawalStatusesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListWithdrawalStatusesQuery, Result<PagedList<WithdrawalStatusDto>>>
{
    public async Task<Result<PagedList<WithdrawalStatusDto>>> Handle(
        ListWithdrawalStatusesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.WithdrawalStatuses
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.PersonCode.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "personcode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.PersonCode)
                : query.OrderBy(e => e.PersonCode),
            "requestdate" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.RequestDate)
                : query.OrderBy(e => e.RequestDate),
            _ => query.OrderByDescending(e => e.RequestDate)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new WithdrawalStatusDto
            {
                PublicId = e.PublicId,
                PersonCode = e.PersonCode,
                RequestDate = e.RequestDate,
                ReasonCode = e.ReasonCode,
                Status = e.Status,
                EffectiveDate = e.EffectiveDate,
                Remarks = e.Remarks,
                Period = e.Period
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<WithdrawalStatusDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetWithdrawalStatusByIdQuery(Guid PublicId) : IRequest<Result<WithdrawalStatusDto>>;

public class GetWithdrawalStatusByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetWithdrawalStatusByIdQuery, Result<WithdrawalStatusDto>>
{
    public async Task<Result<WithdrawalStatusDto>> Handle(
        GetWithdrawalStatusByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.WithdrawalStatuses
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new WithdrawalStatusDto
            {
                PublicId = e.PublicId,
                PersonCode = e.PersonCode,
                RequestDate = e.RequestDate,
                ReasonCode = e.ReasonCode,
                Status = e.Status,
                EffectiveDate = e.EffectiveDate,
                Remarks = e.Remarks,
                Period = e.Period
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<WithdrawalStatusDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
