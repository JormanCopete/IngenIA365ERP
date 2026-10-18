using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Debit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Debit.Cards.Commands.IssueDebitCard;

public record IssueDebitCardCommand : IRequest<Result<Guid>>
{
    public Guid PersonPublicId { get; init; }
    public Guid SavingsAccountPublicId { get; init; }
    public string CardType { get; init; } = "D";
}

/// <summary>
/// Feature 014 (T007, R28): la tarjeta se vinculaba a la cuenta de ahorros heredada (<c>LND_SavingsAccounts</c>), que se
/// retiró con lo demás de ahorros, aportes y CDT. Las tarjetas débito están fuera de la feature de captaciones (spec, «Fuera de
/// esta feature»): hasta que su propia feature las vincule a una cuenta de captación, emitir responde
/// <c>DebitCard.SavingsAccountUnavailable</c> sin efectos.
/// </summary>
public class IssueDebitCardCommandHandler
    : IRequestHandler<IssueDebitCardCommand, Result<Guid>>
{
    public Task<Result<Guid>> Handle(
        IssueDebitCardCommand request,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<Guid>(DebitCardErrors.SavingsAccountUnavailable));
}

/// <summary>Feature 014 (T007): el error de las tarjetas mientras no haya cuenta de captación a la que vincularlas.</summary>
public static class DebitCardErrors
{
    public static readonly Error SavingsAccountUnavailable = new("DebitCard.SavingsAccountUnavailable",
        "Las cuentas de ahorro heredadas se retiraron: la tarjeta débito no se puede vincular ni mover hasta que se vincule a una cuenta de captación.");
}

public class IssueDebitCardCommandValidator : AbstractValidator<IssueDebitCardCommand>
{
    public IssueDebitCardCommandValidator()
    {
        RuleFor(x => x.PersonPublicId)
            .NotEmpty().WithMessage("La persona es requerida.");

        RuleFor(x => x.SavingsAccountPublicId)
            .NotEmpty().WithMessage("La cuenta de ahorros es requerida.");
    }
}
