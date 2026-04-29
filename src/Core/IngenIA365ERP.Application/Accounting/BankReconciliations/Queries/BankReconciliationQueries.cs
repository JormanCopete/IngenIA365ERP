using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.BankReconciliations.Queries;

// --- DTOs ---

public record BankReconciliationItemDto(
    Guid PublicId,
    DateOnly TransactionDate,
    string? DocumentType,
    string? DocumentNumber,
    string? Description,
    decimal DebitAmount,
    decimal CreditAmount,
    bool IsReconciled,
    DateOnly? ReconciliationDate,
    bool IsAdditional,
    string? ModuleCode);

public record BankReconciliationDetailDto(
    Guid MasterPublicId,
    string AccountCode,
    string AccountName,
    string PeriodCode,
    decimal InitialBalance,
    decimal FinalBalance,
    decimal ReconciledDebit,
    decimal ReconciledCredit,
    decimal UnreconciledDebit,
    decimal UnreconciledCredit,
    bool IsClosed,
    List<BankReconciliationItemDto> Items);

// --- Get Bank Reconciliation ---

public record GetBankReconciliationQuery(Guid AccountPublicId, int Year, int Month)
    : IRequest<Result<BankReconciliationDetailDto>>;

public class GetBankReconciliationQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetBankReconciliationQuery, Result<BankReconciliationDetailDto>>
{
    public async Task<Result<BankReconciliationDetailDto>> Handle(
        GetBankReconciliationQuery request, CancellationToken ct)
    {
        // 1. Resolve account
        var account = await context.ChartOfAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PublicId == request.AccountPublicId && !a.IsDeleted, ct);
        if (account is null)
            return Result.Failure<BankReconciliationDetailDto>(new Error("BankReconciliation.AccountNotFound",
                "Cuenta contable no encontrada."));

        // 2. Find reconciliation master
        var periodCode = $"{request.Year}{request.Month:D2}";
        var master = await context.BankReconciliationMasters.AsNoTracking()
            .FirstOrDefaultAsync(m => m.AccountId == account.Id
                                   && m.PeriodCode == periodCode
                                   && !m.IsDeleted, ct);
        if (master is null)
            return Result.Failure<BankReconciliationDetailDto>(new Error("BankReconciliation.NotFound",
                "Conciliacion bancaria no encontrada para este periodo."));

        // 3. Load reconciliation items
        var periodCodeInt = request.Year * 100 + request.Month;
        var items = await context.BankReconciliations.AsNoTracking()
            .Where(r => r.AccountId == account.Id
                     && r.PeriodCode == periodCodeInt
                     && !r.IsDeleted)
            .OrderBy(r => r.TransactionDate)
            .ThenBy(r => r.DocumentNumber)
            .Select(r => new BankReconciliationItemDto(
                r.PublicId,
                r.TransactionDate,
                r.DocumentType,
                r.DocumentNumber,
                r.Description,
                r.DebitAmount,
                r.CreditAmount,
                r.IsReconciled,
                r.ReconciliationDate,
                r.IsAdditional,
                r.ModuleCode))
            .ToListAsync(ct);

        // 4. Calculate reconciled/unreconciled totals
        var reconciledDebit = items.Where(i => i.IsReconciled).Sum(i => i.DebitAmount);
        var reconciledCredit = items.Where(i => i.IsReconciled).Sum(i => i.CreditAmount);
        var unreconciledDebit = items.Where(i => !i.IsReconciled).Sum(i => i.DebitAmount);
        var unreconciledCredit = items.Where(i => !i.IsReconciled).Sum(i => i.CreditAmount);

        var dto = new BankReconciliationDetailDto(
            master.PublicId,
            account.AccountCode,
            account.Name,
            periodCode,
            master.InitialBalance,
            master.FinalBalance,
            reconciledDebit,
            reconciledCredit,
            unreconciledDebit,
            unreconciledCredit,
            master.IsClosed,
            items);

        return Result.Success(dto);
    }
}
