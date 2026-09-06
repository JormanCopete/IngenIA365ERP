using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PayPeriods.Commands.UpdatePayPeriod;

/// <summary>
/// Edita los datos descriptivos de un período <c>Open</c>. El estado nunca lo cambia el
/// cliente (lo mueven calcular, aprobar y reversar), y un período calculado o aprobado
/// no se edita: sus fechas son las de la liquidación.
/// </summary>
public record UpdatePayPeriodCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string? Description { get; init; }
    public string? PayDate { get; init; }
    public string? LiquidationCompanyId { get; init; }
    public int? CycleMonth { get; init; }
    public int? CycleHours { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int? Periodicity { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public int PeriodId { get; init; }
}

public class UpdatePayPeriodCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdatePayPeriodCommand, Result>
{
    public async Task<Result> Handle(
        UpdatePayPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PayPeriods
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        if (entity.Status != PayPeriodStatus.Open)
        {
            return Result.Failure(new Error("Payroll.PeriodNotOpen",
                "Sólo se editan períodos abiertos: uno calculado o aprobado conserva las fechas con las que se liquidó."));
        }

        var start = request.StartDate.Date;
        var end = request.EndDate.Date;

        var overlaps = await context.PayPeriods
            .AnyAsync(p => p.PayrollPlanId == entity.PayrollPlanId
                        && p.Id != entity.Id
                        && p.StartDate <= end && p.EndDate >= start, cancellationToken);
        if (overlaps)
        {
            return Result.Failure(new Error("Payroll.PeriodOverlaps",
                $"Otro período del mismo plan se superpone con {start:dd/MM/yyyy}–{end:dd/MM/yyyy}."));
        }

        entity.Description = request.Description;
        entity.PayDate = request.PayDate;
        entity.LiquidationCompanyId = request.LiquidationCompanyId;
        entity.CycleMonth = request.CycleMonth;
        entity.CycleHours = request.CycleHours;
        entity.StartDate = start;
        entity.EndDate = end;
        entity.Periodicity = request.Periodicity;
        entity.StatusMessage = request.StatusMessage;
        entity.PeriodId = request.PeriodId;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public class UpdatePayPeriodCommandValidator : AbstractValidator<UpdatePayPeriodCommand>
{
    public UpdatePayPeriodCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(100);
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("La fecha final debe ser igual o posterior a la inicial.");
        RuleFor(x => x.StatusMessage).MaximumLength(100);
    }
}
