using FluentValidation;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Savings.Withdrawals.Queries;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Savings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Savings.Withdrawals.Commands.ProcessWithdrawal;

public record ProcessWithdrawalCommand : IRequest<Result<Guid>>
{
    public Guid PersonPublicId { get; init; }
    public Guid WithdrawalReasonPublicId { get; init; }
    public DateOnly? WithdrawalDate { get; init; }
}

public class ProcessWithdrawalCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser,
    IConsultaDeObligaciones obligaciones)
    : IRequestHandler<ProcessWithdrawalCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        ProcessWithdrawalCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve Person, validate is Associate
        var person = await context.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, cancellationToken);

        if (person is null)
            return Result.Failure<Guid>(new Error("Person.NotFound", "Persona no encontrada."));

        var associate = await context.Associates
            .FirstOrDefaultAsync(a => a.PersonId == person.Id && !a.IsDeleted, cancellationToken);

        if (associate is null)
            return Result.Failure<Guid>(new Error("Associate.NotFound",
                "La persona no es un asociado."));

        if (associate.Status == "R")
            return Result.Failure<Guid>(new Error("Associate.AlreadyWithdrawn",
                "El asociado ya se encuentra retirado."));

        // 2. Sin obligaciones vivas en Cartera, como deudor o codeudor, ni carga por activar (feature 013, T0170, A5).
        var deCartera = await ObligacionesEnElRetiro.ContarAsync(obligaciones, person.PublicId, dateTime.HoyLocal, cancellationToken);
        if (deCartera.Bloquea)
            return Result.Failure<Guid>(new Error("Withdrawal.ActiveLoans", deCartera.Vivas > 0
                ? $"El asociado tiene {deCartera.Vivas} obligación(es) viva(s) en Cartera como deudor o codeudor. Debe cancelarlas antes de retirarse."
                : "El asociado está en una carga de Cartera por activar: se retira después de activar Cartera y revisar sus obligaciones."));

        // 3. Resolve withdrawal reason
        var reason = await context.WithdrawalReasons.AsNoTracking()
            .FirstOrDefaultAsync(wr => wr.PublicId == request.WithdrawalReasonPublicId && !wr.IsDeleted, cancellationToken);

        if (reason is null)
            return Result.Failure<Guid>(new Error("WithdrawalReason.NotFound",
                "Motivo de retiro no encontrado."));

        var personCode = person.LegacyCode ?? person.TaxId;
        var today = DateOnly.FromDateTime(dateTime.UtcNow);
        var period = today.Year * 100 + today.Month;

        // 4. Mark Associate as withdrawn
        var priorStatus = associate.Status ?? "A";
        associate.Status = "R";
        associate.WithdrawalDate = request.WithdrawalDate ?? today;
        associate.WithdrawalReasonId = reason.Id;
        associate.UpdatedAt = dateTime.UtcNow;
        associate.UpdatedBy = currentUser.UserName;

        // 5. Create AssociateWithdrawal record
        var withdrawal = new AssociateWithdrawal
        {
            PersonCode = personCode,
            EntryDate = request.WithdrawalDate ?? today,
            PriorStatus = priorStatus,
            CurrentStatus = "R",
            ReasonCode = reason.Id.ToString(),
            WithdrawalReasonId = reason.Id,
            SystemDate = dateTime.UtcNow,
            UserFullName = currentUser.UserName ?? "",
            Period = period,
            ExpirationDate = (request.WithdrawalDate ?? today).AddYears(1),
            CurrentClass = associate.AssociateClass ?? "",
            PriorClass = associate.PreviousClass ?? associate.AssociateClass ?? "",
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.AssociateWithdrawals.Add(withdrawal);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(withdrawal.PublicId);
    }
}

public class ProcessWithdrawalCommandValidator : AbstractValidator<ProcessWithdrawalCommand>
{
    public ProcessWithdrawalCommandValidator()
    {
        RuleFor(x => x.PersonPublicId)
            .NotEmpty().WithMessage("El asociado es requerido.");

        RuleFor(x => x.WithdrawalReasonPublicId)
            .NotEmpty().WithMessage("El motivo de retiro es requerido.");
    }
}
