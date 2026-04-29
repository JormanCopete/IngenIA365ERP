using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Debit.PosTerminals.Queries;

// DTO
public record PosTerminalDto
{
    public Guid PublicId { get; init; }
    public string TerminalCode { get; init; } = string.Empty;
    public int InternalCode { get; init; }
    public string? VoucherCode { get; init; }
    public string? MerchantName { get; init; }
    public string? Location { get; init; }
    public string? Status { get; init; }
}

// List Query
public record ListPosTerminalsQuery : IRequest<Result<PagedList<PosTerminalDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListPosTerminalsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListPosTerminalsQuery, Result<PagedList<PosTerminalDto>>>
{
    public async Task<Result<PagedList<PosTerminalDto>>> Handle(
        ListPosTerminalsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.PosTerminals
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.TerminalCode.ToLower().Contains(term) ||
                                     (e.MerchantName != null && e.MerchantName.ToLower().Contains(term)));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "merchantname" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.MerchantName)
                : query.OrderBy(e => e.MerchantName),
            _ => query.OrderBy(e => e.TerminalCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new PosTerminalDto
            {
                PublicId = e.PublicId,
                TerminalCode = e.TerminalCode,
                InternalCode = e.InternalCode,
                VoucherCode = e.VoucherCode,
                MerchantName = e.MerchantName,
                Location = e.Location,
                Status = e.Status
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<PosTerminalDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetPosTerminalByIdQuery(Guid PublicId) : IRequest<Result<PosTerminalDto>>;

public class GetPosTerminalByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPosTerminalByIdQuery, Result<PosTerminalDto>>
{
    public async Task<Result<PosTerminalDto>> Handle(
        GetPosTerminalByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.PosTerminals
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new PosTerminalDto
            {
                PublicId = e.PublicId,
                TerminalCode = e.TerminalCode,
                InternalCode = e.InternalCode,
                VoucherCode = e.VoucherCode,
                MerchantName = e.MerchantName,
                Location = e.Location,
                Status = e.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<PosTerminalDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
