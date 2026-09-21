using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Payroll.Vacations;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Settlements;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Vacation;

/// <summary>
/// Registra un disfrute o una compensación de vacaciones y calcula su liquidación en la misma
/// acción (feature 010, US4; contracts/api.md §3.3): el movimiento nace <c>Registered</c> y la
/// corrida <c>Vacation</c> en <c>Draft</c>, de un solo empleado (D-03), en UN <c>SaveChanges</c>.
///
/// <para>
/// El disfrute exige fechas; los hábiles los cuenta <c>DiasHabiles.Contar</c> con la semana
/// laboral vigente y los festivos de la cooperativa y quedan congelados en el movimiento con los
/// días saltados (FR-015). La compensación exige días y no puede llevar el total compensado por
/// encima del porcentaje parametrizado de lo causado (FR-016, <c>CompensationOverMax</c> con el
/// máximo que sí cabe). El saldo se comprueba antes de crear nada (<c>NoBalance</c>); un disfrute
/// por encima del saldo se admite con aviso (vacaciones anticipadas, práctica corriente), una
/// compensación no. Las fechas no pueden cruzarse con otro disfrute vivo (<c>Overlaps</c>) y el
/// empleado tiene que estar vinculado (<c>EmployeeTerminated</c>).
/// </para>
///
/// <para>
/// La fecha de corte es el día anterior al inicio del disfrute (data-model §1.1) —o la fecha de
/// pago de la compensación—, <b>nunca posterior a hoy</b>: el comprobante se fecha al corte (D-04,
/// D-21) y una liquidación pagada por anticipado se aprueba antes del descanso. Las novedades
/// que la aprobación dejará en cada período cubierto se proponen aquí (<c>novelties[]</c>); si
/// un período ya está aprobado, la respuesta es <c>PeriodApproved</c> con el período abierto
/// siguiente para el ajuste retroactivo, salvo que quien registra lo acepte
/// (<see cref="AcceptRetroactive"/>).
/// </para>
/// </summary>
public sealed record CalculateVacationCommand(
    Guid EmployeePublicId,
    VacationMovementKind Kind,
    DateOnly? From = null,
    DateOnly? To = null,
    decimal? CompensationDays = null,
    DateOnly? PaymentDate = null,
    string? Notes = null,
    bool AcceptRetroactive = false) : IRequest<Result<VacationCalculatedDto>>;

