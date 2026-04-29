using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.LoanApplications.Commands.CreateLoanApplication;

public record CreateLoanApplicationCommand : IRequest<Result<Guid>>
{
    public Guid PersonPublicId { get; init; }
    public Guid CreditLinePublicId { get; init; }
    public decimal RequestedAmount { get; init; }
    public int RequestedTerm { get; init; }
    public string? Purpose { get; init; }
    public string GuaranteeType { get; init; } = string.Empty;
    public string Periodicity { get; init; } = "1"; // 1=Mensual
    public string PaymentCycle { get; init; } = "30";
}

public class CreateLoanApplicationCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateLoanApplicationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateLoanApplicationCommand request, CancellationToken ct)
    {
        // 1. Resolve Person
        var person = await context.People
            .Include(p => p.Associate)
            .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (person is null)
            return Result.Failure<Guid>(new Error("LoanApplication.PersonNotFound",
                "Persona no encontrada."));

        if (!person.IsAssociate)
            return Result.Failure<Guid>(new Error("LoanApplication.NotAssociate",
                "La persona debe ser asociado activo para solicitar credito."));

        if (person.Associate?.Status != "A" && person.Associate?.Status != null)
            return Result.Failure<Guid>(new Error("LoanApplication.AssociateInactive",
                "El asociado no esta activo."));

        // 2. Resolve CreditLine
        var creditLine = await context.CreditLineParameters
            .FirstOrDefaultAsync(cl => cl.PublicId == request.CreditLinePublicId && !cl.IsDeleted, ct);
        if (creditLine is null)
            return Result.Failure<Guid>(new Error("LoanApplication.CreditLineNotFound",
                "Linea de credito no encontrada."));

        // 3. Validate amount within line limits
        if (creditLine.MaxAmount > 0 && request.RequestedAmount > creditLine.MaxAmount)
            return Result.Failure<Guid>(new Error("LoanApplication.AmountExceedsLimit",
                $"El monto solicitado ({request.RequestedAmount:N0}) excede el maximo de la linea ({creditLine.MaxAmount:N0})."));

        // 4. Validate term
        if (creditLine.MaxTerm > 0 && request.RequestedTerm > creditLine.MaxTerm)
            return Result.Failure<Guid>(new Error("LoanApplication.TermExceedsLimit",
                $"El plazo solicitado ({request.RequestedTerm}) excede el maximo de la linea ({creditLine.MaxTerm})."));

        // 5. Generate application number (auto-increment)
        var maxAppNumber = await context.LoanApplications
            .Where(a => !a.IsDeleted)
            .MaxAsync(a => (int?)a.ApplicationNumber, ct) ?? 0;
        var nextAppNumber = maxAppNumber + 1;

        // 6. Calculate installment (French system approximation)
        var monthlyRate = creditLine.InterestRate / 100m / 12m;
        decimal installment;
        if (monthlyRate > 0)
        {
            var factor = (double)monthlyRate * Math.Pow(1 + (double)monthlyRate, request.RequestedTerm)
                         / (Math.Pow(1 + (double)monthlyRate, request.RequestedTerm) - 1);
            installment = request.RequestedAmount * (decimal)factor;
        }
        else
        {
            installment = request.RequestedAmount / request.RequestedTerm;
        }

        // 7. Create LoanApplication with Status="R" (Radicada)
        var personCode = person.LegacyCode ?? person.TaxId;
        var application = new LoanApplication
        {
            ApplicationNumber = nextAppNumber,
            ApplicationDate = dateTime.TodayUtc,
            PersonCode = personCode,
            CreditLineId = creditLine.Id,
            RequestedAmount = request.RequestedAmount,
            InterestRate = creditLine.InterestRate,
            Term = request.RequestedTerm,
            InstallmentAmount = Math.Round(installment, 0),
            GuaranteeType = request.GuaranteeType,
            Periodicity = request.Periodicity,
            PaymentCycle = request.PaymentCycle,
            Status = "R", // Radicada
            Remarks = request.Purpose,
            RecordDate = dateTime.TodayUtc,
            IdentificationNumber = person.TaxId,
            Salary = person.Salary,
            BranchId = person.Associate?.BranchId?.ToString() ?? "1",
            CostCenterId = person.Associate?.CostCenterId?.ToString() ?? "1",
            EntryUserId = currentUser.UserName ?? "system",
            SolicitedInterestRate = creditLine.InterestRate,
            SolicitedTerm = request.RequestedTerm,
            SolicitedPeriodicity = request.Periodicity,
            SolicitedInstallment = Math.Round(installment, 0),
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.LoanApplications.Add(application);
        await context.SaveChangesAsync(ct);

        return Result.Success(application.PublicId);
    }
}

public class CreateLoanApplicationCommandValidator : AbstractValidator<CreateLoanApplicationCommand>
{
    public CreateLoanApplicationCommandValidator()
    {
        RuleFor(x => x.PersonPublicId)
            .NotEmpty().WithMessage("Asociado requerido.");

        RuleFor(x => x.CreditLinePublicId)
            .NotEmpty().WithMessage("Linea de credito requerida.");

        RuleFor(x => x.RequestedAmount)
            .GreaterThan(0).WithMessage("El monto solicitado debe ser mayor a cero.");

        RuleFor(x => x.RequestedTerm)
            .GreaterThan(0).WithMessage("El plazo debe ser mayor a cero.")
            .LessThanOrEqualTo(360).WithMessage("El plazo no puede exceder 360 meses.");

        RuleFor(x => x.GuaranteeType)
            .NotEmpty().WithMessage("Tipo de garantia requerido.");
    }
}
