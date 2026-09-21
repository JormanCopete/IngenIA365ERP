using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.OpeningBalances;

/// <summary>Los cinco valores del saldo; los comparten la apertura y el ajuste.</summary>
public interface IBenefitBalanceValues
{
    DateOnly AsOfDate { get; }
    decimal PendingVacationDays { get; }
    decimal AccruedSeverance { get; }
    decimal AccruedSeveranceInterest { get; }
    decimal AccruedServiceBonus { get; }
    int? ServiceBonusDaysAccrued { get; }
    int? SeveranceDaysAccrued { get; }
    string? Notes { get; }
}

internal static class BenefitBalanceValueRules
{
    public static void Aplicar<T>(AbstractValidator<T> v) where T : IBenefitBalanceValues
    {
        v.RuleFor(x => x.AsOfDate).NotEqual(default(DateOnly)).WithMessage("La fecha de corte del saldo es obligatoria.");
        v.RuleFor(x => x.PendingVacationDays).GreaterThanOrEqualTo(0m).WithMessage("Los días de vacaciones pendientes no pueden ser negativos.");
        v.RuleFor(x => x.AccruedSeverance).GreaterThanOrEqualTo(0m).WithMessage("Las cesantías causadas no pueden ser negativas.");
        v.RuleFor(x => x.AccruedSeveranceInterest).GreaterThanOrEqualTo(0m).WithMessage("Los intereses a las cesantías no pueden ser negativos.");
        v.RuleFor(x => x.AccruedServiceBonus).GreaterThanOrEqualTo(0m).WithMessage("La prima causada no puede ser negativa.");
        v.RuleFor(x => x.ServiceBonusDaysAccrued).InclusiveBetween(0, 360).When(x => x.ServiceBonusDaysAccrued is not null);
        v.RuleFor(x => x.SeveranceDaysAccrued).InclusiveBetween(0, 360).When(x => x.SeveranceDaysAccrued is not null);
        v.RuleFor(x => x.Notes).MaximumLength(500);
    }

    /// <summary>La misma regla que el validador, pero como error de negocio con código propio (la pantalla lo nombra).</summary>
    public static Error? Negativo(IBenefitBalanceValues v)
    {
        if (v.PendingVacationDays < 0m) return BenefitBalanceErrors.NegativeValue("Los días de vacaciones pendientes");
        if (v.AccruedSeverance < 0m) return BenefitBalanceErrors.NegativeValue("Las cesantías causadas");
        if (v.AccruedSeveranceInterest < 0m) return BenefitBalanceErrors.NegativeValue("Los intereses a las cesantías");
        if (v.AccruedServiceBonus < 0m) return BenefitBalanceErrors.NegativeValue("La prima causada");
        return null;
    }

    /// <summary>
    /// La fecha de la primera corrida aprobada en la que aparece el empleado (inicio del período
    /// en la ordinaria, corte en las especiales), o nula si todavía no lo liquidó ninguna.
    /// </summary>
    public static async Task<DateOnly?> PrimeraCorridaAprobadaAsync(IApplicationDbContext db, int employeeId, CancellationToken ct)
    {
        var ordinaria = await db.PayrollRunEmployees.AsNoTracking()
            .Where(re => re.EmployeeId == employeeId && re.Run!.Status == PayrollRunStatus.Approved && re.Run.Kind == PayrollRunKind.Ordinary && re.Run.PayPeriod != null)
            .Select(re => (DateTime?)re.Run!.PayPeriod!.StartDate)
            .OrderBy(d => d)
            .FirstOrDefaultAsync(ct);
        var especial = await db.PayrollRunEmployees.AsNoTracking()
            .Where(re => re.EmployeeId == employeeId && re.Run!.Status == PayrollRunStatus.Approved && re.Run.Kind != PayrollRunKind.Ordinary && re.Run.CutoffDate != null)
            .Select(re => re.Run!.CutoffDate)
            .OrderBy(d => d)
            .FirstOrDefaultAsync(ct);

        var fechas = new List<DateOnly>();
        if (ordinaria is { } o) fechas.Add(DateOnly.FromDateTime(o));
        if (especial is { } e) fechas.Add(e);
        return fechas.Count == 0 ? null : fechas.Min();
    }

