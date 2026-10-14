using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>usury</c> de <c>/api/reports/lending</c> (feature 013, US15, K1, T0797; FR-021, FR-133; contracts/api.md §17), al corte
/// (<c>asOf</c>, hoy por defecto), en dos secciones:
/// <list type="number">
/// <item><b>Créditos topados</b>: los vivos cuya posición diaria vigente al corte está topada por la usura (<c>UsuryCapped</c>), con la tasa
/// pactada (sus condiciones vigentes), la tasa que se les aplica —la usura— y los días topados hasta el corte (una posición por día, la última
/// versión).</item>
/// <item><b>Líneas cerca de la usura</b>: las líneas activas del alcance cuyo plan tipo queda a menos de <c>points</c> puntos de tasa efectiva anual de
/// la usura vigente —por defecto <c>Informes.PuntosAlertaUsura</c> al corte—, con el mismo control de usura de la línea
/// (<see cref="ReglasDeLineaDeCredito.ConsultarAsync"/>); un margen negativo es una línea que ya la supera.</item>
/// </list>
/// Trae datos del deudor (PD). El alcance va en la consulta de créditos y de líneas (<see cref="AmbitoDeInformeDeCartera"/>,
/// <see cref="IAlcanceDeCartera"/>). Columna oculta <c>_credito</c>. (nuevo)
/// </summary>
public sealed record UsuryReportQuery(FiltrosDeInformeDeCartera Filtros, string? Points = null) : IRequest<Result<TablaExportable>>;

