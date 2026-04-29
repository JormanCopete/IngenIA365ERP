using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.PeriodClose.Commands.ReopenAccountingPeriod;

// Command
public record ReopenAccountingPeriodCommand(int Year, int Month, string Reason) : IRequest<Result>;

// Handler
public class ReopenAccountingPeriodCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<ReopenAccountingPeriodCommand, Result>
{
    public async Task<Result> Handle(
        ReopenAccountingPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var period = await context.AccountingPeriods
            .FirstOrDefaultAsync(p =>
                p.ModuleCode == "CNT" &&
                p.Year == request.Year &&
                p.PeriodNumber == request.Month &&
                !p.IsDeleted,
                cancellationToken);

        if (period is null)
            return Result.Failure(new Error("PeriodReopen.NotFound", "Periodo contable no encontrado."));

        if (period.Status != "C")
            return Result.Failure(new Error("PeriodReopen.NotClosed", "El periodo no se encuentra cerrado."));

        period.Status = "O";
        period.UpdatedAt = dateTime.UtcNow;
        period.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

// Validator
public class ReopenAccountingPeriodCommandValidator : AbstractValidator<ReopenAccountingPeriodCommand>
{
    public ReopenAccountingPeriodCommandValidator()
    {
        RuleFor(x => x.Year)
            .GreaterThan(2000).WithMessage("El ano debe ser mayor a 2000.");
        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("El mes debe estar entre 1 y 12.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Debe indicar el motivo de la reapertura.")
            .MaximumLength(500).WithMessage("El motivo no debe exceder 500 caracteres.");
    }
}
