using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SavingsInterest.Commands.LiquidateSavingsInterest;

// DTOs
public record SavingsLiquidationResultDto
{
    public int AccountsProcessed { get; init; }
    public decimal TotalInterest { get; init; }
    public Guid? AccountingDocumentId { get; init; }
    public DateOnly LiquidationDate { get; init; }
}

public record SavingsLiquidationPreviewLineDto
{
    public long AccountNumber { get; init; }
    public string PersonName { get; init; } = string.Empty;
    public string SavingsLine { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public decimal Rate { get; init; }
    public int Days { get; init; }
    public decimal ProjectedInterest { get; init; }
}

// Command
public record LiquidateSavingsInterestCommand(DateOnly LiquidationDate)
    : IRequest<Result<SavingsLiquidationResultDto>>;

// Handler
public class LiquidateSavingsInterestCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<LiquidateSavingsInterestCommand, Result<SavingsLiquidationResultDto>>
{
    public async Task<Result<SavingsLiquidationResultDto>> Handle(
        LiquidateSavingsInterestCommand request,
        CancellationToken cancellationToken)
    {
        // Load savings parameters for rates
        var savingsParams = await context.SavingsParameters
            .AsNoTracking()
            .Where(sp => !sp.IsDeleted)
            .ToDictionaryAsync(sp => sp.SavingsLineId, cancellationToken);

        // Load active savings accounts (those with deposits, balance > 0 inferred from entries)
        var accounts = await context.SavingsAccounts
            .Where(sa => !sa.IsDeleted)
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
            return Result.Failure<SavingsLiquidationResultDto>(
                new Error("SavingsLiquidation.NoAccounts", "No se encontraron cuentas de ahorro activas."));

        var totalInterest = 0m;
        var processedCount = 0;
        var periodCode = request.LiquidationDate.Year * 100 + request.LiquidationDate.Month;

        foreach (var account in accounts)
        {
            if (!savingsParams.TryGetValue(account.SavingsLineId, out var param))
                continue;

            var rate = param.InterestPaymentRate;
            if (rate <= 0) continue;

            // Calculate days since creation or last liquidation (approximation)
            var lastDate = account.MaturityDate ?? account.CreationDate;
            var days = request.LiquidationDate.DayNumber - lastDate.DayNumber;
            if (days <= 0) continue;

            // Balance approximation: use min interest balance from parameter
            // In production, balance would come from a running balance calculation
            var balance = param.MinInterestBalance;
            if (balance <= 0) continue;

            var interest = balance * rate / 100m / 360m * days;
            if (interest <= 0) continue;

            // Create deposit entry (credit to savings)
            var entry = new DepositEntry
            {
                DepositLineId = account.SavingsLineId,
                PersonCode = account.PersonCode,
                AccountNumber = account.AccountNumber,
                EntryDate = request.LiquidationDate,
                CreationDate = account.CreationDate,
                FirstDeductionDate = account.FirstDeductionDate ?? request.LiquidationDate,
                DeductionType = "LI",
                Periodicity = account.Periodicity,
                PaymentCycle = account.PaymentCycle,
                InstallmentAmount = Math.Round(interest, 2),
                EntryType = "IN",
                UserId = currentUser.UserName ?? "SYSTEM",
                UserFullName = currentUser.UserName ?? "SYSTEM",
                SystemDate = request.LiquidationDate,
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.DepositEntries.Add(entry);

            totalInterest += Math.Round(interest, 2);
            processedCount++;
        }

        // Create accounting document
        Guid? docId = null;
        // E3 (feature 009): contabilización por AccountingPoster pendiente

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new SavingsLiquidationResultDto
        {
            AccountsProcessed = processedCount,
            TotalInterest = totalInterest,
            AccountingDocumentId = docId,
            LiquidationDate = request.LiquidationDate
        });
    }
}

// Preview Query
public record GetSavingsLiquidationPreviewQuery(DateOnly LiquidationDate)
    : IRequest<Result<List<SavingsLiquidationPreviewLineDto>>>;

public class GetSavingsLiquidationPreviewQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSavingsLiquidationPreviewQuery, Result<List<SavingsLiquidationPreviewLineDto>>>
{
    public async Task<Result<List<SavingsLiquidationPreviewLineDto>>> Handle(
        GetSavingsLiquidationPreviewQuery request,
        CancellationToken cancellationToken)
    {
        var savingsParams = await context.SavingsParameters
            .AsNoTracking()
            .Where(sp => !sp.IsDeleted)
            .ToDictionaryAsync(sp => sp.SavingsLineId, cancellationToken);

        var accounts = await context.SavingsAccounts
            .AsNoTracking()
            .Where(sa => !sa.IsDeleted)
            .OrderBy(sa => sa.AccountNumber)
            .ToListAsync(cancellationToken);

        // Load person names
        var personCodes = accounts.Select(a => a.PersonCode).Distinct().ToList();
        var people = await context.People
            .AsNoTracking()
            .Where(p => personCodes.Contains(p.TaxId))
            .ToDictionaryAsync(p => p.TaxId, cancellationToken);

        var result = accounts.Select(account =>
        {
            var param = savingsParams.GetValueOrDefault(account.SavingsLineId);
            var rate = param?.InterestPaymentRate ?? 0;
            var lastDate = account.MaturityDate ?? account.CreationDate;
            var days = request.LiquidationDate.DayNumber - lastDate.DayNumber;
            if (days < 0) days = 0;
            var balance = param?.MinInterestBalance ?? 0;
            var interest = balance > 0 && rate > 0 ? balance * rate / 100m / 360m * days : 0;

            var personName = people.TryGetValue(account.PersonCode, out var person)
                ? NombreDePersona.Completo(person)
                : account.PersonCode;

            return new SavingsLiquidationPreviewLineDto
            {
                AccountNumber = account.AccountNumber,
                PersonName = personName,
                SavingsLine = param?.Name ?? account.SavingsLineId.ToString(),
                Balance = balance,
                Rate = rate,
                Days = days,
                ProjectedInterest = Math.Round(interest, 2)
            };
        }).ToList();

        return Result.Success(result);
    }
}
