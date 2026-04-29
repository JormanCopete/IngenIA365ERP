using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Reports;

// --- DTOs ---

public record GeneralLedgerMovementDto(
    DateOnly Date,
    string VoucherType,
    string DocumentNumber,
    string Description,
    string ThirdPartyTaxId,
    string ThirdPartyName,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance);

public record GeneralLedgerAccountDto(
    string AccountCode,
    string AccountName,
    decimal OpeningBalance,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal ClosingBalance,
    List<GeneralLedgerMovementDto> Movements);

public record GeneralLedgerDto(
    DateTime DateFrom,
    DateTime DateTo,
    string? AccountCodeFrom,
    string? AccountCodeTo,
    List<GeneralLedgerAccountDto> Accounts);

// --- Query ---

public record GetGeneralLedgerQuery(
    DateTime DateFrom,
    DateTime DateTo,
    string? AccountCodeFrom,
    string? AccountCodeTo,
    Guid? BranchPublicId) : IRequest<Result<GeneralLedgerDto>>;

public class GetGeneralLedgerQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetGeneralLedgerQuery, Result<GeneralLedgerDto>>
{
    public async Task<Result<GeneralLedgerDto>> Handle(GetGeneralLedgerQuery request, CancellationToken ct)
    {
        var dateFrom = DateOnly.FromDateTime(request.DateFrom);
        var dateTo = DateOnly.FromDateTime(request.DateTo);

        // Get accounts in range
        var accountsQuery = context.ChartOfAccounts.AsNoTracking()
            .Where(a => !a.IsDeleted);

        if (!string.IsNullOrEmpty(request.AccountCodeFrom))
            accountsQuery = accountsQuery.Where(a => string.Compare(a.AccountCode, request.AccountCodeFrom) >= 0);
        if (!string.IsNullOrEmpty(request.AccountCodeTo))
            accountsQuery = accountsQuery.Where(a => string.Compare(a.AccountCode, request.AccountCodeTo) <= 0);

        var accounts = await accountsQuery.OrderBy(a => a.AccountCode).ToListAsync(ct);
        var accountIds = accounts.Select(a => a.Id).ToHashSet();

        // Opening balance: sum of all journal entries before DateFrom
        var openingBalances = await context.JournalEntries.AsNoTracking()
            .Where(j => j.TransactionDate < dateFrom && accountIds.Contains(j.AccountId))
            .GroupBy(j => j.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Debit = g.Sum(x => x.DebitAmount),
                Credit = g.Sum(x => x.CreditAmount)
            })
            .ToListAsync(ct);

        var openingMap = openingBalances.ToDictionary(o => o.AccountId, o => o.Debit - o.Credit);

        // Movements in date range (use JournalEntries directly — they carry AccountId)
        var movements = await context.JournalEntries.AsNoTracking()
            .Where(j => j.TransactionDate >= dateFrom &&
                        j.TransactionDate <= dateTo &&
                        accountIds.Contains(j.AccountId))
            .OrderBy(j => j.TransactionDate)
            .ThenBy(j => j.DocumentNumber)
            .ToListAsync(ct);

        var movementsByAccount = movements.GroupBy(m => m.AccountId).ToDictionary(g => g.Key, g => g.ToList());

        // Collect PersonIds to resolve names
        var personIds = movements.Where(m => m.PersonId.HasValue).Select(m => m.PersonId!.Value).Distinct().ToList();
        var personMap = new Dictionary<int, (string TaxId, string Name)>();
        if (personIds.Count > 0)
        {
            var people = await context.People.AsNoTracking()
                .Where(p => personIds.Contains(p.Id))
                .Select(p => new { p.Id, p.TaxId, p.FirstName, p.LastName })
                .ToListAsync(ct);
            foreach (var p in people)
                personMap[p.Id] = (p.TaxId, $"{p.FirstName} {p.LastName}");
        }

        var result = new List<GeneralLedgerAccountDto>();

        foreach (var acct in accounts)
        {
            openingMap.TryGetValue(acct.Id, out var opening);
            var acctMovements = movementsByAccount.GetValueOrDefault(acct.Id, []);

            var runningBalance = opening;
            var movDtos = new List<GeneralLedgerMovementDto>();

            foreach (var mov in acctMovements)
            {
                runningBalance += mov.DebitAmount - mov.CreditAmount;

                var taxId = "";
                var personName = "";
                if (mov.PersonId.HasValue && personMap.TryGetValue(mov.PersonId.Value, out var pInfo))
                {
                    taxId = pInfo.TaxId ?? "";
                    personName = pInfo.Name ?? "";
                }

                movDtos.Add(new GeneralLedgerMovementDto(
                    mov.TransactionDate,
                    mov.VoucherTypeCode,
                    mov.DocumentNumber.ToString(),
                    mov.Description ?? "",
                    taxId,
                    personName,
                    mov.DebitAmount,
                    mov.CreditAmount,
                    runningBalance));
            }

            var totalDebit = acctMovements.Sum(m => m.DebitAmount);
            var totalCredit = acctMovements.Sum(m => m.CreditAmount);

            result.Add(new GeneralLedgerAccountDto(
                acct.AccountCode, acct.Name,
                opening, totalDebit, totalCredit, opening + totalDebit - totalCredit,
                movDtos));
        }

        return Result.Success(new GeneralLedgerDto(
            request.DateFrom, request.DateTo,
            request.AccountCodeFrom, request.AccountCodeTo,
            result));
    }
}
