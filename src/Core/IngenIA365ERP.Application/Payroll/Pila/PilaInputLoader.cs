using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Dispersion;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Pila;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Pila;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Pila;

/// <summary>Qué concepto de novedad de la nómina produce cada novedad PILA. Son códigos de la semilla, no valores legales.</summary>
public static class PilaNoveltyMap
{
    public static PilaNoveltyKind? Kind(string conceptCode) => conceptCode.ToUpperInvariant() switch
    {
        WellKnownConceptCodes.GeneralSickLeave => PilaNoveltyKind.IGE,
        "INCAP_LABORAL" => PilaNoveltyKind.IRL,
        "LIC_MATERNIDAD" => PilaNoveltyKind.LMA,
        WellKnownConceptCodes.Vacation or WellKnownConceptCodes.VacationLeave or "LIC_REMUNERADA" => PilaNoveltyKind.VAC,
        "LIC_NO_REMUNERADA" or "SUSPENSION" => PilaNoveltyKind.SLN,
        _ => null,
    };

    /// <summary>Aporte voluntario del afiliado a pensión obligatoria (campo 48), si la cooperativa creó el concepto.</summary>
    public const string VoluntaryPensionEmployee = "AVP_EMPLEADO";
}

/// <summary>Lo que el loader entrega además del <see cref="PilaInput"/>: las corridas fuente y los aportes que la nómina ya liquidó por subsistema (para el cuadre, FR-027).</summary>
public sealed record PilaLoad(
    PilaInput Input,
    PayrollLegalParameter? TransitionSolidarityTable,
    IReadOnlyList<PilaSourceRunDto> Sources,
    IReadOnlyDictionary<string, decimal> LedgerBySubsystem,
    IReadOnlyList<PilaIssueItem> LoaderIssues,
    PilaSettings? Settings,
    string? CompanyNit);

/// <summary>
/// Carga todo lo que la PILA de un mes necesita (feature 010, US5; research R9): la empresa y
/// los datos del aportante, las políticas y los parámetros vigentes al primer día, el layout, y
/// por cada empleado liquidado en el mes su persona, sus catálogos con código PILA, su clase
/// ARL, su salario vigente, las novedades con fechas y lo que las corridas aprobadas dicen
/// (devengos que forman IBC, días, aporte voluntario). El cuadre (FR-027) compara los aportes del
/// archivo con los conceptos de aportes de esas mismas corridas: son las líneas que alimentaron
/// los comprobantes <c>NM</c> del mes.
/// </summary>
public sealed class PilaInputLoader(IApplicationDbContext db, PayrollPolicyReader policies)
{
    public static readonly IReadOnlyList<string> Subsystems = ["Pension", "Health", "Fsp", "Arl", "Ccf", "Sena", "Icbf"];

    public async Task<Result<PilaLoad>> LoadAsync(short year, byte month, CancellationToken ct)
    {
        var inicio = new DateTime(year, month, 1);
        var fin = inicio.AddMonths(1).AddDays(-1);
        var primerDia = DateOnly.FromDateTime(inicio);

        var layout = PilaLayoutCatalog.ForPeriod(primerDia);
        if (layout is null) return Result.Failure<PilaLoad>(PilaErrors.LayoutMissing);

        var empresa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        var settings = await db.PilaSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct);

        var politicas = await policies.ReadAsync(primerDia, ct);

        var parametros = await db.PayrollLegalParameters.AsNoTracking().Include(p => p.Ranges)
            .Where(p => p.Code == PilaParameterCodes.SolidarityFundTable || (p.ValidFrom <= inicio && (p.ValidTo == null || p.ValidTo >= inicio)))
            .ToListAsync(ct);
        var vigentes = parametros.Where(p => p.ValidFrom <= inicio && (p.ValidTo == null || p.ValidTo >= inicio)).ToList();
        var set = new ParameterSet(vigentes, inicio);
        // La tabla del fondo de solidaridad que regía antes del cambio (Ley 797), para quien está en régimen de transición.
        var fspVigente = vigentes.Where(p => p.Code == PilaParameterCodes.SolidarityFundTable).OrderByDescending(p => p.ValidFrom).FirstOrDefault();
        var fspTransicion = fspVigente is null ? null
            : parametros.Where(p => p.Code == PilaParameterCodes.SolidarityFundTable && p.ValidFrom < fspVigente.ValidFrom).OrderByDescending(p => p.ValidFrom).FirstOrDefault();

