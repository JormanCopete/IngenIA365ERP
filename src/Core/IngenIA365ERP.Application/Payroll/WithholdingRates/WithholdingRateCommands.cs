using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Withholding;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WithholdingRates;

// ------------------------------------------------------------------- calcular --

/// <summary>
/// Calcula el porcentaje fijo del semestre para los empleados en procedimiento 2 (FR-022;
/// research R8). Cada cálculo es una versión: la anterior <c>Calculated</c> del mismo semestre
/// queda <c>Superseded</c>; una aprobada no se toca (se calcula otra versión al lado). Quien no
/// tiene historia queda en <c>skipped</c> con el motivo; si al último mes de la ventana le falta
/// nómina aprobada, el lote avisa (<c>SemesterIncomplete</c>) pero calcula.
/// </summary>
public sealed record CalculateWithholdingRatesCommand(short Year, byte Semester, IReadOnlyList<Guid>? EmployeePublicIds = null) : IRequest<Result<WithholdingRateBatchDto>>;

public sealed class CalculateWithholdingRatesCommandValidator : AbstractValidator<CalculateWithholdingRatesCommand>
{
    public CalculateWithholdingRatesCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween((short)2000, (short)2100);
        RuleFor(x => x.Semester).Must(s => s is 1 or 2).WithMessage("El semestre es 1 (rige julio–diciembre) o 2 (rige enero–junio del año siguiente).");
    }
}

