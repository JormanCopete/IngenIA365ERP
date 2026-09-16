using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Checks.Commands.ProcessCheck;

public record ProcessCheckCommand : IRequest<Result>
{
    public Guid CheckPublicId { get; init; }
    public string Action { get; init; } = string.Empty; // C=cash, V=void, R=return
}

public class ProcessCheckCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<ProcessCheckCommand, Result>
{
    public async Task<Result> Handle(
        ProcessCheckCommand request,
        CancellationToken cancellationToken)
    {
        var check = await context.Checks
            .FirstOrDefaultAsync(c => c.PublicId == request.CheckPublicId && !c.IsDeleted, cancellationToken);

        if (check is null)
            return Result.Failure(new Error("Check.NotFound", "Cheque no encontrado."));

        switch (request.Action)
        {
            case "C": // Cash - Cobrar
            {
                if (check.Status != "E")
                    return Result.Failure(new Error("Check.InvalidState",
                        "Solo se pueden cobrar cheques en estado Emitido."));

                check.Status = "C"; // Cobrado
                check.UpdatedAt = dateTime.UtcNow;
                check.UpdatedBy = currentUser.UserName;
                break;
            }

            case "V": // Void - Anular
            {
                if (check.Status is "A" or "D")
                    return Result.Failure(new Error("Check.AlreadyProcessed",
                        "El cheque ya fue anulado o devuelto."));

                check.Status = "A"; // Anulado
                check.VoidDetail = "Anulacion manual";
                check.VoidUserId = currentUser.UserName;
                check.VoidDate = dateTime.UtcNow;
                check.UpdatedAt = dateTime.UtcNow;
                check.UpdatedBy = currentUser.UserName;

                // E3 (feature 009): contabilización por AccountingPoster pendiente
                break;
            }

            case "R": // Return - Devolver
            {
                if (check.Status != "C")
                    return Result.Failure(new Error("Check.NotCashed",
                        "Solo se pueden devolver cheques cobrados."));

                check.Status = "D"; // Devuelto
                check.UpdatedAt = dateTime.UtcNow;
                check.UpdatedBy = currentUser.UserName;

                // E3 (feature 009): contabilización por AccountingPoster pendiente
                break;
            }

            default:
                return Result.Failure(new Error("Check.InvalidAction",
                    "Accion no valida. Use C (cobrar), V (anular) o R (devolver)."));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public class ProcessCheckCommandValidator : AbstractValidator<ProcessCheckCommand>
{
    public ProcessCheckCommandValidator()
    {
        RuleFor(x => x.CheckPublicId)
            .NotEmpty().WithMessage("El cheque es requerido.");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("La accion es requerida.")
            .Must(a => a is "C" or "V" or "R")
            .WithMessage("La accion debe ser C (cobrar), V (anular) o R (devolver).");
    }
}
