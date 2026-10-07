using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Settlement;

/// <summary>
/// Baja un descuento propuesto en la definitiva (FR-018a; contracts/api.md §3.4
/// <c>PUT /{runId}/deductions/{obligationId}</c>). Sólo hacia abajo y con motivo; volver al valor
/// propuesto también vale (y borra el motivo). Recalcula la línea <c>DESC_CARTERA</c>/<c>LIBRANZA</c>
/// enlazada y los totales de la fila y la corrida <b>sin versión nueva</b>: el ajuste es una decisión
/// de la responsable sobre el borrador vigente, no un insumo del motor. Si la suma aplicada supera
/// el neto antes de descuentos queda la bandera <c>DeductionOverNet</c>, que bloquea la aprobación.
/// Todo queda en auditoría (<c>Payroll.Settlement.DeductionAdjusted</c>: propuesto, aplicado, motivo,
/// quién y cuándo). Feature 013 (T0160, R105, DE-06): en un crédito de Cartera pide <c>SimularAsync</c> con valor libre
/// (no escribe) y guarda el saldo <b>exacto</b> que quedaría; si Cartera no simula (sin activar), queda la aproximación del
/// desglose. La línea contabiliza (enmienda D-08).
/// </summary>
public sealed record AdjustSettlementDeductionCommand(Guid RunPublicId, Guid ObligationPublicId, decimal Applied, string? Reason)
    : IRequest<Result<SettlementDeductionItemDto>>;

public sealed class AdjustSettlementDeductionCommandValidator : AbstractValidator<AdjustSettlementDeductionCommand>
{
    public AdjustSettlementDeductionCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.ObligationPublicId).NotEmpty();
        RuleFor(x => x.Applied).GreaterThanOrEqualTo(0m).WithMessage("El descuento aplicado no puede ser negativo.");
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}

