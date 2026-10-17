using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports;

/// <summary>Una línea de cierre como la leen los informes de K5: el crédito, su capital, sus días de mora y su calificación oficial. (nuevo)</summary>
public sealed record LineaDeCierreParaInforme(
    int LoanId,
    int BranchId,
    int CreditLineId,
    string ModalityCode,
    string? SubsegmentCode,
    bool IsWrittenOff,
    int DaysPastDue,
    RatingCategory FinalRating,
    decimal PrincipalBalance,
    decimal TotalImpairment);

/// <summary>
/// Los cierres de Cartera tal como los leen los informes de K5 (feature 013, US15, T0981; FR-134): la calidad y el deterioro salen del
/// <b>último cierre vigente</b> a una fecha —el mensual no reabierto más reciente o, antes del primero, el cierre base de la activación—, nunca de la
/// calificación diaria; la cosecha toma el cierre vigente <b>de cada mes</b>. Un cierre reabierto no cuenta (su fila en
/// <c>LND_MonthlyCloseReopenings</c>); de dos del mismo mes manda el mensual de mayor versión sobre el base. Sólo lee. (nuevo)
/// </summary>
public static class CierresParaInformes
{
    /// <summary>El último cierre vigente con fecha de cierre hasta <paramref name="corte"/> (incluido), o nulo si todavía no hay ninguno.</summary>
    public static Task<MonthlyClose?> AlCorteAsync(IApplicationDbContext db, DateOnly corte, CancellationToken ct) =>
        CategoriaContableDeCartera.UltimoCierreVigenteAsync(db, corte.AddDays(1), ct);

    /// <summary>El cierre vigente anterior a <paramref name="cierre"/> (el que le sirve de comparación), o nulo.</summary>
    public static Task<MonthlyClose?> AnteriorAsync(IApplicationDbContext db, MonthlyClose cierre, CancellationToken ct) =>
        CategoriaContableDeCartera.UltimoCierreVigenteAsync(db, cierre.CloseDate, ct);

    /// <summary>El cierre vigente de cada mes con fecha de cierre hasta <paramref name="hasta"/>, por (año, mes) de la fecha de cierre.</summary>
    public static async Task<IReadOnlyDictionary<(int Anio, int Mes), MonthlyClose>> PorMesAsync(IApplicationDbContext db, DateOnly hasta, CancellationToken ct)
    {
        var cierres = await db.MonthlyCloses.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CloseDate <= hasta && !db.MonthlyCloseReopenings.Any(r => r.MonthlyCloseId == c.Id && !r.IsDeleted))
            .ToListAsync(ct);
        return cierres.GroupBy(c => (c.CloseDate.Year, c.CloseDate.Month))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Kind).ThenByDescending(c => c.Version).ThenByDescending(c => c.CloseDate).First());
    }

    /// <summary>Las líneas de un cierre de los créditos de <paramref name="creditos"/> (la consulta del alcance, sin materializar).</summary>
    public static Task<List<LineaDeCierreParaInforme>> LineasAsync(IApplicationDbContext db, MonthlyClose cierre, IQueryable<int> creditos, CancellationToken ct) =>
        Proyectar(CategoriaContableDeCartera.LineasDe(db, cierre).Where(l => creditos.Contains(l.LoanId))).ToListAsync(ct);

    /// <summary>
    /// Las líneas de varios cierres a la vez para los créditos <paramref name="creditos"/>, por el <c>Id</c> del cierre al que pertenecen (las de un
    /// mensual se buscan por su corrida, las del base por el cierre).
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, List<LineaDeCierreParaInforme>>> LineasDeVariosAsync(IApplicationDbContext db,
        IReadOnlyCollection<MonthlyClose> cierres, IReadOnlyCollection<int> creditos, CancellationToken ct)
    {
        var resultado = cierres.ToDictionary(c => c.Id, _ => new List<LineaDeCierreParaInforme>());
        if (cierres.Count == 0 || creditos.Count == 0) return resultado;
        var porCorrida = cierres.Where(c => c.Kind == MonthlyCloseKind.Monthly && c.ProcessRunId is not null).ToDictionary(c => c.ProcessRunId!.Value, c => c.Id);
        var porCierre = cierres.Where(c => !(c.Kind == MonthlyCloseKind.Monthly && c.ProcessRunId is not null)).Select(c => c.Id).ToHashSet();
        var corridas = porCorrida.Keys.ToArray();
        var directos = porCierre.ToArray();
        var ids = creditos.ToArray();
        var filas = await db.MonthlyCloseLoans.AsNoTracking()
            .Where(l => ids.Contains(l.LoanId)
                        && ((l.ProcessRunId != null && corridas.Contains(l.ProcessRunId.Value)) || (l.MonthlyCloseId != null && directos.Contains(l.MonthlyCloseId.Value))))
            .Select(l => new { l.ProcessRunId, l.MonthlyCloseId, Linea = new LineaDeCierreParaInforme(l.LoanId, l.BranchId, l.CreditLineId, l.ModalityCode,
                l.SubsegmentCode, l.IsWrittenOff, l.DaysPastDueAtClose, l.FinalRating, l.PrincipalBalance, l.TotalImpairment) })
            .ToListAsync(ct);
        foreach (var f in filas)
        {
            if (f.ProcessRunId is { } corrida && porCorrida.TryGetValue(corrida, out var cierre)) resultado[cierre].Add(f.Linea);
            else if (f.MonthlyCloseId is { } id && porCierre.Contains(id)) resultado[id].Add(f.Linea);
        }
        return resultado;
    }

    /// <summary>Cómo se nombra un cierre en las notas: «el cierre de 2026-05 (versión 1, al 2026-05-31)» o «el cierre base al …».</summary>
    public static string Nombre(MonthlyClose cierre) => cierre.Kind == MonthlyCloseKind.Base
        ? $"el cierre base al {cierre.CloseDate:yyyy-MM-dd}"
        : $"el cierre de {cierre.PeriodYear:D4}-{cierre.PeriodMonth:D2} (versión {cierre.Version}, al {cierre.CloseDate:yyyy-MM-dd})";

    private static IQueryable<LineaDeCierreParaInforme> Proyectar(IQueryable<MonthlyCloseLoan> lineas) =>
        lineas.Select(l => new LineaDeCierreParaInforme(l.LoanId, l.BranchId, l.CreditLineId, l.ModalityCode, l.SubsegmentCode, l.IsWrittenOff,
            l.DaysPastDueAtClose, l.FinalRating, l.PrincipalBalance, l.TotalImpairment));
}
