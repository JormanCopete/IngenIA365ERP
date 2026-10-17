using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>quality</c> de <c>/api/reports/lending</c> (feature 013, US15, K5, T0981; FR-134; contracts/api.md §17): los indicadores de calidad
/// de la cartera al corte (<c>asOf</c>), leídos del <b>último cierre vigente</b> hasta esa fecha (<see cref="CierresParaInformes"/>) —categorías
/// oficiales y deterioro del cierre, nunca la calificación diaria—, en total o abiertos por oficina, línea o modalidad (<c>groupBy</c>). Agregada:
/// sin datos del deudor. El alcance va en la consulta de créditos (<see cref="AmbitoDeInformeDeCartera"/>). (nuevo)
///
/// <para>Definiciones fijadas aquí, <b>a validar por la contadora</b> (T0977):</para>
/// <list type="bullet">
/// <item><b>Cartera bruta</b>: el saldo de capital de las líneas del cierre que no están castigadas.</item>
/// <item><b>Vencida</b>: el capital de las que tienen días de mora al cierre <b>mayores</b> que <c>Informes.DiasCarteraVencida</c> (vigente al corte).</item>
/// <item><b>En riesgo</b>: el capital de las calificadas B, C, D o E (la calificación final oficial del cierre).</item>
/// <item><b>Deterioro</b>: el individual de las líneas más el general del cierre de las oficinas y modalidades del alcance; el general no se reparte
/// por línea de crédito, así que con un filtro o un alcance por línea queda fuera (y lo dice la nota) y abierto por línea va en una fila aparte.</item>
/// <item><b>Cobertura</b>: deterioro sobre vencida.</item>
/// </list>
/// </summary>
public sealed record QualityReportQuery(FiltrosDeInformeDeCartera Filtros, string? GroupBy = null) : IRequest<Result<TablaExportable>>;

