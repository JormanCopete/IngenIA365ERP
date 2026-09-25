using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SavingsAccounts.Queries;

// --- DTOs ---

public record SavingsAccountDto(
    Guid PublicId,
    string PersonCode,
    string PersonName,
    long AccountNumber,
    string SavingsLineName,
    DateOnly CreationDate,
    decimal Balance,
    string Status);

public record SavingsAccountDetailDto(
    Guid PublicId,
    string PersonCode,
    string PersonName,
    long AccountNumber,
    int SavingsLineId,
    string SavingsLineName,
    DateOnly CreationDate,
    DateOnly? MaturityDate,
    string Periodicity,
    string PaymentCycle,
    decimal Balance,
    string Status,
    List<DepositEntryDto> Entries);

public record DepositEntryDto(
    Guid PublicId,
    DateOnly EntryDate,
    string EntryType,
    string EntryTypeText,
    decimal Amount,
    string UserId);

// --- Status helper ---

public static class SavingsAccountStatusHelper
{
    public static string GetStatus(bool isDeleted) => isDeleted ? "Cerrada" : "Activa";
    public static string GetEntryTypeText(string code) => code switch
    {
        "D" => "Deposito",
        "R" => "Retiro",
        _ => code
    };
}

// --- List Query ---

public record ListSavingsAccountsQuery : IRequest<Result<PagedList<SavingsAccountDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? PersonPublicId { get; init; }
    public Guid? SavingsLinePublicId { get; init; }
    public string? Status { get; init; } // "A"=active, "C"=closed
}

public class ListSavingsAccountsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListSavingsAccountsQuery, Result<PagedList<SavingsAccountDto>>>
{
    public async Task<Result<PagedList<SavingsAccountDto>>> Handle(
        ListSavingsAccountsQuery request, CancellationToken ct)
    {
        var query = context.SavingsAccounts.AsNoTracking().AsQueryable();

        // Filter by status
        if (request.Status == "C")
            query = query.Where(sa => sa.IsDeleted);
        else if (request.Status == "A" || string.IsNullOrEmpty(request.Status))
            query = query.Where(sa => !sa.IsDeleted);

        // Filter by person
        if (request.PersonPublicId.HasValue)
        {
            var person = await context.People.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId.Value && !p.IsDeleted, ct);
            if (person is not null)
            {
                var personCode = person.LegacyCode ?? person.TaxId;
                query = query.Where(sa => sa.PersonCode == personCode);
            }
        }

        // Filter by savings line
        if (request.SavingsLinePublicId.HasValue)
        {
            var sp = await context.SavingsParameters.AsNoTracking()
                .FirstOrDefaultAsync(s => s.PublicId == request.SavingsLinePublicId.Value && !s.IsDeleted, ct);
            if (sp is not null)
                query = query.Where(sa => sa.SavingsLineId == sp.SavingsLineId);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(sa => sa.CreationDate)
            .ThenByDescending(sa => sa.AccountNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(sa => new
            {
                sa.PublicId,
                sa.PersonCode,
                sa.AccountNumber,
                sa.SavingsLineId,
                sa.CreationDate,
                sa.IsDeleted
            })
            .ToListAsync(ct);

        // Batch load person names
        var personCodes = items.Select(i => i.PersonCode).Distinct().ToList();
        var personNames = await context.People.AsNoTracking()
            .Where(p => personCodes.Contains(p.LegacyCode!) || personCodes.Contains(p.TaxId))
            .ToDictionaryAsync(
                p => p.LegacyCode ?? p.TaxId,
                p => NombreDePersona.Completo(p),
                ct);

        // Batch load savings line names
        var lineIds = items.Select(i => i.SavingsLineId).Distinct().ToList();
        var lineNames = await context.SavingsParameters.AsNoTracking()
            .Where(sp => lineIds.Contains(sp.SavingsLineId))
            .ToDictionaryAsync(sp => sp.SavingsLineId, sp => sp.Name, ct);

        // Calculate balances in batch
        var accountNumbers = items.Select(i => i.AccountNumber).ToList();
        var balances = await context.DepositEntries.AsNoTracking()
            .Where(de => accountNumbers.Contains(de.AccountNumber) && !de.IsDeleted)
            .GroupBy(de => de.AccountNumber)
            .Select(g => new
            {
                AccountNumber = g.Key,
                Balance = g.Sum(de => de.EntryType == "D" ? de.InstallmentAmount : -de.InstallmentAmount)
            })
            .ToDictionaryAsync(x => x.AccountNumber, x => x.Balance, ct);

        var dtos = items.Select(sa => new SavingsAccountDto(
            sa.PublicId,
            sa.PersonCode,
            personNames.TryGetValue(sa.PersonCode, out var name) ? name : sa.PersonCode,
            sa.AccountNumber,
            lineNames.TryGetValue(sa.SavingsLineId, out var lineName) ? lineName : $"Linea {sa.SavingsLineId}",
            sa.CreationDate,
            balances.TryGetValue(sa.AccountNumber, out var bal) ? bal : 0m,
            SavingsAccountStatusHelper.GetStatus(sa.IsDeleted)
        )).ToList();

        return Result.Success(new PagedList<SavingsAccountDto>(dtos, totalCount, request.PageNumber, request.PageSize));
    }
}

// --- Get By Id Query ---

public record GetSavingsAccountByIdQuery(Guid PublicId) : IRequest<Result<SavingsAccountDetailDto>>;

public class GetSavingsAccountByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSavingsAccountByIdQuery, Result<SavingsAccountDetailDto>>
{
    public async Task<Result<SavingsAccountDetailDto>> Handle(
        GetSavingsAccountByIdQuery request, CancellationToken ct)
    {
        var account = await context.SavingsAccounts.AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.PublicId == request.PublicId, ct);