        // --- corridas aprobadas imputadas al mes: ordinarias por su período; vacaciones y definitivas por la fecha de corte ---
        var ordinarias = await (
            from r in db.PayrollRuns.AsNoTracking()
            join p in db.PayPeriods.AsNoTracking() on r.PayPeriodId equals p.Id
            where r.Kind == PayrollRunKind.Ordinary && r.Status == PayrollRunStatus.Approved && p.ImputationYear == year && p.ImputationMonth == month
            select new { Run = r, Period = p }).ToListAsync(ct);
        var especiales = await db.PayrollRuns.AsNoTracking()
            .Where(r => r.Status == PayrollRunStatus.Approved && (r.Kind == PayrollRunKind.Vacation || r.Kind == PayrollRunKind.Settlement)
                        && r.CutoffDate != null && r.CutoffDate >= primerDia && r.CutoffDate <= DateOnly.FromDateTime(fin))
            .ToListAsync(ct);
        var runs = ordinarias.Select(x => x.Run).Concat(especiales).ToList();
        if (runs.Count == 0) return Result.Failure<PilaLoad>(PilaErrors.NoApprovedRuns);
        var runIds = runs.Select(r => r.Id).ToList();
        var fuentes = new List<PilaSourceRunDto>();
        foreach (var x in ordinarias)
            fuentes.Add(new PilaSourceRunDto(x.Run.PublicId, x.Run.Kind.ToString(), x.Run.Status.ToString(), x.Run.Version,
                $"Nómina {x.Period.StartDate:yyyy-MM} {(x.Period.Periodicity is { } per ? PeriodCalendar.Etiqueta((PayrollPeriodicity)per, x.Period.SubPeriodNumber) : x.Period.Description)}".Trim()));
        foreach (var r in especiales)
            fuentes.Add(new PilaSourceRunDto(r.PublicId, r.Kind.ToString(), r.Status.ToString(), r.Version, SettlementLabels.Etiqueta(r)));

        // --- empleados liquidados y sus líneas ---
        var runEmployees = await db.PayrollRunEmployees.AsNoTracking().Where(re => runIds.Contains(re.PayrollRunId)).ToListAsync(ct);
        var reIds = runEmployees.Select(re => re.Id).ToList();
        var lineas = await (
            from l in db.PayrollRunLines.AsNoTracking()
            join c in db.PayrollConceptDefinitions.AsNoTracking() on l.ConceptDefinitionId equals c.Id
            where reIds.Contains(l.PayrollRunEmployeeId)
            select new LineaDeCorrida(l.PayrollRunEmployeeId, l.ConceptCode, l.Nature, l.Amount, c.AffectsContributionBase)).ToListAsync(ct);
        var reIndex = runEmployees.ToDictionary(re => re.Id);
        var runIndex = runs.ToDictionary(r => r.Id);
        var lineasPorEmpleado = lineas.ToLookup(l => reIndex[l.PayrollRunEmployeeId].EmployeeId);
        var diasPorEmpleado = runEmployees.Where(re => runIndex[re.PayrollRunId].Kind == PayrollRunKind.Ordinary)
            .GroupBy(re => re.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(re => re.DaysWorked));
        var ids = runEmployees.Select(re => re.EmployeeId).Distinct().ToList();

        var empleados = await (
            from e in db.Employees.AsNoTracking()
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where ids.Contains(e.Id)
            select new { Employee = e, Person = p }).ToListAsync(ct);

