using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>vintages</c> de <c>/api/reports/lending</c> (feature 013, US15, K5, T0981; FR-134; contracts/api.md §17): la cosecha de los
/// créditos por <b>mes de desembolso</b> (<c>from</c>–<c>to</c>; sin <c>from</c>, los doce meses hasta el de <c>to</c>) y, en cada mes de
/// maduración que tenga cierre vigente, la mora a 30, 60 y 90 días. Agregada: sin datos del deudor; el alcance va en la consulta de créditos
/// (<see cref="AmbitoDeInformeDeCartera"/>). (nuevo)
///
/// <para>Definición fijada aquí, <b>a validar por la contadora</b> (T0977): para la cosecha del mes M y el mes de maduración k (k ≥ 1), «Mora N» es
/// el saldo de capital de los créditos de la cosecha con días de mora <b>mayores o iguales</b> a N al cierre vigente del mes M + k, sobre el monto
/// desembolsado en M, en porcentaje. Los cierres son los mensuales no reabiertos y, para los meses anteriores a la activación, el cierre base
/// (<see cref="CierresParaInformes.PorMesAsync"/>); un crédito castigado sigue contando con el saldo que traiga su línea. El mes de desembolso y el
/// monto de un crédito nacido en el ERP son los de sus desembolsos sin reversar (el primero da el mes; el monto suma todos los tramos); los de
/// uno migrado, los de su fila de la carga (fecha y monto original).</para>
/// </summary>
public sealed record VintagesReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class VintagesReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<VintagesReportQuery, Result<TablaExportable>>
{
    /// <summary>Los umbrales de mora de la cosecha (FR-134: 30, 60 y 90 días).</summary>
    public static readonly IReadOnlyList<int> Umbrales = [30, 60, 90];

    /// <summary>Sin <c>from</c>, cuántos meses de desembolso trae (hasta el de <c>to</c>, incluido).</summary>
    public const int MesesPorDefecto = 12;

