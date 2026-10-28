using System.Globalization;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.GoLive;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Activation;

/// <summary>
/// Las condiciones automáticas de Recaudo en la lista de go/no-go (feature 015, US24, T0320; R-28; FR-227). Sólo lee:
/// <list type="bullet">
/// <item><c>REC.REQ.{código}</c>: cada requisito de la activación completa (<see cref="RequisitosDeActivacionDeRecaudo"/>, la misma regla de <c>/recaudo/activacion</c>),
/// entre ellos la matriz contable al 100 % de lo que no esté en «no pasa»;</item>
/// <item><see cref="ReciboDePruebaPorPuntoYMedio"/>: cada medio ofrecido en cada punto vigente tiene al menos un recibo vigente (el recibo de prueba);</item>
/// <item><see cref="ReciboDePruebaPorConcepto"/>: cada concepto vario activo de la cooperativa (no los del sistema) tiene al menos una aplicación;</item>
/// <item><see cref="SegundoFactorDeCajerosYSupervisores"/>: los cajeros asignados y quienes tienen los permisos del supervisor tienen el segundo factor inscrito;</item>
/// <item><see cref="AlcancesAsignados"/>: cada cajero asignado tiene alcance sobre su caja o su punto.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class VerificacionDeSalidaDeRecaudo(
    IApplicationDbContext db,
    RequisitosDeActivacionDeRecaudo requisitos,
    IAutoridadDeOtroAprobador permisos,
    IDateTimeService reloj) : IVerificacionDeSalida
{
    public const string ReciboDePruebaPorPuntoYMedio = "TestReceiptPerPointAndMeans";
    public const string ReciboDePruebaPorConcepto = "TestReceiptPerConcept";
    public const string SegundoFactorDeCajerosYSupervisores = "SecondFactorCashiersAndSupervisors";
    public const string AlcancesAsignados = "CashierScopesAssigned";
    /// <summary>
    /// El prefijo de los requisitos de la activación, con el módulo: el índice único de los ítems es lista × código (<c>UK_COR_GoLiveChecklistItems_ChecklistId_Code</c>)
    /// y Tesorería tiene requisitos con el mismo código (<c>OpeningSquare</c>, <c>AccountingMatrixComplete</c>…). Hasta la corrida e2e del 2026-10-27 los dos usaban
    /// <c>REQ.</c> y crear la lista respondía 500 por la llave duplicada.
    /// </summary>
    public const string PrefijoDeRequisito = "REC.REQ.";

    public string Modulo => ClavesDeRecaudo.Codigo;

    public async Task<IReadOnlyList<ResultadoDeVerificacion>> VerificarAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var lista = new List<ResultadoDeVerificacion>();
        var (reqs, _) = await requisitos.EvaluarAsync(hoy, completa: true, corte: null, ct);
        lista.AddRange(reqs.Select(r => new ResultadoDeVerificacion(PrefijoDeRequisito + r.Code, $"Activación de Recaudo: {r.Description}", r.Met,
            r.Met ? "Cumple." : "Falta: " + r.Description)));

        // Recibo de prueba por punto y medio.
        var puntos = (await db.CollectionPoints.AsNoTracking().ToListAsync(ct)).Where(p => p.VigenteEn(hoy)).ToDictionary(p => p.Id, p => p.Code);
        var ids = puntos.Keys.ToList();
        var ofrecidos = (await db.PaymentMeansCollectionPoints.AsNoTracking().Where(o => ids.Contains(o.CollectionPointId)).ToListAsync(ct))
            .Where(o => o.VigenteEn(hoy)).Select(o => (o.CollectionPointId, o.PaymentMeansId)).Distinct().ToList();
        var medios = await db.PaymentMeans.AsNoTracking().Where(m => m.IsActive).ToDictionaryAsync(m => m.Id, m => m.Code, ct);
        ofrecidos = ofrecidos.Where(o => medios.ContainsKey(o.PaymentMeansId)).ToList();
        var usados = await (from m in db.ReceiptMeans.AsNoTracking()
                            join r in db.IssuedReceipts.AsNoTracking() on m.ReceiptId equals r.Id
                            where r.Status != ReceiptState.Voided && r.CollectionPointId != null
                            select new { Punto = r.CollectionPointId!.Value, m.PaymentMeansId }).Distinct().ToListAsync(ct);
        var faltan = ofrecidos.Where(o => !usados.Any(u => u.Punto == o.CollectionPointId && u.PaymentMeansId == o.PaymentMeansId))
            .Select(o => $"{puntos[o.CollectionPointId]}/{medios[o.PaymentMeansId]}").ToList();
        lista.Add(new(ReciboDePruebaPorPuntoYMedio, "Un recibo de prueba por cada medio ofrecido en cada punto.", ofrecidos.Count > 0 && faltan.Count == 0,
            ofrecidos.Count == 0
                ? "No hay medios ofrecidos en un punto vigente."
                : faltan.Count == 0
                    ? string.Create(CultureInfo.InvariantCulture, $"{ofrecidos.Count} combinaciones punto/medio con su recibo.")
                    : "Sin recibo: " + string.Join(", ", faltan.Take(30)) + (faltan.Count > 30 ? "…" : string.Empty)));

        // Recibo de prueba por concepto.
        var conceptos = await db.MiscConcepts.AsNoTracking().Where(c => !c.IsSystem && c.Status == ReceiptsCatalogStatus.Active)
            .Select(c => new { c.Id, c.Code }).ToListAsync(ct);
        var conceptosUsados = await (from a in db.ReceiptApplications.AsNoTracking()
                                     join r in db.IssuedReceipts.AsNoTracking() on a.ReceiptId equals r.Id
                                     where a.MiscConceptId != null && r.Status != ReceiptState.Voided
                                     select a.MiscConceptId!.Value).Distinct().ToListAsync(ct);
        var sinRecibo = conceptos.Where(c => !conceptosUsados.Contains(c.Id)).Select(c => c.Code).ToList();
        lista.Add(new(ReciboDePruebaPorConcepto, "Un recibo de prueba por cada concepto vario activo.", sinRecibo.Count == 0,
            conceptos.Count == 0 ? "La cooperativa no tiene conceptos varios propios." : sinRecibo.Count == 0
                ? string.Create(CultureInfo.InvariantCulture, $"{conceptos.Count} concepto(s) con su recibo.")
                : "Sin recibo: " + string.Join(", ", sinRecibo.Take(30))));

        // Segundo factor y alcances.
        var asignaciones = (await db.CashBoxCashiers.AsNoTracking().ToListAsync(ct)).Where(a => a.VigenteEn(hoy)).ToList();
        var cajeros = asignaciones.Select(a => a.UserId).Distinct().ToList();
        var usuarios = await db.Users.AsNoTracking().Where(u => u.IsActive && !u.IsSaasOperator).Select(u => new { u.Id, u.Username, u.IsMfaEnabled }).ToListAsync(ct);
        var supervisores = new List<int>();
        foreach (var u in usuarios)
        {
            var todos = true;
            foreach (var p in RequisitosDeActivacionDeRecaudo.PermisosDelSupervisor)
                if (!await permisos.TienePermisoAsync(u.Id, p, ct)) { todos = false; break; }
            if (todos) supervisores.Add(u.Id);
        }
        var revisados = usuarios.Where(u => cajeros.Contains(u.Id) || supervisores.Contains(u.Id)).ToList();
        var sinFactor = revisados.Where(u => !u.IsMfaEnabled).Select(u => u.Username).ToList();
        // Segunda ronda del 2026-10-27 (D-l; recomendación aceptada): el cajero o supervisor sin segundo factor sólo avisa —la condición queda en verde con los nombres en la
        // evidencia—; antes la dejaba en rojo y exigía una excepción.
        lista.Add(new(SegundoFactorDeCajerosYSupervisores, "Cajeros y supervisores con el segundo factor inscrito (FR-176; si falta, avisa).", revisados.Count > 0,
            revisados.Count == 0 ? "No hay cajeros ni supervisores." : sinFactor.Count == 0
                ? string.Create(CultureInfo.InvariantCulture, $"{revisados.Count} persona(s) con segundo factor.")
                : "Sin segundo factor: " + string.Join(", ", sinFactor)));

        var cajas = await db.CashBoxes.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.CollectionPointId, ct);
        var alcanceCaja = await db.UserCashBoxScopes.AsNoTracking().Select(s => new { s.UserId, s.CashBoxId }).ToListAsync(ct);
        var alcancePunto = await db.UserCollectionPointScopes.AsNoTracking().Select(s => new { s.UserId, s.CollectionPointId }).ToListAsync(ct);
        var sinAlcance = asignaciones.Where(a => !alcanceCaja.Any(s => s.UserId == a.UserId && s.CashBoxId == a.CashBoxId)
                                                && !alcancePunto.Any(s => s.UserId == a.UserId && cajas.TryGetValue(a.CashBoxId, out var p) && s.CollectionPointId == p))
            .Select(a => a.UserId).Distinct().ToList();
        var nombres = usuarios.ToDictionary(u => u.Id, u => u.Username);
        lista.Add(new(AlcancesAsignados, "Cada cajero asignado tiene alcance sobre su caja o su punto.", asignaciones.Count > 0 && sinAlcance.Count == 0,
            asignaciones.Count == 0 ? "No hay cajeros asignados." : sinAlcance.Count == 0
                ? "Todos los cajeros tienen su alcance."
                : "Sin alcance: " + string.Join(", ", sinAlcance.Select(id => nombres.GetValueOrDefault(id) ?? id.ToString(CultureInfo.InvariantCulture)))));
        return lista;
    }
}
