using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Novelties.CorrectNovelty;

/// <summary>
/// Corrige una novedad (FR-005): crea una versión nueva que apunta a la anterior y marca
/// la anterior <c>Superseded</c> con el motivo. Nada se borra. Los traslados de la versión
/// anterior se anulan y se generan desde la nueva.
/// </summary>
public sealed record CorrectNoveltyCommand : IRequest<Result<Guid>>
{
    public Guid NoveltyPublicId { get; init; }
    public decimal? Quantity { get; init; }
    public decimal? Amount { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Notes { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class CorrectNoveltyCommandValidator : AbstractValidator<CorrectNoveltyCommand>
{
    public CorrectNoveltyCommandValidator()
    {
        RuleFor(x => x.NoveltyPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo de la corrección es obligatorio.").MaximumLength(300);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.StartDate is not null && x.EndDate is not null)
            .WithMessage("La fecha final debe ser igual o posterior a la inicial.");
    }
}

public sealed class CorrectNoveltyCommandHandler(
    IApplicationDbContext db,
    IDateTimeService clock,
    ICurrentUserService user,
    IPayrollRunStaleMarker staleMarker,
    CarryOverNoveltiesService carryOver)
    : IRequestHandler<CorrectNoveltyCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CorrectNoveltyCommand request, CancellationToken ct)
    {
        var anterior = await db.PayrollNovelties
            .Include(n => n.PayPeriod)!.ThenInclude(p => p!.PayrollPlan)
            .Include(n => n.Employee)
            .FirstOrDefaultAsync(n => n.PublicId == request.NoveltyPublicId, ct);
        if (anterior is null)
            return Result.Failure<Guid>(new Error("Payroll.NoveltyNotFound", "No existe la novedad indicada."));
        if (anterior.Status != NoveltyStatus.Active)
            return Result.Failure<Guid>(new Error("Payroll.NoveltyNotActive",
                $"La novedad está {anterior.Status}: sólo se corrige la versión activa."));
        if (anterior.Origin is NoveltyOrigin.CarryOver or NoveltyOrigin.LoanDeduction)
            return Result.Failure<Guid>(new Error("Payroll.NoveltyNotActive",
                "Esta novedad la generó el sistema (traslado o Cartera): corrija la novedad origen."));
        if (anterior.Origin == NoveltyOrigin.VacationLeave)
            return Result.Failure<Guid>(new Error("Payroll.NoveltyNotActive",
                "Esta novedad la generó la liquidación de vacaciones: las fechas se corrigen reversando esa liquidación y registrando el disfrute de nuevo."));

        var period = anterior.PayPeriod!;
        var editable = await NoveltyRules.EnsureEditableAsync(db, period, ct);
        if (editable.IsFailure) return Result.Failure<Guid>(editable.Error);

        var employee = anterior.Employee!;
        var conceptoRes = await NoveltyRules.ResolveConceptAsync(db, anterior.ConceptCode, period.EndDate, employee.EmployeeClass, ct);
        if (conceptoRes.IsFailure) return Result.Failure<Guid>(conceptoRes.Error);
        var concept = conceptoRes.Value;

        var campos = NoveltyRules.ValidateFields(concept, period, request.Quantity, request.Amount, request.StartDate, request.EndDate);
        if (campos.IsFailure) return Result.Failure<Guid>(campos.Error);

        var duplicado = await NoveltyRules.EnsureNoDuplicateAsync(db, concept, period.Id, employee.Id, anterior.Id, ct);
        if (duplicado.IsFailure) return Result.Failure<Guid>(duplicado.Error);

        var ahora = clock.UtcNow;
        anterior.Status = NoveltyStatus.Superseded;
        anterior.StatusReason = request.Reason.Trim();
        anterior.UpdatedAt = ahora;
        anterior.UpdatedBy = user.UserName;
        await carryOver.CancelCarryOversAsync(anterior, $"Corrección de la novedad origen: {request.Reason.Trim()}", ct);

        var nueva = new PayrollNovelty
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
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? anterior.Notes : request.Notes.Trim(),
            Status = NoveltyStatus.Active,
            Origin = anterior.Origin,
            SupersedesNoveltyId = anterior.Id,
            RecurringNoveltyId = anterior.RecurringNoveltyId,
            ImportBatchId = anterior.ImportBatchId,
            RetroactiveOfPeriodId = anterior.RetroactiveOfPeriodId,
            InstallmentNumber = anterior.InstallmentNumber,
            InstallmentTotal = anterior.InstallmentTotal,
            CreatedAt = ahora,
            CreatedBy = user.UserName,
        };
        db.PayrollNovelties.Add(nueva);
        await db.SaveChangesAsync(ct);

        await carryOver.CreateCarryOverAsync(nueva, period, ct);
        await staleMarker.MarkStaleAsync(period.Id, $"novedad {concept.Code} corregida", ct);
        await db.SaveChangesAsync(ct);

        return Result.Success(nueva.PublicId);
    }
}
