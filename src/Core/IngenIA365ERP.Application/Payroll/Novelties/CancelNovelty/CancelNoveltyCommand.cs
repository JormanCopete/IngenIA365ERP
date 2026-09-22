using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Novelties.CancelNovelty;

/// <summary>Anula una novedad activa con motivo (FR-005). Conserva la fila; anula también sus traslados.</summary>
public sealed record CancelNoveltyCommand(Guid NoveltyPublicId, string Reason) : IRequest<Result>;

public sealed class CancelNoveltyCommandValidator : AbstractValidator<CancelNoveltyCommand>
{
    public CancelNoveltyCommandValidator()
    {
        RuleFor(x => x.NoveltyPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo de la anulación es obligatorio.").MaximumLength(300);
    }
}

public sealed class CancelNoveltyCommandHandler(
    IApplicationDbContext db,
    IDateTimeService clock,
    ICurrentUserService user,
    IPayrollRunStaleMarker staleMarker,
    CarryOverNoveltiesService carryOver)
    : IRequestHandler<CancelNoveltyCommand, Result>
{
    public async Task<Result> Handle(CancelNoveltyCommand request, CancellationToken ct)
    {
        var novelty = await db.PayrollNovelties
            .Include(n => n.PayPeriod)
            .FirstOrDefaultAsync(n => n.PublicId == request.NoveltyPublicId, ct);
        if (novelty is null)
            return Result.Failure(new Error("Payroll.NoveltyNotFound", "No existe la novedad indicada."));
        if (novelty.Status != NoveltyStatus.Active)
            return Result.Failure(new Error("Payroll.NoveltyNotActive", $"La novedad ya está {novelty.Status}."));
        if (novelty.Origin == NoveltyOrigin.LoanDeduction)
            return Result.Failure(new Error("Payroll.NoveltyNotActive",
                "El descuento lo generó Cartera: se anula desde ese módulo, no desde la nómina."));
        // Feature 010 (D-01): la ausencia por vacaciones la dejó una liquidación aprobada; la deshace la
        // reversión de esa liquidación (o la cancelación del movimiento), no una persona desde aquí.
        if (novelty.Origin == NoveltyOrigin.VacationLeave)
            return Result.Failure(new Error("Payroll.NoveltyNotActive",
                "Esta novedad la generó la liquidación de vacaciones: reverse la liquidación en Nómina › Vacaciones para deshacerla."));

        var editable = await NoveltyRules.EnsureEditableAsync(db, novelty.PayPeriod!, ct);
        if (editable.IsFailure) return editable;

        novelty.Status = NoveltyStatus.Cancelled;
        novelty.StatusReason = request.Reason.Trim();
        novelty.UpdatedAt = clock.UtcNow;
        novelty.UpdatedBy = user.UserName;

        await carryOver.CancelCarryOversAsync(novelty, $"Anulación de la novedad origen: {request.Reason.Trim()}", ct);
        await staleMarker.MarkStaleAsync(novelty.PayPeriodId, $"novedad {novelty.ConceptCode} anulada", ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
