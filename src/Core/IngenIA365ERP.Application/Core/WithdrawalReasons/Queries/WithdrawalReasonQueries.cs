using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.WithdrawalReasons.Queries;

public record WithdrawalReasonDto
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public record ListWithdrawalReasonsQuery : IRequest<Result<PagedList<WithdrawalReasonDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListWithdrawalReasonsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListWithdrawalReasonsQuery, Result<PagedList<WithdrawalReasonDto>>>
{
    public async Task<Result<PagedList<WithdrawalReasonDto>>> Handle(
        ListWithdrawalReasonsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.WithdrawalReasons
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
            .Select(e => new WithdrawalReasonDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<WithdrawalReasonDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

public record GetWithdrawalReasonByIdQuery(Guid PublicId) : IRequest<Result<WithdrawalReasonDto>>;

public class GetWithdrawalReasonByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetWithdrawalReasonByIdQuery, Result<WithdrawalReasonDto>>
{
    public async Task<Result<WithdrawalReasonDto>> Handle(
        GetWithdrawalReasonByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.WithdrawalReasons
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new WithdrawalReasonDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<WithdrawalReasonDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
