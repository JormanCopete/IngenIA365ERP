using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;
using IngenIA365ERP.Domain.Payroll.Settlements;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Common;

/// <summary>
/// La llave de unicidad de una liquidación especial (data-model §1.1, FR-005): prima por año y
/// semestre; cesantías por año; definitiva por empleado y corte; vacaciones por <b>movimiento</b>
/// (D-03 y D-32: una corrida por empleado y movimiento; el corte de un disfrute futuro es «hoy»,
/// así que dos disfrutes o un disfrute y una compensación registrados el mismo día comparten corte
/// y no son duplicados). Es lo que los índices únicos filtrados de <c>PAY_PayrollRuns</c> protegen;
/// el comando la comprueba antes.
/// </summary>
public sealed record SettlementRunKey(PayrollRunKind Kind, DateOnly CutoffDate, short? Year = null, byte? Semester = null, int? EmployeeId = null, int? VacationMovementId = null)
{
    public static SettlementRunKey Prima(int year, int semester) =>
        new(PayrollRunKind.ServiceBonus, semester == 1 ? new DateOnly(year, 6, 30) : new DateOnly(year, 12, 31), (short)year, (byte)semester);

    public static SettlementRunKey Cesantias(int year, DateOnly? cutoff = null) =>
        new(PayrollRunKind.Severance, cutoff ?? new DateOnly(year, 12, 31), (short)year);

    /// <summary>Vacaciones: la llave es el movimiento. Nulo = el movimiento aún no se guardó (registrar), y no puede tener corridas.</summary>
    public static SettlementRunKey Vacaciones(int employeeId, DateOnly cutoff, int? vacationMovementId) =>
        new(PayrollRunKind.Vacation, cutoff, EmployeeId: employeeId, VacationMovementId: vacationMovementId);

    public static SettlementRunKey Definitiva(int employeeId, DateOnly terminationDate) =>
        new(PayrollRunKind.Settlement, terminationDate, EmployeeId: employeeId);

    public SettlementKind SettlementKind => (SettlementKind)(int)Kind;

    /// <summary>Etiqueta para mensajes: «la prima de servicios 2026-II», «la liquidación definitiva del 15/11/2026».</summary>
    public string Descripcion => Kind switch
    {
        PayrollRunKind.ServiceBonus => $"la prima de servicios {Year}-{(Semester == 1 ? "I" : "II")}",
        PayrollRunKind.Severance => $"la liquidación de cesantías e intereses {Year}",
        PayrollRunKind.Vacation => $"la liquidación de vacaciones de este movimiento (corte {CutoffDate:dd/MM/yyyy})",
        PayrollRunKind.Settlement => $"la liquidación definitiva del {CutoffDate:dd/MM/yyyy}",
        _ => SettlementErrors.Nombre(Kind),
    };
}

/// <summary>Un empleado calculado por el motor, listo para volverse fila de corrida.</summary>
public sealed record SettlementCalculatedEmployee(LoadedSettlementEmployee Loaded, SettlementResult Result);

