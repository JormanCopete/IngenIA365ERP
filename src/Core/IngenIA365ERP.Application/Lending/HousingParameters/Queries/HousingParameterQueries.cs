using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.HousingParameters.Queries;

// DTO
public record HousingParameterDto
{
    public Guid PublicId { get; init; }
    public string PersonCode { get; init; } = string.Empty;
    public int CreditLineId { get; init; }
    public long PortfolioNumber { get; init; }
    public int HousingClass { get; init; }
    public int HousingType { get; init; }
    public string SocialInterest { get; init; } = string.Empty;
    public string HasSubsidy { get; init; } = string.Empty;
    public int NetworkEntity { get; init; }
    public long NetworkValue { get; init; }
    public int DisbursementType { get; init; }
    public int CurrencyType { get; init; }
}

// List Query
public record ListHousingParametersQuery : IRequest<Result<PagedList<HousingParameterDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListHousingParametersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListHousingParametersQuery, Result<PagedList<HousingParameterDto>>>
{
    public async Task<Result<PagedList<HousingParameterDto>>> Handle(
        ListHousingParametersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.HousingParameters
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
            "creditlineid" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.CreditLineId)
                : query.OrderBy(e => e.CreditLineId),
            _ => query.OrderBy(e => e.PersonCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new HousingParameterDto
            {
                PublicId = e.PublicId,
                PersonCode = e.PersonCode,
                CreditLineId = e.CreditLineId,
                PortfolioNumber = e.PortfolioNumber,
                HousingClass = e.HousingClass,
                HousingType = e.HousingType,
                SocialInterest = e.SocialInterest,
                HasSubsidy = e.HasSubsidy,
                NetworkEntity = e.NetworkEntity,
                NetworkValue = e.NetworkValue,
                DisbursementType = e.DisbursementType,
                CurrencyType = e.CurrencyType
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<HousingParameterDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetHousingParameterByIdQuery(Guid PublicId) : IRequest<Result<HousingParameterDto>>;

public class GetHousingParameterByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetHousingParameterByIdQuery, Result<HousingParameterDto>>
{
    public async Task<Result<HousingParameterDto>> Handle(
        GetHousingParameterByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.HousingParameters
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new HousingParameterDto
            {
                PublicId = e.PublicId,
                PersonCode = e.PersonCode,
                CreditLineId = e.CreditLineId,
                PortfolioNumber = e.PortfolioNumber,
                HousingClass = e.HousingClass,
                HousingType = e.HousingType,
                SocialInterest = e.SocialInterest,
                HasSubsidy = e.HasSubsidy,
                NetworkEntity = e.NetworkEntity,
                NetworkValue = e.NetworkValue,
                DisbursementType = e.DisbursementType,
                CurrencyType = e.CurrencyType
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<HousingParameterDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
