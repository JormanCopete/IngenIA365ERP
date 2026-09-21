using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.OpeningBalances;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;
using IngenIA365ERP.Domain.Payroll.Settlements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>Qué liquidación cargar y para quién.</summary>
public sealed record SettlementLoadRequest(
    SettlementKind Kind,
    DateOnly CutoffDate,
    DateOnly? PeriodStart = null,
    IReadOnlyList<int>? EmployeeIds = null,
    int? TerminationId = null,
    int? VacationMovementId = null,
    VacationMovement? Movement = null)
{
    public static SettlementLoadRequest Prima(int year, int semester, IReadOnlyList<int>? employeeIds = null) =>
        new(SettlementKind.ServiceBonus, semester == 1 ? new DateOnly(year, 6, 30) : new DateOnly(year, 12, 31),
            semester == 1 ? new DateOnly(year, 1, 1) : new DateOnly(year, 7, 1), employeeIds);

    public static SettlementLoadRequest Cesantias(int year, DateOnly? cutoff = null, IReadOnlyList<int>? employeeIds = null) =>
        new(SettlementKind.Severance, cutoff ?? new DateOnly(year, 12, 31), new DateOnly(year, 1, 1), employeeIds);

    public static SettlementLoadRequest Vacaciones(int employeeId, int vacationMovementId, DateOnly cutoff) =>
        new(SettlementKind.Vacation, cutoff, null, [employeeId], VacationMovementId: vacationMovementId);

    /// <summary>
    /// US4: el movimiento recién armado, todavía sin guardar (registrar el disfrute crea el
    /// movimiento y la corrida en UN <c>SaveChanges</c>, así que al cargar aún no tiene Id).
    /// </summary>
    public static SettlementLoadRequest Vacaciones(int employeeId, VacationMovement movimiento, DateOnly cutoff) =>
        new(SettlementKind.Vacation, cutoff, null, [employeeId], VacationMovementId: movimiento.Id == 0 ? null : movimiento.Id, Movement: movimiento);

    public static SettlementLoadRequest Definitiva(int employeeId, int terminationId, DateOnly terminationDate) =>
        new(SettlementKind.Settlement, terminationDate, null, [employeeId], TerminationId: terminationId);
}

/// <summary>Una deuda propuesta desde Cartera o desde una libranza (FR-018a): lo que se persiste como <c>PAY_SettlementDeductions</c> y lo que el motor recibe ya validado.</summary>
public sealed record DeudaPropuesta(
    SettlementDeductionKind Kind,
    int? LoanPortfolioId,
    int? RecurringNoveltyId,
    string Description,
    decimal Proposed,
    decimal Applied,
    string BreakdownJson,
    string ConceptCode,
    bool AccountedByOtherModule,
    Guid? DeductionPublicId,
    string? AdjustmentReason)
{
    public ProposedDeductionInput ComoEntrada() => new()
    {
        PublicId = DeductionPublicId,
        ConceptCode = ConceptCode,
        Description = Description,
        ProposedAmount = Proposed,
        AppliedAmount = Applied,
        AccountedByOtherModule = AccountedByOtherModule,
    };
}

/// <summary>Un empleado cargado para liquidar: la ficha, su nombre para las pantallas y la entrada del motor.</summary>
public sealed record LoadedSettlementEmployee(
    Employee Employee,
    string FullName,
    string Document,
    string? Email,
    SettlementInput Input,
    IReadOnlyList<DeudaPropuesta> Deudas);

/// <summary>Todo lo que un cálculo de liquidación necesita, cargado de una vez.</summary>
public sealed class SettlementBatch
{
    public required SettlementKind Kind { get; init; }
    public required DateOnly CutoffDate { get; init; }
    public DateOnly? PeriodStart { get; init; }
    public required IReadOnlyList<LoadedSettlementEmployee> Employees { get; init; }
    public required IReadOnlyList<PayrollConceptDefinition> Concepts { get; init; }
    public required IReadOnlyList<PayrollLegalParameter> Parameters { get; init; }
    public required PoliticasDeNomina Politicas { get; init; }

    /// <summary>Empleados que el cargador dejó fuera antes del motor (retirado con definitiva aprobada en el año).</summary>
    public IReadOnlyList<ExcludedEmployeeDto> Excluded { get; init; } = [];

    /// <summary>Avisos del cargador (Cartera sin respuesta, propuesta que cambió respecto del ajuste).</summary>
    public IReadOnlyList<WarningDto> Warnings { get; init; } = [];

    public EmploymentTermination? Termination { get; init; }
    public VacationMovement? Movement { get; init; }

    /// <summary>Id interno de cada novedad del período pendiente por su PublicId, para <c>PayrollRunLine.NoveltyId</c> (D-29).</summary>
    public IReadOnlyDictionary<Guid, int> NoveltyIds { get; init; } = new Dictionary<Guid, int>();

    public IReadOnlyList<string> MissingRequiredParameters =>
        SettlementCalculationEngine.MissingRequiredParameters(Parameters, CutoffDate.ToDateTime(TimeOnly.MinValue));
}