public sealed class CalculateVacationCommandValidator : AbstractValidator<CalculateVacationCommand>
{
    public CalculateVacationCommandValidator()
    {
        RuleFor(x => x.EmployeePublicId).NotEmpty().WithMessage("El empleado es obligatorio.");
        RuleFor(x => x.Kind).Must(k => k is VacationMovementKind.Enjoyment or VacationMovementKind.Compensation)
            .WithMessage("Se registra un disfrute (Enjoyment) o una compensación en dinero (Compensation); los ajustes van por su propia ruta.");
        RuleFor(x => x.From).NotNull().When(x => x.Kind == VacationMovementKind.Enjoyment).WithMessage("El disfrute exige fecha inicial.");
        RuleFor(x => x.To).NotNull().When(x => x.Kind == VacationMovementKind.Enjoyment).WithMessage("El disfrute exige fecha final.");
        RuleFor(x => x.CompensationDays).NotNull().GreaterThan(0m).When(x => x.Kind == VacationMovementKind.Compensation)
            .WithMessage("La compensación exige los días hábiles que se pagan en dinero, mayores que cero.");
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}

public sealed class CalculateVacationCommandHandler(
    IApplicationDbContext db,
    VacationBalanceCalculator saldos,
    VacationNoveltyPlanner planner,
    PayrollPolicyReader policies,
    SettlementInputLoader loader,
    SettlementRunPersister persister,
    IDateTimeService clock,
    ICurrentUserService user,
    PayrollAuditEmitter audit)
    : IRequestHandler<CalculateVacationCommand, Result<VacationCalculatedDto>>
{
    public async Task<Result<VacationCalculatedDto>> Handle(CalculateVacationCommand request, CancellationToken ct)
    {
        var empleado = await db.Employees.Include(e => e.Person).FirstOrDefaultAsync(e => e.PublicId == request.EmployeePublicId && !e.IsDeleted, ct);
        if (empleado is null) return Result.Failure<VacationCalculatedDto>(SettlementErrors.EmployeeNotFound);
        var hoy = clock.TodayUtc;
        if (await RetiradoAsync(empleado, hoy, ct)) return Result.Failure<VacationCalculatedDto>(SettlementErrors.VacationEmployeeTerminated);

        // --- el movimiento, con sus días congelados ---
        VacationMovement movimiento;
        DateOnly corte;
        IReadOnlyList<SkippedDayDto> saltados = [];
        var avisos = new List<WarningDto>();
        if (request.Kind == VacationMovementKind.Enjoyment)
        {
            var desde = request.From!.Value;
            var hasta = request.To!.Value;
            if (hasta < desde) return Result.Failure<VacationCalculatedDto>(SettlementErrors.VacationDatesInvalid);
            if (desde.ToDateTime(TimeOnly.MinValue) < empleado.JoinDate.Date)
                return Result.Failure<VacationCalculatedDto>(new ErrorConDatos("Payroll.Vacation.DatesInvalid",
                    $"El disfrute empieza el {desde:dd/MM/yyyy}, antes del ingreso del empleado ({empleado.JoinDate:dd/MM/yyyy}).", new { hireDate = empleado.JoinDate }));

            var cruce = await db.VacationMovements.AsNoTracking()
                .Where(m => m.EmployeeId == empleado.Id && m.Kind == VacationMovementKind.Enjoyment && m.Status != VacationMovementStatus.Cancelled
                            && m.StartDate <= hasta && m.EndDate >= desde)
                .Select(m => (Guid?)m.PublicId).FirstOrDefaultAsync(ct);
            if (cruce is { } otro) return Result.Failure<VacationCalculatedDto>(SettlementErrors.VacationOverlaps(otro));

            var conteo = await VacationQueriesSupport.ContarHabilesAsync(db, policies, desde, hasta, ct);
            if (conteo.WorkingDays == 0) return Result.Failure<VacationCalculatedDto>(SettlementErrors.VacationNoWorkingDays);
            saltados = conteo.Skipped;
            avisos.AddRange(conteo.Warnings); // un año sin festivos cargados (Payroll.Holiday.YearNotLoaded)

            var vispera = desde.AddDays(-1);
            corte = vispera < hoy ? vispera : hoy;
            movimiento = new VacationMovement
            {
                EmployeeId = empleado.Id,
                Kind = VacationMovementKind.Enjoyment,
                StartDate = desde,
                EndDate = hasta,
                BusinessDays = conteo.WorkingDays,
                CalendarDays = conteo.CalendarDays,
                WeekPolicyUsed = conteo.WorkWeek.ToString(),
                SkippedDaysJson = SkippedDayDto.Escribir(conteo.Skipped),
            };
        }
        else
        {
            var dias = request.CompensationDays!.Value;
            var pago = request.PaymentDate ?? hoy;
            corte = pago < hoy ? pago : hoy;
            movimiento = new VacationMovement
            {
                EmployeeId = empleado.Id,
                Kind = VacationMovementKind.Compensation,
                StartDate = pago,
                EndDate = null,
                BusinessDays = dias,
                CalendarDays = 0,
                WeekPolicyUsed = string.Empty,
                SkippedDaysJson = "[]",
            };
        }
        movimiento.Status = VacationMovementStatus.Registered;
        movimiento.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        movimiento.CreatedAt = clock.UtcNow;
        movimiento.CreatedBy = user.UserName;

        // --- saldo y tope antes de crear nada ---
        var saldo = await saldos.CalcularAsync(empleado, corte, ct);
        if (saldo.IsFailure) return Result.Failure<VacationCalculatedDto>(saldo.Error);
        var pendientes = saldo.Value.PendingDays;
        if (request.Kind == VacationMovementKind.Compensation)
        {
            if (pendientes <= 0m || movimiento.BusinessDays > pendientes)
                return Result.Failure<VacationCalculatedDto>(SettlementErrors.VacationNoBalance(pendientes));
            var maximo = await saldos.MaximoCompensableAsync(saldo.Value, ct);
            if (maximo.IsFailure) return Result.Failure<VacationCalculatedDto>(maximo.Error);
            var disponible = Math.Max(0m, maximo.Value.MaxDays - saldo.Value.CompensatedDays);
            if (movimiento.BusinessDays > disponible)
                return Result.Failure<VacationCalculatedDto>(SettlementErrors.VacationCompensationOverMax(movimiento.BusinessDays, disponible, saldo.Value.TotalAccrued));
        }
        else
        {
            if (pendientes <= 0m) return Result.Failure<VacationCalculatedDto>(SettlementErrors.VacationNoBalance(pendientes));
            if (movimiento.BusinessDays > pendientes)
                avisos.Add(new WarningDto("Payroll.Vacation.Anticipated",
                    $"El disfrute consume {movimiento.BusinessDays:0.##} días hábiles y el empleado tiene {pendientes:0.##} pendientes al {corte:dd/MM/yyyy}: quedan {pendientes - movimiento.BusinessDays:0.##} (vacaciones anticipadas).",
                    new { requestedDays = movimiento.BusinessDays, pendingDays = pendientes }));
        }

        // --- las novedades que la aprobación dejará (D-01) ---
        var plan = request.Kind == VacationMovementKind.Enjoyment
            ? await planner.PlanearAsync(empleado, movimiento.StartDate, movimiento.EndDate!.Value, corte, request.AcceptRetroactive, ct)
            : Result.Success(new PlanDeNovedades([], [], string.Empty));
        if (plan.IsFailure) return Result.Failure<VacationCalculatedDto>(plan.Error);
        avisos.AddRange(plan.Value.Warnings);

        // --- una sola liquidación viva por empleado y corte (FR-005) ---
        var key = SettlementRunKey.Vacaciones(empleado.Id, corte);
        var anteriores = await persister.CorridasDeAsync(key, ct);
        if (SettlementRunPersister.Duplicado(anteriores, key, recalculo: false) is { } duplicado)
            return Result.Failure<VacationCalculatedDto>(duplicado);

        // --- cargar, calcular, persistir: movimiento y corrida en el mismo SaveChanges ---
        var calculo = await VacationRunCalculator.CalcularAsync(loader, persister, empleado, movimiento, corte, key, anteriores, recalculo: false, ct);
        if (calculo.IsFailure) return Result.Failure<VacationCalculatedDto>(calculo.Error);
        var (run, batch, calculados) = calculo.Value;
        db.VacationMovements.Add(movimiento);
        run.VacationMovement = movimiento;
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollVacationRegistered, nameof(VacationMovement), movimiento.PublicId, null,
            new
            {
                kind = movimiento.Kind.ToString(), employeePublicId = empleado.PublicId, from = movimiento.StartDate, to = movimiento.EndDate,
                workingDays = movimiento.BusinessDays, calendarDays = movimiento.CalendarDays, weekPolicy = movimiento.WeekPolicyUsed, runPublicId = run.PublicId,
            }, ct);
        await audit.EmitAsync(AuditEventTypes.PayrollSettlementCalculated, nameof(PayrollRun), run.PublicId, null,
            new { kind = "Vacation", cutoffDate = corte, version = run.Version, employeePublicId = empleado.PublicId, movementPublicId = movimiento.PublicId, totalNet = run.TotalNet }, ct);

        avisos.AddRange(batch.Warnings);
        return Result.Success(VacationRunCalculator.Respuesta(run, movimiento, saltados, plan.Value, calculados, avisos));
    }

