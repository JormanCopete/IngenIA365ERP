using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Debit.DebitAgreementParameters.Queries;

// DTO
public record DebitAgreementParameterDto
{
    public Guid PublicId { get; init; }
    public string AgreementCode { get; init; } = string.Empty;
    public int TokenRS { get; init; }
    public string? Name { get; init; }
}

// List Query
public record ListDebitAgreementParametersQuery : IRequest<Result<PagedList<DebitAgreementParameterDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListDebitAgreementParametersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListDebitAgreementParametersQuery, Result<PagedList<DebitAgreementParameterDto>>>
{
    public async Task<Result<PagedList<DebitAgreementParameterDto>>> Handle(
        ListDebitAgreementParametersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.DebitAgreementParameters
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.AgreementCode.ToLower().Contains(term) ||
                                     (e.Name != null && e.Name.ToLower().Contains(term)));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "name" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name),
            _ => query.OrderBy(e => e.AgreementCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new DebitAgreementParameterDto
            {
                PublicId = e.PublicId,
                AgreementCode = e.AgreementCode,
                TokenRS = e.TokenRS,
                Name = e.Name
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<DebitAgreementParameterDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetDebitAgreementParameterByIdQuery(Guid PublicId) : IRequest<Result<DebitAgreementParameterDto>>;

public class GetDebitAgreementParameterByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDebitAgreementParameterByIdQuery, Result<DebitAgreementParameterDto>>
{
    public async Task<Result<DebitAgreementParameterDto>> Handle(
        GetDebitAgreementParameterByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.DebitAgreementParameters
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new DebitAgreementParameterDto
            {
                PublicId = e.PublicId,
                AgreementCode = e.AgreementCode,
                TokenRS = e.TokenRS,
                Name = e.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<DebitAgreementParameterDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