public sealed class CalculateWithholdingRatesCommandHandler(
    IApplicationDbContext db, WithholdingRateInputLoader loader, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<CalculateWithholdingRatesCommand, Result<WithholdingRateBatchDto>>
{
    internal static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    public async Task<Result<WithholdingRateBatchDto>> Handle(CalculateWithholdingRatesCommand request, CancellationToken ct)
    {
        var q = from e in db.Employees join p in db.People.AsNoTracking() on e.PersonId equals p.Id
                where e.WithholdingProcedure == 2 && e.Status == 1
                select new { Employee = e, p.FirstName, p.LastName, p.TaxId };
        if (request.EmployeePublicIds is { Count: > 0 } ids) q = q.Where(x => ids.Contains(x.Employee.PublicId));
        var empleados = await q.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync(ct);
        if (empleados.Count == 0) return Result.Failure<WithholdingRateBatchDto>(WithholdingRateErrors.NoProcedure2Employees);

        var ahora = clock.UtcNow;
        var lote = Guid.NewGuid();
        var items = new List<WithholdingRateItemDto>();
        var saltados = new List<WithholdingRateSkippedDto>();
        var avisos = new List<WarningDto>();
        var incompletos = new List<string>();

        foreach (var x in empleados)
        {
            var e = x.Employee;
            var nombre = $"{x.FirstName} {x.LastName}";
            var carga = await loader.LoadAsync(e, request.Year, request.Semester, ct);
            if (carga.IsFailure)
            {
                if (carga.Error.Code == WithholdingRateErrors.TableMissing.Code) return Result.Failure<WithholdingRateBatchDto>(carga.Error);
                if (carga.Error.Code.EndsWith(".ParametersMissing", StringComparison.Ordinal)) return Result.Failure<WithholdingRateBatchDto>(carga.Error);
                saltados.Add(new WithholdingRateSkippedDto(e.PublicId, nombre, carga.Error.Code, carga.Error.Message));
                continue;
            }
            var l = carga.Value;
            if (l.TableParameterId == 0) return Result.Failure<WithholdingRateBatchDto>(WithholdingRateErrors.TableMissing);
            if (!l.LastMonthApproved) incompletos.Add(nombre);

            var r = FixedRateCalculator.Calculate(l.Input);

            var previas = await db.WithholdingRateCalculations
                .Where(c => c.EmployeeId == e.Id && c.TargetYear == request.Year && c.TargetSemester == request.Semester).ToListAsync(ct);
            var version = (previas.Count == 0 ? 0 : previas.Max(c => c.Version)) + 1;
            foreach (var prev in previas.Where(c => c.Status == WithholdingRateCalculationStatus.Calculated))
            {
                prev.Status = WithholdingRateCalculationStatus.Superseded; prev.UpdatedAt = ahora; prev.UpdatedBy = user.UserName;
            }

            var calc = new WithholdingRateCalculation
            {
                EmployeeId = e.Id, TargetYear = request.Year, TargetSemester = request.Semester, Version = version,
                CalculatedAt = ahora, CalculatedBy = user.UserName ?? "sistema",
                MonthsConsidered = (byte)r.MonthsConsidered, Divisor = r.Divisor,
                TotalGrossIncome = r.TotalGrossIncome, TotalMandatoryContributions = r.TotalMandatoryContributions,
                TotalDeclaredDeductions = r.TotalDeclaredDeductions, TotalExemptIncome = r.TotalExemptIncome,
                DepuratedBase = r.DepuratedBase, AverageMonthlyBase = r.AverageMonthlyBase, UvtValueUsed = r.UvtValue, AverageInUvt = r.AverageInUvt,
                TheoreticalWithholding = r.TheoreticalWithholding, RatePercent = r.RatePercent,
                DepurationSequence = r.Sequence.ToString(), TableParameterId = l.TableParameterId, PlanTableUsed = l.Input.PlanTableUsed,
                Status = WithholdingRateCalculationStatus.Calculated,
                ExplanationJson = JsonSerializer.Serialize(new { divisorSource = r.DivisorSource, rangeText = r.RangeText, tableCode = r.TableCode, tableValidFrom = r.TableValidFrom, steps = r.Steps, depuration = r.DepurationSteps, rounding = "dos decimales, mitad hacia arriba" }, JsonWeb),
                CreatedAt = ahora, CreatedBy = user.UserName,
            };
            foreach (var m in l.Input.Months)
                calc.Months.Add(new WithholdingRateCalculationMonth
                {
                    Year = m.Year, Month = m.Month, GrossIncome = m.GrossIncome, MandatoryContributions = m.MandatoryContributions,
                    IncludedSpecialRuns = m.IncludedSpecialRuns, SourceRunsJson = JsonSerializer.Serialize(m.SourceRuns, JsonWeb),
                });
            db.WithholdingRateCalculations.Add(calc);
            await db.SaveChangesAsync(ct);

            await audit.EmitAsync(AuditEventTypes.PayrollWithholdingRateCalculated, nameof(WithholdingRateCalculation), calc.PublicId, null,
                new { employeePublicId = e.PublicId, year = request.Year, semester = request.Semester, version, months = r.MonthsConsidered, divisor = r.Divisor, averageBase = r.AverageMonthlyBase, percentage = r.RatePercent, sequence = r.Sequence.ToString(), batch = lote }, ct);

            var vigente = await db.EmployeeWithholdingRates.AsNoTracking().Where(t => t.EmployeeId == e.Id && t.ValidFrom <= ahora && (t.ValidTo == null || t.ValidTo >= ahora))
                .OrderByDescending(t => t.ValidFrom).Select(t => (decimal?)t.RatePercent).FirstOrDefaultAsync(ct);
            items.Add(WithholdingRateMappers.Item(calc, e.PublicId, nombre, x.TaxId, l.ValidFrom, l.ValidTo, vigente));
        }

        if (incompletos.Count > 0)
            avisos.Add(new WarningDto(WithholdingRateErrors.SemesterIncomplete,
                $"Al último mes de la ventana le falta nómina aprobada para: {string.Join(", ", incompletos)}. El cálculo usó los meses disponibles; recalcule cuando la apruebe.", new { employees = incompletos }));
        return Result.Success(new WithholdingRateBatchDto(lote, items, saltados, avisos));
    }
}

// -------------------------------------------------------------------- aprobar --

/// <summary>
/// Aprueba un cálculo: cierra la vigencia actual del empleado en <c>PAY_EmployeeWithholdingRates</c>
/// (<c>ValidTo</c> = día anterior al inicio del semestre; nunca la borra, R8) y abre la nueva
/// con <c>Origin = Calculated</c>. La nómina siguiente la lee sin cambios.
/// </summary>
public sealed record ApproveWithholdingRateCommand(Guid CalculationPublicId) : IRequest<Result<WithholdingRateApprovedDto>>;

public sealed class ApproveWithholdingRateCommandValidator : AbstractValidator<ApproveWithholdingRateCommand>
{
    public ApproveWithholdingRateCommandValidator() => RuleFor(x => x.CalculationPublicId).NotEmpty();
}

public sealed class ApproveWithholdingRateCommandHandler(
    IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, IPayrollRunStaleMarker stale, PayrollAuditEmitter audit)
    : IRequestHandler<ApproveWithholdingRateCommand, Result<WithholdingRateApprovedDto>>
{
    public async Task<Result<WithholdingRateApprovedDto>> Handle(ApproveWithholdingRateCommand request, CancellationToken ct)
    {
        var calc = await db.WithholdingRateCalculations.Include(c => c.Employee).FirstOrDefaultAsync(c => c.PublicId == request.CalculationPublicId, ct);
        if (calc is null) return Result.Failure<WithholdingRateApprovedDto>(WithholdingRateErrors.CalculationNotFound);
        if (calc.Status == WithholdingRateCalculationStatus.Approved) return Result.Failure<WithholdingRateApprovedDto>(WithholdingRateErrors.AlreadyApproved(calc.PublicId));
        if (calc.Status != WithholdingRateCalculationStatus.Calculated) return Result.Failure<WithholdingRateApprovedDto>(WithholdingRateErrors.NotCalculated);

        var (_, desde, hasta) = WithholdingRateInputLoader.Semestre(calc.TargetYear, calc.TargetSemester);
        var desdeDt = desde.ToDateTime(TimeOnly.MinValue);
        var hastaDt = hasta.ToDateTime(TimeOnly.MinValue);
        var ahora = clock.UtcNow;

        var tasas = await db.EmployeeWithholdingRates.Where(t => t.EmployeeId == calc.EmployeeId).ToListAsync(ct);
        var antes = tasas.Select(t => new { t.RatePercent, t.ValidFrom, t.ValidTo, Origin = t.Origin.ToString() }).ToList();
        DateOnly? cerradaEn = null;
        foreach (var t in tasas)
        {
            // Lo que cruza el semestre nuevo se cierra la víspera; lo que empieza dentro de él queda retirado en blando.
            if (t.ValidFrom < desdeDt && (t.ValidTo is null || t.ValidTo >= desdeDt))
            {
                t.ValidTo = desdeDt.AddDays(-1); t.UpdatedAt = ahora; t.UpdatedBy = user.UserName;
                cerradaEn = DateOnly.FromDateTime(t.ValidTo.Value);
            }
            else if (t.ValidFrom >= desdeDt && t.ValidFrom <= hastaDt)
            {
                t.IsDeleted = true; t.DeletedAt = ahora; t.DeletedBy = user.UserName;
            }
        }
        var nueva = new EmployeeWithholdingRate
        {
            EmployeeId = calc.EmployeeId, RatePercent = calc.RatePercent, ValidFrom = desdeDt, ValidTo = hastaDt,
            Origin = WithholdingRateOrigin.Calculated, SourceCalculationId = calc.Id, CreatedAt = ahora, CreatedBy = user.UserName,
        };
        db.EmployeeWithholdingRates.Add(nueva);
        calc.Status = WithholdingRateCalculationStatus.Approved;
        calc.ApprovedAt = ahora; calc.ApprovedBy = user.UserName ?? "sistema";
        calc.UpdatedAt = ahora; calc.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct);
        calc.ResultingRateId = nueva.Id;
        await db.SaveChangesAsync(ct);

        var periodosCalculados = await db.PayPeriods.Where(p => p.PayrollPlanId == calc.Employee!.PayrollPlanId && p.Status == PayPeriodStatus.Calculated).Select(p => p.Id).ToListAsync(ct);
        foreach (var pid in periodosCalculados)
            await stale.MarkStaleAsync(pid, $"porcentaje fijo del empleado {calc.Employee!.PublicId} aprobado", ct);

        var empleadoPublicId = calc.Employee!.PublicId;
        await audit.EmitAsync(AuditEventTypes.PayrollWithholdingRateApproved, nameof(WithholdingRateCalculation), calc.PublicId, null,
            new { employeePublicId = empleadoPublicId, year = calc.TargetYear, semester = calc.TargetSemester, version = calc.Version, percentage = calc.RatePercent, validFrom = desde, validTo = hasta, previousClosedAt = cerradaEn }, ct);
        await audit.EmitAsync(AuditEventTypes.PayrollEmployeeWithholdingChanged, nameof(Employee), empleadoPublicId, new { rates = antes },
            new { procedure = 2, rates = new[] { new { calc.RatePercent, ValidFrom = desdeDt, ValidTo = (DateTime?)hastaDt, Origin = "Calculated", sourceCalculation = calc.PublicId } } }, ct);

        return Result.Success(new WithholdingRateApprovedDto(calc.PublicId, empleadoPublicId, desde, hasta, cerradaEn, calc.RatePercent));
    }
}

