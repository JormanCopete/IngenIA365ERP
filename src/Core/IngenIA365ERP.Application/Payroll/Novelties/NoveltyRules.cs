using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Novelties;

/// <summary>
/// Las reglas de FR-002 que comparten registrar, corregir e importar novedades: período
/// abierto, empleado vigente en el período y en el plan, concepto vigente y aplicable a
/// la clase, campos que el concepto exige, topes, repetición y días dentro del período
/// (FR-003). Cada rechazo dice la causa con su propio código (contracts/api.md §3).
/// </summary>
public static class NoveltyRules
{
    public static async Task<Result<PayPeriod>> ResolvePeriodAsync(IApplicationDbContext db, Guid periodPublicId, CancellationToken ct)
    {
        var period = await db.PayPeriods.Include(p => p.PayrollPlan).FirstOrDefaultAsync(p => p.PublicId == periodPublicId, ct);
        return period is null
            ? Result.Failure<PayPeriod>(new Error("Payroll.PeriodNotFound", "No existe el período de pago indicado."))
            : Result.Success(period);
    }

    /// <summary>
    /// Sólo se registra o cambia una novedad en un período <c>Open</c> o <c>Calculated</c>.
    /// En uno aprobado, el mensaje ofrece el período abierto siguiente del plan para el
    /// ajuste retroactivo (FR-005).
    /// </summary>
    public static async Task<Result> EnsureEditableAsync(IApplicationDbContext db, PayPeriod period, CancellationToken ct)
    {
        if (period.Status is PayPeriodStatus.Open or PayPeriodStatus.Calculated) return Result.Success();

        if (period.Status == PayPeriodStatus.Approved)
        {
            var siguiente = await db.PayPeriods.AsNoTracking()
                .Where(p => p.PayrollPlanId == period.PayrollPlanId && p.StartDate > period.EndDate
                            && (p.Status == PayPeriodStatus.Open || p.Status == PayPeriodStatus.Calculated))
                .OrderBy(p => p.StartDate)
                .FirstOrDefaultAsync(ct);
            var pista = siguiente is null
                ? "No hay un período abierto posterior: cree el siguiente período del plan y registre allí el ajuste retroactivo."
                : $"Registre un ajuste retroactivo en el período abierto siguiente ({siguiente.StartDate:dd/MM/yyyy}–{siguiente.EndDate:dd/MM/yyyy}, retroactiveTargetPeriodPublicId={siguiente.PublicId}).";
            return Result.Failure(new Error("Payroll.PeriodApproved",
                $"El período {period.StartDate:dd/MM/yyyy}–{period.EndDate:dd/MM/yyyy} está aprobado y sus novedades son inmutables. {pista}"));
        }

        return Result.Failure(new Error("Payroll.PeriodNotOpen",
            $"El período está en estado {period.Status} y no admite novedades."));
    }

    public static Result EnsureEmployeeInPeriod(Employee employee, PayPeriod period)
    {
        if (employee.PayrollPlanId != period.PayrollPlanId)
            return Result.Failure(new Error("Payroll.EmployeeNotInPlan",
                "El empleado pertenece a otro plan de nómina: sus novedades van en los períodos de ese plan."));

        var start = period.StartDate.Date;
        var end = period.EndDate.Date;
        var vinculadoDesde = employee.PayrollPlanEffectiveFrom is { } ef && ef > employee.JoinDate ? ef.Date : employee.JoinDate.Date;
        if (vinculadoDesde > end || employee.TerminationDate.Date < start || employee.Status == -1 && employee.TerminationDate.Date < start)
            return Result.Failure(new Error("Payroll.EmployeeNotActiveInPeriod",
                $"El empleado no está vigente en el período {start:dd/MM/yyyy}–{end:dd/MM/yyyy} " +
                $"(ingreso {vinculadoDesde:dd/MM/yyyy}" + (employee.TerminationDate < DateTime.MaxValue.Date ? $", retiro {employee.TerminationDate:dd/MM/yyyy}" : string.Empty) + ")."));

        return Result.Success();
    }

