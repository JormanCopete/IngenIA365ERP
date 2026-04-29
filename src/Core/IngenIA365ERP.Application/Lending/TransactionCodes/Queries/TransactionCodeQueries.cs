using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.TransactionCodes.Queries;

// DTO
public record TransactionCodeDto
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public string AccountCode { get; init; } = string.Empty;
    public string DebitCreditFlag { get; init; } = string.Empty;
    public int FormatId { get; init; }
    public int ConceptId { get; init; }
    public int SourceId { get; init; }
}

// List Query
public record ListTransactionCodesQuery : IRequest<Result<PagedList<TransactionCodeDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListTransactionCodesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListTransactionCodesQuery, Result<PagedList<TransactionCodeDto>>>
{
    public async Task<Result<PagedList<TransactionCodeDto>>> Handle(
        ListTransactionCodesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.TransactionCodes
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Name.ToLower().Contains(term) || e.Code.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "code" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Code)
                : query.OrderBy(e => e.Code),
            "name" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name),
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new TransactionCodeDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                TransactionType = e.TransactionType,
                AccountCode = e.AccountCode,
                DebitCreditFlag = e.DebitCreditFlag,
                FormatId = e.FormatId,
                ConceptId = e.ConceptId,
                SourceId = e.SourceId
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<TransactionCodeDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetTransactionCodeByIdQuery(Guid PublicId) : IRequest<Result<TransactionCodeDto>>;

public class GetTransactionCodeByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetTransactionCodeByIdQuery, Result<TransactionCodeDto>>
{
    public async Task<Result<TransactionCodeDto>> Handle(
        GetTransactionCodeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.TransactionCodes
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new TransactionCodeDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                TransactionType = e.TransactionType,
                AccountCode = e.AccountCode,
                DebitCreditFlag = e.DebitCreditFlag,
                FormatId = e.FormatId,
                ConceptId = e.ConceptId,
                SourceId = e.SourceId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<TransactionCodeDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