    public static object Foto(EmployeeBenefitOpeningBalance f) => new
    {
        kind = f.Kind.ToString(), asOfDate = f.AsOfDate, pendingVacationDays = f.PendingVacationDays, accruedSeverance = f.AccruedSeverance,
        accruedSeveranceInterest = f.AccruedSeveranceInterest, accruedServiceBonus = f.AccruedServiceBonus,
        serviceBonusDaysAccrued = f.ServiceBonusDaysAccrued, severanceDaysAccrued = f.SeveranceDaysAccrued, notes = f.Notes, adjustmentReason = f.AdjustmentReason,
    };
}

// ------------------------------------------------------------------ apertura --

/// <summary>
/// Crea o reemplaza el saldo de apertura de un empleado (contracts/api.md §4, PUT). Reemplaza
/// sólo mientras ninguna liquidación aprobada lo consumió: después, la corrección es un ajuste
/// con motivo (<see cref="AddBenefitBalanceAdjustmentCommand"/>) o reversar la liquidación.
/// <c>ConsumedByRunId</c> lo escribe la aprobación de la liquidación (ola de historias), no esto.
/// </summary>
public sealed record UpsertBenefitBalanceCommand(
    Guid EmployeePublicId,
    DateOnly AsOfDate,
    decimal PendingVacationDays,
    decimal AccruedSeverance,
    decimal AccruedSeveranceInterest,
    decimal AccruedServiceBonus,
    int? ServiceBonusDaysAccrued = null,
    int? SeveranceDaysAccrued = null,
    string? Notes = null) : IRequest<Result<Guid>>, IBenefitBalanceValues;

public sealed class UpsertBenefitBalanceCommandValidator : AbstractValidator<UpsertBenefitBalanceCommand>
{
    public UpsertBenefitBalanceCommandValidator()
    {
        RuleFor(x => x.EmployeePublicId).NotEmpty();
        BenefitBalanceValueRules.Aplicar(this);
    }
}

public sealed class UpsertBenefitBalanceCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<UpsertBenefitBalanceCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UpsertBenefitBalanceCommand request, CancellationToken ct)
    {
        if (BenefitBalanceValueRules.Negativo(request) is { } negativo) return Result.Failure<Guid>(negativo);

        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.EmployeePublicId, ct);
        if (e is null) return Result.Failure<Guid>(BenefitBalanceErrors.EmployeeNotFound);

        var filas = await db.EmployeeBenefitOpeningBalances.Include(b => b.ConsumedByRun).Where(b => b.EmployeeId == e.Id && !b.IsDeleted).ToListAsync(ct);
        var consumidas = filas.Where(f => f.ConsumedByRunId is not null).Select(f => f.ConsumedByRun?.PublicId ?? Guid.Empty).Distinct().ToList();
        if (consumidas.Count > 0) return Result.Failure<Guid>(BenefitBalanceErrors.Consumed(consumidas));

        var primera = await BenefitBalanceValueRules.PrimeraCorridaAprobadaAsync(db, e.Id, ct);
        if (primera is { } p && request.AsOfDate > p) return Result.Failure<Guid>(BenefitBalanceErrors.AsOfAfterFirstRun(request.AsOfDate, p));

        var ahora = clock.UtcNow;
        var apertura = filas.Where(f => f.Kind == OpeningBalanceKind.Opening).OrderByDescending(f => f.Id).FirstOrDefault();
        var antes = apertura is null ? null : BenefitBalanceValueRules.Foto(apertura);

        // Mientras nada lo consumió, los ajustes anteriores sobran: el saldo vuelve a ser uno solo.
        foreach (var ajuste in filas.Where(f => f.Kind == OpeningBalanceKind.Adjustment))
        {
            ajuste.IsDeleted = true; ajuste.DeletedAt = ahora; ajuste.DeletedBy = user.UserName;
        }

        if (apertura is null)
        {
            apertura = new EmployeeBenefitOpeningBalance { EmployeeId = e.Id, Kind = OpeningBalanceKind.Opening, CreatedAt = ahora, CreatedBy = user.UserName };
            db.EmployeeBenefitOpeningBalances.Add(apertura);
        }
        else
        {
            apertura.UpdatedAt = ahora;
            apertura.UpdatedBy = user.UserName;
        }

        apertura.AsOfDate = request.AsOfDate;
        apertura.PendingVacationDays = request.PendingVacationDays;
        apertura.AccruedSeverance = request.AccruedSeverance;
        apertura.AccruedSeveranceInterest = request.AccruedSeveranceInterest;
        apertura.AccruedServiceBonus = request.AccruedServiceBonus;
        apertura.ServiceBonusDaysAccrued = request.ServiceBonusDaysAccrued;
        apertura.SeveranceDaysAccrued = request.SeveranceDaysAccrued;
        apertura.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollOpeningBalanceChanged, nameof(EmployeeBenefitOpeningBalance), apertura.PublicId, antes,
            new { employeePublicId = e.PublicId, balance = BenefitBalanceValueRules.Foto(apertura) }, ct);
        return Result.Success(apertura.PublicId);
    }
}

