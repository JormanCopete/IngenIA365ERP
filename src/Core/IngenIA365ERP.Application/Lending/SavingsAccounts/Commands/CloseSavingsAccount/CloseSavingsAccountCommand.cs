using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SavingsAccounts.Commands.CloseSavingsAccount;

public record CloseSavingsAccountCommand(Guid PublicId) : IRequest<Result>;

public class CloseSavingsAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CloseSavingsAccountCommand, Result>
{
    public async Task<Result> Handle(
        CloseSavingsAccountCommand request,
        CancellationToken cancellationToken)
    {
        var account = await context.SavingsAccounts
            .FirstOrDefaultAsync(sa => sa.PublicId == request.PublicId && !sa.IsDeleted, cancellationToken);

        if (account is null)
            return Result.Failure(new Error("SavingsAccount.NotFound",
                "Cuenta de ahorro no encontrada."));

        // Calculate current balance
        var balance = await context.DepositEntries
            .Where(de => de.AccountNumber == account.AccountNumber && !de.IsDeleted)
            .SumAsync(de => de.EntryType == "D" ? de.InstallmentAmount : -de.InstallmentAmount, cancellationToken);

        // If balance > 0, create a final withdrawal entry
        if (balance > 0)
        {
            var withdrawal = new DepositEntry
            {
                DepositLineId = account.SavingsLineId,
                PersonCode = account.PersonCode,
                AccountNumber = account.AccountNumber,
                EntryDate = DateOnly.FromDateTime(dateTime.UtcNow),
                CreationDate = DateOnly.FromDateTime(dateTime.UtcNow),
                FirstDeductionDate = DateOnly.FromDateTime(dateTime.UtcNow),
                InstallmentAmount = balance,
                EntryType = "R",
                UserId = currentUser.UserName ?? "",
                SystemDate = DateOnly.FromDateTime(dateTime.UtcNow),
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };

            context.DepositEntries.Add(withdrawal);
        }

        // Soft-delete the account
        account.IsDeleted = true;
        account.DeletedAt = dateTime.UtcNow;
        account.DeletedBy = currentUser.UserName;
        account.UpdatedAt = dateTime.UtcNow;
        account.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public class CloseSavingsAccountCommandValidator : AbstractValidator<CloseSavingsAccountCommand>
{
    public CloseSavingsAccountCommandValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("El ID de la cuenta es requerido.");
    }
}
