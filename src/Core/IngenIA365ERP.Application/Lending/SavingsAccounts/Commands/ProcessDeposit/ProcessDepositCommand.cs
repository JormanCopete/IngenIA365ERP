using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SavingsAccounts.Commands.ProcessDeposit;

public record ProcessDepositCommand : IRequest<Result<Guid>>
{
    public Guid SavingsAccountPublicId { get; init; }
    public decimal Amount { get; init; }
    public string TransactionType { get; init; } = "D"; // D=deposit, R=withdrawal
    public string? Reference { get; init; }
}

public class ProcessDepositCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<ProcessDepositCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        ProcessDepositCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find SavingsAccount
        var account = await context.SavingsAccounts
            .FirstOrDefaultAsync(sa => sa.PublicId == request.SavingsAccountPublicId && !sa.IsDeleted, cancellationToken);

        if (account is null)
            return Result.Failure<Guid>(new Error("SavingsAccount.NotFound",
                "Cuenta de ahorro no encontrada."));

        // 2. If withdrawal, validate available balance
        if (request.TransactionType == "R")
        {
            var deposits = await context.DepositEntries
                .Where(de => de.AccountNumber == account.AccountNumber && !de.IsDeleted)
                .SumAsync(de => de.EntryType == "D" ? de.InstallmentAmount : -de.InstallmentAmount, cancellationToken);

            if (deposits < request.Amount)
                return Result.Failure<Guid>(new Error("SavingsAccount.InsufficientBalance",
                    $"Saldo insuficiente. Disponible: {deposits:N0}"));
        }

        // 3. Create DepositEntry
        var entry = new DepositEntry
        {
            DepositLineId = account.SavingsLineId,
            PersonCode = account.PersonCode,
            AccountNumber = account.AccountNumber,
            EntryDate = DateOnly.FromDateTime(dateTime.UtcNow),
            CreationDate = DateOnly.FromDateTime(dateTime.UtcNow),
            FirstDeductionDate = DateOnly.FromDateTime(dateTime.UtcNow),
            InstallmentAmount = request.Amount,
            EntryType = request.TransactionType,
            UserId = currentUser.UserName ?? "",
            SystemDate = DateOnly.FromDateTime(dateTime.UtcNow),
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.DepositEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entry.PublicId);
    }
}

public class ProcessDepositCommandValidator : AbstractValidator<ProcessDepositCommand>
{
    public ProcessDepositCommandValidator()
    {
        RuleFor(x => x.SavingsAccountPublicId)
            .NotEmpty().WithMessage("La cuenta de ahorro es requerida.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");

        RuleFor(x => x.TransactionType)
            .Must(t => t is "D" or "R")
            .WithMessage("Tipo de transaccion invalido. Use 'D' para deposito o 'R' para retiro.");
    }
}
