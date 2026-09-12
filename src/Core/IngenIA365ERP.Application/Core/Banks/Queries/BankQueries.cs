using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Banks.Queries;

public record BankDto
{
    public Guid PublicId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? AccountCode { get; init; }
    public string? VoucherTypeCode { get; init; }
    public string? TransferCode { get; init; }
    public string? AccountClass { get; init; }
    public bool CheckDigitRequired { get; init; }
    public int? LastCheckNumber { get; init; }
    public string? AccountingAccountCode { get; init; }
    public string? PrintFormat { get; init; }
    public short? Copies { get; init; }
    public decimal FinancialTaxRate { get; init; }
    public string? FileStructure { get; init; }
    public bool ChargesCommission { get; init; }
    public string? CommissionAccount { get; init; }
    public int? CommissionType { get; init; }
    public decimal? CommissionAmount { get; init; }
    public bool PromptForPrinter { get; init; }
    public string? ControlSequential { get; init; }
}

public record ListBanksQuery : IRequest<Result<PagedList<BankDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListBanksQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListBanksQuery, Result<PagedList<BankDto>>>
{
    public async Task<Result<PagedList<BankDto>>> Handle(
        ListBanksQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Banks
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e =>
                e.Name.ToLower().Contains(term) ||
                (e.ShortName != null && e.ShortName.ToLower().Contains(term)));
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
            .Select(e => new BankDto
            {
                PublicId = e.PublicId,
                Code = e.LegacyCode,
                Name = e.Name,
                ShortName = e.ShortName,
                AccountCode = e.AccountCode,
                VoucherTypeCode = e.VoucherTypeCode,
                TransferCode = e.TransferCode,
                AccountClass = e.AccountClass,
                CheckDigitRequired = e.CheckDigitRequired,
                LastCheckNumber = e.LastCheckNumber,
                AccountingAccountCode = e.AccountingAccountCode,
                PrintFormat = e.PrintFormat,
                Copies = e.Copies,
                FinancialTaxRate = e.FinancialTaxRate,
                FileStructure = e.FileStructure,
                ChargesCommission = e.ChargesCommission,
                CommissionAccount = e.CommissionAccount,
                CommissionType = e.CommissionType,
                CommissionAmount = e.CommissionAmount,
                PromptForPrinter = e.PromptForPrinter,
                ControlSequential = e.ControlSequential
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<BankDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

public record GetBankByIdQuery(Guid PublicId) : IRequest<Result<BankDto>>;

public class GetBankByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetBankByIdQuery, Result<BankDto>>
{
    public async Task<Result<BankDto>> Handle(
        GetBankByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Banks
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new BankDto
            {
                PublicId = e.PublicId,
                Code = e.LegacyCode,
                Name = e.Name,
                ShortName = e.ShortName,
                AccountCode = e.AccountCode,
                VoucherTypeCode = e.VoucherTypeCode,
                TransferCode = e.TransferCode,
                AccountClass = e.AccountClass,
                CheckDigitRequired = e.CheckDigitRequired,
                LastCheckNumber = e.LastCheckNumber,
                AccountingAccountCode = e.AccountingAccountCode,
                PrintFormat = e.PrintFormat,
                Copies = e.Copies,
                FinancialTaxRate = e.FinancialTaxRate,
                FileStructure = e.FileStructure,
                ChargesCommission = e.ChargesCommission,
                CommissionAccount = e.CommissionAccount,
                CommissionType = e.CommissionType,
                CommissionAmount = e.CommissionAmount,
                PromptForPrinter = e.PromptForPrinter,
                ControlSequential = e.ControlSequential
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<BankDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
