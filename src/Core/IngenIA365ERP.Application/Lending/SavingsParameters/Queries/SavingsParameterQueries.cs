using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SavingsParameters.Queries;

// DTO
public record SavingsParameterDto
{
    public Guid PublicId { get; init; }
    public int SavingsLineId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string InterestPaymentPeriod { get; init; } = string.Empty;
    public decimal MinInterestBalance { get; init; }
    public decimal MinTransactionAmount { get; init; }
    public decimal MinAccountBalance { get; init; }
    public decimal InterestPaymentRate { get; init; }
    public decimal MaxWithdrawalAmount { get; init; }
    public decimal TaxRate { get; init; }
}

// List Query
public record ListSavingsParametersQuery : IRequest<Result<PagedList<SavingsParameterDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListSavingsParametersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListSavingsParametersQuery, Result<PagedList<SavingsParameterDto>>>
{
    public async Task<Result<PagedList<SavingsParameterDto>>> Handle(
        ListSavingsParametersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.SavingsParameters
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
            "savingslineid" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.SavingsLineId)
                : query.OrderBy(e => e.SavingsLineId),
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new SavingsParameterDto
            {
                PublicId = e.PublicId,
                SavingsLineId = e.SavingsLineId,
                Name = e.Name,
                ShortName = e.ShortName,
                InterestPaymentPeriod = e.InterestPaymentPeriod,
                MinInterestBalance = e.MinInterestBalance,
                MinTransactionAmount = e.MinTransactionAmount,
                MinAccountBalance = e.MinAccountBalance,
                InterestPaymentRate = e.InterestPaymentRate,
                MaxWithdrawalAmount = e.MaxWithdrawalAmount,
                TaxRate = e.TaxRate
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<SavingsParameterDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetSavingsParameterByIdQuery(Guid PublicId) : IRequest<Result<SavingsParameterDto>>;

public class GetSavingsParameterByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSavingsParameterByIdQuery, Result<SavingsParameterDto>>
{
    public async Task<Result<SavingsParameterDto>> Handle(
        GetSavingsParameterByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.SavingsParameters
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new SavingsParameterDto
            {
                PublicId = e.PublicId,
                SavingsLineId = e.SavingsLineId,
                Name = e.Name,
                ShortName = e.ShortName,
                InterestPaymentPeriod = e.InterestPaymentPeriod,
                MinInterestBalance = e.MinInterestBalance,
                MinTransactionAmount = e.MinTransactionAmount,
                MinAccountBalance = e.MinAccountBalance,
                InterestPaymentRate = e.InterestPaymentRate,
                MaxWithdrawalAmount = e.MaxWithdrawalAmount,
                TaxRate = e.TaxRate
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<SavingsParameterDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
