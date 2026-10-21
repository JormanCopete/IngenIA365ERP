using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Regulatory;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>bureau-exclusions</c> de <c>/api/reports/lending</c> (feature 013, US16, K5, T1006; FR-137, SC-012; contracts/api.md §17): los titulares
/// que quedaron <b>fuera</b> del archivo de una central (<c>bureau</c>, obligatorio) en un período (<c>year</c>, <c>month</c>; sin ellos, el último
/// generado), con su motivo y la gestión que lo corrige. Lee las <c>LND_RegulatoryFileLines</c> excluidas de la generación vigente del período (la de
/// mayor versión no reemplazada: generada, cargada o validada con bloqueantes). Trae datos del deudor (PD, C84) y filtra por la oficina y la línea del
/// crédito (<see cref="IAlcanceDeCartera"/>). Columna oculta <c>_credito</c>. (nuevo)
/// </summary>
public sealed record BureauExclusionsReportQuery(FiltrosDeInformeDeCartera Filtros, string? Bureau = null, int? Year = null, int? Month = null)
    : IRequest<Result<TablaExportable>>;

public sealed class BureauExclusionsReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, CatalogoDeLayoutsDeCartera catalogo)
    : IRequestHandler<BureauExclusionsReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "bureau-exclusions", "Excluidos del reporte a centrales",
        "Los titulares que quedaron fuera del archivo de una central en un período: sin autorización de reporte, sin aviso previo con constancia, "
        + "reclamación al seguro en trámite, insolvencia o pagaduría dentro de su umbral, con la gestión que lo corrige.",
        "excluidos-de-centrales", [], ["bureau", "year", "month"], EntregaDeCartera.K5);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Crédito"),
        new("Deudor"),
        new("Motivo"),
        new("Gestión sugerida"),
        new("_credito", Clave: "_credito"),
    ];

    public async Task<Result<TablaExportable>> Handle(BureauExclusionsReportQuery request, CancellationToken ct)
    {
        var centrales = catalogo.Centrales;
        var central = request.Bureau?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(central) || !centrales.Contains(central, StringComparer.Ordinal))
            return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("bureau", request.Bureau, centrales));
        if (request.Month is { } m && (m < 1 || m > 12))
            return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("month", m.ToString(CultureInfo.InvariantCulture),
                Enumerable.Range(1, 12).Select(x => x.ToString(CultureInfo.InvariantCulture))));
        if (request.Month is not null && request.Year is null)
            return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("year", null, ["el año del período (con month)"]));

        var consulta = db.RegulatoryFiles.AsNoTracking()
            .Where(f => !f.IsDeleted && f.Kind == RegulatoryFileKind.CreditBureau && f.BureauCode == central && f.Status != RegulatoryFileStatus.Superseded);
        if (request.Year is { } y) consulta = consulta.Where(f => f.PeriodYear == y);
        if (request.Month is { } mes) consulta = consulta.Where(f => f.PeriodMonth == mes);
        var archivo = await consulta.OrderByDescending(f => f.PeriodYear).ThenByDescending(f => f.PeriodMonth).ThenByDescending(f => f.Version)
            .FirstOrDefaultAsync(ct);
        if (archivo is null)
            return Result.Success(new TablaExportable("Excluidos del reporte a centrales", $"{central} · sin archivo generado del período", Columnas, [], null,
                ["No hay una generación de esa central para el período: valide o genere el archivo desde «Centrales»."]));

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        var filas = await (from l in db.RegulatoryFileLines.AsNoTracking()
                           join c in creditos on l.LoanId equals c.Id
                           join p in db.People.AsNoTracking().IgnoreQueryFilters() on l.PersonId equals p.Id
                           where l.RegulatoryFileId == archivo.Id && !l.Included && !l.IsDeleted
                           orderby c.Number, l.LineNumber
                           select new { c.Number, c.PublicId, Deudor = c.DebtorPersonId == p.Id, Persona = p, l.ExclusionReason })
            .ToListAsync(ct);

        var resultado = filas.Select(f => new FilaExportable(
        [
            f.Number,
            ReferenciasDeGobierno.Nombre(f.Persona) + (f.Deudor ? string.Empty : " (codeudor)"),
            ArchivosRegulatorios.Motivo(f.ExclusionReason ?? string.Empty),
            ArchivosRegulatorios.GestionSugerida(f.ExclusionReason ?? string.Empty),
            f.PublicId.ToString(),
        ])).ToList();
        var periodo = string.Create(CultureInfo.InvariantCulture, $"{archivo.PeriodYear:0000}-{archivo.PeriodMonth:00}");
        return Result.Success(new TablaExportable("Excluidos del reporte a centrales",
            $"{central} · {periodo} · versión {archivo.Version} ({archivo.Status}) · {resultado.Count} excluidos", Columnas, resultado, null,
            ["Un dato negativo sólo entra con aviso previo con constancia de su episodio y desde su fecha más temprana de reporte (Ley 2157 de 2021).",
             "Quien no autorizó el reporte a centrales con evidencia queda fuera aunque esté al día (FR-006)."]));
    }
}