    /// <summary>Retirado: ficha cerrada o terminación viva (registrada o liquidada) con fecha no posterior a hoy.</summary>
    private async Task<bool> RetiradoAsync(Employee e, DateOnly hoy, CancellationToken ct)
    {
        if (e.Status < 0 || e.TerminationDate.Date <= hoy.ToDateTime(TimeOnly.MinValue)) return true;
        return await db.EmploymentTerminations.AsNoTracking()
            .AnyAsync(t => t.EmployeeId == e.Id && (t.Status == TerminationStatus.Registered || t.Status == TerminationStatus.Settled), ct);
    }
}

/// <summary>Lo que registrar y recalcular comparten: cargar por el cargador, calcular con el motor puro y armar el borrador con el molde común.</summary>
public static class VacationRunCalculator
{
    public static async Task<Result<(PayrollRun Run, SettlementBatch Batch, IReadOnlyList<SettlementCalculatedEmployee> Calculados)>> CalcularAsync(
        SettlementInputLoader loader, SettlementRunPersister persister, Employee empleado, VacationMovement movimiento, DateOnly corte,
        SettlementRunKey key, IReadOnlyList<PayrollRun> anteriores, bool recalculo, CancellationToken ct)
    {
        var batch = await loader.LoadAsync(SettlementLoadRequest.Vacaciones(empleado.Id, movimiento, corte), ct);
        var faltantes = batch.MissingRequiredParameters;
        if (faltantes.Count > 0)
            return Result.Failure<(PayrollRun, SettlementBatch, IReadOnlyList<SettlementCalculatedEmployee>)>(SettlementErrors.ParametersMissing(faltantes, corte.ToDateTime(TimeOnly.MinValue)));

        var engine = new SettlementCalculationEngine();
        var calculados = new List<SettlementCalculatedEmployee>();
        foreach (var e in batch.Employees)
        {
            try
            {
                calculados.Add(new SettlementCalculatedEmployee(e, engine.Calculate(e.Input)));
            }
            catch (CalculationRefusedException ex)
            {
                return Result.Failure<(PayrollRun, SettlementBatch, IReadOnlyList<SettlementCalculatedEmployee>)>(SettlementErrors.CalculationRefused(e.FullName, ex.Message));
            }
        }
        if (calculados.Count == 0)
            return Result.Failure<(PayrollRun, SettlementBatch, IReadOnlyList<SettlementCalculatedEmployee>)>(SettlementErrors.EmployeeNotFound);

        var run = persister.CrearBorrador(batch, key, calculados, anteriores, vacationMovementId: recalculo ? movimiento.Id : null);
        return Result.Success<(PayrollRun, SettlementBatch, IReadOnlyList<SettlementCalculatedEmployee>)>((run, batch, calculados));
    }

