using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Caching;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Settlements;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Severance;

/// <summary>
/// Liquida las cesantías e intereses del año (US2, FR-010, FR-011; contracts/api.md §3.2
/// <c>POST /severance</c>): una corrida <c>Severance</c> por empresa y año (D-02) con corte el 31 de
/// diciembre —u otro dentro del año, para pruebas y cierres anticipados—, en <c>Draft</c>. El
/// cargador deja fuera al retirado con definitiva aprobada (<c>RetiradoConDefinitiva</c>) y el motor
/// al integral, al aprendiz en etapa lectiva, al pasante y a quien no tiene días en el año; todos
/// salen en <c>excluded</c> con su razón. Una segunda del mismo año se rechaza mientras la anterior
/// esté en borrador o aprobada (<c>Duplicate</c>, FR-005).
/// </summary>
public sealed record CalculateSeveranceCommand(int Year, DateOnly? CutoffDate = null, IReadOnlyList<Guid>? EmployeePublicIds = null)
    : IRequest<Result<SettlementCalculatedDto>>;

public sealed class CalculateSeveranceCommandValidator : AbstractValidator<CalculateSeveranceCommand>
{
    public CalculateSeveranceCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100).WithMessage("El año de la liquidación debe ser un año de cuatro cifras.");
        RuleFor(x => x.CutoffDate).Must((c, corte) => corte is null || corte.Value.Year == c.Year)
            .WithMessage("La fecha de corte debe caer dentro del año liquidado.");
        RuleFor(x => x.EmployeePublicIds).Must(ids => ids is null || ids.All(id => id != Guid.Empty))
            .WithMessage("Los identificadores de empleado no pueden estar vacíos.");
    }
}

/// <summary>Recalcula el borrador vigente: versión N+1, la anterior queda <c>Superseded</c>; una aprobada no se recalcula, se reversa.</summary>
public sealed record RecalculateSeveranceCommand(Guid RunPublicId) : IRequest<Result<SettlementCalculatedDto>>;

public sealed class RecalculateSeveranceCommandValidator : AbstractValidator<RecalculateSeveranceCommand>
{
    public RecalculateSeveranceCommandValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class CalculateSeveranceCommandHandler(
    IApplicationDbContext db, SettlementInputLoader loader, SettlementRunPersister persister,
    IDistributedLock distributedLock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<CalculateSeveranceCommand, Result<SettlementCalculatedDto>>
{
    public Task<Result<SettlementCalculatedDto>> Handle(CalculateSeveranceCommand request, CancellationToken ct)
    {
        var calculator = new SeveranceCalculator(db, loader, persister, distributedLock, user, audit);
        var corte = request.CutoffDate ?? new DateOnly(request.Year, 12, 31);
        if (corte.Year != request.Year) return Task.FromResult(Result.Failure<SettlementCalculatedDto>(SeveranceErrors.CutoffOutsideYear(request.Year, corte)));
        return calculator.CalcularAsync(request.Year, corte, request.EmployeePublicIds, recalculo: false, ct);
    }
}

public sealed class RecalculateSeveranceCommandHandler(
    IApplicationDbContext db, SettlementInputLoader loader, SettlementRunPersister persister,
    IDistributedLock distributedLock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<RecalculateSeveranceCommand, Result<SettlementCalculatedDto>>
{
    public async Task<Result<SettlementCalculatedDto>> Handle(RecalculateSeveranceCommand request, CancellationToken ct)
    {
        var calculator = new SeveranceCalculator(db, loader, persister, distributedLock, user, audit);
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<SettlementCalculatedDto>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Severance) return Result.Failure<SettlementCalculatedDto>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Severance));
        if (!run.IsEditableDraft) return Result.Failure<SettlementCalculatedDto>(SettlementErrors.NotDraft(run.Status));

        // El recálculo vuelve a tomar a toda la población del año: un borrador acotado a unos empleados
        // (POST con employeePublicIds) se recalcula completo, que es lo que una liquidación anual es.
        return await calculator.CalcularAsync(run.Year!.Value, run.CutoffDate!.Value, employeePublicIds: null, recalculo: true, ct);
    }
}

