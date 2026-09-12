using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PayPeriods.Commands.CreatePayPeriod;

/// <summary>
/// Crea un período de pago dentro de un plan de nómina (feature 005, FR-037). Nace
/// siempre <c>Open</c>: el estado lo mueven calcular, aprobar y reversar, nunca el
/// cliente. Dos períodos del mismo plan no se superponen.
/// </summary>
public record CreatePayPeriodCommand : IRequest<Result<Guid>>
{
    /// <summary>Plan de nómina. Nulo = el plan por defecto de la cooperativa.</summary>
    public Guid? PlanPublicId { get; init; }

    /// <summary>Número de planilla (legado). 0 = siguiente disponible para la empresa.</summary>
    public int PlanId { get; init; }

    public int PayrollCompanyId { get; init; }
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

    /// <summary>
    /// Número del período dentro del mes y mes de imputación (feature 006). Nulos = los
    /// propone <c>PeriodCalendar</c> desde la fecha de inicio y la periodicidad del plan.
    /// </summary>
    public byte? SubPeriodNumber { get; init; }
    public short? ImputationYear { get; init; }
    public byte? ImputationMonth { get; init; }
}

public class CreatePayPeriodCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser,
    Novelties.CarryOverNoveltiesService carryOver)
    : IRequestHandler<CreatePayPeriodCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePayPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var plan = request.PlanPublicId is Guid planPublicId
            ? await context.PayrollPlans.FirstOrDefaultAsync(p => p.PublicId == planPublicId, cancellationToken)
            : await context.PayrollPlans.FirstOrDefaultAsync(p => p.IsDefault, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<Guid>(new Error("Payroll.PlanNotFound",
                "No existe el plan de nómina indicado (ni un plan por defecto)."));
        }
        if (!plan.IsActive)
        {
            return Result.Failure<Guid>(new Error("Payroll.PlanInactive",
                $"El plan de nómina «{plan.Name}» está inactivo."));
        }

        var start = request.StartDate.Date;
        var end = request.EndDate.Date;

        var overlaps = await context.PayPeriods
            .AnyAsync(p => p.PayrollPlanId == plan.Id && p.StartDate <= end && p.EndDate >= start, cancellationToken);
        if (overlaps)
        {
            return Result.Failure<Guid>(new Error("Payroll.PeriodOverlaps",
                $"Ya existe un período del plan «{plan.Name}» que se superpone con {start:dd/MM/yyyy}–{end:dd/MM/yyyy}."));
        }

        // Feature 006: la duración tiene que ser la del plan (con las excepciones del calendario).
        if (!PeriodCalendar.DuracionValida(plan.Periodicity, start, end))
            return Result.Failure<Guid>(new Error("Payroll.PeriodLengthMismatch",
                $"Un período del plan «{plan.Name}» ({PeriodCalendar.Nombre(plan.Periodicity)}) dura {(int)plan.Periodicity} días; {start:dd/MM/yyyy}–{end:dd/MM/yyyy} son {(end - start).Days + 1}. Sólo el último período del mes admite lo que el calendario le quite o le sume."));

        var calendario = PeriodCalendar.Proponer(plan.Periodicity, start);
        var subPeriodo = request.SubPeriodNumber ?? calendario.SubPeriodNumber;
        if (subPeriodo < 1 || subPeriodo > PeriodCalendar.PeriodosPorMes(plan.Periodicity))
            return Result.Failure<Guid>(new Error("Payroll.SubPeriodOutOfRange",
                $"El número de período va de 1 a {PeriodCalendar.PeriodosPorMes(plan.Periodicity)} para un plan {PeriodCalendar.Nombre(plan.Periodicity).ToLowerInvariant()}."));
        var mesImputacion = request.ImputationMonth ?? calendario.Month;
        if (mesImputacion < 1 || mesImputacion > 12)
            return Result.Failure<Guid>(new Error("Payroll.ImputationMonthInvalid", "El mes de imputación va de 1 a 12."));

        var planillaNumber = request.PlanId;
        if (planillaNumber <= 0)
        {
            var last = await context.PayPeriods
                .Where(p => p.PayrollCompanyId == request.PayrollCompanyId)
                .MaxAsync(p => (int?)p.PlanId, cancellationToken) ?? 0;
            planillaNumber = last + 1;
        }

        var entity = new PayPeriod
        {
            PlanId = planillaNumber,
            PayrollCompanyId = request.PayrollCompanyId,
            PayrollPlanId = plan.Id,
            Description = request.Description,
            PayDate = request.PayDate,
            LiquidationCompanyId = request.LiquidationCompanyId,
            CycleMonth = request.CycleMonth,
            CycleHours = request.CycleHours,
            StartDate = start,
            EndDate = end,
            Periodicity = request.Periodicity ?? (int)plan.Periodicity,
            SubPeriodNumber = subPeriodo,
            ImputationYear = request.ImputationYear ?? calendario.Year,
            ImputationMonth = mesImputacion,
            Status = PayPeriodStatus.Open,
            StatusMessage = request.StatusMessage,
            PeriodId = request.PeriodId,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.PayPeriods.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        // FR-003: las novedades del período anterior que cruzan hacia este aparecen
        // aquí automáticamente. Va en un segundo SaveChanges porque necesita el Id del
        // período; si falla, el período ya existe y el traslado se reintenta al
        // registrar la siguiente novedad o al abrir el siguiente período.
        var trasladadas = await carryOver.MaterializePendingAsync(entity, cancellationToken);
        if (trasladadas > 0) await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreatePayPeriodCommandValidator : AbstractValidator<CreatePayPeriodCommand>
{
    public CreatePayPeriodCommandValidator()
    {
        RuleFor(x => x.Description)
            .MaximumLength(100).WithMessage("La descripción no puede pasar de 100 caracteres.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("La fecha inicial es obligatoria.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("La fecha final es obligatoria.")
            .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("La fecha final debe ser igual o posterior a la inicial.");

        RuleFor(x => x.StatusMessage)
            .MaximumLength(100).WithMessage("El mensaje de estado no puede pasar de 100 caracteres.");
    }
}
