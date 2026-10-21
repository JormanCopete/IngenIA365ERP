using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>migration-reconciliation</c> de <c>/api/reports/lending</c> (feature 013, US15, K1, T0797; FR-141, FR-143, FR-133; contracts/api.md
/// §17, §19.2) de la carga <c>load</c> (obligatorio; su PublicId), en la versión vigente de su cuadre, en dos secciones:
/// <list type="number">
/// <item><b>Cuadre</b>: la instantánea guardada (<see cref="CuadreDeMigracion.LeerAsync"/>, la misma de <c>GET …/balance-check</c>, sin escribir
/// ni preguntar a Contabilidad) por concepto, modalidad, sub-segmento, categoría y conjunto de cuentas, con lo de Cartera, lo contable, la
/// diferencia y su aceptación con el punto del acta.</item>
/// <item><b>Ventas</b>: la conciliación de las ventas a crédito hasta el corte —factura, cliente, clasificación, saldo, aceptación de su diferencia y
/// punto del acta—.</item>
/// </list>
/// La carga es de <b>toda la cooperativa</b>: sólo la ve quien tiene el alcance de todas las oficinas y todas las líneas
/// (<see cref="IAlcanceDeCartera"/>); con un alcance parcial el resultado es vacío, con su nota (falla cerrado). Trae datos del deudor (PD: el
/// cliente de cada venta). Columna oculta <c>_persona</c>. (nuevo)
/// </summary>
public sealed record MigrationReconciliationReportQuery(FiltrosDeInformeDeCartera Filtros, Guid? Load = null) : IRequest<Result<TablaExportable>>;

public sealed class MigrationReconciliationReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<MigrationReconciliationReportQuery, Result<TablaExportable>>
{
    public const string SeccionDeCuadre = "Cuadre con Contabilidad";
    public const string SeccionDeVentas = "Conciliación de ventas a crédito";

    public const string NotaDeAlcance =
        "La conciliación de la carga es de toda la cooperativa: sólo la ve quien tiene el alcance de todas las oficinas y todas las líneas.";

    /// <summary>Lo que la vista declara al publicarse (T0798).</summary>
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "migration-reconciliation", "Conciliación de la migración",
        "El cuadre de la carga con Contabilidad por concepto, modalidad, sub-segmento, categoría y cuentas, y la conciliación de las ventas a crédito.",
        "conciliacion-de-la-migracion", [], ["load"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Concepto"),
        new("Modalidad"),
        new("Sub-segmento"),
        new("Categoría"),
        new("Conjunto de cuentas"),
        new("Cartera", TipoDeColumna.Moneda),
        new("Contabilidad", TipoDeColumna.Moneda),
        new("Diferencia", TipoDeColumna.Moneda),
        new("Factura"),
        new("Cliente"),
        new("Clasificación"),
        new("Saldo", TipoDeColumna.Moneda),
        new("Aceptación"),
        new("Punto de acta"),
        new("_persona", Clave: "_persona"),
    ];

    public async Task<Result<TablaExportable>> Handle(MigrationReconciliationReportQuery request, CancellationToken ct)
    {
        if (request.Load is not { } cargaPublicId)
            return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroObligatorio("load", "la carga de migración (su PublicId)."));

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var carga = await db.MigrationLoads.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == cargaPublicId && !l.IsDeleted && l.Nature == LineNature.Placement, ct);
        if (carga is null) return Result.Failure<TablaExportable>(ErroresDeActivacion.CargaNoEncontrada(cargaPublicId));
        var subtitulo = $"Carga {carga.Number} con corte al {carga.CutoffDate:yyyy-MM-dd} · versión {carga.CurrentCheckVersion} del cuadre";
        if (!(alcance.TodasLasOficinas && alcance.TodasLasLineas))
            return Result.Success(new TablaExportable("Conciliación de la migración", subtitulo, Columnas, [], null, [NotaDeAlcance]));

        var cuadre = await CuadreDeMigracion.LeerAsync(db, carga, ct);
        var diferencias = await db.MigrationLoadDifferences.AsNoTracking()
            .Where(d => d.MigrationLoadId == carga.Id && d.CheckVersion == carga.CurrentCheckVersion && !d.IsDeleted).ToListAsync(ct);
        var ventas = await db.MigrationSaleReconciliations.AsNoTracking()
            .Where(r => r.MigrationLoadId == carga.Id && r.CheckVersion == carga.CurrentCheckVersion && !r.IsDeleted)
            .OrderBy(r => r.SaleDate).ThenBy(r => r.InvoiceNumber).ToListAsync(ct);
        var puntos = await PuntosDeActaAsync(diferencias.Where(d => d.SessionItemId is not null).Select(d => d.SessionItemId!.Value).ToList(), ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, ventas.Where(v => v.PersonId is not null).Select(v => v.PersonId!.Value), ct);
        var porDimension = diferencias.Where(d => d.Kind == MigrationDifferenceKind.BalanceCheck)
            .GroupBy(d => d.DimensionKey, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var porId = diferencias.ToDictionary(d => d.Id);

        var filas = new List<FilaExportable>();
        foreach (var f in cuadre.Rows)
        {
            var diferencia = porDimension.GetValueOrDefault(f.DimensionKey);
            filas.Add(new FilaExportable(
            [
                f.Concept ?? f.Family, f.Modality, f.Subsegment, f.Category, string.Join(", ", f.Accounts.Select(a => $"{a.Code} {a.Name}")),
                f.Lending, f.Accounting, f.Difference, null, null, null, null,
                Aceptacion(f.Difference != 0m, diferencia, f.Unresolved), Punto(diferencia, puntos), null,
            ], SeccionDeCuadre, Resaltada: f.Difference != 0m));
        }
        foreach (var v in ventas)
        {
            var diferencia = v.DifferenceId is { } id ? porId.GetValueOrDefault(id) : null;
            var persona = v.PersonId is { } p ? personas.GetValueOrDefault(p) : null;
            filas.Add(new FilaExportable(
            [
                null, null, null, null, null, null, null, null, v.InvoiceNumber, persona?.FullName ?? string.Empty,
                TextosDeInformesDeCartera.Clasificacion(v.Outcome), v.Amount, Aceptacion(diferencia is not null, diferencia, false), Punto(diferencia, puntos),
                persona?.PersonPublicId.ToString(),
            ], SeccionDeVentas, Resaltada: diferencia is { Aceptada: false }));
        }
        return Result.Success(new TablaExportable("Conciliación de la migración", subtitulo, Columnas, filas, null,
        [
            "Cuadre: la instantánea guardada de la versión vigente; Cartera no resuelve cuentas, muestra el conjunto que respondió Contabilidad.",
            "Ventas: las ventas a crédito hasta el corte clasificadas contra la carga; las no migradas con saldo y las de fuera de Cartera tienen su diferencia, que se acepta con el punto de un acta cerrada.",
        ]));
    }

    private static string Aceptacion(bool conDiferencia, MigrationLoadDifference? diferencia, bool sinRegla)
    {
        if (sinRegla) return "Sin regla en la matriz contable";
        if (!conDiferencia) return "Sin diferencia";
        if (diferencia is null) return "Pendiente";
        return diferencia.AcceptedAt is { } en
            ? $"Aceptada el {en:yyyy-MM-dd}" + (string.IsNullOrWhiteSpace(diferencia.AcceptanceReason) ? string.Empty : $": {diferencia.AcceptanceReason}")
            : "Pendiente";
    }

    private static string? Punto(MigrationLoadDifference? diferencia, IReadOnlyDictionary<int, string> puntos) =>
        diferencia?.SessionItemId is { } id ? puntos.GetValueOrDefault(id) : null;

    /// <summary>Los puntos de acta como se leen: «Acta {número} · punto {orden}: {título}».</summary>
    private async Task<IReadOnlyDictionary<int, string>> PuntosDeActaAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<int, string>();
        return (await (from i in db.SessionItems.AsNoTracking().IgnoreQueryFilters()
                       where ids.Contains(i.Id)
                       join s in db.LendingSessions.AsNoTracking().IgnoreQueryFilters() on i.SessionId equals s.Id
                       select new { i.Id, s.Number, s.HeldOn, i.Order, i.Title }).ToListAsync(ct))
            .ToDictionary(x => x.Id, x => $"Acta {x.Number ?? x.HeldOn.ToString("yyyy-MM-dd")} · punto {x.Order}: {x.Title}");
    }
}
