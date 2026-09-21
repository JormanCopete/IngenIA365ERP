using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>Un empleado del período con lo que el motor necesita y lo que la corrida persiste de él.</summary>
public sealed record LoadedEmployee(
    Employee Employee,
    string FullName,
    string Document,
    string? Email,
    EmployeeInput Input,
    IReadOnlyList<NoveltyInput> Novelties);

/// <summary>Todo el período cargado de una vez: empleados, novedades, versiones vigentes, políticas.</summary>
public sealed class PayrollCalculationBatch
{
    public required PayPeriod Period { get; init; }
    public required PayrollPlan Plan { get; init; }
    public required PeriodInput PeriodInput { get; init; }
    public required IReadOnlyList<LoadedEmployee> Employees { get; init; }
    public required IReadOnlyList<PayrollConceptDefinition> Concepts { get; init; }
    public required IReadOnlyList<PayrollLegalParameter> Parameters { get; init; }
    public required CalculationPolicies Policies { get; init; }

    /// <summary>Id interno de cada novedad por su PublicId, para <c>PayrollRunLine.NoveltyId</c>.</summary>
    public required IReadOnlyDictionary<Guid, int> NoveltyIds { get; init; }

    public IReadOnlyList<string> MissingRequiredParameters =>
        PayrollCalculationEngine.MissingRequiredParameters(Parameters, Period.EndDate);

    public CalculationInput InputFor(LoadedEmployee e) => new()
    {
        Period = PeriodInput,
        Employee = e.Input,
        Novelties = e.Novelties,
        Concepts = Concepts,
        Parameters = Parameters,
        Policies = Policies,
    };
}

