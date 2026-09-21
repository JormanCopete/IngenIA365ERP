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
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Payroll.Settlements.ServiceBonus;

/// <summary>
/// Liquida la prima de servicios de un semestre (feature 010, US1; contracts/api.md §3.1
/// <c>POST /service-bonus</c>): una corrida <c>ServiceBonus</c> por empresa, año y semestre
/// (D-02), con corte el 30-06 o el 31-12. Carga los insumos por <see cref="SettlementInputLoader"/>,
/// corre el motor puro por empleado y persiste el borrador con <see cref="SettlementRunPersister"/>.
/// Quien no tiene derecho queda en <c>excluded</c> con su razón (<c>SalarioIntegral</c>,
/// <c>AprendizLectiva</c>, <c>Pasante</c>, <c>YaPagadaEnDefinitiva</c>, <c>SinDiasEnElSemestre</c>);
/// el saldo inicial ausente es aviso, no bloqueo. Una segunda prima del mismo semestre en
/// borrador o aprobada se rechaza (<c>Payroll.Settlement.Duplicate</c>, FR-005).
/// </summary>
public sealed record CalculateServiceBonusCommand(int Year, int Semester, IReadOnlyList<Guid>? EmployeePublicIds = null)
    : IRequest<Result<SettlementCalculatedDto>>;

public sealed class CalculateServiceBonusCommandValidator : AbstractValidator<CalculateServiceBonusCommand>
{
    public CalculateServiceBonusCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100).WithMessage("El año de la prima no es válido.");
        RuleFor(x => x.Semester).InclusiveBetween(1, 2).WithMessage("El semestre es 1 (enero–junio) o 2 (julio–diciembre).");
        RuleForEach(x => x.EmployeePublicIds).NotEmpty().WithMessage("Un empleado indicado no tiene identificador.");
    }
}

public sealed class CalculateServiceBonusCommandHandler(
    IApplicationDbContext db,
    SettlementInputLoader loader,
    SettlementRunPersister persister,
    IDistributedLock distributedLock,
    ICurrentUserService user,
    PayrollAuditEmitter audit,
    ILogger<CalculateServiceBonusCommandHandler> logger)
    : IRequestHandler<CalculateServiceBonusCommand, Result<SettlementCalculatedDto>>
{
    public Task<Result<SettlementCalculatedDto>> Handle(CalculateServiceBonusCommand request, CancellationToken ct) =>
        new LiquidacionDePrima(db, loader, persister, distributedLock, user, audit, logger)
            .EjecutarAsync(request.Year, request.Semester, request.EmployeePublicIds, recalculo: false, ct);
}

/// <summary>
/// Recalcula la prima de un semestre (contracts/api.md §3.1 <c>POST /{runId}/recalculate</c>):
/// versión N+1 sobre la misma llave, el borrador anterior queda <c>Superseded</c>. Una aprobada
/// no se recalcula: se reversa (<c>Payroll.Settlement.Duplicate</c>).
/// </summary>
public sealed record RecalculateServiceBonusCommand(Guid RunPublicId, IReadOnlyList<Guid>? EmployeePublicIds = null)
    : IRequest<Result<SettlementCalculatedDto>>;

public sealed class RecalculateServiceBonusCommandValidator : AbstractValidator<RecalculateServiceBonusCommand>
{
    public RecalculateServiceBonusCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty().WithMessage("La corrida es obligatoria.");
        RuleForEach(x => x.EmployeePublicIds).NotEmpty().WithMessage("Un empleado indicado no tiene identificador.");
    }
}

public sealed class RecalculateServiceBonusCommandHandler(
    IApplicationDbContext db,
    SettlementInputLoader loader,
    SettlementRunPersister persister,
    IDistributedLock distributedLock,
    ICurrentUserService user,
    PayrollAuditEmitter audit,
    ILogger<RecalculateServiceBonusCommandHandler> logger)
    : IRequestHandler<RecalculateServiceBonusCommand, Result<SettlementCalculatedDto>>
{
    public async Task<Result<SettlementCalculatedDto>> Handle(RecalculateServiceBonusCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<SettlementCalculatedDto>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.ServiceBonus) return Result.Failure<SettlementCalculatedDto>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.ServiceBonus));
        if (run.Year is not { } year || run.Semester is not { } semester)
            return Result.Failure<SettlementCalculatedDto>(new Error("Payroll.Settlement.KeyMissing", "La corrida de prima no tiene año y semestre: no se puede recalcular."));

        return await new LiquidacionDePrima(db, loader, persister, distributedLock, user, audit, logger)
            .EjecutarAsync(year, semester, request.EmployeePublicIds, recalculo: true, ct);
    }
}

