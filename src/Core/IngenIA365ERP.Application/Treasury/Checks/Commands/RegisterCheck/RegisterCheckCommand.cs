using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Checks.Commands.RegisterCheck;

public record RegisterCheckCommand : IRequest<Result<Guid>>
{
    public Guid BankPublicId { get; init; }
    public string CheckNumber { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public Guid PayeePublicId { get; init; }
    public DateOnly IssueDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public Guid ConceptPublicId { get; init; }
}

public class RegisterCheckCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<RegisterCheckCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        RegisterCheckCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve Bank
        var bank = await context.Banks.AsNoTracking()
            .FirstOrDefaultAsync(b => b.PublicId == request.BankPublicId && !b.IsDeleted, cancellationToken);

        if (bank is null)
            return Result.Failure<Guid>(new Error("Bank.NotFound", "Banco no encontrado."));

        // 2. Resolve Payee (Person)
        var payee = await context.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PayeePublicId && !p.IsDeleted, cancellationToken);

        if (payee is null)
            return Result.Failure<Guid>(new Error("Person.NotFound", "Beneficiario no encontrado."));

        // 3. Resolve Concept
        var concept = await context.TreasuryConcepts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == request.ConceptPublicId && !c.IsDeleted, cancellationToken);

        if (concept is null)
            return Result.Failure<Guid>(new Error("TreasuryConcept.NotFound",
                "Concepto de tesoreria no encontrado."));

        // 4. Get sequential number
        var lastSeq = await context.Checks
            .Where(c => c.BankId == bank.Id)
            .OrderByDescending(c => c.SequentialNumber)
            .Select(c => c.SequentialNumber)
            .FirstOrDefaultAsync(cancellationToken);

        // 5. Parse check number
        int.TryParse(request.CheckNumber, out var checkNum);

        // 6. Create Check with Status="E" (emitted)
        var check = new Check
        {
            ConceptCode = concept.ConceptCode,
            BankId = bank.Id,
            SequentialNumber = lastSeq + 1,
            PersonId = payee.Id,
            CheckDate = request.IssueDate,
            CheckNumber = checkNum,
            Amount = request.Amount,
            Status = "E", // Emitido
            RecordUserId = currentUser.UserName,
            RecordDate = dateTime.UtcNow,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Checks.Add(check);

        // 7. Create AccountingDocument (Debit: CxP/Gasto, Credit: Banco)
        var lastDocNum = await context.AccountingDocuments
            .Where(d => d.VoucherTypeCode == "EG") // Egreso
            .OrderByDescending(d => d.DocumentNumber)
            .Select(d => d.DocumentNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var accDoc = new AccountingDocument
        {
            VoucherTypeCode = "EG",
            DocumentNumber = lastDocNum + 1,
            Detail = $"Cheque #{request.CheckNumber} - {payee.FirstName} {payee.LastName}",
            TotalDebit = request.Amount,
            TotalCredit = request.Amount,
            DocumentDate = request.IssueDate,
            BeneficiaryId = payee.Id,
            CheckNumber = request.CheckNumber,
            BankId = (short)bank.Id,
            ModuleCode = "TRS",
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.AccountingDocuments.Add(accDoc);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(check.PublicId);
    }
}

public class RegisterCheckCommandValidator : AbstractValidator<RegisterCheckCommand>
{
    public RegisterCheckCommandValidator()
    {
        RuleFor(x => x.BankPublicId)
            .NotEmpty().WithMessage("El banco es requerido.");

        RuleFor(x => x.CheckNumber)
            .NotEmpty().WithMessage("El numero de cheque es requerido.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");

        RuleFor(x => x.PayeePublicId)
            .NotEmpty().WithMessage("El beneficiario es requerido.");

        RuleFor(x => x.ConceptPublicId)
            .NotEmpty().WithMessage("El concepto es requerido.");
    }
}
