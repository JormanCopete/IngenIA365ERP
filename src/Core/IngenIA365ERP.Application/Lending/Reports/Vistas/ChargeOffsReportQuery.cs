using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>charge-offs</c> de <c>/api/reports/lending</c> (feature 013, US14, K3, T0911; FR-096; contracts/api.md §17): el <b>seguimiento de la
/// cartera castigada</b>. Una fila por crédito castigado hasta <c>to</c> (por el ERP, con su lote y el punto del acta del consejo, o migrado ya castigado):
/// la fecha del castigo, el saldo castigado (lo que salió del activo al castigarlo; el saldo inicial en lo migrado), lo <b>recuperado</b> en el rango
/// (<c>from</c>–<c>to</c>, las filas <c>Recovery</c> del libro por fecha valor, netas de reversos), y las gestiones y el último contacto —vacíos hasta que
/// exista el registro de gestiones de K4—. Trae datos del deudor (PD); el alcance va en la consulta de créditos (<see cref="AmbitoDeInformeDeCartera"/>).
/// Columna oculta <c>_credito</c>. (nuevo)
/// </summary>
public sealed record ChargeOffsReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class ChargeOffsReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<ChargeOffsReportQuery, Result<TablaExportable>>
{
    /// <summary>Lo que la vista declara al publicarse (T0798).</summary>
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "charge-offs", "Cartera castigada",
        "Los créditos castigados: fecha y punto de acta del castigo, saldo castigado, lo recuperado en el rango, las gestiones y el último contacto.",
        "cartera-castigada", ["from", "to", "branch", "line", "modality", "subsegment"], [], EntregaDeCartera.K3);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Crédito"),
        new("Deudor"),
        new("Fecha de castigo", TipoDeColumna.Fecha),
        new("Punto de acta"),
        new("Saldo castigado", TipoDeColumna.Moneda),
        new("Recuperado", TipoDeColumna.Moneda),
        new("Gestiones", TipoDeColumna.Entero),
        new("Último contacto", TipoDeColumna.Fecha),
        new("_credito", Clave: "_credito"),
    ];

    public async Task<Result<TablaExportable>> Handle(ChargeOffsReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var (desde, hasta) = (f.Desde(hoy), f.Hasta(hoy));
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var castigados = await ambito.Creditos(db).Where(l => l.WrittenOffOn != null && l.WrittenOffOn <= hasta).ToListAsync(ct);
        var ids = castigados.Select(c => c.Id).ToList();

        var posiciones = await AmbitoDeInformeDeCartera.PosicionesAlCorteAsync(db, ids, hasta, ct);
        var deLaModalidad = castigados.Where(c => ambito.Modalidad is null && ambito.Subsegmento is null
                                                  || (posiciones.TryGetValue(c.Id, out var p) && ambito.DeLaModalidad(p.ModalityCode, p.SubsegmentCode)))
            .ToDictionary(c => c.Id);
        ids = deLaModalidad.Keys.ToList();

        // El castigo del ERP (la operación WriteOff y el punto del acta de su lote) o, en lo migrado, el saldo inicial castigado.
        var delErp = await (from o in db.LoanOperations.AsNoTracking()
                            join i in db.WriteOffBatchItems.AsNoTracking() on o.WriteOffBatchItemId equals i.Id
                            join b in db.WriteOffBatches.AsNoTracking() on i.WriteOffBatchId equals b.Id
                            join p in db.SessionItems.AsNoTracking() on b.SessionItemId equals p.Id into puntos
                            from p in puntos.DefaultIfEmpty()
                            join s in db.LendingSessions.AsNoTracking() on p.SessionId equals s.Id into sesiones
                            from s in sesiones.DefaultIfEmpty()
                            where ids.Contains(o.LoanId) && o.Kind == LoanOperationKind.WriteOff && o.ReversedByOperationId == null
                            select new { o.LoanId, o.Amount, Lote = b.Number, Acta = s == null ? null : s.Number, Orden = p == null ? (int?)null : p.Order })
            .ToListAsync(ct);
        var porCastigo = delErp.GroupBy(x => x.LoanId).ToDictionary(g => g.Key, g => g.First());
        var migrados = ids.Where(id => !porCastigo.ContainsKey(id)).ToList();
        var saldoInicial = await db.LoanTransactions.AsNoTracking()
            .Where(t => migrados.Contains(t.LoanId) && t.Kind == LoanTransactionKind.OpeningBalance)
            .GroupBy(t => t.LoanId).Select(g => new { LoanId = g.Key, Total = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.LoanId, x => x.Total, ct);
        var recuperado = await db.LoanTransactions.AsNoTracking()
            .Where(t => ids.Contains(t.LoanId) && t.Kind == LoanTransactionKind.Recovery && t.ValueDate >= desde && t.ValueDate <= hasta)
            .GroupBy(t => t.LoanId).Select(g => new { LoanId = g.Key, Total = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.LoanId, x => -x.Total, ct);
        var (personas, _, _) = await ListLoansQueryHandler.ReferenciasAsync(db, deLaModalidad.Values.ToList(), ct);

        var filas = new List<FilaExportable>();
        foreach (var c in deLaModalidad.Values.OrderBy(c => c.WrittenOffOn).ThenBy(c => c.Number, StringComparer.Ordinal))
        {
            var castigo = porCastigo.GetValueOrDefault(c.Id);
            var punto = castigo is null
                ? "Castigado antes de la activación"
                : castigo.Acta is null ? $"Lote {castigo.Lote}" : $"Acta {castigo.Acta}, punto {castigo.Orden} (lote {castigo.Lote})";
            filas.Add(new FilaExportable(
            [
                c.Number, personas.TryGetValue(c.DebtorPersonId, out var p) ? p.Name : string.Empty, c.WrittenOffOn, punto,
                castigo?.Amount ?? saldoInicial.GetValueOrDefault(c.Id), recuperado.GetValueOrDefault(c.Id), null, null, c.PublicId.ToString(),
            ]));
        }
        var notas = new List<string>
        {
            "Créditos castigados hasta el final del rango (por el ERP con su lote y su acta, o migrados ya castigados). Saldo castigado: lo que salió del activo al castigar; en lo migrado, el saldo inicial.",
            "Recuperado: los pagos aplicados como recuperación de cartera castigada en el rango, netos de reversos.",
            "Gestiones y último contacto: llegan con el registro de gestiones de cobranza (entrega K4).",
        };
        return Result.Success(new TablaExportable("Cartera castigada", $"Del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd} · {ambito.Descripcion()}", Columnas, filas,
            null, notas));
    }
}
