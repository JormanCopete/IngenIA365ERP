using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Debit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Debit.Cards.Commands.ProcessDebitTransaction;

public record ProcessDebitTransactionCommand : IRequest<Result<Guid>>
{
    public Guid CardPublicId { get; init; }
    public decimal Amount { get; init; }
    public string? MerchantName { get; init; }
    public string TransactionType { get; init; } = "P"; // P=purchase, A=ATM, T=transfer
    public Guid? PosTerminalPublicId { get; init; }
}

public class ProcessDebitTransactionCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<ProcessDebitTransactionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        ProcessDebitTransactionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find card and validate active
        var card = await context.DebitCards
            .FirstOrDefaultAsync(c => c.PublicId == request.CardPublicId && !c.IsDeleted, cancellationToken);

        if (card is null)
            return Result.Failure<Guid>(new Error("DebitCard.NotFound",
                "Tarjeta debito no encontrada."));

        if (card.Status != "A")
            return Result.Failure<Guid>(new Error("DebitCard.NotActive",
                "La tarjeta debito no esta activa."));

        // 2. Find linked SavingsAccount
        if (!card.AccountNumber.HasValue)
            return Result.Failure<Guid>(new Error("DebitCard.NoAccount",
                "La tarjeta no tiene cuenta de ahorros vinculada."));

        var account = await context.SavingsAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountNumber == card.AccountNumber.Value && !a.IsDeleted, cancellationToken);

        if (account is null)
            return Result.Failure<Guid>(new Error("SavingsAccount.NotFound",
                "Cuenta de ahorros vinculada no encontrada."));

        // 3. Validate balance >= amount (sum DepositEntries for account)
        var balance = await context.DepositEntries.AsNoTracking()
            .Where(d => d.AccountNumber == account.AccountNumber && !d.IsDeleted)
            .SumAsync(d => d.InstallmentAmount, cancellationToken);

        if (balance < request.Amount)
            return Result.Failure<Guid>(new Error("DebitCard.InsufficientFunds",
                $"Fondos insuficientes. Saldo disponible: {balance:N0}."));

        // 4. Validate daily limits
        var today = DateOnly.FromDateTime(dateTime.UtcNow);
        var todayTransactions = await context.DebitTransactions.AsNoTracking()
            .Where(t => t.CardId == card.Id
                        && t.TransactionDate.HasValue
                        && DateOnly.FromDateTime(t.TransactionDate.Value) == today
                        && t.Status == "A")
            .ToListAsync(cancellationToken);

        if (request.TransactionType == "A") // ATM
        {
            var todayAtmCount = todayTransactions.Count(t => t.TransactionType == "A");
            var todayAtmTotal = todayTransactions.Where(t => t.TransactionType == "A").Sum(t => t.Amount ?? 0);

            if (card.DailyAtmTransactions.HasValue && todayAtmCount >= card.DailyAtmTransactions.Value)
                return Result.Failure<Guid>(new Error("DebitCard.AtmLimitReached",
                    "Limite de transacciones ATM diarias alcanzado."));

            if (card.DailyAtmLimit.HasValue && todayAtmTotal + request.Amount > card.DailyAtmLimit.Value)
                return Result.Failure<Guid>(new Error("DebitCard.AtmAmountLimitReached",
                    "Limite de monto ATM diario superado."));
        }
        else if (request.TransactionType == "P") // POS Purchase
        {
            var todayPosCount = todayTransactions.Count(t => t.TransactionType == "P");
            var todayPosTotal = todayTransactions.Where(t => t.TransactionType == "P").Sum(t => t.Amount ?? 0);

            if (card.DailyPosTransactions.HasValue && todayPosCount >= card.DailyPosTransactions.Value)
                return Result.Failure<Guid>(new Error("DebitCard.PosLimitReached",
                    "Limite de transacciones POS diarias alcanzado."));

            if (card.DailyPosLimit.HasValue && todayPosTotal + request.Amount > card.DailyPosLimit.Value)
                return Result.Failure<Guid>(new Error("DebitCard.PosAmountLimitReached",
                    "Limite de monto POS diario superado."));
        }

        // 5. Resolve POS terminal if provided
        string? merchantCode = null;
        if (request.PosTerminalPublicId.HasValue)
        {
            var terminal = await context.PosTerminals.AsNoTracking()
                .FirstOrDefaultAsync(t => t.PublicId == request.PosTerminalPublicId.Value && !t.IsDeleted, cancellationToken);
            merchantCode = terminal?.TerminalCode;
        }

        // 6. Create DebitTransaction
        var transaction = new DebitTransaction
        {
            CardId = card.Id,
            CardNumber = card.CardNumber,
            TransactionDate = dateTime.UtcNow,
            Amount = request.Amount,
            TransactionType = request.TransactionType,
            Status = "A", // Aprobado
            MerchantCode = merchantCode,
            SourceSystem = "ERP",
            TransactionTime = dateTime.UtcNow.ToString("HHmmss"),
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.DebitTransactions.Add(transaction);

        // 7. Update card available balance
        card.AvailableBalance = (card.AvailableBalance ?? 0) - request.Amount;
        card.LastEventDate = DateOnly.FromDateTime(dateTime.UtcNow);
        card.UpdatedAt = dateTime.UtcNow;
        card.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(transaction.PublicId);
    }
}

public class ProcessDebitTransactionCommandValidator : AbstractValidator<ProcessDebitTransactionCommand>
{
    public ProcessDebitTransactionCommandValidator()
    {
        RuleFor(x => x.CardPublicId)
            .NotEmpty().WithMessage("La tarjeta es requerida.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");

        RuleFor(x => x.TransactionType)
            .NotEmpty().WithMessage("El tipo de transaccion es requerido.")
            .Must(t => t is "P" or "A" or "T")
            .WithMessage("El tipo de transaccion debe ser P (compra), A (cajero) o T (transferencia).");
    }
}
