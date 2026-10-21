using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>Una fila de una carga complementaria del historial (<c>LND_ArrearsHistoryLoads.RowsJson</c>). (nuevo)</summary>
public sealed record FilaDeHistorialComplementario(string LoanNumber, int Year, int Month, int MaxDaysPastDue, int? DaysPastDueAtMonthEnd);

/// <summary>Un crédito migrado sin el historial completo: número, segmento y meses que faltan (AAAA-MM). (nuevo)</summary>
public sealed record CreditoMigradoIncompletoDto(string LoanNumber, string Segment, IReadOnlyList<string> MissingMonths);

/// <summary>La completitud del historial de los créditos migrados de un segmento cubierto (plantillas.md §7, <c>extra.completeness[]</c>). (nuevo)</summary>
public sealed record CoberturaDeSegmentoDto(string Segment, int Loans, int Complete, IReadOnlyList<CreditoMigradoIncompletoDto> Incomplete);

/// <summary>
/// La completitud del historial de mora de los créditos de una carga de migración por la <b>preparación</b> (feature 013, K6a, T1035, T1037; FR-105,
/// FR-140, US8-10): sirve antes de activar, cuando los créditos todavía no existen, y después (las filas de la preparación siguen). Por cada crédito
/// de <c>loans</c> de un segmento cubierto por la <c>ReglaDePerdidaEsperada</c> (la modalidad de su línea vigente al corte y el catálogo de
/// modalidades), los meses que pide la regla hacia atrás desde la fecha de referencia (la del primer cierre en pérdida esperada) o desde su desembolso
/// si es más joven, <b>anteriores al mes del corte</b> —el mes del corte lo cubre la mora migrada al corte y los siguientes las transacciones, como
/// en <see cref="CompletitudDelHistorial.CorteParaHistorial"/>—, contra los meses del historial migrado, de las cargas complementarias de esa carga
/// y de las filas que se están revisando. Sólo lee. (nuevo)
/// </summary>
public sealed class CoberturaDelHistorialMigrado(IApplicationDbContext db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Las filas guardadas de una carga complementaria.</summary>
    public static IReadOnlyList<FilaDeHistorialComplementario> Filas(string rowsJson) =>
        JsonSerializer.Deserialize<List<FilaDeHistorialComplementario>>(rowsJson, Json) ?? [];

    public static string Serializar(IReadOnlyList<FilaDeHistorialComplementario> filas) => JsonSerializer.Serialize(filas, Json);

    public async Task<IReadOnlyList<CoberturaDeSegmentoDto>> CalcularAsync(MigrationLoad carga, ReglaDePerdidaEsperada regla, CatalogoDeModalidades catalogo,
        DateOnly referencia, IReadOnlyCollection<FilaDeHistorialComplementario> adicionales, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(carga);
        ArgumentNullException.ThrowIfNull(regla);
        ArgumentNullException.ThrowIfNull(catalogo);
        var partes = await db.MigrationLoadParts.AsNoTracking()
            .Where(p => p.MigrationLoadId == carga.Id && p.Status == MigrationPartStatus.Imported && !p.IsDeleted).Select(p => p.Id).ToListAsync(ct);
        var creditos = await db.MigrationLoanRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId))
            .Select(r => new { r.SourceLoanNumber, r.CreditLineCode, r.DisbursementDate }).ToListAsync(ct);
        if (creditos.Count == 0) return [];

        var codigos = creditos.Select(c => c.CreditLineCode).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().Where(l => codigos.Contains(l.Code)).Select(l => new { l.Id, l.Code }).ToListAsync(ct);
        var versiones = await new Configuration.VersionVigenteDeLinea(db).LeerVariasAsync(lineas.Select(l => l.Id).ToList(), carga.CutoffDate, ct: ct);
        var segmentoDeLinea = lineas.ToDictionary(l => l.Code, l => versiones.TryGetValue(l.Id, out var v)
            ? catalogo.Buscar(v.Version.ModalityCode, v.Version.SubsegmentCode)?.ExpectedLossSegment
            : null, StringComparer.OrdinalIgnoreCase);

        var meses = (await db.MigrationArrearsHistoryRows.AsNoTracking().Where(h => partes.Contains(h.MigrationLoadPartId))
                .Select(h => new { h.SourceLoanNumber, h.PeriodYear, h.PeriodMonth }).ToListAsync(ct))
            .Select(h => (h.SourceLoanNumber, (int)h.PeriodYear, (int)h.PeriodMonth)).ToHashSet();
        foreach (var rows in await db.ArrearsHistoryLoads.AsNoTracking().Where(l => l.MigrationLoadId == carga.Id && !l.IsDeleted).Select(l => l.RowsJson)
                     .ToListAsync(ct))
            foreach (var f in Filas(rows)) meses.Add((f.LoanNumber, f.Year, f.Month));
        foreach (var f in adicionales) meses.Add((f.LoanNumber, f.Year, f.Month));

        var mesDelCorte = new DateOnly(carga.CutoffDate.Year, carga.CutoffDate.Month, 1);
        var inicioDeLaVentana = new DateOnly(referencia.Year, referencia.Month, 1).AddMonths(-(regla.MesesDeHistorial - 1));
        var porSegmento = new Dictionary<string, (int Total, List<CreditoMigradoIncompletoDto> Incompletos)>(StringComparer.Ordinal);
        foreach (var c in creditos.OrderBy(c => c.SourceLoanNumber, StringComparer.Ordinal))
        {
            var segmento = segmentoDeLinea.GetValueOrDefault(c.CreditLineCode);
            if (segmento is null || !regla.SegmentosCubiertos.Contains(segmento, StringComparer.Ordinal)) continue;
            if (!porSegmento.TryGetValue(segmento, out var grupo)) porSegmento[segmento] = grupo = (0, []);
            var desde = new DateOnly(c.DisbursementDate.Year, c.DisbursementDate.Month, 1);
            if (desde < inicioDeLaVentana) desde = inicioDeLaVentana;
            var faltan = new List<string>();
            for (var m = desde; m < mesDelCorte; m = m.AddMonths(1))
                if (!meses.Contains((c.SourceLoanNumber, m.Year, m.Month))) faltan.Add(string.Create(CultureInfo.InvariantCulture, $"{m.Year:0000}-{m.Month:00}"));
            if (faltan.Count > 0) grupo.Incompletos.Add(new CreditoMigradoIncompletoDto(c.SourceLoanNumber, segmento, faltan));
            porSegmento[segmento] = (grupo.Total + 1, grupo.Incompletos);
        }
        return porSegmento.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new CoberturaDeSegmentoDto(x.Key, x.Value.Total, x.Value.Total - x.Value.Incompletos.Count, x.Value.Incompletos)).ToList();
    }

    /// <summary>
    /// La fecha de referencia del historial de una carga: el último día del mes de la activación (corte + 1), el del primer cierre mensual que la usa
    /// si la cooperativa está obligada desde la activación.
    /// </summary>
    public static DateOnly ReferenciaDeLaCarga(MigrationLoad carga)
    {
        var activacion = carga.CutoffDate.AddDays(1);
        return Domain.Entities.Lending.Risk.MonthlyClose.UltimoDiaDe(activacion.Year, activacion.Month);
    }
}