public sealed record ApproveWithholdingRatesBatchCommand(IReadOnlyList<Guid> CalculationPublicIds) : IRequest<Result<IReadOnlyList<WithholdingRateBatchApproveItemDto>>>;

public sealed class ApproveWithholdingRatesBatchCommandValidator : AbstractValidator<ApproveWithholdingRatesBatchCommand>
{
    public ApproveWithholdingRatesBatchCommandValidator() => RuleFor(x => x.CalculationPublicIds).NotEmpty().WithMessage("Indique al menos un cálculo.");
}

public sealed class ApproveWithholdingRatesBatchCommandHandler(ISender sender) : IRequestHandler<ApproveWithholdingRatesBatchCommand, Result<IReadOnlyList<WithholdingRateBatchApproveItemDto>>>
{
    public async Task<Result<IReadOnlyList<WithholdingRateBatchApproveItemDto>>> Handle(ApproveWithholdingRatesBatchCommand request, CancellationToken ct)
    {
        var salida = new List<WithholdingRateBatchApproveItemDto>();
        foreach (var id in request.CalculationPublicIds.Distinct())
        {
            var r = await sender.Send(new ApproveWithholdingRateCommand(id), ct);
            salida.Add(r.IsSuccess
                ? new WithholdingRateBatchApproveItemDto(id, true, null, null, r.Value)
                : new WithholdingRateBatchApproveItemDto(id, false, r.Error.Code, r.Error.Message, null));
        }
        return Result.Success<IReadOnlyList<WithholdingRateBatchApproveItemDto>>(salida);
    }
}