/// <summary>
/// El cálculo en sí, compartido por calcular y recalcular: un solo cálculo a la vez por semestre
/// (lock), insumos, motor puro, borrador; todo en UN <c>SaveChanges</c>, o nada. No es un servicio
/// registrado: cada handler lo arma con sus dependencias.
/// </summary>
internal sealed class LiquidacionDePrima(
    IApplicationDbContext db,
    SettlementInputLoader loader,
    SettlementRunPersister persister,
    IDistributedLock distributedLock,
    ICurrentUserService user,
    PayrollAuditEmitter audit,
    ILogger logger)
{
    public static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(5);

    public async Task<Result<SettlementCalculatedDto>> EjecutarAsync(int year, int semester, IReadOnlyList<Guid>? employeePublicIds, bool recalculo, CancellationToken ct)
    {
        if (semester is not (1 or 2)) return Result.Failure<SettlementCalculatedDto>(SettlementErrors.SemesterInvalid);
        var key = SettlementRunKey.Prima(year, semester);

        var lockKey = $"payroll:settlement:service-bonus:{user.TenantId ?? "-"}:{year}-{semester}";
        await using var handle = await distributedLock.TryAcquireAsync(lockKey, LockTtl, ct);
        if (handle is null)
            return Result.Failure<SettlementCalculatedDto>(new Error("Payroll.RunInProgress",
                $"Ya hay un cálculo en curso de {key.Descripcion}. Espere a que termine y refresque."));

        // FR-005: la anterior en borrador se reemplaza sólo al recalcular; la aprobada, nunca.
        var anteriores = await persister.CorridasDeAsync(key, ct);
        if (SettlementRunPersister.Duplicado(anteriores, key, recalculo) is { } duplicado)
            return Result.Failure<SettlementCalculatedDto>(duplicado);

        IReadOnlyList<int>? ids = null;
        if (employeePublicIds is { Count: > 0 })
        {
            var pedidos = employeePublicIds.Distinct().ToList();
            var encontrados = await db.Employees.AsNoTracking().Where(e => pedidos.Contains(e.PublicId)).Select(e => e.Id).ToListAsync(ct);
            if (encontrados.Count != pedidos.Count) return Result.Failure<SettlementCalculatedDto>(SettlementErrors.EmployeeNotFound);
            ids = encontrados;
        }

        var batch = await loader.LoadAsync(SettlementLoadRequest.Prima(year, semester, ids), ct);
        var faltantes = batch.MissingRequiredParameters;
        if (faltantes.Count > 0)
            return Result.Failure<SettlementCalculatedDto>(SettlementErrors.ParametersMissing(faltantes, key.CutoffDate.ToDateTime(TimeOnly.MinValue)));

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

        var bloqueos = SettlementRunPersister.Bloqueos(calculados);
        var avisos = Avisos(batch, calculados);

        logger.LogInformation("Prima de servicios {Year}-{Semester} v{Version}: {Employees} empleados, {Excluded} excluidos, {Blockers} bloqueos, hash {Hash}",
            year, semester, run.Version, run.EmployeeCount, excluidos.Count, bloqueos.Count, run.InputsHash);

        await audit.EmitAsync(AuditEventTypes.PayrollSettlementCalculated, nameof(PayrollRun), run.PublicId, null, new
        {
            kind = run.Kind.ToString(), year, semester, cutoffDate = run.CutoffDate, version = run.Version, recalculo,
            employees = run.EmployeeCount, excluded = excluidos.Count, blockers = bloqueos.Count, totalNet = run.TotalNet, inputsHash = run.InputsHash,
        }, ct);

        return Result.Success(new SettlementCalculatedDto(
            run.PublicId, run.Version, run.Kind.ToString(), key.CutoffDate, run.EmployeeCount,
            new RunTotalsDto(run.TotalEarnings, run.TotalDeductions, run.TotalEmployerContributions, run.TotalProvisions, run.TotalNet, run.RoundingAdjustment),
            bloqueos, excluidos, avisos));
    }

    /// <summary>Los avisos que no bloquean: el saldo inicial ausente agrupado por código (contracts/api.md §3.5) y lo que trajo el cargador.</summary>
    internal static IReadOnlyList<WarningDto> Avisos(SettlementBatch batch, IReadOnlyList<SettlementCalculatedEmployee> calculados)
    {
        var avisos = new List<WarningDto>(batch.Warnings);
        var sinSaldo = calculados.Where(c => !c.Result.Excluded && c.Result.Flags.HasFlag(SettlementFlags.OpeningBalanceMissing)).ToList();
        if (sinSaldo.Count > 0)
            avisos.Add(SettlementErrors.OpeningBalanceMissing(sinSaldo.Select(c => c.Loaded.Employee.PublicId).ToList(), sinSaldo.Select(c => c.Loaded.FullName).ToList()));
        return avisos;
    }
}