    public static readonly VistaDeInformeDeCartera Vista = new(
        "vintages", "Cosechas", "Por mes de desembolso: el monto y, en cada mes de maduración con cierre, la mora a 30, 60 y 90 días sobre lo desembolsado.",
        "cosechas", ["from", "to", "branch", "line", "modality", "subsegment"], AvailableFrom: EntregaDeCartera.K5);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Mes de desembolso"),
        new("Monto", TipoDeColumna.Moneda),
        new("Mes de maduración", TipoDeColumna.Entero),
        new("Mes del cierre"),
        new("Mora 30", TipoDeColumna.Porcentaje),
        new("Mora 60", TipoDeColumna.Porcentaje),
        new("Mora 90", TipoDeColumna.Porcentaje),
    ];

    /// <summary>Un crédito de una cosecha: su mes de desembolso y lo desembolsado.</summary>
    public sealed record CreditoDeCosecha(int LoanId, DateOnly PrimerDesembolso, decimal Monto);

    public async Task<Result<TablaExportable>> Handle(VintagesReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var hasta = f.Hasta(hoy);
        var mesHasta = new DateOnly(hasta.Year, hasta.Month, 1);
        var desde = f.From is { } d ? new DateOnly(d.Year, d.Month, 1) : mesHasta.AddMonths(1 - MesesPorDefecto);
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);

        var cosecha = (await CreditosAsync(db, ambito, mesHasta.AddMonths(1).AddDays(-1), ct))
            .Where(c => c.PrimerDesembolso >= desde)
            .ToList();
        var condiciones = await AmbitoDeInformeDeCartera.CondicionesInicialesAsync(db, cosecha.Select(c => c.LoanId).ToList(), ct);
        cosecha = cosecha.Where(c => condiciones.TryGetValue(c.LoanId, out var t) ? ambito.DeLaModalidad(t.ModalityCode, t.SubsegmentCode) : ambito.Modalidad is null && ambito.Subsegmento is null)
            .ToList();

        var cierres = await CierresParaInformes.PorMesAsync(db, hoy, ct);
        var lineas = await CierresParaInformes.LineasDeVariosAsync(db, cierres.Values.ToList(), cosecha.Select(c => c.LoanId).ToList(), ct);
        var lineaPorCierre = lineas.ToDictionary(x => x.Key, x => x.Value.GroupBy(l => l.LoanId).ToDictionary(g => g.Key, g => g.First()));

        var filas = new List<FilaExportable>();
        foreach (var mes in cosecha.GroupBy(c => new DateOnly(c.PrimerDesembolso.Year, c.PrimerDesembolso.Month, 1)).OrderBy(g => g.Key))
        {
            var etiqueta = mes.Key.ToString("yyyy-MM");
            var monto = mes.Sum(c => c.Monto);
            foreach (var cierre in cierres.Where(c => new DateOnly(c.Key.Anio, c.Key.Mes, 1) > mes.Key).OrderBy(c => c.Key))
            {
                var k = (cierre.Key.Anio - mes.Key.Year) * 12 + cierre.Key.Mes - mes.Key.Month;
                var suyas = lineaPorCierre[cierre.Value.Id];
                var mora = Umbrales.Select(u => QualityReportQueryHandler.Porcentaje(
                    mes.Where(c => suyas.TryGetValue(c.LoanId, out var l) && l.DaysPastDue >= u).Sum(c => suyas[c.LoanId].PrincipalBalance), monto)).ToList();
                filas.Add(new FilaExportable([etiqueta, monto, k, $"{cierre.Key.Anio:D4}-{cierre.Key.Mes:D2}", mora[0], mora[1], mora[2]], etiqueta));
            }
            if (!cierres.Keys.Any(c => new DateOnly(c.Anio, c.Mes, 1) > mes.Key))
                filas.Add(new FilaExportable([etiqueta, monto, null, null, null, null, null], etiqueta));
        }

        return Result.Success(new TablaExportable("Cosechas",
            $"Desembolsos de {desde:yyyy-MM} a {mesHasta:yyyy-MM} · {cosecha.Count} créditos · {ambito.Descripcion()}", Columnas, filas, null,
        [
            "Mora N: capital de los créditos de la cosecha con N días de mora o más al cierre del mes de maduración, sobre lo desembolsado en el mes (%).",
            "Mes de maduración k: el cierre del mes k meses después del desembolso. Una cosecha sin cierres posteriores sale sin mora.",
            "Definición a validar por la contadora.",
        ]));
    }

    /// <summary>
    /// Los créditos del alcance con su primer desembolso hasta <paramref name="hasta"/>: los nacidos en el ERP por sus desembolsos sin reversar, los
    /// migrados por su fila de la carga.
    /// </summary>
    public static async Task<IReadOnlyList<CreditoDeCosecha>> CreditosAsync(IApplicationDbContext db, AmbitoDeInformeDeCartera ambito, DateOnly hasta,
        CancellationToken ct)
    {
        var creditos = ambito.Creditos(db);
        var desembolsos = await (from o in db.LoanOperations.AsNoTracking()
                                 join l in creditos on o.LoanId equals l.Id
                                 where l.Origin != LoanOrigin.Migration && o.Kind == LoanOperationKind.Disbursement && o.ReversedByOperationId == null
                                       && o.ValueDate <= hasta
                                 select new { o.LoanId, o.ValueDate, o.Amount }).ToListAsync(ct);
        var propios = desembolsos.GroupBy(o => o.LoanId).Select(g => new CreditoDeCosecha(g.Key, g.Min(o => o.ValueDate), g.Sum(o => o.Amount)));

        var migrados = await (from l in creditos
                              from p in db.MigrationLoadParts.AsNoTracking()
                              join r in db.MigrationLoanRows.AsNoTracking() on p.Id equals r.MigrationLoadPartId
                              where l.Origin == LoanOrigin.Migration && l.MigrationLoadId == p.MigrationLoadId && r.SourceLoanNumber == l.Number
                                    && r.DisbursementDate <= hasta
                              select new { l.Id, r.DisbursementDate, r.OriginalAmount }).ToListAsync(ct);
        var deLaCarga = migrados.GroupBy(m => m.Id).Select(g => new CreditoDeCosecha(g.Key, g.First().DisbursementDate, g.First().OriginalAmount));
        return propios.Concat(deLaCarga).ToList();
    }
}