/// <summary>
/// Persiste una liquidación especial con el molde de <c>CalculatePayrollRunCommand</c> (feature 010,
/// T028, R2): una corrida NUEVA en <c>Draft</c> con su <c>Kind</c>, versión siguiente, fecha de
/// corte, año/semestre o empleado según el tipo, los empleados con sus tramos, bases
/// (<c>BasesJson</c>), notas (<c>NotesJson</c>: negativas, omisiones y avisos) y banderas, y las
/// líneas con su explicación; el borrador anterior de la misma llave queda <c>Superseded</c>.
/// No guarda: el comando que la usa agrega lo suyo (terminación, movimiento, descuentos) y hace
/// un solo <c>SaveChanges</c>, o nada (Principio XI).
/// </summary>
public sealed class SettlementRunPersister(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
{
    /// <summary>Todas las versiones de la llave, de la más reciente a la más vieja. Un movimiento de vacaciones sin guardar no tiene ninguna.</summary>
    public Task<List<PayrollRun>> CorridasDeAsync(SettlementRunKey key, CancellationToken ct) =>
        key.Kind == PayrollRunKind.Vacation && key.VacationMovementId is null
            ? Task.FromResult(new List<PayrollRun>())
            : db.PayrollRuns
                .Where(r => r.Kind == key.Kind
                            && (key.Kind != PayrollRunKind.ServiceBonus || (r.Year == key.Year && r.Semester == key.Semester))
                            && (key.Kind != PayrollRunKind.Severance || r.Year == key.Year)
                            && (key.Kind != PayrollRunKind.Vacation || r.VacationMovementId == key.VacationMovementId)
                            && (key.Kind != PayrollRunKind.Settlement || (r.EmployeeId == key.EmployeeId && r.CutoffDate == key.CutoffDate)))
                .OrderByDescending(r => r.Version)
                .ToListAsync(ct);

    /// <summary>
    /// FR-005: una segunda liquidación del mismo tipo, período y empleado se rechaza mientras la
    /// anterior esté en borrador o aprobada; sólo tras reversarla (o descartar el borrador) se
    /// admite otra. Recalcular es distinto: reemplaza el borrador vigente (la aprobada nunca).
    /// </summary>
    public static Error? Duplicado(IReadOnlyList<PayrollRun> corridas, SettlementRunKey key, bool recalculo)
    {
        var aprobada = corridas.FirstOrDefault(r => r.Status == PayrollRunStatus.Approved);
        if (aprobada is not null) return SettlementErrors.Duplicate(aprobada.PublicId, aprobada.Status, key.Descripcion);
        var borrador = corridas.FirstOrDefault(r => r.IsEditableDraft);
        if (borrador is not null && !recalculo) return SettlementErrors.Duplicate(borrador.PublicId, borrador.Status, key.Descripcion);
        return null;
    }

    /// <summary>
    /// Arma la corrida y la agrega al contexto. <paramref name="corridasAnteriores"/> son las de la
    /// misma llave (<see cref="CorridasDeAsync"/>): dan la versión siguiente y el borrador que se reemplaza.
    /// </summary>
    public PayrollRun CrearBorrador(
        SettlementBatch batch,
        SettlementRunKey key,
        IReadOnlyList<SettlementCalculatedEmployee> calculados,
        IReadOnlyList<PayrollRun> corridasAnteriores,
        int? terminationId = null,
        int? vacationMovementId = null)
    {
        var ahora = clock.UtcNow;
        var usuario = user.UserName ?? "sistema";
        var version = (corridasAnteriores.Count == 0 ? 0 : corridasAnteriores.Max(r => r.Version)) + 1;

        var run = new PayrollRun
        {
            Kind = key.Kind,
            PayPeriodId = null,
            Version = version,
            CutoffDate = key.CutoffDate,
            Year = key.Year,
            Semester = key.Semester,
            EmployeeId = key.EmployeeId,
            TerminationId = terminationId,
            VacationMovementId = vacationMovementId ?? key.VacationMovementId,
            Status = PayrollRunStatus.Draft,
            CalculatedAt = ahora,
            CalculatedBy = usuario,
            CreatedAt = ahora,
            CreatedBy = usuario,
        };
        if (!run.EsCoherente) throw new InvalidOperationException("Una liquidación especial lleva corte y no período.");

        var hashes = new List<string>();
        decimal devengos = 0m, deducciones = 0m, aportes = 0m, provisiones = 0m, neto = 0m;
        var inicioVentana = (batch.PeriodStart ?? key.CutoffDate).ToDateTime(TimeOnly.MinValue);

        foreach (var (cargado, resultado) in calculados.Select(c => (c.Loaded, c.Result)))
        {
            hashes.Add(resultado.InputsHash);
            if (resultado.Excluded) continue;

            var tramos = Tramos(cargado.Input, inicioVentana, key.CutoffDate.ToDateTime(TimeOnly.MinValue));
            var runEmployee = new PayrollRunEmployee
            {
                EmployeeId = cargado.Employee.Id,
                PayrollPlanId = cargado.Employee.PayrollPlanId,
                DaysWorked = DiasDe(key.Kind, resultado),
                SalaryTranchesJson = JsonSerializer.Serialize(tramos, RunJson.Options),
                EmployeeClass = cargado.Input.Employee.Class,
                TotalEarnings = resultado.Totals.Earnings,
                TotalDeductions = resultado.Totals.Deductions,
                TotalEmployerContributions = resultado.Totals.EmployerContributions,
                TotalProvisions = resultado.Totals.Provisions,
                NetPay = NetoPagadero(key.Kind, resultado),
                Flags = (RunEmployeeFlag)(int)resultado.Flags,
                BasesJson = JsonSerializer.Serialize(resultado.BaseSteps, RunJson.Options),
                NotesJson = JsonSerializer.Serialize(new RunEmployeeNotes(resultado.Refusals, resultado.Skips.Select(s => s.Text).ToList(), resultado.Warnings), RunJson.Options),
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
                    NoveltyId = l.NoveltyPublicId is { } novedad && batch.NoveltyIds.TryGetValue(novedad, out var noveltyId) ? noveltyId : null,
                    ExplanationJson = JsonSerializer.Serialize(l.Explanation, RunJson.Options),
                    AffectsAccounting = l.AffectsAccounting,
                    Order = l.Order,
                    CreatedAt = ahora,
                    CreatedBy = usuario,
                });
            }

            devengos += resultado.Totals.Earnings;
            deducciones += resultado.Totals.Deductions;
            aportes += resultado.Totals.EmployerContributions;
            provisiones += resultado.Totals.Provisions;
            neto += runEmployee.NetPay;
            run.Employees.Add(runEmployee);
        }

        run.EmployeeCount = run.Employees.Count;
        run.TotalEarnings = devengos;
        run.TotalDeductions = deducciones;
        run.TotalEmployerContributions = aportes;
        run.TotalProvisions = provisiones;
        run.TotalNet = neto;
        run.RoundingAdjustment = 0m;
        run.InputsHash = InputsHasher.Combine(hashes);

        foreach (var previa in corridasAnteriores.Where(r => r.IsEditableDraft))
        {
            previa.Status = PayrollRunStatus.Superseded;
            previa.UpdatedAt = ahora;
            previa.UpdatedBy = usuario;
        }

        db.PayrollRuns.Add(run);
        return run;
    }

    /// <summary>Los bloqueos por empleado como los muestra la ordinaria; el saldo inicial ausente es aviso, no bloqueo.</summary>
    public static IReadOnlyList<RunBlockerDto> Bloqueos(IReadOnlyList<SettlementCalculatedEmployee> calculados) =>
        calculados.Where(c => !c.Result.Excluded)
            .SelectMany(c => RunJson.FlagNames(SinAvisos((RunEmployeeFlag)(int)c.Result.Flags))
                .Select(flag => new RunBlockerDto(c.Loaded.Employee.PublicId, c.Loaded.FullName, flag,
                    c.Result.Refusals.FirstOrDefault() ?? RunJson.FlagLabel(Enum.Parse<RunEmployeeFlag>(flag)))))
            .ToList();

    /// <summary>Las banderas que sí bloquean la aprobación: todas menos el saldo inicial ausente, que es un aviso (contracts/api.md §3.5).</summary>
    public static RunEmployeeFlag SinAvisos(RunEmployeeFlag flags) => flags & ~RunEmployeeFlag.OpeningBalanceMissing;

    public static IReadOnlyList<ExcludedEmployeeDto> Excluidos(IReadOnlyList<SettlementCalculatedEmployee> calculados) =>
        calculados.Where(c => c.Result.Excluded)
            .Select(c => new ExcludedEmployeeDto(c.Loaded.Employee.PublicId, c.Loaded.FullName, c.Result.ExclusionReasonCode!, c.Result.ExclusionReason ?? c.Result.ExclusionReasonCode!))
            .ToList();

    /// <summary>
    /// Lo que se le <b>paga al empleado</b> (<c>NetPay</c>, <c>TotalNet</c>): el neto del motor, salvo en las
    /// cesantías anuales, donde las cesantías van al fondo (FR-011; el tercero de la CxP lo decide
    /// <c>SettlementAccountingPoster.TerceroPara</c>) y al empleado sólo le llegan los intereses menos la
    /// retención. La relación de pago, la marca de pago, el comprobante y la dispersión leen este valor;
    /// hasta la revisión N1 (2026-09-21) incluía las cesantías del fondo y «pagado» dejaba al empleado
    /// cobrado por un valor que nunca recibió. Los devengos (<c>TotalEarnings</c>) sí las incluyen: son suyas.
    /// </summary>
    public static decimal NetoPagadero(PayrollRunKind kind, SettlementResult r) =>
        kind == PayrollRunKind.Severance
            ? r.Totals.Net - r.Lines.Where(l => l.Nature == ConceptNature.Earning && l.Code.Equals(WellKnownConceptCodes.Severance, StringComparison.OrdinalIgnoreCase)).Sum(l => l.Amount)
            : r.Totals.Net;

    private static int DiasDe(PayrollRunKind kind, SettlementResult r)
    {
        var codigo = kind switch
        {
            PayrollRunKind.ServiceBonus => WellKnownConceptCodes.ServiceBonus,
            PayrollRunKind.Severance => WellKnownConceptCodes.Severance,
            PayrollRunKind.Vacation => WellKnownConceptCodes.VacationPayout,
            _ => WellKnownConceptCodes.PendingSalary,
        };
        var linea = r.Lines.FirstOrDefault(l => l.Code.Equals(codigo, StringComparison.OrdinalIgnoreCase))
                    ?? (kind == PayrollRunKind.Vacation ? r.Lines.FirstOrDefault(l => l.Code.Equals(WellKnownConceptCodes.VacationCompensation, StringComparison.OrdinalIgnoreCase)) : null);
        return (int)Math.Round(linea?.Quantity ?? 0m, MidpointRounding.AwayFromZero);
    }

    private static List<SalaryTrancheDto> Tramos(SettlementInput input, DateTime desde, DateTime hasta)
    {
        var inicio = input.Employee.JoinDate.Date > desde ? input.Employee.JoinDate.Date : desde;
        if (hasta < inicio) return [];
        var period = new PeriodInput(inicio, hasta, PayrollPeriodicity.Monthly);
        var employee = new EmployeeInput
        {
            PublicId = input.Employee.PublicId,
            DisplayName = input.Employee.DisplayName,
            Class = input.Employee.Class,
            JoinDate = input.Employee.JoinDate,
            TerminationDate = input.Employee.TerminationDate,
            SalaryHistory = input.Employee.SalaryHistory,
        };
        return SalaryTranches.Build(period, employee, input.Absences.Select(a => (a.From.Date, a.To.Date)).ToList())
            .Select(t => new SalaryTrancheDto(t.From, t.To, t.Days, t.AbsenceDays, t.MonthlySalary, t.PaidDays))
            .ToList();
    }
}