    public static async Task<Result<PayrollConceptDefinition>> ResolveConceptAsync(
        IApplicationDbContext db, string conceptCode, DateTime asOf, EmployeeClass employeeClass, CancellationToken ct)
    {
        var code = conceptCode.Trim().ToUpperInvariant();
        var concept = await db.PayrollConceptDefinitions.AsNoTracking()
            .Where(c => c.Code == code && c.IsActive && c.ValidFrom <= asOf && (c.ValidTo == null || c.ValidTo >= asOf))
            .OrderByDescending(c => c.ValidFrom)
            .FirstOrDefaultAsync(ct);
        if (concept is null)
            return Result.Failure<PayrollConceptDefinition>(new Error("Payroll.ConceptNotFound",
                $"No hay una versión vigente al {asOf:dd/MM/yyyy} del concepto {code}."));
        if (!concept.AppliesTo(employeeClass))
            return Result.Failure<PayrollConceptDefinition>(new Error("Payroll.ConceptNotApplicable",
                $"El concepto {concept.Name} no aplica a la clase de empleado {employeeClass}."));
        if (concept.IsAutomatic && !concept.RequiresAmount && !concept.RequiresQuantity && !concept.RequiresDates)
            return Result.Failure<PayrollConceptDefinition>(new Error("Payroll.ConceptNotApplicable",
                $"El concepto {concept.Name} lo genera el cálculo automáticamente: no se registra como novedad."));
        return Result.Success(concept);
    }

    public sealed record Dias(int DaysInPeriod, int CarryOverDays);

    /// <summary>Valida los campos que el concepto exige y los topes; devuelve los días dentro del período y los trasladados.</summary>
    public static Result<Dias> ValidateFields(PayrollConceptDefinition concept, PayPeriod period,
        decimal? quantity, decimal? amount, DateTime? startDate, DateTime? endDate)
    {
        if (concept.RequiresDates && (startDate is null || endDate is null))
            return Result.Failure<Dias>(new Error("Payroll.ConceptRequiresDates", $"El concepto {concept.Name} exige fecha inicial y final."));
        if (concept.RequiresQuantity && quantity is null)
            return Result.Failure<Dias>(new Error("Payroll.ConceptRequiresQuantity", $"El concepto {concept.Name} exige cantidad (horas o días)."));
        if (concept.RequiresAmount && amount is null)
            return Result.Failure<Dias>(new Error("Payroll.ConceptRequiresAmount", $"El concepto {concept.Name} exige valor."));
        if (quantity is { } q && q <= 0m)
            return Result.Failure<Dias>(new Error("Payroll.ConceptRequiresQuantity", "La cantidad debe ser mayor que cero."));
        if (amount is { } a && a <= 0m)
            return Result.Failure<Dias>(new Error("Payroll.ConceptRequiresAmount", "El valor debe ser mayor que cero."));
        if (concept.MaxQuantity is { } mq && quantity is { } q2 && q2 > mq)
            return Result.Failure<Dias>(new Error("Payroll.NoveltyOverMax", $"La cantidad {q2} supera el máximo del concepto ({mq})."));
        if (concept.MaxAmount is { } ma && amount is { } a2 && a2 > ma)
            return Result.Failure<Dias>(new Error("Payroll.NoveltyOverMax", $"El valor {a2:N0} supera el máximo del concepto ({ma:N0})."));

        var dias = new Dias(0, 0);
        if (startDate is { } s && endDate is { } e)
        {
            s = s.Date; e = e.Date;
            if (e < s)
                return Result.Failure<Dias>(new Error("Payroll.ConceptRequiresDates", "La fecha final no puede ser anterior a la inicial."));
            if (s < period.StartDate.Date)
                return Result.Failure<Dias>(new Error("Payroll.ConceptRequiresDates",
                    $"La novedad empieza el {s:dd/MM/yyyy}, antes del período: regístrela en el período que contiene esa fecha."));
            if (s > period.EndDate.Date)
                return Result.Failure<Dias>(new Error("Payroll.ConceptRequiresDates",
                    $"La novedad empieza el {s:dd/MM/yyyy}, después del período: regístrela en el período correspondiente."));

            var total = CalendarConventions.Days(s, e);
            var enPeriodo = CalendarConventions.Days(s, e < period.EndDate.Date ? e : period.EndDate.Date);
            dias = new Dias(enPeriodo, Math.Max(0, total - enPeriodo));
        }
        return Result.Success(dias);
    }

    public static async Task<Result> EnsureNoDuplicateAsync(IApplicationDbContext db, PayrollConceptDefinition concept,
        int payPeriodId, int employeeId, int? ignoreNoveltyId, CancellationToken ct)
    {
        if (concept.AllowsRepeatInPeriod) return Result.Success();
        var existente = await db.PayrollNovelties.AsNoTracking()
            .Where(n => n.PayPeriodId == payPeriodId && n.EmployeeId == employeeId && n.ConceptCode == concept.Code
                        && n.Status == NoveltyStatus.Active && (ignoreNoveltyId == null || n.Id != ignoreNoveltyId))
            .Select(n => new { n.PublicId })
            .FirstOrDefaultAsync(ct);
        return existente is null
            ? Result.Success()
            : Result.Failure(new Error("Payroll.NoveltyDuplicate",
                $"El concepto {concept.Name} no admite repetirse en el período y ya existe una novedad activa (existingPublicId={existente.PublicId})."));
    }
}
