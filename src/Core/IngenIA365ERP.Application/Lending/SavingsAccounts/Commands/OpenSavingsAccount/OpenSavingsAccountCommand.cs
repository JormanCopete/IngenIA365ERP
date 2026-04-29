using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SavingsAccounts.Commands.OpenSavingsAccount;

public record OpenSavingsAccountCommand : IRequest<Result<Guid>>
{
    public Guid PersonPublicId { get; init; }
    public Guid SavingsLinePublicId { get; init; }
    public decimal? InitialDeposit { get; init; }
}

public class OpenSavingsAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<OpenSavingsAccountCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        OpenSavingsAccountCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve Person and validate is active associate
        var person = await context.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, cancellationToken);

        if (person is null)
            return Result.Failure<Guid>(new Error("Person.NotFound", "Persona no encontrada."));

        var associate = await context.Associates.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PersonId == person.Id && !a.IsDeleted, cancellationToken);

        if (associate is null || associate.Status == "R")
            return Result.Failure<Guid>(new Error("Associate.NotActive",
                "La persona no es un asociado activo."));

        // 2. Resolve SavingsParameter
        var savingsParam = await context.SavingsParameters.AsNoTracking()
            .FirstOrDefaultAsync(sp => sp.PublicId == request.SavingsLinePublicId && !sp.IsDeleted, cancellationToken);

        if (savingsParam is null)
            return Result.Failure<Guid>(new Error("SavingsParameter.NotFound",
                "Linea de ahorro no encontrada."));

        // 3. Generate account number (auto-increment)
        var lastAccount = await context.SavingsAccounts
            .Where(sa => sa.SavingsLineId == savingsParam.SavingsLineId && !sa.IsDeleted)
            .OrderByDescending(sa => sa.AccountNumber)
            .Select(sa => sa.AccountNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var newAccountNumber = lastAccount + 1;
        var personCode = person.LegacyCode ?? person.TaxId;

        // 4. Create SavingsAccount
        var account = new SavingsAccount
        {
            PersonCode = personCode,
            SavingsLineId = savingsParam.SavingsLineId,
            AccountNumber = newAccountNumber,
            CreationDate = DateOnly.FromDateTime(dateTime.UtcNow),
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.SavingsAccounts.Add(account);

        // 5. If InitialDeposit > 0, create DepositEntry
        if (request.InitialDeposit.HasValue && request.InitialDeposit.Value > 0)
        {
            var deposit = new DepositEntry
            {
                DepositLineId = savingsParam.SavingsLineId,
                PersonCode = personCode,
                AccountNumber = newAccountNumber,
                EntryDate = DateOnly.FromDateTime(dateTime.UtcNow),
                CreationDate = DateOnly.FromDateTime(dateTime.UtcNow),
                FirstDeductionDate = DateOnly.FromDateTime(dateTime.UtcNow),
                InstallmentAmount = request.InitialDeposit.Value,
                EntryType = "D",
                UserId = currentUser.UserName ?? "",
                SystemDate = DateOnly.FromDateTime(dateTime.UtcNow),
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };

            context.DepositEntries.Add(deposit);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(account.PublicId);
    }
}

public class OpenSavingsAccountCommandValidator : AbstractValidator<OpenSavingsAccountCommand>
{
    public OpenSavingsAccountCommandValidator()
    {
        RuleFor(x => x.PersonPublicId)
            .NotEmpty().WithMessage("El asociado es requerido.");

        RuleFor(x => x.SavingsLinePublicId)
            .NotEmpty().WithMessage("La linea de ahorro es requerida.");

        RuleFor(x => x.InitialDeposit)
            .GreaterThanOrEqualTo(0).When(x => x.InitialDeposit.HasValue)
            .WithMessage("El deposito inicial no puede ser negativo.");
    }
}
