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

public class IssueDebitCardCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<IssueDebitCardCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        IssueDebitCardCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve Person (Include Associate to get DepositBankId)
        var person = await context.People.AsNoTracking()
            .Include(p => p.Associate)
            .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, cancellationToken);

        if (person is null)
            return Result.Failure<Guid>(new Error("Person.NotFound", "Persona no encontrada."));

        // 2. Resolve SavingsAccount and validate it belongs to person
        var account = await context.SavingsAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PublicId == request.SavingsAccountPublicId && !a.IsDeleted, cancellationToken);

        if (account is null)
            return Result.Failure<Guid>(new Error("SavingsAccount.NotFound",
                "Cuenta de ahorros no encontrada."));

        if (account.PersonCode != person.LegacyCode && account.PersonCode != person.TaxId)
            return Result.Failure<Guid>(new Error("SavingsAccount.NotOwned",
                "La cuenta de ahorros no pertenece a la persona indicada."));

        // 3. Generate card number (masked format: ****-****-****-XXXX)
        var lastCard = await context.DebitCards
            .Where(c => !c.IsDeleted)
            .OrderByDescending(c => c.Id)
            .Select(c => c.CardNumber)
            .FirstOrDefaultAsync(cancellationToken);

        long lastNum = 0;
        if (!string.IsNullOrEmpty(lastCard))
            long.TryParse(lastCard.Replace("-", "").Replace("*", ""), out lastNum);

        var newCardNumber = (lastNum + 1).ToString().PadLeft(4, '0');
        var maskedCardNumber = $"****-****-****-{newCardNumber[^4..]}";

        // 4. Create DebitCard with Status="A" (active)
        var card = new DebitCard
        {
            CardNumber = maskedCardNumber,
            PersonId = person.Id,
            AccountNumber = (int?)account.AccountNumber,
            IsDebitOrCredit = request.CardType,
            Status = "A", // Activa
            IssueDate = DateOnly.FromDateTime(dateTime.UtcNow),
            ExpiryDate = DateOnly.FromDateTime(dateTime.UtcNow).AddYears(5),
            // Banca del asociado (decision P5b: Associate.DepositBankId).
            // Si la persona no es asociado o no tiene banca de deposito, 0.
            BankId = person.Associate?.DepositBankId ?? 0,
            AvailableBalance = 0,
            DailyAtmLimit = 2000000,
            DailyAtmTransactions = 5,
            DailyPosLimit = 5000000,
            DailyPosTransactions = 20,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.DebitCards.Add(card);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(card.PublicId);
    }
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