/// <summary>
/// Carga los insumos del cálculo de un período en pocas consultas (T044): el plan, los
/// empleados del plan vigentes en el período, su historial de salario, sus novedades
/// activas, los descuentos que Cartera ya generó para el período (D-08), las versiones
/// de concepto y parámetro vigentes a la fecha de fin, la retención y las deducciones
/// declaradas por empleado, y las políticas de la cooperativa. No calcula nada.
///
/// <para>
/// Un empleado está en el período si pertenece al plan, su fecha de efecto en el plan no
/// es posterior al fin del período, ingresó antes del fin y no se retiró antes del
/// inicio. Una ficha cerrada dentro del período sin definitiva se liquida por los días hasta
/// el retiro; con definitiva aprobada dentro del período (o antes) el empleado no entra: ese
/// tramo lo pagó la definitiva (D-28).
/// </para>
/// </summary>
public sealed class CalculationInputLoader(IApplicationDbContext db, PayrollPolicyReader policies)
{
    public async Task<PayrollCalculationBatch> LoadAsync(PayPeriod period, CancellationToken ct)
    {
        var plan = period.PayrollPlan
            ?? await db.PayrollPlans.AsNoTracking().FirstAsync(p => p.Id == period.PayrollPlanId, ct);

        var start = period.StartDate.Date;
        var end = period.EndDate.Date;
        var periodInput = new PeriodInput(start, end, plan.Periodicity);

        // --- empleados del plan vigentes en el período, con su persona ---
        var empleados = await (
            from e in db.Employees.AsNoTracking()
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where e.PayrollPlanId == plan.Id
                  && (e.PayrollPlanEffectiveFrom == null || e.PayrollPlanEffectiveFrom <= end)
                  && e.JoinDate <= end
                  && e.TerminationDate >= start
            orderby p.LastName, p.FirstName
            select new { Employee = e, p.FirstName, p.LastName, p.TaxId, p.Email }
        ).ToListAsync(ct);

        // Feature 010 (FR-005, FR-020, D-28): la verdad del retiro es PAY_EmploymentTerminations. Una
        // definitiva aprobada (Settled) con fecha anterior al período o DENTRO de él saca al empleado
        // aunque la ficha no se hubiera cerrado: el último tramo —salario, auxilio y novedades hasta el
        // retiro— lo pagó la definitiva (SALARIO_PENDIENTE), y liquidarlo aquí por días lo pagaba dos veces.
        // Sólo una definitiva con fecha posterior al fin del período deja al empleado en éste, completo.
        var candidatos = empleados.Select(x => x.Employee.Id).ToList();
        var retiros = await db.EmploymentTerminations.AsNoTracking()
            .Where(t => candidatos.Contains(t.EmployeeId) && t.Status == TerminationStatus.Settled)
            .GroupBy(t => t.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Fecha = g.Max(t => t.TerminationDate) })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Fecha.ToDateTime(TimeOnly.MinValue), ct);
        empleados = empleados.Where(x => !retiros.TryGetValue(x.Employee.Id, out var f) || f > end).ToList();

        var ids = empleados.Select(x => x.Employee.Id).ToList();
        var documentos = empleados.Select(x => x.TaxId).ToList();

        // --- clase de riesgo ARL de cada empleado ---
        // La ficha guarda la FILA de PAY_WorkRiskRates (WorkRiskRateId), no la clase; la
        // clase (1..5) es el Code de esa fila, y es lo que el motor traduce a
        // ARL_CLASE_{I..V}_PCT. Hasta el 2026-09-11 se tomaba el Id como si fuera la
        // clase: sólo acertaba si las cinco filas tenían por casualidad Id 1..5.
        var tarifas = empleados.Select(x => x.Employee.WorkRiskRateId).Where(id => id > 0).Distinct().ToList();
        var clasePorTarifa = tarifas.Count == 0
            ? new Dictionary<int, int>()
            : await db.WorkRiskRates.AsNoTracking()
                .Where(r => tarifas.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.Code, ct);

        // --- historial de salario hasta el fin del período ---
        var cambios = await db.SalaryChanges.AsNoTracking()
            .Where(s => ids.Contains(s.EmployeeId) && s.EffectiveDate <= end)
            .OrderBy(s => s.EffectiveDate)
            .ToListAsync(ct);
        var cambiosPorEmpleado = cambios.ToLookup(s => s.EmployeeId);

        // --- novedades activas del período ---
        var novedades = await db.PayrollNovelties.AsNoTracking()
            .Where(n => n.PayPeriodId == period.Id && n.Status == NoveltyStatus.Active)
            .Join(db.PayrollConceptDefinitions.AsNoTracking(), n => n.ConceptDefinitionId, c => c.Id,
                (n, c) => new { Novelty = n, ConceptName = c.Name })
            .OrderBy(x => x.Novelty.Id)
            .ToListAsync(ct);
        var novedadesPorEmpleado = novedades.ToLookup(x => x.Novelty.EmployeeId);
        var noveltyIds = novedades.ToDictionary(x => x.Novelty.PublicId, x => x.Novelty.Id);

        // --- descuentos por nómina que Cartera ya aplicó y contabilizó (D-08) ---
        var desdeTexto = start.ToString("yyyyMMdd");
        var hastaTexto = end.ToString("yyyyMMdd");
        var cartera = await db.PayrollDeductionEntries.AsNoTracking()
            .Where(d => d.StartDate == desdeTexto && d.EndDate == hastaTexto && d.EntryType == "DN"
                        && documentos.Contains(d.PersonCode) && d.Amount > 0)
            .ToListAsync(ct);
        var carteraPorDocumento = cartera.ToLookup(d => d.PersonCode);

        // --- versiones vigentes a la fecha de fin ---
        var conceptos = await db.PayrollConceptDefinitions.AsNoTracking()
            .Where(c => c.IsActive && c.ValidFrom <= end && (c.ValidTo == null || c.ValidTo >= end))
            .ToListAsync(ct);
        var parametros = await db.PayrollLegalParameters.AsNoTracking()
            .Include(p => p.Ranges)
            .Where(p => p.ValidFrom <= end && (p.ValidTo == null || p.ValidTo >= end))
            .ToListAsync(ct);
        // La tabla de retención propia del plan (Parámetros de retención) reemplaza a la legal si el plan trae tramos.
        var tramosDelPlan = await db.WithholdingParameters.AsNoTracking()
            .Where(t => t.PayrollPlanId == plan.Id && !t.IsDeleted)
            .ToListAsync(ct);
        var parametrosDelPlan = TablaDeRetencionDelPlan.Aplicar(parametros, tramosDelPlan, plan, end);

        // --- retención y deducciones declaradas ---
        var tasas = await db.EmployeeWithholdingRates.AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && r.ValidFrom <= end && (r.ValidTo == null || r.ValidTo >= end))
            .ToListAsync(ct);
        var tasasPorEmpleado = tasas.GroupBy(r => r.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ValidFrom).First().RatePercent);
        var deducciones = await db.EmployeeTaxDeductions.AsNoTracking()
            .Where(d => ids.Contains(d.EmployeeId) && d.ValidFrom <= end && (d.ValidTo == null || d.ValidTo >= end))
            .ToListAsync(ct);
        var deduccionesPorEmpleado = deducciones.ToLookup(d => d.EmployeeId);

        // Las políticas rigen por vigencia: las del fin del período, no las de hoy (R4).
        var politicas = (await policies.ReadAsync(DateOnly.FromDateTime(end), ct)).ForCalculation();

        var cargados = new List<LoadedEmployee>(empleados.Count);
        foreach (var x in empleados)
        {
            var e = x.Employee;
            var nombre = $"{x.FirstName} {x.LastName}".Trim();

            var historial = cambiosPorEmpleado[e.Id]
                .Select(s => new SalaryChangeInput(s.EffectiveDate.Date, s.NewSalary))
                .ToList();
            // Sin un cambio con efecto antes del período, el salario de la ficha es el
            // vigente desde el ingreso. RegisterSalaryChange siembra la línea base al
            // registrar el primer cambio, así que esto sólo aplica a fichas sin historial.
            if (!historial.Any(h => h.EffectiveDate <= start))
                historial.Insert(0, new SalaryChangeInput(e.JoinDate.Date < start ? e.JoinDate.Date : start, e.Salary));

            var novs = novedadesPorEmpleado[e.Id]
                .Select(n => new NoveltyInput
                {
                    PublicId = n.Novelty.PublicId,
                    ConceptCode = n.Novelty.ConceptCode,
                    Quantity = n.Novelty.Quantity,
                    Amount = n.Novelty.Amount,
                    StartDate = n.Novelty.StartDate,
                    EndDate = n.Novelty.EndDate,
                    Origin = n.Novelty.Origin,
                    Description = string.IsNullOrWhiteSpace(n.Novelty.Notes) ? n.ConceptName : $"{n.ConceptName}: {n.Novelty.Notes}",
                })
                .ToList();
            novs.AddRange(carteraPorDocumento[x.TaxId].Select(d => new NoveltyInput
            {
                PublicId = d.PublicId,
                ConceptCode = WellKnownConceptCodes.LoanDeduction,
                Amount = d.Amount,
                Origin = NoveltyOrigin.LoanDeduction,
                Description = $"Cuota de crédito {d.ConceptCode} (Cartera)",
            }));

            var input = new EmployeeInput
            {
                PublicId = e.PublicId,
                DisplayName = nombre,
                Class = e.EmployeeClass,
                // Un cambio de plan con fecha de efecto dentro del período: el empleado
                // entra al plan desde esa fecha, nunca se le liquidan días dos veces.
                JoinDate = e.PayrollPlanEffectiveFrom is { } ef && ef > e.JoinDate ? ef.Date : e.JoinDate.Date,
                TerminationDate = FechaDeRetiro(e, end),
                SalaryHistory = historial,
                Affiliations = new AffiliationsInput
                {
                    Health = e.HealthInsuranceId > 0,
                    Pension = e.PensionFundId > 0,
                    WorkRiskClass = clasePorTarifa.TryGetValue(e.WorkRiskRateId, out var clase) && clase is >= 1 and <= 5 ? clase : null,
                    FamilyCompensation = e.FamilySubsidyId > 0,
                },
                WithholdingProcedure = e.WithholdingProcedure is 1 or 2 ? e.WithholdingProcedure : (byte)1,
                // Sin vigencia = nulo, no cero: cero se calcularía como una tasa válida del 0 %.
                WithholdingRatePercent = tasasPorEmpleado.TryGetValue(e.Id, out var tasa) ? tasa : null,
                TaxDeductions = deduccionesPorEmpleado[e.Id]
                    .Select(d => new TaxDeductionInput(d.Kind, d.MonthlyAmount, d.Percent))
                    .ToList(),
            };

            cargados.Add(new LoadedEmployee(e, nombre, x.TaxId, x.Email, input, novs));
        }

        return new PayrollCalculationBatch
        {
            Period = period,
            Plan = plan,
            PeriodInput = periodInput,
            Employees = cargados,
            Concepts = conceptos,
            Parameters = parametrosDelPlan,
            Policies = politicas,
            NoveltyIds = noveltyIds,
        };
    }

    /// <summary>
    /// La fecha de retiro de la ficha cuando cae antes del fin del período (una ficha cerrada por el camino
    /// anterior, sin definitiva): el motor liquida por días hasta ella. Con definitiva aprobada el empleado
    /// ya no llega aquí (D-28). Nula si el retiro no cae antes del fin.
    /// </summary>
    private static DateTime? FechaDeRetiro(Employee e, DateTime end) =>
        e.TerminationDate < end ? e.TerminationDate.Date : null;
}