    public static VacationCalculatedDto Respuesta(PayrollRun run, VacationMovement movimiento, IReadOnlyList<SkippedDayDto> saltados, PlanDeNovedades plan,
        IReadOnlyList<SettlementCalculatedEmployee> calculados, List<WarningDto> avisos)
    {
        var resultado = calculados[0].Result;
        var codigo = movimiento.Kind == VacationMovementKind.Compensation ? WellKnownConceptCodes.VacationCompensation : WellKnownConceptCodes.VacationPayout;
        var valor = resultado.Lines.Where(l => l.Code.Equals(codigo, StringComparison.OrdinalIgnoreCase)).Sum(l => l.Amount);
        foreach (var r in resultado.Refusals) avisos.Add(new WarningDto("Payroll.Settlement.CalculationRefused", r, null));
        foreach (var w in resultado.Warnings) avisos.Add(new WarningDto("Payroll.Vacation.Warning", w, null));
        return new VacationCalculatedDto(
            run.PublicId, movimiento.PublicId, run.Version, movimiento.Kind, run.CutoffDate!.Value, movimiento.StartDate,
            movimiento.Kind == VacationMovementKind.Enjoyment ? movimiento.EndDate : null,
            movimiento.BusinessDays, movimiento.CalendarDays, saltados, valor,
            new RunTotalsDto(run.TotalEarnings, run.TotalDeductions, run.TotalEmployerContributions, run.TotalProvisions, run.TotalNet, run.RoundingAdjustment),
            plan.ComoDto(), SettlementRunPersister.Bloqueos(calculados), SettlementRunPersister.Excluidos(calculados), avisos);
    }
}