/// <summary>
/// Arma el <see cref="SettlementInput"/> de cada empleado (feature 010, T026, research R1–R3, R7):
/// la ficha con su historial de salarios; las <b>bases prestacionales por mes</b> de los últimos
/// doce meses tomadas de las corridas ordinarias aprobadas no reversadas por año/mes de
/// imputación (variables prestacionales, variables de vacaciones e ingreso laboral); la
/// <b>provisión acumulada</b> por concepto (<see cref="ProvisionBalanceReader"/>); el saldo
/// inicial de prestaciones; los movimientos de vacaciones; las ausencias y suspensiones (las
/// novedades con fechas que reducen días); la prima ya pagada en definitivas o en la semestral del
/// semestre y las cesantías pagadas en la anual del año (D-29); el salario pendiente del período
/// abierto donde cae el retiro con las novedades activas del empleado en él; el acumulado anual de retención
/// cuando la política es «acumulado»; y, en la definitiva, las deudas propuestas desde Cartera
/// por persona y las libranzas recurrentes según <c>DeduccionAlRetiroModo</c>. Las políticas se
/// leen a la fecha de corte por <see cref="PayrollPolicyReader"/>. No calcula nada: el motor es
/// puro y ésta es su única puerta de entrada.
/// </summary>
public sealed class SettlementInputLoader(
    IApplicationDbContext db,
    PayrollPolicyReader policies,
    ProvisionBalanceReader provisiones,
    ILogger<SettlementInputLoader> logger)
{
    /// <summary>Meses de corridas aprobadas que se cargan hacia atrás desde el corte: alcanzan para el promedio de doce meses de cesantías y vacaciones.</summary>
    private const int MesesDeHistoria = 12;

    public async Task<SettlementBatch> LoadAsync(SettlementLoadRequest request, CancellationToken ct)
    {
        var corte = request.CutoffDate;
        var corteDt = corte.ToDateTime(TimeOnly.MinValue);
        var inicioPeriodo = request.PeriodStart ?? InicioPorDefecto(request.Kind, corte);
        var politicas = await policies.ReadAsync(corte, ct);
        var avisos = new List<WarningDto>();
        var excluidos = new List<ExcludedEmployeeDto>();

        // --- lo que el tipo trae de más ---
        EmploymentTermination? terminacion = null;
        VacationMovement? movimiento = null;
        if (request.Kind == SettlementKind.Settlement)
        {
            terminacion = await db.EmploymentTerminations.Include(t => t.TerminationReason).Include(t => t.Deductions)
                .FirstOrDefaultAsync(t => t.Id == request.TerminationId, ct)
                ?? throw new InvalidOperationException("La definitiva necesita la terminación registrada.");
        }
        if (request.Kind == SettlementKind.Vacation)
        {
            movimiento = request.Movement
                ?? await db.VacationMovements.FirstOrDefaultAsync(m => m.Id == request.VacationMovementId, ct)
                ?? throw new InvalidOperationException("La liquidación de vacaciones necesita el movimiento registrado.");
        }

        // --- empleados ---
        var empleadosQuery =
            from e in db.Employees.AsNoTracking()
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where e.JoinDate <= corteDt
            select new { Employee = e, p.FirstName, p.LastName, p.TaxId, p.Email };
        if (request.EmployeeIds is { Count: > 0 } ids)
            empleadosQuery = empleadosQuery.Where(x => ids.Contains(x.Employee.Id));
        else
            empleadosQuery = empleadosQuery.Where(x => x.Employee.TerminationDate >= inicioPeriodo.ToDateTime(TimeOnly.MinValue));
        var empleados = (await empleadosQuery.ToListAsync(ct)).OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToList();
        var idsEmpleados = empleados.Select(x => x.Employee.Id).ToList();
        var idsPersonas = empleados.Select(x => x.Employee.PersonId).ToList();

        // --- terminaciones vivas (FR-005, FR-013, exclusión «RetiradoConDefinitiva») ---
        // Una definitiva aprobada (Settled) retiró al empleado; una registrada (Registered) tiene su
        // borrador vivo y va a pagar el tramo, la prima y las cesantías del retiro (D-29): la corrida
        // colectiva no lo incluye en ninguno de los dos casos, para no pagarle dos veces.
        var vivas = await db.EmploymentTerminations.AsNoTracking()
            .Where(t => idsEmpleados.Contains(t.EmployeeId) && (t.Status == TerminationStatus.Settled || t.Status == TerminationStatus.Registered))
            .ToListAsync(ct);
        var liquidadaPorEmpleado = vivas.GroupBy(t => t.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.TerminationDate).ThenByDescending(t => t.Id).First());
        if (request.Kind is SettlementKind.ServiceBonus or SettlementKind.Severance)
        {
            foreach (var x in empleados.ToList())
            {
                if (!liquidadaPorEmpleado.TryGetValue(x.Employee.Id, out var terminacionViva)) continue;
                var fecha = terminacionViva.TerminationDate;
                var nombre = $"{x.FirstName} {x.LastName}".Trim();
                if (fecha < inicioPeriodo)
                {
                    empleados.Remove(x);
                    continue;
                }
                if (fecha > corte) continue;
                var enBorrador = terminacionViva.Status == TerminationStatus.Registered;
                if (request.Kind == SettlementKind.Severance)
                {
                    empleados.Remove(x);
                    excluidos.Add(new ExcludedEmployeeDto(x.Employee.PublicId, nombre, "RetiradoConDefinitiva", enBorrador
                        ? $"Retirado el {fecha:dd/MM/yyyy} con liquidación definitiva registrada (borrador pendiente de aprobar): sus cesantías e intereses del año se pagan allí."
                        : $"Retirado el {fecha:dd/MM/yyyy} con liquidación definitiva aprobada: sus cesantías e intereses del año ya se pagaron allí."));
                }
                else if (enBorrador)
                {
                    // Con la definitiva aprobada, la prima pagada allí viaja al motor (FR-009) y él decide; con la
                    // definitiva en borrador todavía no hay prima pagada que informar, así que se excluye aquí.
                    empleados.Remove(x);
                    excluidos.Add(new ExcludedEmployeeDto(x.Employee.PublicId, nombre, SettlementReasonCodes.YaPagadaEnDefinitiva,
                        $"Retirado el {fecha:dd/MM/yyyy} con liquidación definitiva registrada (borrador pendiente de aprobar): la prima proporcional del semestre se paga allí (FR-009)."));
                }
            }
            idsEmpleados = empleados.Select(x => x.Employee.Id).ToList();
            idsPersonas = empleados.Select(x => x.Employee.PersonId).ToList();
        }

        // --- clase ARL (la ficha guarda la fila; la clase es su Code) ---
        var tarifas = empleados.Select(x => x.Employee.WorkRiskRateId).Where(id => id > 0).Distinct().ToList();
        var clasePorTarifa = tarifas.Count == 0
            ? new Dictionary<int, int>()
            : await db.WorkRiskRates.AsNoTracking().Where(r => tarifas.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Code, ct);

        // --- historial de salario hasta el corte ---
        var cambios = await db.SalaryChanges.AsNoTracking()
            .Where(s => idsEmpleados.Contains(s.EmployeeId) && s.EffectiveDate <= corteDt)
            .OrderBy(s => s.EffectiveDate).ToListAsync(ct);
        var cambiosPorEmpleado = cambios.ToLookup(s => s.EmployeeId);

        // --- versiones vigentes al corte ---
        var conceptos = await db.PayrollConceptDefinitions.AsNoTracking()
            .Where(c => c.IsActive && c.ValidFrom <= corteDt && (c.ValidTo == null || c.ValidTo >= corteDt))
            .ToListAsync(ct);
        var parametros = await db.PayrollLegalParameters.AsNoTracking().Include(p => p.Ranges)
            .Where(p => p.ValidFrom <= corteDt && (p.ValidTo == null || p.ValidTo >= corteDt))
            .ToListAsync(ct);

        // --- retención: procedimiento, porcentaje y deducciones declaradas al corte ---
        var tasas = await db.EmployeeWithholdingRates.AsNoTracking()
            .Where(r => idsEmpleados.Contains(r.EmployeeId) && r.ValidFrom <= corteDt && (r.ValidTo == null || r.ValidTo >= corteDt))
            .ToListAsync(ct);
        var tasaPorEmpleado = tasas.GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ValidFrom).First().RatePercent);
        var deducciones = await db.EmployeeTaxDeductions.AsNoTracking()
            .Where(d => idsEmpleados.Contains(d.EmployeeId) && d.ValidFrom <= corteDt && (d.ValidTo == null || d.ValidTo >= corteDt))
            .ToListAsync(ct);
        var deduccionesPorEmpleado = deducciones.ToLookup(d => d.EmployeeId);

        // --- corridas ordinarias aprobadas de los últimos doce meses, por imputación ---
        var desdeHistoria = new DateOnly(corte.Year, corte.Month, 1).AddMonths(1 - MesesDeHistoria);
        var lineasMensuales = await (
            from l in db.PayrollRunLines.AsNoTracking()
            join re in db.PayrollRunEmployees.AsNoTracking() on l.PayrollRunEmployeeId equals re.Id
            join r in db.PayrollRuns.AsNoTracking() on re.PayrollRunId equals r.Id
            join p in db.PayPeriods.AsNoTracking() on r.PayPeriodId equals p.Id
            where idsEmpleados.Contains(re.EmployeeId)
                  && r.Kind == PayrollRunKind.Ordinary && r.Status == PayrollRunStatus.Approved
                  && (l.Nature == ConceptNature.Earning || l.Nature == ConceptNature.Deduction)
                  && (p.ImputationYear > desdeHistoria.Year || (p.ImputationYear == desdeHistoria.Year && p.ImputationMonth >= desdeHistoria.Month))
                  && (p.ImputationYear < corte.Year || (p.ImputationYear == corte.Year && p.ImputationMonth <= corte.Month))
            select new { re.EmployeeId, p.ImputationYear, p.ImputationMonth, l.ConceptDefinitionId, l.ConceptCode, l.Nature, l.Amount, l.ExplanationJson })
            .ToListAsync(ct);
        var idsConcepto = lineasMensuales.Select(l => l.ConceptDefinitionId).Distinct().ToList();
        var definicionesDeLineas = idsConcepto.Count == 0
            ? new Dictionary<int, PayrollConceptDefinition>()
            : await db.PayrollConceptDefinitions.AsNoTracking().IgnoreQueryFilters().Where(c => idsConcepto.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var lineasPorEmpleado = lineasMensuales.ToLookup(l => l.EmployeeId);

        // --- retención practicada en el año en liquidaciones especiales aprobadas (modo Acumulado) ---
        // El cupo anual lo consumen también la prima, las vacaciones, la indemnización y la definitiva
        // (RETEFTE_PRIMA, RETEFTE_INDEMNIZACION, RETEFTE de esas corridas); esas corridas no tienen
        // período, así que su año es el del corte. Hasta el 2026-09-21 sólo se sumaba la ordinaria y
        // cada liquidación siguiente volvía a partir de un «usado» corto: se retenía de menos.
        var inicioAnio = new DateOnly(corte.Year, 1, 1);
        var retencionesEspeciales = politicas.RetefteTopesAnualesModo == ModoDeTopesAnuales.Acumulado
            ? await (
                from l in db.PayrollRunLines.AsNoTracking()
                join re in db.PayrollRunEmployees.AsNoTracking() on l.PayrollRunEmployeeId equals re.Id
                join r in db.PayrollRuns.AsNoTracking() on re.PayrollRunId equals r.Id
                where idsEmpleados.Contains(re.EmployeeId)
                      && r.Kind != PayrollRunKind.Ordinary && r.Status == PayrollRunStatus.Approved
                      && r.CutoffDate >= inicioAnio && r.CutoffDate <= corte
                      && l.Nature == ConceptNature.Deduction && l.ConceptCode.StartsWith(WellKnownConceptCodes.Withholding)
                select new { re.EmployeeId, l.ExplanationJson })
                .ToListAsync(ct)
            : [];
        var retencionesEspecialesPorEmpleado = retencionesEspeciales.ToLookup(l => l.EmployeeId, l => l.ExplanationJson);

        // --- ausencias: novedades con fechas que reducen días (aprobadas o del período abierto) ---
        // Prima y cesantías miran a lo sumo un año hacia atrás; el saldo de vacaciones se deriva desde el
        // ingreso (CST art. 53: toda suspensión del contrato descuenta), así que vacaciones y definitiva
        // cargan las suspensiones completas. Hasta el 2026-09-21 la ventana de doce meses se aplicaba a
        // las cuatro y una licencia no remunerada de hace dos años no descontaba días en la definitiva.
        var conceptosQueReducen = await db.PayrollConceptDefinitions.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.ReducesWorkedDays).Select(c => new { c.Id, c.Code, c.Nature }).ToListAsync(ct);
        var reducenPorId = conceptosQueReducen.ToDictionary(c => c.Id);
        var idsReducen = conceptosQueReducen.Select(c => c.Id).ToList();
        var desdeAusencias = desdeHistoria.ToDateTime(TimeOnly.MinValue);
        var ausenciasDesdeElIngreso = request.Kind is SettlementKind.Vacation or SettlementKind.Settlement;
        var ausencias = await db.PayrollNovelties.AsNoTracking()
            .Where(n => idsEmpleados.Contains(n.EmployeeId) && n.Status == NoveltyStatus.Active
                        && idsReducen.Contains(n.ConceptDefinitionId) && n.StartDate != null && n.EndDate != null
                        && (ausenciasDesdeElIngreso || n.EndDate >= desdeAusencias) && n.StartDate <= corteDt)
            .ToListAsync(ct);
        var ausenciasPorEmpleado = ausencias.ToLookup(n => n.EmployeeId);

        // --- saldos iniciales, movimientos de vacaciones, provisiones ---
        var saldos = await db.EmployeeBenefitOpeningBalances.AsNoTracking()
            .Where(b => idsEmpleados.Contains(b.EmployeeId) && b.AsOfDate <= corte)
            .OrderBy(b => b.AsOfDate).ThenBy(b => b.Kind).ToListAsync(ct);
        var saldosPorEmpleado = saldos.ToLookup(b => b.EmployeeId);
        // Todos los movimientos vivos, también los registrados para después del corte: un disfrute ya
        // registrado (y pagado por anticipado, D-01) compromete el saldo igual que en la pantalla
        // (VacationBalanceCalculator); el motor avisa cuando uno es posterior al corte o al retiro.
        var movimientos = await db.VacationMovements.AsNoTracking()
            .Where(m => idsEmpleados.Contains(m.EmployeeId) && m.Status != VacationMovementStatus.Cancelled
                        && (movimiento == null || m.Id != movimiento.Id))
            .OrderBy(m => m.StartDate).ToListAsync(ct);
        var movimientosPorEmpleado = movimientos.ToLookup(m => m.EmployeeId);
        var saldosProvision = await provisiones.LeerAsync(idsEmpleados, corte, excludeRunId: null, ct);

        // --- prima pagada en corridas aprobadas del semestre del corte: definitivas (FR-009) y, para la
        // definitiva, la semestral aprobada antes de registrar el retiro (D-29) ---
        var (semInicio, semFin) = SemestreDe(corte);
        var esDefinitiva = request.Kind == SettlementKind.Settlement;
        var primasPagadas = await (
            from l in db.PayrollRunLines.AsNoTracking()
            join re in db.PayrollRunEmployees.AsNoTracking() on l.PayrollRunEmployeeId equals re.Id
            join r in db.PayrollRuns.AsNoTracking() on re.PayrollRunId equals r.Id
            where idsEmpleados.Contains(re.EmployeeId) && (r.Kind == PayrollRunKind.Settlement || (esDefinitiva && r.Kind == PayrollRunKind.ServiceBonus))
                  && r.Status == PayrollRunStatus.Approved
                  && r.CutoffDate >= semInicio && r.CutoffDate <= semFin
                  && l.ConceptCode == WellKnownConceptCodes.ServiceBonus
            select new { re.EmployeeId, r.PublicId, r.CutoffDate, r.Kind, l.Amount, l.Quantity })
            .ToListAsync(ct);
        var primasPorEmpleado = primasPagadas.ToLookup(x => x.EmployeeId);

        // --- cesantías pagadas en la corrida anual aprobada del año del retiro (definitiva, D-29) ---
        var anioInicio = new DateOnly(corte.Year, 1, 1);
        var anioFin = new DateOnly(corte.Year, 12, 31);
        var cesantiasPagadas = request.Kind == SettlementKind.Settlement
            ? await (
                from l in db.PayrollRunLines.AsNoTracking()
                join re in db.PayrollRunEmployees.AsNoTracking() on l.PayrollRunEmployeeId equals re.Id
                join r in db.PayrollRuns.AsNoTracking() on re.PayrollRunId equals r.Id
                where idsEmpleados.Contains(re.EmployeeId) && r.Kind == PayrollRunKind.Severance && r.Status == PayrollRunStatus.Approved
                      && r.CutoffDate >= anioInicio && r.CutoffDate <= anioFin
                      && l.ConceptCode == WellKnownConceptCodes.Severance
                select new { re.EmployeeId, r.PublicId, r.CutoffDate, l.Amount })
                .ToListAsync(ct)
            : [];
        var cesantiasPorEmpleado = cesantiasPagadas.ToLookup(x => x.EmployeeId);

        // --- salario pendiente: el período abierto del plan donde cae el retiro (definitiva) ---
        var periodosAbiertos = request.Kind == SettlementKind.Settlement
            ? await db.PayPeriods.AsNoTracking()
                .Where(p => (p.Status == PayPeriodStatus.Open || p.Status == PayPeriodStatus.Calculated) && p.StartDate <= corteDt && p.EndDate >= corteDt)
                .ToListAsync(ct)
            : [];

        // --- novedades activas del empleado en ese período (definitiva, D-29): la bonificación por retiro
        // pactada viaja en la terminación; las demás devengadas y deducciones se liquidan con su concepto
        // porque la ordinaria del período ya no lo incluye. Las informativas ya entraron como ausencias y
        // la cuota de una libranza recurrente ya viene propuesta como deuda (FR-018a).
        var idsAbiertos = periodosAbiertos.Select(p => p.Id).ToList();
        var novedadesPendientes = request.Kind == SettlementKind.Settlement && idsAbiertos.Count > 0
            ? await db.PayrollNovelties.AsNoTracking()
                .Where(n => idsEmpleados.Contains(n.EmployeeId) && n.Status == NoveltyStatus.Active && idsAbiertos.Contains(n.PayPeriodId))
                .Join(db.PayrollConceptDefinitions.AsNoTracking().IgnoreQueryFilters(), n => n.ConceptDefinitionId, c => c.Id,
                    (n, c) => new { Novelty = n, ConceptName = c.Name, c.Nature })
                .OrderBy(x => x.Novelty.Id)
                .ToListAsync(ct)
            : [];
        var bonificaciones = novedadesPendientes.Where(x => x.Novelty.ConceptCode == WellKnownConceptCodes.RetirementBonus).Select(x => x.Novelty).ToList();
        var novedadesPorEmpleado = novedadesPendientes
            .Where(x => x.Nature is ConceptNature.Earning or ConceptNature.Deduction
                        && !x.Novelty.ConceptCode.Equals(WellKnownConceptCodes.RetirementBonus, StringComparison.OrdinalIgnoreCase)
                        && !(x.Novelty.ConceptCode.Equals(LibranzaCode, StringComparison.OrdinalIgnoreCase) && x.Novelty.RecurringNoveltyId != null))
            .ToLookup(x => x.Novelty.EmployeeId);
        var idsNovedades = novedadesPorEmpleado.SelectMany(g => g).ToDictionary(x => x.Novelty.PublicId, x => x.Novelty.Id);

        // --- deudas (definitiva) ---
        var deudasPorEmpleado = new Dictionary<int, IReadOnlyList<DeudaPropuesta>>();
        if (request.Kind == SettlementKind.Settlement && terminacion is not null)
        {
            foreach (var x in empleados)
                deudasPorEmpleado[x.Employee.Id] = await ProponerDeudasAsync(x.Employee, terminacion, corte, politicas, avisos, ct);
        }

        var cargados = new List<LoadedSettlementEmployee>(empleados.Count);
        foreach (var x in empleados)
        {
            var e = x.Employee;
            var nombre = $"{x.FirstName} {x.LastName}".Trim();

            // Sin historial, el salario de la ficha rige desde el ingreso (RegisterSalaryChange siembra la
            // línea base al registrar el primer cambio, así que esto sólo aplica a fichas sin cambios).
            var historial = cambiosPorEmpleado[e.Id].Select(s => new SalaryChangeInput(s.EffectiveDate.Date, s.NewSalary)).ToList();
            if (!historial.Any(h => h.EffectiveDate <= e.JoinDate.Date))
                historial.Insert(0, new SalaryChangeInput(e.JoinDate.Date, historial.Count == 0 ? e.Salary : historial[0].MonthlySalary));

            // Fecha de retiro que el motor debe ver: la de la definitiva en curso, la de una definitiva
            // aprobada o la de la ficha; nula si sigue vinculado al corte.
            DateTime? retiro = request.Kind == SettlementKind.Settlement && terminacion is not null && terminacion.EmployeeId == e.Id
                ? terminacion.TerminationDate.ToDateTime(TimeOnly.MinValue)
                : liquidadaPorEmpleado.TryGetValue(e.Id, out var liq) ? liq.TerminationDate.ToDateTime(TimeOnly.MinValue)
                : e.TerminationDate < DateTime.MaxValue.Date && e.Status < 0 ? e.TerminationDate.Date : null;

            var basesMensuales = BasesPorMes(lineasPorEmpleado[e.Id].Select(l => (l.ImputationYear, l.ImputationMonth, l.ConceptDefinitionId, l.ConceptCode, l.Nature, l.Amount)), definicionesDeLineas);
            var acumuladoRetencion = politicas.RetefteTopesAnualesModo == ModoDeTopesAnuales.Acumulado
                ? AcumuladoDeRetencion(lineasPorEmpleado[e.Id]
                    .Where(l => l.ImputationYear == corte.Year && l.Nature == ConceptNature.Deduction && l.ConceptCode.StartsWith(WellKnownConceptCodes.Withholding, StringComparison.OrdinalIgnoreCase))
                    .Select(l => l.ExplanationJson)
                    .Concat(retencionesEspecialesPorEmpleado[e.Id]))
                : null;

            var deudas = deudasPorEmpleado.GetValueOrDefault(e.Id) ?? [];
            var esLaTerminacion = terminacion is not null && terminacion.EmployeeId == e.Id;

            var input = new SettlementInput
            {
                Kind = request.Kind,
                CutoffDate = corteDt,
                PeriodStart = request.PeriodStart is { } ps ? ps.ToDateTime(TimeOnly.MinValue) : null,
                Employee = new SettlementEmployeeInput
                {
                    PublicId = e.PublicId,
                    DisplayName = nombre,
                    Class = e.EmployeeClass,
                    JoinDate = e.JoinDate.Date,
                    TerminationDate = retiro,
                    SalaryHistory = historial,
                    ApprenticeStage = e.ApprenticeStage,
                    Affiliations = new AffiliationsInput
                    {
                        Health = e.HealthInsuranceId > 0,
                        Pension = e.PensionFundId > 0,
                        WorkRiskClass = clasePorTarifa.TryGetValue(e.WorkRiskRateId, out var clase) && clase is >= 1 and <= 5 ? clase : null,
                        FamilyCompensation = e.FamilySubsidyId > 0,
                    },
                    WithholdingProcedure = e.WithholdingProcedure is 1 or 2 ? e.WithholdingProcedure : (byte)1,
                    WithholdingRatePercent = tasaPorEmpleado.TryGetValue(e.Id, out var tasa) ? tasa : null,
                    TaxDeductions = deduccionesPorEmpleado[e.Id].Select(d => new TaxDeductionInput(d.Kind, d.MonthlyAmount, d.Percent)).ToList(),
                    ContractType = (esLaTerminacion ? terminacion!.ContractTypeAtTermination : null) ?? e.DianContractType ?? DianContractType.Indefinite,
                    ContractEndDate = esLaTerminacion ? terminacion!.ContractEndDate?.ToDateTime(TimeOnly.MinValue) : null,
                },
                Absences = ausenciasPorEmpleado[e.Id]
                    .Select(n => new AbsenceInput(n.StartDate!.Value.Date, n.EndDate!.Value.Date, EsSuspension(reducenPorId.GetValueOrDefault(n.ConceptDefinitionId)?.Nature, n.ConceptCode), n.ConceptCode))
                    .ToList(),
                OpeningBalance = SaldoInicial(saldosPorEmpleado[e.Id].ToList()),
                VacationMovements = movimientosPorEmpleado[e.Id].Select(Movimiento).ToList(),
                MovementToSettle = movimiento is not null && movimiento.EmployeeId == e.Id ? Movimiento(movimiento) : null,
                MonthlyBases = basesMensuales,
                Provisions = (saldosProvision.GetValueOrDefault(e.Id) ?? [])
                    .Select(s => new ProvisionBalanceInput(s.ProvisionCode, s.Balance)).ToList(),
                Termination = esLaTerminacion
                    ? new TerminationInput
                    {
                        ReasonCode = terminacion!.TerminationReason?.Code ?? string.Empty,
                        ReasonName = terminacion.TerminationReason?.Name ?? string.Empty,
                        GeneratesSeverancePay = terminacion.TerminationReason?.GeneratesSeverancePay ?? false,
                        VoluntaryRetirementBonus = bonificaciones.Where(b => b.EmployeeId == e.Id).Select(b => (decimal?)b.Amount).FirstOrDefault(),
                    }
                    : null,
                ProposedDeductions = deudas.Select(d => d.ComoEntrada()).ToList(),
                ServiceBonusPaidInSettlements = primasPorEmpleado[e.Id]
                    .Select(p => new PaidServiceBonusInput(p.PublicId, p.CutoffDate!.Value.ToDateTime(TimeOnly.MinValue), p.Amount, (int)(p.Quantity ?? 0m), (SettlementKind)(int)p.Kind)).ToList(),
                SeverancePaidInRuns = cesantiasPorEmpleado[e.Id]
                    .Select(p => new PaidSeveranceInput(p.PublicId, p.CutoffDate!.Value.ToDateTime(TimeOnly.MinValue), p.Amount)).ToList(),
                PendingSalary = esLaTerminacion
                    ? periodosAbiertos.Where(p => p.PayrollPlanId == e.PayrollPlanId).Select(p => new PendingSalaryInput(p.StartDate.Date, p.EndDate.Date)).FirstOrDefault()
                    : null,
                PendingNovelties = esLaTerminacion
                    ? novedadesPorEmpleado[e.Id]
                        .Where(x => periodosAbiertos.Any(p => p.Id == x.Novelty.PayPeriodId && p.PayrollPlanId == e.PayrollPlanId))
                        .Select(x => new NoveltyInput
                        {
                            PublicId = x.Novelty.PublicId,
                            ConceptCode = x.Novelty.ConceptCode,
                            Quantity = x.Novelty.Quantity,
                            Amount = x.Novelty.Amount,
                            StartDate = x.Novelty.StartDate,
                            EndDate = x.Novelty.EndDate,
                            Origin = x.Novelty.Origin,
                            Description = string.IsNullOrWhiteSpace(x.Novelty.Notes) ? x.ConceptName : $"{x.ConceptName}: {x.Novelty.Notes}",
                        }).ToList()
                    : [],
                WithholdingYearToDate = acumuladoRetencion,
                Policies = politicas.ForSettlement(),
                Parameters = parametros,
                Concepts = conceptos,
            };

            cargados.Add(new LoadedSettlementEmployee(e, nombre, x.TaxId, x.Email, input, deudas));
        }

        return new SettlementBatch
        {
            Kind = request.Kind,
            CutoffDate = corte,
            PeriodStart = request.PeriodStart,
            Employees = cargados,
            Concepts = conceptos,
            Parameters = parametros,
            Politicas = politicas,
            Excluded = excluidos,
            Warnings = avisos,
            Termination = terminacion,
            Movement = movimiento,
            NoveltyIds = idsNovedades,
        };
    }

    // ------------------------------------------------------------------ piezas --

    private static DateOnly InicioPorDefecto(SettlementKind kind, DateOnly corte) => kind switch
    {
        SettlementKind.ServiceBonus => corte.Month <= 6 ? new DateOnly(corte.Year, 1, 1) : new DateOnly(corte.Year, 7, 1),
        SettlementKind.Severance => new DateOnly(corte.Year, 1, 1),
        _ => new DateOnly(corte.Year, 1, 1),
    };

    private static (DateOnly Start, DateOnly End) SemestreDe(DateOnly corte) =>
        corte.Month <= 6 ? (new DateOnly(corte.Year, 1, 1), new DateOnly(corte.Year, 6, 30)) : (new DateOnly(corte.Year, 7, 1), new DateOnly(corte.Year, 12, 31));

    /// <summary>Una ausencia informativa que reduce días (suspensión, licencia no remunerada) es suspensión del contrato (CST art. 51); la ausencia por vacaciones pagadas no lo es.</summary>
    private static bool EsSuspension(ConceptNature? nature, string code) =>
        nature == ConceptNature.Informative && !code.Equals(WellKnownConceptCodes.VacationLeave, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Las bases por mes de imputación: variables prestacionales (todo devengo que afecta la base
    /// prestacional salvo salario y auxilio), variables de la base de vacaciones (todo lo que afecta
    /// esa base salvo el salario) e ingreso laboral (todos los devengos). Los meses sin corrida no
    /// aparecen y no cuentan en el promedio; un mes con corrida y sin variables cuenta en cero.
    /// </summary>
    public static IReadOnlyList<MonthlyBaseInput> BasesPorMes(
        IEnumerable<(short Year, byte Month, int ConceptDefinitionId, string Code, ConceptNature Nature, decimal Amount)> lineas,
        IReadOnlyDictionary<int, PayrollConceptDefinition> definiciones)
    {
        var meses = new Dictionary<(int, int), (decimal Prestacional, decimal Vacaciones, decimal Ingreso)>();
        foreach (var l in lineas.Where(l => l.Nature == ConceptNature.Earning))
        {
            var clave = ((int)l.Year, (int)l.Month);
            var actual = meses.GetValueOrDefault(clave);
            definiciones.TryGetValue(l.ConceptDefinitionId, out var def);
            var esSalario = l.Code.Equals(WellKnownConceptCodes.BasicSalary, StringComparison.OrdinalIgnoreCase);
            var esAuxilio = l.Code.Equals(WellKnownConceptCodes.TransportAllowance, StringComparison.OrdinalIgnoreCase);
            var esDeLiquidacion = WellKnownConceptCodes.SettlementOnly.Contains(l.Code, StringComparer.OrdinalIgnoreCase);
            var prestacional = def is { AffectsBenefitsBase: true } && !esSalario && !esAuxilio && !esDeLiquidacion ? l.Amount : 0m;
            var vacaciones = def is { AffectsVacationBase: true } && !esSalario && !esDeLiquidacion ? l.Amount : 0m;
            meses[clave] = (actual.Prestacional + prestacional, actual.Vacaciones + vacaciones, actual.Ingreso + l.Amount);
        }
        return meses.OrderBy(m => m.Key.Item1).ThenBy(m => m.Key.Item2)
            .Select(m => new MonthlyBaseInput(m.Key.Item1, m.Key.Item2, m.Value.Prestacional, m.Value.Vacaciones, m.Value.Ingreso))
            .ToList();
    }

    /// <summary>
    /// El saldo inicial vigente: <b>una sola fila</b>, la que manda según
    /// <see cref="BenefitBalanceRules.Vigente"/> (corte más reciente y, a igual corte, la última
    /// registrada). Un ajuste se digita como el saldo <b>completo</b> corregido —así lo dicen el
    /// comando, la pantalla y `contracts/api.md` §4—, de modo que reemplaza a la apertura y no se le
    /// suma: hasta el 2026-09-21 esto sumaba apertura más ajustes y la prima, las cesantías y los
    /// días de vacaciones salían doblados tras corregir un saldo. Los «días ya contados», opcionales
    /// en el ajuste, se heredan de la apertura cuando el ajuste no los trae.
    /// </summary>
    public static OpeningBalanceInput? SaldoInicial(IReadOnlyList<EmployeeBenefitOpeningBalance> filas)
    {
        var vigente = BenefitBalanceRules.Vigente(filas);
        if (vigente is null) return null;
        var apertura = filas.Where(f => f.Kind == OpeningBalanceKind.Opening).OrderByDescending(f => f.AsOfDate).ThenByDescending(f => f.Id).FirstOrDefault() ?? vigente;
        return new OpeningBalanceInput
        {
            AsOfDate = vigente.AsOfDate.ToDateTime(TimeOnly.MinValue),
            PendingVacationDays = vigente.PendingVacationDays,
            AccruedSeverance = vigente.AccruedSeverance,
            AccruedSeveranceInterest = vigente.AccruedSeveranceInterest,
            AccruedServiceBonus = vigente.AccruedServiceBonus,
            ServiceBonusDaysAccrued = vigente.ServiceBonusDaysAccrued ?? apertura.ServiceBonusDaysAccrued,
            SeveranceDaysAccrued = vigente.SeveranceDaysAccrued ?? apertura.SeveranceDaysAccrued,
            EnteredBy = vigente.CreatedBy ?? string.Empty,
            EnteredAt = vigente.CreatedAt,
        };
    }

    private static VacationMovementInput Movimiento(VacationMovement m) => new()
    {
        PublicId = m.PublicId,
        Kind = m.Kind,
        StartDate = m.StartDate.ToDateTime(TimeOnly.MinValue),
        EndDate = m.EndDate?.ToDateTime(TimeOnly.MinValue),
        BusinessDays = m.BusinessDays,
        CalendarDays = m.CalendarDays,
        IsCancelled = m.Status == VacationMovementStatus.Cancelled,
        Description = m.Notes,
    };

    /// <summary>
    /// Lo consumido en el año de los cupos anuales de retención, leído de la explicación de las líneas
    /// de retención de las corridas aprobadas del año —ordinarias y especiales, de cualquier tipo—: el
    /// último paso «Renta exenta…» es la exenta que se aplicó y el de «Total de deducciones y rentas
    /// exentas» (o su versión limitada) el total depurado; una retención sin ese total (la de la
    /// indemnización, ET art. 401-3, que sólo resta la exenta) consume del cupo global lo mismo que de
    /// la exenta. Sólo se usa con la política <c>RetefteTopesAnualesModo = Acumulado</c>.
    /// </summary>
    public static AcumuladoAnualDeRetencion AcumuladoDeRetencion(IEnumerable<string> explicacionesDeRetencion)
    {
        decimal exenta = 0m, total = 0m;
        foreach (var json in explicacionesDeRetencion)
        {
            Explanation? exp;
            try { exp = JsonSerializer.Deserialize<Explanation>(json, RunJson.Options); }
            catch (JsonException) { continue; }
            if (exp is null) continue;
            var pasoExenta = exp.Steps.LastOrDefault(s => s.Value is not null && s.Label.StartsWith("Renta exenta", StringComparison.OrdinalIgnoreCase));
            var pasoTotal = exp.Steps.LastOrDefault(s => s.Value is not null
                && (s.Label.StartsWith("Total de deducciones y rentas exentas", StringComparison.OrdinalIgnoreCase)
                    || s.Label.StartsWith("Deducciones y rentas exentas limitadas", StringComparison.OrdinalIgnoreCase)));
            exenta += pasoExenta?.Value ?? 0m;
            total += pasoTotal?.Value ?? pasoExenta?.Value ?? 0m;
        }
        return new AcumuladoAnualDeRetencion(exenta, total);
    }

    /// <summary>
    /// Las deudas del empleado que la definitiva propone descontar (FR-018a, R7): préstamos de la
    /// cooperativa leídos de Cartera por persona (saldo total, o la cuota causada y la mora, según la
    /// política) y libranzas con terceros (cuotas causadas y no descontadas de la recurrente). Un
    /// descuento que la responsable ya ajustó en una versión anterior conserva su valor aplicado si
    /// lo propuesto no cambió; si cambió, vuelve a lo propuesto y se avisa. Si Cartera falla, la
    /// propuesta de préstamos sale vacía con el aviso <c>Payroll.Settlement.PortfolioUnavailable</c>:
    /// la definitiva no se bloquea, pero quien aprueba lo ve.
    /// </summary>
    private async Task<IReadOnlyList<DeudaPropuesta>> ProponerDeudasAsync(Employee e, EmploymentTermination terminacion, DateOnly corte,
        PoliticasDeNomina politicas, List<WarningDto> avisos, CancellationToken ct)
    {
        if (politicas.DeduccionAlRetiroModo == DeduccionAlRetiro.NoProponer) return [];
        var anteriores = terminacion.Deductions.Where(d => !d.IsDeleted && d.Status != SettlementDeductionStatus.Reverted).ToList();
        var propuestas = new List<DeudaPropuesta>();

        // --- préstamos de la cooperativa (Cartera) ---
        try
        {
            var creditos = await db.LoanPortfolios.AsNoTracking()
                .Where(l => l.PersonId == e.PersonId && l.CurrentBalance > 0m && l.ClosingDate == null)
                .OrderBy(l => l.DisbursementDate)
                .ToListAsync(ct);
            foreach (var c in creditos)
            {
                var propuesto = politicas.DeduccionAlRetiroModo == DeduccionAlRetiro.SaldoTotal
                    ? c.CurrentBalance
                    : Math.Min(c.CurrentBalance, c.InstallmentAmount + c.DefaultBalanceCurrent);
                var desglose = JsonSerializer.Serialize(new
                {
                    capital = c.CapitalBalanceCurrent,
                    intereses = c.InterestBalanceCurrent,
                    mora = c.DefaultBalanceCurrent,
                    cuotasPendientes = c.PendingInstallmentCount,
                    cuota = c.InstallmentAmount,
                    saldoTotal = c.CurrentBalance,
                    modo = politicas.DeduccionAlRetiroModo.ToString(),
                }, RunJson.Options);
                propuestas.Add(Conservar(anteriores.FirstOrDefault(a => a.LoanPortfolioId == c.Id), new DeudaPropuesta(
                    SettlementDeductionKind.CooperativeLoan, c.Id, null,
                    $"Crédito {c.PortfolioNumber} ({c.DocumentType} {c.DocumentNumber})",
                    propuesto, propuesto, desglose, WellKnownConceptCodes.LoanDeduction, AccountedByOtherModule: true, null, null), avisos, e));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cartera no respondió al proponer los descuentos de la definitiva del empleado {Employee}", e.PublicId);
            avisos.Add(SettlementErrors.PortfolioUnavailable(ex.Message));
        }

        // --- libranzas con terceros: recurrentes LIBRANZA con cuotas causadas y no descontadas ---
        var libranzas = await db.PayrollRecurringNovelties.AsNoTracking()
            .Where(r => r.EmployeeId == e.Id && r.IsActive && r.ConceptCode == LibranzaCode && r.StartDate <= corte.ToDateTime(TimeOnly.MinValue))
            .ToListAsync(ct);
        if (libranzas.Count > 0)
        {
            // Cuotas causadas: los períodos del plan que empezaron antes del retiro, incluido el que lo
            // contiene (la definitiva paga su salario pendiente, así que su cuota también se causa),
            // contados con la MISMA regla con que el materializador genera la recurrente (feature 006,
            // RecurringNoveltiesMaterializer): `ApplyOn` decide si la cuota cae en cada período, en el
            // primero del mes o en el último. Hasta el 2026-09-21 se contaba todo período del plan y una
            // libranza mensual en un plan quincenal proponía una cuota de más por cada mes transcurrido.
            var periodicidad = await db.PayrollPlans.AsNoTracking().Where(p => p.Id == e.PayrollPlanId).Select(p => p.Periodicity).FirstAsync(ct);
            var periodosDelPlan = await db.PayPeriods.AsNoTracking()
                .Where(p => p.PayrollPlanId == e.PayrollPlanId)
                .Select(p => new { p.StartDate, p.EndDate, p.SubPeriodNumber, p.ImputationYear, p.ImputationMonth })
                .ToListAsync(ct);
            // En semanal el último del mes es la mayor semana creada para ese mes (como en el materializador).
            var mayorSemanaPorMes = periodicidad == PayrollPeriodicity.Weekly
                ? periodosDelPlan.GroupBy(p => (p.ImputationYear, p.ImputationMonth)).ToDictionary(g => g.Key, g => (byte?)g.Max(p => p.SubPeriodNumber))
                : null;
            var periodos = periodosDelPlan.Where(p => p.StartDate <= corte.ToDateTime(TimeOnly.MinValue)).ToList();
            foreach (var r in libranzas)
            {
                var causadas = periodos.Count(p =>
                    r.StartDate <= p.EndDate && (r.EndDate is null || r.EndDate >= p.StartDate)
                    && r.ApplyOn switch
                    {
                        RecurringApplyRule.FirstOfMonth => p.SubPeriodNumber == 1,
                        RecurringApplyRule.LastOfMonth => PeriodCalendar.EsUltimoDelMes(periodicidad, p.SubPeriodNumber, mayorSemanaPorMes?.GetValueOrDefault((p.ImputationYear, p.ImputationMonth))),
                        _ => true,
                    });
                if (r.TotalInstallments is { } total) causadas = Math.Min(causadas, total);
                var pendientes = Math.Max(0, causadas - r.InstallmentsIssued);
                var cuota = r.Amount ?? 0m;
                var propuesto = pendientes * cuota;
                if (propuesto <= 0m) continue;
                var desglose = JsonSerializer.Serialize(new
                {
                    cuota, cuotasCausadas = causadas, cuotasDescontadas = r.InstallmentsIssued, cuotasPendientes = pendientes,
                    cuotasTotales = r.TotalInstallments, modo = politicas.DeduccionAlRetiroModo.ToString(),
                }, RunJson.Options);
                propuestas.Add(Conservar(anteriores.FirstOrDefault(a => a.RecurringNoveltyId == r.Id), new DeudaPropuesta(
                    SettlementDeductionKind.ThirdPartyLibranza, null, r.Id,
                    string.IsNullOrWhiteSpace(r.Notes) ? $"Libranza ({pendientes} cuota(s) causada(s) sin descontar)" : $"Libranza {r.Notes} ({pendientes} cuota(s) causada(s) sin descontar)",
                    propuesto, propuesto, desglose, LibranzaCode, AccountedByOtherModule: false, null, null), avisos, e));
            }
        }

        return propuestas;
    }

    /// <summary>Código del concepto de libranza de la semilla 005.</summary>
    public const string LibranzaCode = "LIBRANZA";

    /// <summary>Un descuento ya ajustado conserva lo aplicado si lo propuesto no cambió; si cambió, vuelve a lo propuesto y se avisa (contracts/api.md §3.4, recalcular).</summary>
    private static DeudaPropuesta Conservar(SettlementDeduction? anterior, DeudaPropuesta nueva, List<WarningDto> avisos, Employee e)
    {
        if (anterior is null) return nueva;
        if (anterior.ProposedAmount == nueva.Proposed)
            return nueva with { Applied = anterior.AppliedAmount, DeductionPublicId = anterior.PublicId, AdjustmentReason = anterior.AdjustmentReason };
        if (anterior.FueAjustado)
            avisos.Add(new WarningDto("Payroll.Settlement.DeductionReproposed",
                $"{nueva.Description}: el saldo en Cartera cambió de {anterior.ProposedAmount:N0} a {nueva.Proposed:N0}; el ajuste anterior ({anterior.AppliedAmount:N0}) se descartó y vuelve a proponerse el saldo. Revíselo antes de aprobar.",
                new { employeePublicId = e.PublicId, deductionPublicId = anterior.PublicId, previousProposed = anterior.ProposedAmount, proposed = nueva.Proposed }));
        return nueva with { DeductionPublicId = anterior.PublicId };
    }
}