// ------------------------------------------------------------------- rechazar --

public sealed record RejectWithholdingRateCommand(Guid CalculationPublicId, string Reason) : IRequest<Result>;

public sealed class RejectWithholdingRateCommandValidator : AbstractValidator<RejectWithholdingRateCommand>
{
    public RejectWithholdingRateCommandValidator()
    {
        RuleFor(x => x.CalculationPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo es obligatorio.").MaximumLength(300);
    }
}

public sealed class RejectWithholdingRateCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user) : IRequestHandler<RejectWithholdingRateCommand, Result>
{
    public async Task<Result> Handle(RejectWithholdingRateCommand request, CancellationToken ct)
    {
        var calc = await db.WithholdingRateCalculations.FirstOrDefaultAsync(c => c.PublicId == request.CalculationPublicId, ct);
        if (calc is null) return Result.Failure(WithholdingRateErrors.CalculationNotFound);
        if (calc.Status != WithholdingRateCalculationStatus.Calculated) return Result.Failure(WithholdingRateErrors.NotCalculated);
        calc.Status = WithholdingRateCalculationStatus.Rejected;
        calc.RejectReason = request.Reason.Trim();
        calc.UpdatedAt = clock.UtcNow; calc.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal static class WithholdingRateMappers
{
    public static WithholdingRateItemDto Item(WithholdingRateCalculation c, Guid employeePublicId, string nombre, string documento, DateOnly desde, DateOnly hasta, decimal? vigente) =>
        new(c.PublicId, employeePublicId, nombre, documento, c.TargetYear, c.TargetSemester, c.Version, c.MonthsConsidered, c.Divisor, c.AverageMonthlyBase, c.AverageInUvt,
            c.TheoreticalWithholding, c.RatePercent, c.Status, desde, hasta, c.CalculatedAt, c.CalculatedBy, c.ApprovedAt, c.ApprovedBy, c.DepurationSequence, c.PlanTableUsed, vigente);
}