        if (account is null)
            return Result.Failure<SavingsAccountDetailDto>(new Error("SavingsAccount.NotFound",
                "Cuenta de ahorro no encontrada."));

        // Person name
        var person = await context.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.LegacyCode == account.PersonCode || p.TaxId == account.PersonCode, ct);
        var personName = person is not null ? NombreDePersona.Completo(person) : account.PersonCode;

        // Savings line name
        var savingsParam = await context.SavingsParameters.AsNoTracking()
            .FirstOrDefaultAsync(sp => sp.SavingsLineId == account.SavingsLineId, ct);
        var lineName = savingsParam?.Name ?? $"Linea {account.SavingsLineId}";

        // Entries
        var entries = await context.DepositEntries.AsNoTracking()
            .Where(de => de.AccountNumber == account.AccountNumber && !de.IsDeleted)
            .OrderByDescending(de => de.EntryDate)
            .ThenByDescending(de => de.Id)
            .Take(100)
            .Select(de => new DepositEntryDto(
                de.PublicId,
                de.EntryDate,
                de.EntryType,
                SavingsAccountStatusHelper.GetEntryTypeText(de.EntryType),
                de.InstallmentAmount,
                de.UserId))
            .ToListAsync(ct);

        // Balance
        var balance = await context.DepositEntries.AsNoTracking()
            .Where(de => de.AccountNumber == account.AccountNumber && !de.IsDeleted)
            .SumAsync(de => de.EntryType == "D" ? de.InstallmentAmount : -de.InstallmentAmount, ct);

        return Result.Success(new SavingsAccountDetailDto(
            account.PublicId,
            account.PersonCode,
            personName,
            account.AccountNumber,
            account.SavingsLineId,
            lineName,
            account.CreationDate,
            account.MaturityDate,
            account.Periodicity,
            account.PaymentCycle,
            balance,
            SavingsAccountStatusHelper.GetStatus(account.IsDeleted),
            entries));
    }
}

// --- Statement Query ---

public record GetSavingsStatementQuery : IRequest<Result<List<DepositEntryDto>>>
{
    public Guid AccountPublicId { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
}

public class GetSavingsStatementQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSavingsStatementQuery, Result<List<DepositEntryDto>>>
{
    public async Task<Result<List<DepositEntryDto>>> Handle(
        GetSavingsStatementQuery request, CancellationToken ct)
    {
        var account = await context.SavingsAccounts.AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.PublicId == request.AccountPublicId, ct);

        if (account is null)
            return Result.Failure<List<DepositEntryDto>>(new Error("SavingsAccount.NotFound",
                "Cuenta de ahorro no encontrada."));

        var query = context.DepositEntries.AsNoTracking()
            .Where(de => de.AccountNumber == account.AccountNumber && !de.IsDeleted);

        if (request.DateFrom.HasValue)
            query = query.Where(de => de.EntryDate >= request.DateFrom.Value);
        if (request.DateTo.HasValue)
            query = query.Where(de => de.EntryDate <= request.DateTo.Value);

        var entries = await query
            .OrderByDescending(de => de.EntryDate)
            .ThenByDescending(de => de.Id)
            .Select(de => new DepositEntryDto(
                de.PublicId,
                de.EntryDate,
                de.EntryType,
                SavingsAccountStatusHelper.GetEntryTypeText(de.EntryType),
                de.InstallmentAmount,
                de.UserId))
            .ToListAsync(ct);

        return Result.Success(entries);
    }
}