        var eps = await db.HealthInsuranceProviders.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        var afp = await db.PensionProviders.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        var arl = await db.WorkRiskProviders.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        var ccf = await db.FamilyCompensationFunds.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        var clases = await db.WorkRiskRates.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code, ct);

        var cambios = await db.SalaryChanges.AsNoTracking().Where(s => ids.Contains(s.EmployeeId) && s.EffectiveDate <= fin).OrderBy(s => s.EffectiveDate).ToListAsync(ct);
        var cambiosPorEmpleado = cambios.ToLookup(s => s.EmployeeId);

        var novedades = await db.PayrollNovelties.AsNoTracking()
            .Where(n => ids.Contains(n.EmployeeId) && n.Status == NoveltyStatus.Active && n.StartDate != null && n.EndDate != null && n.StartDate <= fin && n.EndDate >= inicio)
            .ToListAsync(ct);
        var novedadesPorEmpleado = novedades.ToLookup(n => n.EmployeeId);

        var retiros = await db.EmploymentTerminations.AsNoTracking()
            .Where(t => ids.Contains(t.EmployeeId) && t.Status == TerminationStatus.Settled)
            .GroupBy(t => t.EmployeeId).Select(g => new { EmployeeId = g.Key, Fecha = g.Max(t => t.TerminationDate) })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Fecha.ToDateTime(TimeOnly.MinValue), ct);

        var cotizantes = new List<PilaContributor>();
        var libro = Subsystems.ToDictionary(s => s, _ => 0m);
        foreach (var x in empleados)
        {
            var e = x.Employee; var p = x.Person;
            var propias = lineasPorEmpleado[e.Id].ToList();
            var ibcDevengos = propias.Where(l => l.Nature == ConceptNature.Earning && l.AffectsContributionBase && !l.ConceptCode.Equals(WellKnownConceptCodes.TransportAllowance, StringComparison.OrdinalIgnoreCase)).Sum(l => l.Amount);
            var avp = propias.Where(l => l.ConceptCode.Equals(PilaNoveltyMap.VoluntaryPensionEmployee, StringComparison.OrdinalIgnoreCase)).Sum(l => l.Amount);

            libro["Health"] += Suma(propias, WellKnownConceptCodes.HealthEmployee, WellKnownConceptCodes.HealthEmployer, "SALUD_APRENDIZ");
            libro["Pension"] += Suma(propias, WellKnownConceptCodes.PensionEmployee, WellKnownConceptCodes.PensionEmployer);
            libro["Fsp"] += Suma(propias, WellKnownConceptCodes.SolidarityFund);
            libro["Arl"] += Suma(propias, WellKnownConceptCodes.WorkRisk);
            libro["Ccf"] += Suma(propias, WellKnownConceptCodes.FamilyCompensation);
            libro["Sena"] += Suma(propias, WellKnownConceptCodes.Sena);
            libro["Icbf"] += Suma(propias, WellKnownConceptCodes.Icbf);

            var historial = cambiosPorEmpleado[e.Id].ToList();
            var salario = historial.LastOrDefault(s => s.EffectiveDate <= fin)?.NewSalary ?? e.Salary;
            var cambioEnMes = historial.Where(s => s.EffectiveDate >= inicio && s.EffectiveDate <= fin && s.EffectiveDate.Date != e.JoinDate.Date).Select(s => (DateTime?)s.EffectiveDate).LastOrDefault();

            DateTime? retiro = null;
            if (retiros.TryGetValue(e.Id, out var liquidado) && liquidado <= fin) retiro = liquidado.Date;
            else if (e.TerminationDate.Date <= fin && e.TerminationDate.Year > 1900) retiro = e.TerminationDate.Date;

            var pilaNovedades = novedadesPorEmpleado[e.Id]
                .Select(n => (Kind: PilaNoveltyMap.Kind(n.ConceptCode), N: n))
                .Where(t => t.Kind is not null)
                .Select(t => new PilaNovelty(t.Kind!.Value, t.N.StartDate!.Value.Date, t.N.EndDate!.Value.Date, t.N.Notes is { Length: > 0 and <= 15 } ? t.N.Notes : null, t.N.PublicId))
                .ToList();

            eps.TryGetValue(e.HealthInsuranceId, out var miEps); afp.TryGetValue(e.PensionFundId, out var miAfp);
            arl.TryGetValue(e.WorkRiskId, out var miArl); ccf.TryGetValue(e.FamilySubsidyId, out var miCcf);

            cotizantes.Add(new PilaContributor
            {
                EmployeeId = e.Id, EmployeePublicId = e.PublicId,
                DocumentType = PayrollDisbursementLines.TipoDeDocumento(p.IdType), Document = p.TaxId.Trim(),
                FirstLastName = p.LastName.Trim(), SecondLastName = Vacio(p.SecondLastName), FirstName = p.FirstName.Trim(), OtherNames = Vacio(p.OtherNames),
                Class = e.EmployeeClass, ApprenticeStage = e.ApprenticeStage,
                ContributorTypeOverride = Vacio(e.PilaContributorType), ContributorSubTypeOverride = Vacio(e.PilaContributorSubType),
                ForeignNotRequiredToContributePension = e.ForeignNotRequiredToContributePension, ColombianAbroad = e.ColombianAbroad,
                MunicipalityDaneCode = Vacio(e.WorkMunicipalityDaneCode), EconomicActivityCode = Vacio(e.EconomicActivityCode), WorkCenterCode = Vacio(e.WorkCenterCode),
                HighRiskPension = e.HighRiskPension, TransitionRegime = e.PensionTransitionRegime,
                HireDate = e.JoinDate.Date, TerminationDate = retiro,
                BasicSalary = salario, SalaryChangeDate = cambioEnMes,
                HasHealthProvider = miEps is not null, HealthPilaCode = Vacio(miEps?.PilaCode),
                HasPensionProvider = miAfp is not null, PensionPilaCode = Vacio(miAfp?.PilaCode),
                HasWorkRiskProvider = miArl is not null, WorkRiskPilaCode = Vacio(miArl?.PilaCode),
                HasFamilyCompensationFund = miCcf is not null, FamilyCompensationPilaCode = Vacio(miCcf?.PilaCode),
                WorkRiskClass = clases.TryGetValue(e.WorkRiskRateId, out var clase) ? clase : null,
                ContributionEarnings = ibcDevengos, DaysWorked = diasPorEmpleado.GetValueOrDefault(e.Id),
                VoluntaryPensionEmployee = avp, Novelties = pilaNovedades,
            });
        }

        // Cotizante activo sin nómina en el mes: el operador lo espera con RET si se fue, o con su línea si sigue.
        var avisos = new List<PilaIssueItem>();
        var sinNomina = await (
            from e in db.Employees.AsNoTracking()
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where e.Status == 1 && !ids.Contains(e.Id) && e.JoinDate <= fin && e.TerminationDate >= inicio
            select new { e.Id, e.PublicId, p.FirstName, p.LastName }).ToListAsync(ct);
        foreach (var s in sinNomina)
            avisos.Add(new PilaIssueItem(PilaIssueSeverity.Warning, "Pila.EmpleadoSinNomina",
                $"{s.FirstName} {s.LastName}: está activo y no tiene nómina aprobada imputada a {year}-{month:00}; no va en la planilla.",
                null, s.Id, s.PublicId, $"{s.FirstName} {s.LastName}", $"/nomina/empleados/{s.PublicId}"));

        var employer = new PilaEmployer(
            empresa?.Name?.Trim() ?? string.Empty, empresa?.TaxId?.Trim() ?? string.Empty, empresa?.TaxIdCheckDigit?.Trim() ?? string.Empty,
            settings?.ContributorType ?? "1", settings?.ContributorClass ?? "B", settings?.PresentationForm ?? "U",
            Vacio(settings?.BranchCode), Vacio(settings?.BranchName), Vacio(settings?.ArlPilaCode), Vacio(settings?.OperatorCode),
            settings?.PlanillaType ?? "E", Vacio(settings?.MunicipalityDaneCode), Vacio(settings?.EconomicActivityCode));

        var input = new PilaInput(year, month, employer, layout, set, new PilaPolicies(politicas.Exonerada114_1, politicas.CotizaArlEnVacaciones), cotizantes);
        return Result.Success(new PilaLoad(input, fspTransicion, fuentes, libro, avisos, settings, empresa?.TaxId));
    }

    private sealed record LineaDeCorrida(int PayrollRunEmployeeId, string ConceptCode, ConceptNature Nature, decimal Amount, bool AffectsContributionBase);

    private static decimal Suma(IEnumerable<LineaDeCorrida> lineas, params string[] codigos)
    {
        var set = new HashSet<string>(codigos, StringComparer.OrdinalIgnoreCase);
        return lineas.Where(l => set.Contains(l.ConceptCode)).Sum(l => l.Amount);
    }

    private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
