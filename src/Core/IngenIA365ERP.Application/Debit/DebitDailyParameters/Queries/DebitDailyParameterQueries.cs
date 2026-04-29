using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Debit.DebitDailyParameters.Queries;

// DTO
public record DebitDailyParameterDto
{
    public Guid PublicId { get; init; }
    public int ParameterCode { get; init; }
    public string BankId { get; init; } = string.Empty;
    public string BatchVoucherCode { get; init; } = string.Empty;
    public string OnlineVoucherCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateOnly? LastUpdateDate { get; init; }
    public int NewCardsCount { get; init; }
    public string? LastCardNumber { get; init; }
    public DateOnly? ClosingDate { get; init; }
    public string? ClosingVoucherCode { get; init; }
    public long ClosingSequenceNumber { get; init; }
    public string? PosClosingVoucherCode { get; init; }
    public int PosClosingSequence { get; init; }
    public string? ClosingFlag { get; init; }
    public decimal NetworkCommission { get; init; }
    public decimal OtherNetworkCommission { get; init; }
}

// List Query
public record ListDebitDailyParametersQuery : IRequest<Result<PagedList<DebitDailyParameterDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListDebitDailyParametersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListDebitDailyParametersQuery, Result<PagedList<DebitDailyParameterDto>>>
{
    public async Task<Result<PagedList<DebitDailyParameterDto>>> Handle(
        ListDebitDailyParametersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.DebitDailyParameters
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Description.ToLower().Contains(term) ||
                                     e.BankId.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "description" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Description)
                : query.OrderBy(e => e.Description),
            _ => query.OrderBy(e => e.ParameterCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new DebitDailyParameterDto
            {
                PublicId = e.PublicId,
                ParameterCode = e.ParameterCode,
                BankId = e.BankId,
                BatchVoucherCode = e.BatchVoucherCode,
                OnlineVoucherCode = e.OnlineVoucherCode,
                Description = e.Description,
                LastUpdateDate = e.LastUpdateDate,
                NewCardsCount = e.NewCardsCount,
                LastCardNumber = e.LastCardNumber,
                ClosingDate = e.ClosingDate,
                ClosingVoucherCode = e.ClosingVoucherCode,
                ClosingSequenceNumber = e.ClosingSequenceNumber,
                PosClosingVoucherCode = e.PosClosingVoucherCode,
                PosClosingSequence = e.PosClosingSequence,
                ClosingFlag = e.ClosingFlag,
                NetworkCommission = e.NetworkCommission,
                OtherNetworkCommission = e.OtherNetworkCommission
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<DebitDailyParameterDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetDebitDailyParameterByIdQuery(Guid PublicId) : IRequest<Result<DebitDailyParameterDto>>;

public class GetDebitDailyParameterByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDebitDailyParameterByIdQuery, Result<DebitDailyParameterDto>>
{
    public async Task<Result<DebitDailyParameterDto>> Handle(
        GetDebitDailyParameterByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.DebitDailyParameters
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new DebitDailyParameterDto
            {
                PublicId = e.PublicId,
                ParameterCode = e.ParameterCode,
                BankId = e.BankId,
                BatchVoucherCode = e.BatchVoucherCode,
                OnlineVoucherCode = e.OnlineVoucherCode,
                Description = e.Description,
                LastUpdateDate = e.LastUpdateDate,
                NewCardsCount = e.NewCardsCount,
                LastCardNumber = e.LastCardNumber,
                ClosingDate = e.ClosingDate,
                ClosingVoucherCode = e.ClosingVoucherCode,
                ClosingSequenceNumber = e.ClosingSequenceNumber,
                PosClosingVoucherCode = e.PosClosingVoucherCode,
                PosClosingSequence = e.PosClosingSequence,
                ClosingFlag = e.ClosingFlag,
                NetworkCommission = e.NetworkCommission,
                OtherNetworkCommission = e.OtherNetworkCommission
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<DebitDailyParameterDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