public sealed class QualityReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    ReglasDeParametrosDeCartera parametros,
    IDateTimeService reloj) : IRequestHandler<QualityReportQuery, Result<TablaExportable>>
{
    public const string Total = "total";
    public const string PorOficina = "office";
    public const string PorLinea = "line";
    public const string PorModalidad = "modality";

    public static readonly IReadOnlyList<string> Dimensiones = [Total, PorOficina, PorLinea, PorModalidad];

    /// <summary>La fila del deterioro general cuando se abre por línea: el general no tiene línea.</summary>
    public const string FilaDelGeneral = "Deterioro general (no se reparte por línea)";

    public static readonly VistaDeInformeDeCartera Vista = new(
        "quality", "Calidad de la cartera",
        "Cartera bruta, vencida, en riesgo (B a E), deterioro y cobertura del último cierre al corte, en total o por oficina, línea o modalidad.",
        "calidad-de-la-cartera", ["asOf", "branch", "line", "modality", "subsegment"], ["groupBy"], AvailableFrom: EntregaDeCartera.K5);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Dimensión"),
        new("Cartera bruta", TipoDeColumna.Moneda),
        new("Vencida", TipoDeColumna.Moneda),
        new("Vencida/bruta", TipoDeColumna.Porcentaje),
        new("En riesgo (B–E)", TipoDeColumna.Moneda),
        new("Riesgo/bruta", TipoDeColumna.Porcentaje),
        new("Deterioro", TipoDeColumna.Moneda),
        new("Cobertura", TipoDeColumna.Porcentaje),
    ];

    public async Task<Result<TablaExportable>> Handle(QualityReportQuery request, CancellationToken ct)
    {
        var dimension = FiltrosDeInformeDeCartera.Elegir("groupBy", request.GroupBy, Dimensiones, Total);
        if (dimension.IsFailure) return Result.Failure<TablaExportable>(dimension.Error);

        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        var dias = await DiasDeVencidaAsync(parametros, corte, ct);
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var subtitulo = $"Al {corte:yyyy-MM-dd} · {ambito.Descripcion()}";
        var notas = new List<string>
        {
            $"Vencida: capital con más de {dias} días de mora al cierre (Informes.DiasCarteraVencida). En riesgo: calificadas B a E. Cobertura: deterioro sobre vencida.",
            "Definiciones a validar por la contadora.",
        };

        var cierre = await CierresParaInformes.AlCorteAsync(db, corte, ct);
        if (cierre is null)
        {
            notas.Insert(0, "Todavía no hay un cierre de cartera vigente hasta el corte: los indicadores de calidad salen de los cierres.");
            return Result.Success(new TablaExportable("Calidad de la cartera", subtitulo, Columnas, [], null, notas));
        }

        var calidad = await CalidadAsync(db, ambito, cierre, ct);
        notas.Insert(0, $"Cifras de {CierresParaInformes.Nombre(cierre)}, con la calificación y el deterioro oficiales de ese cierre.");
        if (calidad.General is null)
            notas.Add("El deterioro general no se reparte por línea de crédito: con un filtro o un alcance por línea sólo se suma el individual.");

        var nombres = await NombresAsync(dimension.Value, calidad.Lineas, ct);
        var grupos = calidad.Lineas.GroupBy(l => nombres(l)).ToDictionary(g => g.Key, g => g.ToList());
        var generalPorGrupo = new Dictionary<string, decimal>();
        decimal generalSinLinea = 0m;
        foreach (var g in calidad.General ?? [])
        {
            if (dimension.Value == PorLinea) { generalSinLinea += g.Monto; continue; }
            var clave = dimension.Value switch
            {
                PorOficina => nombres(new LineaDeCierreParaInforme(0, g.BranchId, 0, g.ModalityCode, g.SubsegmentCode, false, 0, RatingCategory.A, 0m, 0m)),
                PorModalidad => g.ModalityCode,
                _ => "Total",
            };
            generalPorGrupo[clave] = generalPorGrupo.GetValueOrDefault(clave) + g.Monto;
        }

        var filas = grupos.Keys.Union(generalPorGrupo.Keys)
            .OrderBy(k => k, StringComparer.CurrentCulture)
            .Select(k => Fila(k, grupos.GetValueOrDefault(k) ?? [], generalPorGrupo.GetValueOrDefault(k), dias))
            .ToList();
        if (dimension.Value == PorLinea && generalSinLinea != 0m) filas.Add(Fila(FilaDelGeneral, [], generalSinLinea, dias));
        var totales = dimension.Value == Total ? null : Fila("Total", calidad.Lineas, (calidad.General ?? []).Sum(g => g.Monto), dias);
        return Result.Success(new TablaExportable("Calidad de la cartera", subtitulo, Columnas, filas, totales, notas));
    }

    /// <summary>Los días de mora desde los que la cartera cuenta como vencida, vigentes a la fecha (el defecto del catálogo si no se pudo leer).</summary>
    public static async Task<int> DiasDeVencidaAsync(ReglasDeParametrosDeCartera parametros, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.InformesDiasCarteraVencida, fecha, ct: ct);
        if (leido.IsSuccess && leido.Value.Valor is int n) return n;
        if (leido.IsSuccess && leido.Value.Valor is decimal d) return (int)d;
        return int.Parse(ParametrosDeCartera.Definiciones.First(x => x.Clave == ParametrosDeCartera.InformesDiasCarteraVencida).DefectoSeguro,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Un deterioro general del cierre por oficina y modalidad.</summary>
    public sealed record DeterioroGeneralDelCierre(int BranchId, string ModalityCode, string? SubsegmentCode, decimal Monto);

    /// <summary>Lo que el cierre dice del alcance: sus líneas sin castigar (de la modalidad pedida) y el deterioro general atribuible (nulo si no lo es).</summary>
    public sealed record CalidadDelCierre(IReadOnlyList<LineaDeCierreParaInforme> Lineas, IReadOnlyList<DeterioroGeneralDelCierre>? General);

    /// <summary>
    /// Las líneas del cierre del alcance (sin castigadas, de la modalidad y el sub-segmento pedidos) y el deterioro general de las oficinas y
    /// modalidades del alcance; el general es nulo cuando el alcance o el filtro van por línea, porque no se reparte por línea.
    /// </summary>
    public static async Task<CalidadDelCierre> CalidadAsync(IApplicationDbContext db, AmbitoDeInformeDeCartera ambito, MonthlyClose cierre, CancellationToken ct)
    {
        var lineas = (await CierresParaInformes.LineasAsync(db, cierre, ambito.Creditos(db).Select(l => l.Id), ct))
            .Where(l => !l.IsWrittenOff && ambito.DeLaModalidad(l.ModalityCode, l.SubsegmentCode))
            .ToList();
        if (ambito.Vacio || ambito.Linea is not null || ambito.Asesor is not null || !ambito.Alcance.TodasLasLineas)
            return new CalidadDelCierre(lineas, null);

        var generales = await db.MonthlyCloseGeneralImpairments.AsNoTracking()
            .Where(g => g.MonthlyCloseId == cierre.Id && !g.IsDeleted)
            .Select(g => new { g.BranchId, g.ModalityCode, g.SubsegmentCode, g.TotalAmount })
            .ToListAsync(ct);
        var general = generales
            .Where(g => (ambito.Alcance.TodasLasOficinas || ambito.Alcance.Oficinas.Contains(g.BranchId))
                        && (ambito.Sucursal is not { } s || g.BranchId == s)
                        && ambito.DeLaModalidad(g.ModalityCode, g.SubsegmentCode))
            .GroupBy(g => (g.BranchId, g.ModalityCode, g.SubsegmentCode))
            .Select(g => new DeterioroGeneralDelCierre(g.Key.BranchId, g.Key.ModalityCode, g.Key.SubsegmentCode, g.Sum(x => x.TotalAmount)))
            .ToList();
        return new CalidadDelCierre(lineas, general);
    }

    /// <summary>Lo vencido de unas líneas: el capital con más de <paramref name="dias"/> días de mora.</summary>
    public static decimal Vencida(IEnumerable<LineaDeCierreParaInforme> lineas, int dias) => lineas.Where(l => l.DaysPastDue > dias).Sum(l => l.PrincipalBalance);

    /// <summary>Lo que está en riesgo: el capital de las calificadas B a E.</summary>
    public static decimal EnRiesgo(IEnumerable<LineaDeCierreParaInforme> lineas) => lineas.Where(l => l.FinalRating >= RatingCategory.B).Sum(l => l.PrincipalBalance);

    /// <summary>Una razón en porcentaje con dos decimales, o nula si el denominador es cero.</summary>
    public static decimal? Porcentaje(decimal parte, decimal todo) => todo == 0m ? null : Math.Round(parte / todo * 100m, 2, MidpointRounding.AwayFromZero);

    private static FilaExportable Fila(string dimension, IReadOnlyCollection<LineaDeCierreParaInforme> lineas, decimal general, int dias)
    {
        var bruta = lineas.Sum(l => l.PrincipalBalance);
        var vencida = Vencida(lineas, dias);
        var riesgo = EnRiesgo(lineas);
        var deterioro = lineas.Sum(l => l.TotalImpairment) + general;
        return new FilaExportable([dimension, bruta, vencida, Porcentaje(vencida, bruta), riesgo, Porcentaje(riesgo, bruta), deterioro, Porcentaje(deterioro, vencida)]);
    }

    private async Task<Func<LineaDeCierreParaInforme, string>> NombresAsync(string dimension, IReadOnlyList<LineaDeCierreParaInforme> lineas, CancellationToken ct)
    {
        switch (dimension)
        {
            case PorOficina:
            {
                var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
                return l => oficinas.GetValueOrDefault(l.BranchId) ?? string.Empty;
            }
            case PorLinea:
            {
                var ids = lineas.Select(l => l.CreditLineId).Distinct().ToList();
                var nombres = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id))
                    .ToDictionaryAsync(l => l.Id, l => $"{l.Code} · {l.Name}", ct);
                return l => nombres.GetValueOrDefault(l.CreditLineId) ?? string.Empty;
            }
            case PorModalidad:
                return l => l.ModalityCode;
            default:
                return _ => "Total";
        }
    }
}
