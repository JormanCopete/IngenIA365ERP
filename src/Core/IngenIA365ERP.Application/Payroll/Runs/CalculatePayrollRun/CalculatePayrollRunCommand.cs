using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Caching;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;

public sealed record CalculateRunResultDto(
    Guid RunPublicId,
    int Version,
    int EmployeeCount,
    int ChangedEmployees,
    RunTotalsDto Totals,
    IReadOnlyList<RunBlockerDto> Blockers,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Calcula el período en borrador (FR-007, FR-008, FR-011, FR-014, FR-015; D-05, D-06):
/// un solo cálculo a la vez por período (lock Redis), carga de insumos, motor puro por
/// empleado, y una corrida NUEVA con versión siguiente; la corrida en borrador anterior
/// queda <c>Superseded</c>. Todo en un solo <c>SaveChanges</c>: o queda completa o no
/// deja rastro. Si falta un parámetro legal requerido, se niega antes de tocar a nadie.
/// </summary>
public sealed record CalculatePayrollRunCommand(Guid PeriodPublicId) : IRequest<Result<CalculateRunResultDto>>;

public sealed class CalculatePayrollRunCommandValidator : AbstractValidator<CalculatePayrollRunCommand>
{
    public CalculatePayrollRunCommandValidator()
    {
        RuleFor(x => x.PeriodPublicId).NotEmpty().WithMessage("El período es obligatorio.");
    }
}

public sealed class CalculatePayrollRunCommandHandler(
    IApplicationDbContext db,
    CalculationInputLoader loader,
    IDistributedLock distributedLock,
    IDateTimeService clock,
    ICurrentUserService user,
    ILogger<CalculatePayrollRunCommandHandler> logger)
    : IRequestHandler<CalculatePayrollRunCommand, Result<CalculateRunResultDto>>
{
    public static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(5);

    public async Task<Result<CalculateRunResultDto>> Handle(CalculatePayrollRunCommand request, CancellationToken ct)
    {
        var period = await db.PayPeriods.Include(p => p.PayrollPlan).FirstOrDefaultAsync(p => p.PublicId == request.PeriodPublicId, ct);
        if (period is null)
            return Result.Failure<CalculateRunResultDto>(new Error("Payroll.PeriodNotFound", "No existe el período de pago indicado."));
        if (period.Status == PayPeriodStatus.Approved)
            return Result.Failure<CalculateRunResultDto>(new Error("Payroll.PeriodApproved",
                "El período está aprobado: sólo la reversión controlada permite volver a calcularlo."));
        if (period.Status is not (PayPeriodStatus.Open or PayPeriodStatus.Calculated))
            return Result.Failure<CalculateRunResultDto>(new Error("Payroll.PeriodNotOpen", $"El período está en estado {period.Status}."));

        var lockKey = $"payroll:run:{user.TenantId ?? "-"}:{period.PublicId}";
        await using var handle = await distributedLock.TryAcquireAsync(lockKey, LockTtl, ct);
        if (handle is null)
            return Result.Failure<CalculateRunResultDto>(new Error("Payroll.RunInProgress",
                "Ya hay un cálculo en curso para este período. Espere a que termine y refresque."));

        var batch = await loader.LoadAsync(period, ct);
        var faltantes = batch.MissingRequiredParameters;
        if (faltantes.Count > 0)
            return Result.Failure<CalculateRunResultDto>(new Error("Payroll.LegalParameterMissing",
                $"No hay vigencia al {period.EndDate:dd/MM/yyyy} para los parámetros legales: {string.Join(", ", faltantes)}. " +
                "Regístrelos en Nómina › Parámetros legales antes de calcular."));

        var ahora = clock.UtcNow;
        var usuario = user.UserName ?? "sistema";
        var engine = new PayrollCalculationEngine();

        // Corrida anterior: la versión siguiente y, si estaba en borrador, queda reemplazada.
        var corridas = await db.PayrollRuns.Where(r => r.PayPeriodId == period.Id).OrderByDescending(r => r.Version).ToListAsync(ct);
        var anterior = corridas.FirstOrDefault();
        var version = (anterior?.Version ?? 0) + 1;
        var lineasAnteriores = await LineasDeComparacionAsync(anterior, ct);

        var run = new PayrollRun
        {
            PayPeriodId = period.Id,
            Version = version,
            Status = PayrollRunStatus.Draft,
            CalculatedAt = ahora,
            CalculatedBy = usuario,
            CreatedAt = ahora,
            CreatedBy = usuario,
        };

        var hashes = new List<string>();
        var bloqueos = new List<RunBlockerDto>();
        var avisos = new List<string>();
        var cambiados = 0;
        decimal devengos = 0m, deducciones = 0m, aportes = 0m, provisiones = 0m, neto = 0m, ajuste = 0m;

        foreach (var e in batch.Employees)
        {
            CalculationResult resultado;
            try
            {
                resultado = engine.Calculate(batch.InputFor(e));
            }
            catch (CalculationRefusedException ex)
            {
                return Result.Failure<CalculateRunResultDto>(new Error("Payroll.CalculationRefused",
                    $"El motor se negó a liquidar a {e.FullName}: {ex.Message}"));
            }
            hashes.Add(resultado.InputsHash);

            if (resultado.DaysLinked == 0)
            {
                avisos.Add($"{e.FullName}: sin días vinculados en el período, no se liquidó.");
                continue;
            }

            var runEmployee = new PayrollRunEmployee
            {
                EmployeeId = e.Employee.Id,
                PayrollPlanId = batch.Plan.Id,
                DaysWorked = resultado.Tranches.Sum(t => t.PaidDays),
                SalaryTranchesJson = JsonSerializer.Serialize(
                    resultado.Tranches.Select(t => new SalaryTrancheDto(t.From, t.To, t.Days, t.AbsenceDays, t.MonthlySalary, t.PaidDays)), RunJson.Options),
                EmployeeClass = e.Input.Class,
                TotalEarnings = resultado.Totals.Earnings,
                TotalDeductions = resultado.Totals.Deductions,
                TotalEmployerContributions = resultado.Totals.EmployerContributions,
                TotalProvisions = resultado.Totals.Provisions,
                NetPay = resultado.Totals.Net,
                Flags = resultado.Flags,
                BasesJson = JsonSerializer.Serialize(resultado.BaseSteps, RunJson.Options),
                NotesJson = JsonSerializer.Serialize(new RunEmployeeNotes(resultado.Refusals, resultado.Skips), RunJson.Options),
                CreatedAt = ahora,
                CreatedBy = usuario,
            };

            foreach (var l in resultado.Lines)
            {
                runEmployee.Lines.Add(new PayrollRunLine
                {
                    ConceptDefinitionId = l.ConceptDefinitionId,
                    ConceptCode = l.Code,
                    ConceptName = l.Name,
                    Nature = l.Nature,
                    Quantity = l.Quantity,
                    BaseAmount = l.BaseAmount,
                    Factor = l.Factor,
                    RangeFrom = l.RangeFrom,
                    RangeTo = l.RangeTo,
                    Amount = l.Amount,
                    LegalParameterId = l.LegalParameterId,
                    NoveltyId = l.NoveltyPublicId is { } np && batch.NoveltyIds.TryGetValue(np, out var nid) ? nid : null,
                    ExplanationJson = JsonSerializer.Serialize(l.Explanation, RunJson.Options),
                    AffectsAccounting = l.AffectsAccounting,
                    Order = l.Order,
                    CreatedAt = ahora,
                    CreatedBy = usuario,
                });
            }

            runEmployee.ChangedFromPreviousRun = Cambio(lineasAnteriores, e.Employee.Id, runEmployee);
            if (runEmployee.ChangedFromPreviousRun) cambiados++;

            foreach (var flag in RunJson.FlagNames(resultado.Flags))
            {
                var detalle = resultado.Refusals.FirstOrDefault() ?? RunJson.FlagLabel(Enum.Parse<RunEmployeeFlag>(flag));
                bloqueos.Add(new RunBlockerDto(e.Employee.PublicId, e.FullName, flag, detalle));
            }
            foreach (var r in resultado.Refusals) avisos.Add($"{e.FullName}: {r}");

            devengos += resultado.Totals.Earnings;
            deducciones += resultado.Totals.Deductions;
            aportes += resultado.Totals.EmployerContributions;
            provisiones += resultado.Totals.Provisions;
            neto += resultado.Totals.Net;
            ajuste += resultado.Totals.RoundingAdjustment;
            run.Employees.Add(runEmployee);
        }

        if (batch.Employees.Count == 0)
            avisos.Insert(0, $"El plan «{batch.Plan.Name}» no tiene empleados vigentes en el período: el borrador queda vacío.");

        run.EmployeeCount = run.Employees.Count;
        run.TotalEarnings = devengos;
        run.TotalDeductions = deducciones;
        run.TotalEmployerContributions = aportes;
        run.TotalProvisions = provisiones;
        run.TotalNet = neto;
        run.RoundingAdjustment = ajuste;
        run.InputsHash = InputsHasher.Combine(hashes);

        foreach (var previa in corridas.Where(r => r.IsEditableDraft))
        {
            previa.Status = PayrollRunStatus.Superseded;
            previa.UpdatedAt = ahora;
            previa.UpdatedBy = usuario;
        }

        db.PayrollRuns.Add(run);
        period.Status = PayPeriodStatus.Calculated;
        period.RunPublicId = run.PublicId;
        period.StatusMessage = $"Borrador v{version} calculado por {usuario} el {ahora:dd/MM/yyyy HH:mm} UTC";
        period.UpdatedAt = ahora;
        period.UpdatedBy = usuario;

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Nómina calculada: período {Period} v{Version}, {Employees} empleados, {Blockers} bloqueos, hash {Hash}",
            period.PublicId, version, run.EmployeeCount, bloqueos.Count, run.InputsHash);

        return Result.Success(new CalculateRunResultDto(run.PublicId, version, run.EmployeeCount, cambiados,
            new RunTotalsDto(devengos, deducciones, aportes, provisiones, neto, ajuste), bloqueos, avisos));
    }

    private async Task<Dictionary<int, string>> LineasDeComparacionAsync(PayrollRun? anterior, CancellationToken ct)
    {
        if (anterior is null) return new Dictionary<int, string>();
        var filas = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join l in db.PayrollRunLines.AsNoTracking() on re.Id equals l.PayrollRunEmployeeId
            where re.PayrollRunId == anterior.Id
            select new { re.EmployeeId, l.ConceptCode, l.Amount }).ToListAsync(ct);
        return filas.GroupBy(f => f.EmployeeId)
            .ToDictionary(g => g.Key, g => Firma(g.Select(x => (x.ConceptCode, x.Amount))));
    }

    private static bool Cambio(Dictionary<int, string> anteriores, int employeeId, PayrollRunEmployee nuevo) =>
        !anteriores.TryGetValue(employeeId, out var firmaAnterior)
        || firmaAnterior != Firma(nuevo.Lines.Select(l => (l.ConceptCode, l.Amount)));

    private static string Firma(IEnumerable<(string Code, decimal Amount)> lineas) =>
        string.Join("|", lineas.OrderBy(l => l.Code, StringComparer.Ordinal).ThenBy(l => l.Amount)
            .Select(l => $"{l.Code}={l.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}"));
}
