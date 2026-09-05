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
/// inicio. El retiro dentro del período se liquida por los días hasta el retiro.
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

        var ids = empleados.Select(x => x.Employee.Id).ToList();
        var documentos = empleados.Select(x => x.TaxId).ToList();

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

        var politicas = (await policies.ReadAsync(ct)).ForCalculation();

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
                TerminationDate = e.TerminationDate < end ? e.TerminationDate.Date : null,
                SalaryHistory = historial,
                Affiliations = new AffiliationsInput
                {
                    Health = e.HealthInsuranceId > 0,
                    Pension = e.PensionFundId > 0,
                    WorkRiskClass = e.WorkRiskRateId is >= 1 and <= 5 ? e.WorkRiskRateId : null,
                    FamilyCompensation = e.FamilySubsidyId > 0,
                },
                WithholdingProcedure = e.WithholdingProcedure is 1 or 2 ? e.WithholdingProcedure : (byte)1,
                WithholdingRatePercent = tasasPorEmpleado.GetValueOrDefault(e.Id),
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
            Parameters = parametros,
            Policies = politicas,
            NoveltyIds = noveltyIds,
        };
    }
}
