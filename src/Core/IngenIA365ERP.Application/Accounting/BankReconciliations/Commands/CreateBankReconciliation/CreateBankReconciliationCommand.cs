using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.BankReconciliations.Commands.CreateBankReconciliation;

public record CreateBankReconciliationCommand : IRequest<Result<Guid>>
{
    public Guid AccountPublicId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
}

public class CreateBankReconciliationCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateBankReconciliationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateBankReconciliationCommand request, CancellationToken ct)
    {
        // 1. Resolve account
        var account = await context.ChartOfAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PublicId == request.AccountPublicId && !a.IsDeleted, ct);
        if (account is null)
            return Result.Failure<Guid>(new Error("BankReconciliation.AccountNotFound",
                "Cuenta contable no encontrada."));

        // 2. Validate the account has a bank reconciliation code (it's a bank account)
        if (string.IsNullOrEmpty(account.BankReconciliationCode))
            return Result.Failure<Guid>(new Error("BankReconciliation.NotBankAccount",
                "La cuenta seleccionada no es una cuenta bancaria."));

        // 3. Check if reconciliation master already exists for this period
        var periodCode = $"{request.Year}{request.Month:D2}";
        var existing = await context.BankReconciliationMasters.FirstOrDefaultAsync(
            m => m.AccountId == account.Id
              && m.PeriodCode == periodCode
              && !m.IsDeleted, ct);
        if (existing is not null)
            return Result.Failure<Guid>(new Error("BankReconciliation.AlreadyExists",
                "Ya existe una conciliacion bancaria para este periodo."));

        // 4. Calculate initial balance from previous period
        var prevMonth = request.Month == 1 ? 12 : request.Month - 1;
        var prevYear = request.Month == 1 ? request.Year - 1 : request.Year;
        var prevPeriodCode = $"{prevYear}{prevMonth:D2}";

        decimal initialBalance = 0;
        var prevMaster = await context.BankReconciliationMasters.AsNoTracking()
            .FirstOrDefaultAsync(m => m.AccountId == account.Id
                                   && m.PeriodCode == prevPeriodCode
                                   && !m.IsDeleted, ct);
        if (prevMaster is not null)
            initialBalance = prevMaster.FinalBalance;

        // 5. Create reconciliation master
        var master = new BankReconciliationMaster
        {
            AccountId = account.Id,
            BankId = 0, // Will be resolved from account's bank association
            PeriodCode = periodCode,
            InitialBalance = initialBalance,
            FinalBalance = initialBalance, // Will be updated as items are reconciled
            Status = 0,
            IsClosed = false,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.BankReconciliationMasters.Add(master);

        // 6. Load unreconciled journal entries for this bank account into reconciliation items
        var periodCodeInt = request.Year * 100 + request.Month;
        var unreconciledEntries = await context.JournalEntries
            .AsNoTracking()
            .Where(j => j.AccountId == account.Id
                     && !j.IsDeleted
                     && j.Status >= 0) // Not voided
            .Where(j => j.TransactionDate.Year == request.Year
                     && j.TransactionDate.Month == request.Month)
            .ToListAsync(ct);

        foreach (var entry in unreconciledEntries)
        {
            var item = new BankReconciliation
            {
                AccountId = account.Id,
                BankId = master.BankId,
                PeriodCode = periodCodeInt,
                TransactionDate = entry.TransactionDate,
                DocumentType = entry.VoucherTypeCode,
                DocumentNumber = entry.DocumentNumber.ToString(),
                Description = entry.Description,
                DebitAmount = entry.DebitAmount,
                CreditAmount = entry.CreditAmount,
                IsReconciled = false,
                Status = 0,
                IsAdditional = false,
                IsClosed = false,
                MovementSequence = (int?)entry.Id,
                ModuleCode = "CNT",
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.BankReconciliations.Add(item);
        }

        // 7. Also load any unreconciled items from previous periods
        var previousUnreconciled = await context.BankReconciliations
            .Where(r => r.AccountId == account.Id
                     && !r.IsReconciled
                     && !r.IsClosed
                     && !r.IsDeleted
                     && r.PeriodCode < periodCodeInt)
            .ToListAsync(ct);

        foreach (var prev in previousUnreconciled)
        {
            var carryOver = new BankReconciliation
            {
                AccountId = account.Id,
                BankId = master.BankId,
                PeriodCode = periodCodeInt,
                TransactionDate = prev.TransactionDate,
                DocumentType = prev.DocumentType,
                DocumentNumber = prev.DocumentNumber,
                Description = $"[Arrastre] {prev.Description}",
                DebitAmount = prev.DebitAmount,
                CreditAmount = prev.CreditAmount,
                IsReconciled = false,
                Status = 0,
                IsAdditional = false,
                IsClosed = false,
                MovementSequence = prev.MovementSequence,
                ModuleCode = prev.ModuleCode,
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.BankReconciliations.Add(carryOver);
        }

        await context.SaveChangesAsync(ct);
        return Result.Success(master.PublicId);
    }
}

public class CreateBankReconciliationCommandValidator : AbstractValidator<CreateBankReconciliationCommand>
{
    public CreateBankReconciliationCommandValidator()
    {
        RuleFor(x => x.AccountPublicId)
            .NotEmpty().WithMessage("Cuenta bancaria requerida.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Anio invalido.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Mes invalido.");
    }
}
