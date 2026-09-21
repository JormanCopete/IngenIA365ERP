using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Vacations;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;
using IngenIA365ERP.Domain.Payroll.Pila;
using IngenIA365ERP.Domain.Payroll.Settlements.Calendar;

namespace IngenIA365ERP.Application.Payroll.Pila;

/// <summary>Lo que validar y generar comparten: la carga, el motor, el cuadre y la fecha límite.</summary>
public sealed record PilaPreparada(PilaLoad Load, PilaResult Result, PilaReconciliationDto Reconciliation, IReadOnlyList<PilaIssueDto> Issues, DateOnly? DueDate)
{
    public bool HasBlocking => Issues.Any(i => i.Severity == PilaIssueSeverity.Blocking);
    public int Blocking => Issues.Count(i => i.Severity == PilaIssueSeverity.Blocking);
    public int Warnings => Issues.Count(i => i.Severity == PilaIssueSeverity.Warning);
    public PilaTotalsDto Totals => Totales(Result);

    public static PilaTotalsDto Totales(PilaResult r) =>
        new(r.TotalPension, r.TotalHealth, r.TotalWorkRisk, r.TotalFamilyCompensation, r.TotalSena, r.TotalIcbf, r.TotalSolidarityFund, r.TotalContributions);
}

/// <summary>
/// Un solo camino para validar y generar (feature 010, US5): carga los insumos, corre el motor
/// puro, cuadra los aportes del archivo contra los que la nómina liquidó por subsistema (FR-027)
/// y propone la fecha límite de pago con <c>PILA_PLAZO_PAGO_POR_NIT</c> y los días hábiles.
/// </summary>
public sealed class PreparacionDePila(IApplicationDbContext db, PilaInputLoader loader)
{
    public async Task<Result<PilaPreparada>> PrepararAsync(short year, byte month, CancellationToken ct)
    {
        var carga = await loader.LoadAsync(year, month, ct);
        if (carga.IsFailure) return Result.Failure<PilaPreparada>(carga.Error);
        var load = carga.Value;

        var result = PilaBuilder.Build(load.Input, load.TransitionSolidarityTable);
        result.Issues.AddRange(load.LoaderIssues);

        var cuadre = Cuadre(result, load.LedgerBySubsystem);
        var issues = result.Issues
            .OrderBy(i => i.Severity).ThenBy(i => i.EmployeeName).ThenBy(i => i.Field)
            .Select(i => new PilaIssueDto(i.Severity, i.Code, i.Field, i.Message, i.EmployeePublicId, i.EmployeeName, i.LinkRoute))
            .ToList();
        var vence = await FechaLimiteAsync(year, month, load.CompanyNit, load.Input.Parameters, ct);
        return Result.Success(new PilaPreparada(load, result, cuadre, issues, vence?.DueDate));
    }

    /// <summary>Σ campos 47/55/(51+52)/63/65/67/69 del archivo contra los conceptos de aportes de las corridas aprobadas del mes.</summary>
    public static PilaReconciliationDto Cuadre(PilaResult r, IReadOnlyDictionary<string, decimal> libro)
    {
        var filas = new List<PilaReconciliationRowDto>
        {
            Fila("Pension", r.TotalPension, libro), Fila("Health", r.TotalHealth, libro), Fila("Fsp", r.TotalSolidarityFund, libro),
            Fila("Arl", r.TotalWorkRisk, libro), Fila("Ccf", r.TotalFamilyCompensation, libro), Fila("Sena", r.TotalSena, libro), Fila("Icbf", r.TotalIcbf, libro),
        };
        var balanced = filas.All(f => f.Difference == 0m);
        return new PilaReconciliationDto(filas, balanced, balanced ? null
            : "La diferencia sale del redondeo al múltiplo de 100 del anexo, de aportes que la nómina liquidó sobre otra base o de novedades sin fechas; según el anexo v30 el fondo de solidaridad lo liquida el operador.");
    }

    private static PilaReconciliationRowDto Fila(string subsistema, decimal archivo, IReadOnlyDictionary<string, decimal> libro)
    {
        var contable = libro.GetValueOrDefault(subsistema);
        return new PilaReconciliationRowDto(subsistema, archivo, contable, archivo - contable);
    }

    /// <summary>Día hábil del mes siguiente según los dos últimos dígitos del NIT (Decreto 780/2016 art. 3.2.2.1), o nulo sin tabla.</summary>
    public async Task<PilaDueDateDto?> FechaLimiteAsync(short year, byte month, string? nit, ParameterSet parametros, CancellationToken ct)
    {
        var digitos = Digitos(nit);
        if (!parametros.Has(PilaParameterCodes.PaymentDeadlineByNitTable))
            return new PilaDueDateDto(null, PilaParameterCodes.PaymentDeadlineByNitTable, digitos, "Sin la tabla PILA_PLAZO_PAGO_POR_NIT vigente no se propone fecha.");
        if (!int.TryParse(digitos, out var dd))
            return new PilaDueDateDto(null, PilaParameterCodes.PaymentDeadlineByNitTable, digitos, "La empresa no tiene NIT: no se propone fecha.");
        var tramo = RangeTableLookup.Find(PilaParameterCodes.PaymentDeadlineByNitTable, dd, parametros);
        if (tramo.Range?.FixedValue is not { } habiles || habiles <= 0m)
            return new PilaDueDateDto(null, PilaParameterCodes.PaymentDeadlineByNitTable, digitos, "Los dos últimos dígitos del NIT no caen en ningún tramo de la tabla.");
        // El plazo corre en el mes siguiente al período de pago de los sistemas distintos a salud.
        var inicio = new DateTime(year, month, 1).AddMonths(1);
        var festivos = await VacationQueriesSupport.FestivosAsync(db, DateOnly.FromDateTime(inicio), DateOnly.FromDateTime(inicio.AddMonths(1)), ct);
        var fecha = DiasHabiles.FinDeHabiles(inicio, (int)habiles, SemanaLaboral.LunesAViernes, festivos.Keys);
        return new PilaDueDateDto(DateOnly.FromDateTime(fecha), PilaParameterCodes.PaymentDeadlineByNitTable, digitos,
            $"Día hábil {habiles:0} del mes siguiente (de lunes a viernes, sin festivos), NIT terminado en {digitos}.");
    }

    public static string Digitos(string? nit)
    {
        var solo = new string((nit ?? string.Empty).Where(char.IsDigit).ToArray());
        return solo.Length >= 2 ? solo[^2..] : solo;
    }
}