public sealed class UsuryReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    ReglasDeParametrosDeCartera parametros,
    ReglasDeLineaDeCredito reglasDeLinea,
    IDateTimeService reloj) : IRequestHandler<UsuryReportQuery, Result<TablaExportable>>
{
    public const string SeccionDeCreditos = "Créditos topados por la usura";
    public const string SeccionDeLineas = "Líneas cerca de la usura";

    /// <summary>Lo que la vista declara al publicarse (T0798).</summary>
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "usury", "Usura", "Créditos topados por la usura al corte y líneas a menos de N puntos de la usura vigente.",
        "usura", ["asOf", "branch", "line", "modality", "subsegment"], ["points"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Crédito"),
        new("Deudor"),
        new("Línea"),
        new("Modalidad"),
        new("Tasa", TipoDeColumna.Porcentaje),
        new("Usura", TipoDeColumna.Porcentaje),
        new("Margen", TipoDeColumna.Porcentaje),
        new("Días topados", TipoDeColumna.Entero),
        new("_credito", Clave: "_credito"),
    ];

    public async Task<Result<TablaExportable>> Handle(UsuryReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        decimal puntos;
        if (!string.IsNullOrWhiteSpace(request.Points))
        {
            if (!decimal.TryParse(request.Points.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out puntos) || puntos < 0m)
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("points", request.Points,
                    ["un número de puntos de tasa efectiva anual mayor o igual a cero, con punto decimal, como 2.5"]));
        }
        else
        {
            var leido = await parametros.LeerAsync(ParametrosDeCartera.InformesPuntosAlertaUsura, corte, ct: ct);
            if (leido.IsFailure) return Result.Failure<TablaExportable>(leido.Error);
            puntos = leido.Value.Valor is decimal d ? d
                : decimal.TryParse(leido.Value.Texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var t) ? t : 0m;
        }

        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var notas = new List<string>
        {
            "Tasas efectivas anuales en puntos porcentuales. Créditos: la tasa pactada (condiciones vigentes) y la que se aplica topada por la usura; los días topados cuentan las posiciones diarias topadas hasta el corte.",
            $"Líneas: el plan tipo de la versión vigente contra la usura vigente al corte; se listan las que quedan a menos de {puntos.ToString("0.##", CultureInfo.InvariantCulture)} puntos"
                + (string.IsNullOrWhiteSpace(request.Points) ? " (Informes.PuntosAlertaUsura)" : string.Empty) + "; un margen negativo ya la supera.",
        };

        var filas = new List<FilaExportable>();
        filas.AddRange(await CreditosTopadosAsync(ambito, corte, ct));
        filas.AddRange(await LineasCercaAsync(ambito, corte, puntos, notas, ct));
        return Result.Success(new TablaExportable("Usura", $"Al {corte:yyyy-MM-dd} · {ambito.Descripcion()}", Columnas, filas, null, notas));
    }

    private async Task<IReadOnlyList<FilaExportable>> CreditosTopadosAsync(AmbitoDeInformeDeCartera ambito, DateOnly corte, CancellationToken ct)
    {
        var creditos = await CargadorDeRiesgo.VivosAl(ambito.Creditos(db), corte).ToListAsync(ct);
        var posiciones = await AmbitoDeInformeDeCartera.PosicionesAlCorteAsync(db, creditos.Select(c => c.Id).ToList(), corte, ct);
        var topados = creditos.Where(c => posiciones.TryGetValue(c.Id, out var p) && p.UsuryCapped && ambito.DeLaModalidad(p.ModalityCode, p.SubsegmentCode))
            .OrderBy(c => c.Number, StringComparer.Ordinal).ToList();
        if (topados.Count == 0) return [];

        var ids = topados.Select(c => c.Id).ToList();
        var dias = (await db.LoanDailyPositions.AsNoTracking().Where(p => ids.Contains(p.LoanId) && p.PositionDate <= corte)
                .Select(p => new { p.LoanId, p.PositionDate, p.Version, p.UsuryCapped }).ToListAsync(ct))
            .GroupBy(p => (p.LoanId, p.PositionDate))
            .Select(g => g.OrderByDescending(p => p.Version).First())
            .Where(p => p.UsuryCapped)
            .GroupBy(p => p.LoanId).ToDictionary(g => g.Key, g => g.Count());
        var condiciones = await AmbitoDeInformeDeCartera.CondicionesVigentesAsync(db, ids, ct);
        var (personas, lineas, _) = await ListLoansQueryHandler.ReferenciasAsync(db, topados, ct);
        return topados.Select(c =>
        {
            var p = posiciones[c.Id];
            var pactada = condiciones.TryGetValue(c.Id, out var t) ? t.AnnualRate : null;
            return new FilaExportable(
            [
                c.Number, personas[c.DebtorPersonId].Name, $"{lineas[c.CreditLineId].Code} · {lineas[c.CreditLineId].Name}", p.ModalityCode,
                Puntos(pactada), Puntos(p.AppliedAnnualRate), null, dias.GetValueOrDefault(c.Id), c.PublicId.ToString(),
            ], SeccionDeCreditos);
        }).ToList();
    }

    private async Task<IReadOnlyList<FilaExportable>> LineasCercaAsync(AmbitoDeInformeDeCartera ambito, DateOnly corte, decimal puntos, List<string> notas,
        CancellationToken ct)
    {
        if (ambito.Vacio) return [];
        var consulta = db.CreditLines.AsNoTracking().Where(l => !l.IsDeleted && l.Status == CreditLineStatus.Active)
            .PorLinea(ambito.Alcance, l => l.Id);
        if (ambito.Linea is { } linea) consulta = consulta.Where(l => l.Id == linea);
        var lineas = await consulta.OrderBy(l => l.Code).ToListAsync(ct);
        if (lineas.Count == 0) return [];

        var versiones = await new VersionVigenteDeLinea(db).LeerVariasAsync(lineas.Select(l => l.Id).ToList(), corte, ct: ct);
        var filas = new List<FilaExportable>();
        var sinUsura = new List<string>();
        foreach (var l in lineas)
        {
            if (!versiones.TryGetValue(l.Id, out var version)) continue;
            var borrador = version.Borrador();
            if (!ambito.DeLaModalidad(borrador.ModalityCode, borrador.SubsegmentCode)) continue;
            var consultada = await reglasDeLinea.ConsultarAsync(borrador, corte, null, null, null, ct);
            if (consultada.IsFailure || consultada.Value.Control.Resultado.Usura is null || consultada.Value.Control.Resultado.Holgura is null)
            {
                sinUsura.Add(l.Code);
                continue;
            }
            var (control, tasa, combinacion) = consultada.Value;
            var margen = control.Resultado.Holgura!.Value * 100m;
            if (margen >= puntos) continue;
            filas.Add(new FilaExportable(
            [
                null, null, $"{l.Code} · {l.Name}", combinacion.ModalityCode, Puntos(tasa), Puntos(control.Resultado.Usura), Math.Round(margen, 4), null, null,
            ], SeccionDeLineas));
        }
        if (sinUsura.Count > 0) notas.Add($"Sin usura o sin índice vigente al corte para comparar: {string.Join(", ", sinUsura)}.");
        return filas;
    }

    /// <summary>Una tasa como fracción expresada en puntos porcentuales (0,2 → 20).</summary>
    private static decimal? Puntos(decimal? fraccion) => fraccion is { } f ? Math.Round(f * 100m, 4) : null;
}
