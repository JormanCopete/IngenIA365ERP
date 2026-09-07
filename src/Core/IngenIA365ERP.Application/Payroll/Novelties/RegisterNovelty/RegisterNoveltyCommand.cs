using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Novelties.RegisterNovelty;

/// <summary>
/// Registra una novedad en un período abierto (FR-001..FR-003). Si el período está
/// aprobado, se niega y ofrece el ajuste retroactivo: la misma novedad en el período
/// abierto siguiente con <see cref="RetroactiveOfPeriodPublicId"/> apuntando al original
/// (FR-005). Deja el borrador del período desactualizado (FR-016).
/// </summary>
public sealed record RegisterNoveltyCommand : IRequest<Result<Guid>>
{
    public Guid PeriodPublicId { get; init; }
    public Guid EmployeePublicId { get; init; }
    public string ConceptCode { get; init; } = string.Empty;
    public decimal? Quantity { get; init; }
    public decimal? Amount { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Notes { get; init; }

    /// <summary>Ajuste retroactivo: período aprobado al que corresponde realmente la novedad.</summary>
    public Guid? RetroactiveOfPeriodPublicId { get; init; }
}

public sealed class RegisterNoveltyCommandValidator : AbstractValidator<RegisterNoveltyCommand>
{
    public RegisterNoveltyCommandValidator()
    {
        RuleFor(x => x.PeriodPublicId).NotEmpty().WithMessage("El período es obligatorio.");
        RuleFor(x => x.EmployeePublicId).NotEmpty().WithMessage("El empleado es obligatorio.");
        RuleFor(x => x.ConceptCode).NotEmpty().WithMessage("El concepto es obligatorio.").MaximumLength(30);
        RuleFor(x => x.Notes).MaximumLength(500).WithMessage("La observación no puede pasar de 500 caracteres.");
        RuleFor(x => x).Must(x => x.Quantity is not null || x.Amount is not null || (x.StartDate is not null && x.EndDate is not null))
            .WithMessage("La novedad necesita cantidad, valor o fechas.");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.StartDate is not null && x.EndDate is not null)
            .WithMessage("La fecha final debe ser igual o posterior a la inicial.");
    }
}

public sealed class RegisterNoveltyCommandHandler(
    IApplicationDbContext db,
    IDateTimeService clock,
    ICurrentUserService user,
    IPayrollRunStaleMarker staleMarker,
    CarryOverNoveltiesService carryOver)
    : IRequestHandler<RegisterNoveltyCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterNoveltyCommand request, CancellationToken ct)
    {
        var periodo = await NoveltyRules.ResolvePeriodAsync(db, request.PeriodPublicId, ct);
        if (periodo.IsFailure) return Result.Failure<Guid>(periodo.Error);
        var period = periodo.Value;

        var editable = await NoveltyRules.EnsureEditableAsync(db, period, ct);
        if (editable.IsFailure) return Result.Failure<Guid>(editable.Error);

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.PublicId == request.EmployeePublicId, ct);
        if (employee is null)
            return Result.Failure<Guid>(new Error("Payroll.EmployeeNotFound", "No existe el empleado indicado."));
        var vigente = NoveltyRules.EnsureEmployeeInPeriod(employee, period);
        if (vigente.IsFailure) return Result.Failure<Guid>(vigente.Error);

        var conceptoRes = await NoveltyRules.ResolveConceptAsync(db, request.ConceptCode, period.EndDate, employee.EmployeeClass, ct);
        if (conceptoRes.IsFailure) return Result.Failure<Guid>(conceptoRes.Error);
        var concept = conceptoRes.Value;

        var campos = NoveltyRules.ValidateFields(concept, period, request.Quantity, request.Amount, request.StartDate, request.EndDate);
        if (campos.IsFailure) return Result.Failure<Guid>(campos.Error);

        var duplicado = await NoveltyRules.EnsureNoDuplicateAsync(db, concept, period.Id, employee.Id, null, ct);
        if (duplicado.IsFailure) return Result.Failure<Guid>(duplicado.Error);

        int? retroactivoDe = null;
        if (request.RetroactiveOfPeriodPublicId is { } retroId)
        {
            var original = await db.PayPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == retroId, ct);
            if (original is null)
                return Result.Failure<Guid>(new Error("Payroll.PeriodNotFound", "No existe el período original del ajuste retroactivo."));
            if (original.Status != PayPeriodStatus.Approved)
                return Result.Failure<Guid>(new Error("Payroll.PeriodNotOpen", "El ajuste retroactivo sólo referencia períodos aprobados."));
            retroactivoDe = original.Id;
        }

        var novelty = new PayrollNovelty
        {
            PayPeriodId = period.Id,
            EmployeeId = employee.Id,
            ConceptDefinitionId = concept.Id,
            ConceptCode = concept.Code,
            Quantity = request.Quantity,
            Amount = request.Amount,
            StartDate = request.StartDate?.Date,
            EndDate = request.EndDate?.Date,
            DaysInPeriod = campos.Value.DaysInPeriod,
            CarryOverDays = campos.Value.CarryOverDays,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Status = NoveltyStatus.Active,
            Origin = retroactivoDe is null ? NoveltyOrigin.Manual : NoveltyOrigin.Retroactive,
            RetroactiveOfPeriodId = retroactivoDe,
            CreatedAt = clock.UtcNow,
            CreatedBy = user.UserName,
        };
        db.PayrollNovelties.Add(novelty);

        // El traslado necesita el Id de la novedad origen: se guarda primero y el resto va
        // en el mismo SaveChanges final. Si el traslado falla, la transacción del
        // SaveChanges no se completa y no queda una novedad sin su traslado.
        await db.SaveChangesAsync(ct);
        await carryOver.CreateCarryOverAsync(novelty, period, ct);
        await staleMarker.MarkStaleAsync(period.Id, $"novedad {concept.Code} registrada", ct);
        await db.SaveChangesAsync(ct);

        return Result.Success(novelty.PublicId);
    }
}