/// <summary>
/// El camino común de calcular y recalcular: cargar (<see cref="SettlementInputLoader"/>),
/// liquidar con el motor puro y persistir el borrador (<see cref="SettlementRunPersister"/>) en un
/// solo <c>SaveChanges</c>, con auditoría <c>Payroll.Settlement.Calculated</c>. Un solo cálculo a la
/// vez por cooperativa y año (lock distribuido, como la ordinaria). Lo arman los dos handlers con lo
/// que MediatR les inyecta: no se registra en el contenedor.
/// </summary>
public sealed class SeveranceCalculator(
    IApplicationDbContext db,
    SettlementInputLoader loader,
    SettlementRunPersister persister,
    IDistributedLock distributedLock,
    ICurrentUserService user,
    PayrollAuditEmitter audit)
{
    public static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(5);

    public async Task<Result<SettlementCalculatedDto>> CalcularAsync(int year, DateOnly cutoff, IReadOnlyList<Guid>? employeePublicIds, bool recalculo, CancellationToken ct)
    {
        var key = SettlementRunKey.Cesantias(year, cutoff);

        var lockKey = $"payroll:severance:{user.TenantId ?? "-"}:{year}";
        await using var handle = await distributedLock.TryAcquireAsync(lockKey, LockTtl, ct);
        if (handle is null)
            return Result.Failure<SettlementCalculatedDto>(new Error("Payroll.RunInProgress",
                "Ya hay una liquidación de cesantías en curso para este año. Espere a que termine y refresque."));

        var anteriores = await persister.CorridasDeAsync(key, ct);
        if (SettlementRunPersister.Duplicado(anteriores, key, recalculo) is { } duplicado)
            return Result.Failure<SettlementCalculatedDto>(duplicado);

        // --- empleados pedidos por PublicId → Id; uno que no exista es error, no silencio ---
        IReadOnlyList<int>? ids = null;
        if (employeePublicIds is { Count: > 0 })
        {
            var pedidos = employeePublicIds.Distinct().ToList();
            var encontrados = await db.Employees.AsNoTracking().Where(e => pedidos.Contains(e.PublicId)).Select(e => new { e.Id, e.PublicId }).ToListAsync(ct);
            if (encontrados.Count != pedidos.Count) return Result.Failure<SettlementCalculatedDto>(SettlementErrors.EmployeeNotFound);
            ids = encontrados.Select(e => e.Id).ToList();
        }

        var batch = await loader.LoadAsync(SettlementLoadRequest.Cesantias(year, cutoff, ids), ct);
        var faltantes = batch.MissingRequiredParameters;
        if (faltantes.Count > 0)
            return Result.Failure<SettlementCalculatedDto>(SettlementErrors.ParametersMissing(faltantes, cutoff.ToDateTime(TimeOnly.MinValue)));

        var engine = new SettlementCalculationEngine();
        var calculados = new List<SettlementCalculatedEmployee>(batch.Employees.Count);
        foreach (var e in batch.Employees)
        {
            try
            {
                calculados.Add(new SettlementCalculatedEmployee(e, engine.Calculate(e.Input)));
            }
            catch (CalculationRefusedException ex)
            {
                return Result.Failure<SettlementCalculatedDto>(SettlementErrors.CalculationRefused(e.FullName, ex.Message));
            }
        }

        var excluidos = batch.Excluded.Concat(SettlementRunPersister.Excluidos(calculados)).ToList();
        if (calculados.All(c => c.Result.Excluded))
            return Result.Failure<SettlementCalculatedDto>(SettlementErrors.NoEligibleEmployees(excluidos));

        var run = persister.CrearBorrador(batch, key, calculados, anteriores);
        await db.SaveChangesAsync(ct);

        var avisos = Avisos(batch, calculados);
        await audit.EmitAsync(AuditEventTypes.PayrollSettlementCalculated, nameof(PayrollRun), run.PublicId, null, new
        {
            kind = run.Kind.ToString(), year, cutoffDate = cutoff, version = run.Version, recalculated = recalculo,
            employees = run.EmployeeCount, excluded = excluidos.Count, totalNet = run.TotalNet, inputsHash = run.InputsHash,
            warnings = avisos.Select(w => w.Code).ToList(),
        }, ct);

        return Result.Success(new SettlementCalculatedDto(
            run.PublicId, run.Version, run.Kind.ToString(), cutoff, run.EmployeeCount,
            new RunTotalsDto(run.TotalEarnings, run.TotalDeductions, run.TotalEmployerContributions, run.TotalProvisions, run.TotalNet, run.RoundingAdjustment),
            SettlementRunPersister.Bloqueos(calculados), excluidos, avisos));
    }

    /// <summary>Los avisos de la liquidación: el saldo inicial ausente (por empleado, con código) y lo que el cargador y el motor dijeron sin bloquear.</summary>
    private static IReadOnlyList<WarningDto> Avisos(SettlementBatch batch, IReadOnlyList<SettlementCalculatedEmployee> calculados)
    {
        var avisos = new List<WarningDto>(batch.Warnings);
        var sinSaldo = calculados.Where(c => !c.Result.Excluded && c.Result.Flags.HasFlag(SettlementFlags.OpeningBalanceMissing)).ToList();
        if (sinSaldo.Count > 0)
            avisos.Add(SettlementErrors.OpeningBalanceMissing(sinSaldo.Select(c => c.Loaded.Employee.PublicId).ToList(), sinSaldo.Select(c => c.Loaded.FullName).ToList()));
        foreach (var c in calculados.Where(c => !c.Result.Excluded))
            foreach (var w in c.Result.Warnings)
                avisos.Add(new WarningDto("Payroll.Settlement.Warning", $"{c.Loaded.FullName}: {w}", new { employeePublicId = c.Loaded.Employee.PublicId }));
        return avisos;
    }
}