public sealed class AdjustSettlementDeductionCommandHandler(IApplicationDbContext db, RecaudoPorNomina recaudo, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<AdjustSettlementDeductionCommand, Result<SettlementDeductionItemDto>>
{
    public async Task<Result<SettlementDeductionItemDto>> Handle(AdjustSettlementDeductionCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<SettlementDeductionItemDto>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Settlement) return Result.Failure<SettlementDeductionItemDto>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Settlement));
        if (!run.IsEditableDraft) return Result.Failure<SettlementDeductionItemDto>(SettlementErrors.NotDraft(run.Status));

        var descuento = await db.SettlementDeductions.FirstOrDefaultAsync(d => d.PublicId == request.ObligationPublicId && d.TerminationId == run.TerminationId, ct);
        if (descuento is null) return Result.Failure<SettlementDeductionItemDto>(SettlementErrors.DeductionNotFound);
        if (request.Applied > descuento.ProposedAmount) return Result.Failure<SettlementDeductionItemDto>(SettlementErrors.DeductionAboveProposed(descuento.ProposedAmount));
        var baja = request.Applied < descuento.ProposedAmount;
        if (baja && string.IsNullOrWhiteSpace(request.Reason)) return Result.Failure<SettlementDeductionItemDto>(SettlementErrors.DeductionReasonRequired);

        var fila = await db.PayrollRunEmployees.Include(e => e.Lines).FirstOrDefaultAsync(e => e.PayrollRunId == run.Id, ct);
        if (fila is null) return Result.Failure<SettlementDeductionItemDto>(SettlementErrors.RunNotFound);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? string.Empty;
        var antes = new { proposed = descuento.ProposedAmount, applied = descuento.AppliedAmount, reason = descuento.AdjustmentReason, status = descuento.Status.ToString() };

        descuento.AppliedAmount = request.Applied;
        descuento.AdjustmentReason = baja ? request.Reason!.Trim() : null;
        descuento.AdjustedBy = quien;
        descuento.AdjustedAt = ahora;
        descuento.Status = baja ? SettlementDeductionStatus.Adjusted : SettlementDeductionStatus.Proposed;
        descuento.UpdatedAt = ahora;
        descuento.UpdatedBy = quien;

        // --- feature 013: el saldo exacto que quedaría en Cartera, por la simulación del contrato (no escribe) ---
        if (descuento.Kind == SettlementDeductionKind.CooperativeLoan && descuento.ObligationPublicId is { } obligacion)
        {
            var simulacion = request.Applied > 0m ? await recaudo.SimularValorLibreAsync(obligacion, request.Applied, clock.HoyLocal, ct) : null;
            descuento.RemainingBalanceAfter = simulacion?.BalancesAfter.PayoffAmount;
        }

        // --- la línea enlazada toma el valor aplicado; si no había (aplicado en cero), se crea ---
        var linea = fila.Lines.FirstOrDefault(l => l.SettlementDeductionId == descuento.Id);
        if (linea is null)
        {
            var codigo = descuento.Kind == SettlementDeductionKind.ThirdPartyLibranza ? SettlementInputLoader.LibranzaCode : WellKnownConceptCodes.LoanDeduction;
            var corte = (run.CutoffDate ?? DateOnly.FromDateTime(ahora)).ToDateTime(TimeOnly.MinValue);
            var concepto = await db.PayrollConceptDefinitions.AsNoTracking()
                .Where(c => c.Code == codigo && c.IsActive && c.ValidFrom <= corte && (c.ValidTo == null || c.ValidTo >= corte))
                .OrderByDescending(c => c.ValidFrom).FirstOrDefaultAsync(ct);
            if (concepto is null)
                return Result.Failure<SettlementDeductionItemDto>(SettlementErrors.ConceptMissing(codigo, corte));
            linea = new PayrollRunLine
            {
                ConceptDefinitionId = concepto.Id,
                ConceptCode = concepto.Code,
                ConceptName = concepto.Name,
                Nature = ConceptNature.Deduction,
                SettlementDeductionId = descuento.Id,
                // Feature 013 (enmienda D-08 de la 010, T53): DESC_CARTERA contabiliza contra la cuenta del concepto.
                AffectsAccounting = true,
                Order = (fila.Lines.Count == 0 ? 0 : fila.Lines.Max(l => l.Order)) + 1,
                CreatedAt = ahora,
                CreatedBy = quien,
            };
            fila.Lines.Add(linea);
        }
        linea.Amount = request.Applied;
        linea.ExplanationJson = JsonSerializer.Serialize(Explicacion(descuento, quien, ahora), RunJson.Options);
        linea.UpdatedAt = ahora;
        linea.UpdatedBy = quien;

        // --- totales de la fila y banderas: sólo lo que el descuento mueve ---
        var descontado = fila.Lines.Where(l => l.SettlementDeductionId != null).Sum(l => l.Amount);
        var otrasDeducciones = fila.Lines.Where(l => l.Nature == ConceptNature.Deduction && l.SettlementDeductionId == null).Sum(l => l.Amount);
        fila.TotalDeductions = otrasDeducciones + descontado;
        fila.NetPay = fila.TotalEarnings - fila.TotalDeductions;
        var netoAntes = fila.TotalEarnings - otrasDeducciones;
        fila.Flags = descontado > netoAntes ? fila.Flags | RunEmployeeFlag.DeductionOverNet : fila.Flags & ~RunEmployeeFlag.DeductionOverNet;
        fila.Flags = fila.NetPay < 0m ? fila.Flags | RunEmployeeFlag.NegativeNet : fila.Flags & ~RunEmployeeFlag.NegativeNet;
        fila.UpdatedAt = ahora;
        fila.UpdatedBy = quien;

        run.TotalDeductions = fila.TotalDeductions;
        run.TotalNet = fila.NetPay;
        run.UpdatedAt = ahora;
        run.UpdatedBy = quien;

        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollSettlementDeductionAdjusted, nameof(SettlementDeduction), descuento.PublicId, antes,
            new
            {
                runPublicId = run.PublicId, obligation = descuento.Description, proposed = descuento.ProposedAmount, applied = descuento.AppliedAmount,
                reason = descuento.AdjustmentReason, adjustedBy = quien, adjustedAt = ahora, status = descuento.Status.ToString(),
                remainingAfter = descuento.RemainingBalanceAfter,
                netAfterDeductions = fila.NetPay, deductionOverNet = fila.Flags.HasFlag(RunEmployeeFlag.DeductionOverNet),
            }, ct);

        return Result.Success(SettlementDeductionsReader.Item(descuento, run));
    }

    /// <summary>La misma explicación que deja el motor (<c>ProposedDeductionsRule</c>), más quién bajó el descuento, cuándo y por qué.</summary>
    private static Explanation Explicacion(SettlementDeduction d, string quien, DateTime cuando)
    {
        var exp = new Explanation { Form = "Descuento al retiro" };
        exp.Note("Obligación", d.Description);
        exp.Step("Propuesto (saldo total o cuotas causadas, según la política)", d.ProposedAmount);
        if (d.FueAjustado)
        {
            exp.Step("Aplicado tras la validación de la responsable (bajado con motivo, auditado)", d.AppliedAmount);
            exp.Note("Motivo", d.AdjustmentReason ?? string.Empty);
            exp.Note("Ajustado por", $"{quien} el {Fmt.Date(cuando)}");
        }
        else
        {
            exp.Step("Aplicado", d.AppliedAmount);
        }
        if (d.Kind == SettlementDeductionKind.CooperativeLoan)
        {
            exp.Note("Origen", "Descuento de un crédito de Cartera: se aplica en Cartera al aprobar la liquidación y contabiliza contra la cuenta de DESC_CARTERA.");
            if (d.RemainingBalanceAfter is { } saldo) exp.Note("Saldo que quedaría en Cartera (simulado)", Fmt.Money(saldo));
        }
        exp.Summary = $"{d.Description}: {Fmt.Money(d.AppliedAmount)}";
        return exp;
    }
}