// -------------------------------------------------------------------- ajuste --

/// <summary>
/// Fila <c>Adjustment</c> con motivo que corrige el saldo vigente cuando ya lo consumió una
/// liquidación (R3). Los valores son el saldo <b>completo</b> corregido a la fecha de corte,
/// no la diferencia: el motor toma la fila más reciente como tramo inicial.
/// </summary>
public sealed record AddBenefitBalanceAdjustmentCommand(
    Guid EmployeePublicId,
    DateOnly AsOfDate,
    decimal PendingVacationDays,
    decimal AccruedSeverance,
    decimal AccruedSeveranceInterest,
    decimal AccruedServiceBonus,
    string Reason,
    int? ServiceBonusDaysAccrued = null,
    int? SeveranceDaysAccrued = null,
    string? Notes = null) : IRequest<Result<Guid>>, IBenefitBalanceValues;

public sealed class AddBenefitBalanceAdjustmentCommandValidator : AbstractValidator<AddBenefitBalanceAdjustmentCommand>
{
    public AddBenefitBalanceAdjustmentCommandValidator()
    {
        RuleFor(x => x.EmployeePublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El ajuste exige un motivo.").MaximumLength(300);
        BenefitBalanceValueRules.Aplicar(this);
    }
}

public sealed class AddBenefitBalanceAdjustmentCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<AddBenefitBalanceAdjustmentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddBenefitBalanceAdjustmentCommand request, CancellationToken ct)
    {
        if (BenefitBalanceValueRules.Negativo(request) is { } negativo) return Result.Failure<Guid>(negativo);

        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.EmployeePublicId, ct);
        if (e is null) return Result.Failure<Guid>(BenefitBalanceErrors.EmployeeNotFound);

        var filas = await db.EmployeeBenefitOpeningBalances.AsNoTracking().Where(b => b.EmployeeId == e.Id && !b.IsDeleted).ToListAsync(ct);
        var vigente = BenefitBalanceRules.Vigente(filas);
        if (vigente is null) return Result.Failure<Guid>(BenefitBalanceErrors.NoBalanceToAdjust);
        if (filas.Any(f => f.Kind == OpeningBalanceKind.Adjustment && f.AsOfDate == request.AsOfDate))
            return Result.Failure<Guid>(BenefitBalanceErrors.AsOfDuplicate(request.AsOfDate));

        var ahora = clock.UtcNow;
        var ajuste = new EmployeeBenefitOpeningBalance
        {
            EmployeeId = e.Id,
            Kind = OpeningBalanceKind.Adjustment,
            AsOfDate = request.AsOfDate,
            PendingVacationDays = request.PendingVacationDays,
            AccruedSeverance = request.AccruedSeverance,
            AccruedSeveranceInterest = request.AccruedSeveranceInterest,
            AccruedServiceBonus = request.AccruedServiceBonus,
            ServiceBonusDaysAccrued = request.ServiceBonusDaysAccrued,
            SeveranceDaysAccrued = request.SeveranceDaysAccrued,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            AdjustsBalanceId = vigente.Id,
            AdjustmentReason = request.Reason.Trim(),
            CreatedAt = ahora,
            CreatedBy = user.UserName,
        };
        db.EmployeeBenefitOpeningBalances.Add(ajuste);
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollOpeningBalanceChanged, nameof(EmployeeBenefitOpeningBalance), ajuste.PublicId,
            BenefitBalanceValueRules.Foto(vigente),
            new { employeePublicId = e.PublicId, adjustsBalancePublicId = vigente.PublicId, balance = BenefitBalanceValueRules.Foto(ajuste) }, ct);
        return Result.Success(ajuste.PublicId);
    }
}
