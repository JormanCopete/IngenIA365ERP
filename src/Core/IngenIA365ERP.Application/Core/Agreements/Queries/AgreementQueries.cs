using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Agreements.Queries;

public record AgreementDto
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? AccountNumber { get; init; }
    public string? EntityCode { get; init; }
    public short Currency { get; init; }
    public string? SavingsCode { get; init; }
    public string? CheckingCode { get; init; }
    public string? BlockCode { get; init; }

    // Availability config
    public short AvailabilityOption { get; init; }
    public decimal AvailabilityLimit { get; init; }
    public decimal AvailabilityRate { get; init; }

    // ATM config
    public short AtmOption { get; init; }
    public decimal AtmLimit { get; init; }
    public decimal AtmRate { get; init; }
    public short AtmTransactions { get; init; }

    // POS config
    public short PosOption { get; init; }
    public decimal PosLimit { get; init; }
    public decimal PosRate { get; init; }
    public short PosTransactions { get; init; }

    // Balances and limits
    public short ShowBalances { get; init; }
    public int Bin { get; init; }
    public decimal AvailableLimit { get; init; }
    public decimal CashLimit { get; init; }

    // File paths
    public string? OutputPath { get; init; }
    public string? InputPath { get; init; }

    // Additional config
    public int AverageDays { get; init; }
    public int FreeTransactions { get; init; }
    public int HandlingFee { get; init; }
    public decimal AvailableLimit2 { get; init; }
    public decimal CashLimit2 { get; init; }
    public int ServiceType { get; init; }
}

public record ListAgreementsQuery : IRequest<Result<PagedList<AgreementDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListAgreementsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListAgreementsQuery, Result<PagedList<AgreementDto>>>
{
    public async Task<Result<PagedList<AgreementDto>>> Handle(
        ListAgreementsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Agreements
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e =>
                e.Name.ToLower().Contains(term) ||
                (e.AccountNumber != null && e.AccountNumber.ToLower().Contains(term)));
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
            .Select(e => new AgreementDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                AccountNumber = e.AccountNumber,
                EntityCode = e.EntityCode,
                Currency = e.Currency,
                SavingsCode = e.SavingsCode,
                CheckingCode = e.CheckingCode,
                BlockCode = e.BlockCode,
                AvailabilityOption = e.AvailabilityOption,
                AvailabilityLimit = e.AvailabilityLimit,
                AvailabilityRate = e.AvailabilityRate,
                AtmOption = e.AtmOption,
                AtmLimit = e.AtmLimit,
                AtmRate = e.AtmRate,
                AtmTransactions = e.AtmTransactions,
                PosOption = e.PosOption,
                PosLimit = e.PosLimit,
                PosRate = e.PosRate,
                PosTransactions = e.PosTransactions,
                ShowBalances = e.ShowBalances,
                Bin = e.Bin,
                AvailableLimit = e.AvailableLimit,
                CashLimit = e.CashLimit,
                OutputPath = e.OutputPath,
                InputPath = e.InputPath,
                AverageDays = e.AverageDays,
                FreeTransactions = e.FreeTransactions,
                HandlingFee = e.HandlingFee,
                AvailableLimit2 = e.AvailableLimit2,
                CashLimit2 = e.CashLimit2,
                ServiceType = e.ServiceType
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<AgreementDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

public record GetAgreementByIdQuery(Guid PublicId) : IRequest<Result<AgreementDto>>;

public class GetAgreementByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAgreementByIdQuery, Result<AgreementDto>>
{
    public async Task<Result<AgreementDto>> Handle(
        GetAgreementByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Agreements
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new AgreementDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                AccountNumber = e.AccountNumber,
                EntityCode = e.EntityCode,
                Currency = e.Currency,
                SavingsCode = e.SavingsCode,
                CheckingCode = e.CheckingCode,
                BlockCode = e.BlockCode,
                AvailabilityOption = e.AvailabilityOption,
                AvailabilityLimit = e.AvailabilityLimit,
                AvailabilityRate = e.AvailabilityRate,
                AtmOption = e.AtmOption,
                AtmLimit = e.AtmLimit,
                AtmRate = e.AtmRate,
                AtmTransactions = e.AtmTransactions,
                PosOption = e.PosOption,
                PosLimit = e.PosLimit,
                PosRate = e.PosRate,
                PosTransactions = e.PosTransactions,
                ShowBalances = e.ShowBalances,
                Bin = e.Bin,
                AvailableLimit = e.AvailableLimit,
                CashLimit = e.CashLimit,
                OutputPath = e.OutputPath,
                InputPath = e.InputPath,
                AverageDays = e.AverageDays,
                FreeTransactions = e.FreeTransactions,
                HandlingFee = e.HandlingFee,
                AvailableLimit2 = e.AvailableLimit2,
                CashLimit2 = e.CashLimit2,
                ServiceType = e.ServiceType
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<AgreementDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
