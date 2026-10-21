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

/// <summary>
/// Feature 014 (T007, R28): el movimiento leía el saldo de la cuenta de ahorros heredada (<c>LND_SavingsAccounts</c> y
/// <c>LND_DepositEntries</c>), que se retiró. Hasta que las tarjetas se vinculen a una cuenta de captación (fuera de la feature
/// 014), responde <c>DebitCard.SavingsAccountUnavailable</c> sin efectos.
/// </summary>
public class ProcessDebitTransactionCommandHandler
    : IRequestHandler<ProcessDebitTransactionCommand, Result<Guid>>
{
    public Task<Result<Guid>> Handle(
        ProcessDebitTransactionCommand request,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<Guid>(IssueDebitCard.DebitCardErrors.SavingsAccountUnavailable));
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
