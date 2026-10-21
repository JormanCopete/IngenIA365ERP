using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Reports;

/// <summary>
/// La vista <c>garnishments</c> (feature 014, US11, T189; api.md §18.1; research R24, E165): los embargos recibidos en un rango, con su autoridad, oficio,
/// valor, si es por alimentos, estado, lo retenido hoy, lo consignado y el plazo informativo de respuesta (con la marca de vencido sin respuesta). Con datos
/// del titular; sólo personas cuyas cuentas de captación están todas en el alcance (falla cerrado). Con <c>status</c> opcional. (nuevo)
/// </summary>
public sealed record GarnishmentsReportQuery(FiltrosDeInformeDeCartera Filtros, string? Status = null) : IRequest<Result<TablaExportable>>;

public sealed class GarnishmentsReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<GarnishmentsReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistasDeCaptaciones.ConDatosDelTitular(
        "garnishments", "Embargos",
        "Los embargos recibidos en un rango, con su oficio, lo retenido, lo consignado y el plazo informativo de respuesta.", "embargos",
        ["from", "to"], ["status"], EntregaDeCartera.K8);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Recibido", TipoDeColumna.Fecha),
        new("Embargado"),
        new("Documento"),
        new("Autoridad"),
        new("Oficio"),
        new("Proceso"),
        new("Demandante"),
        new("Valor de la medida", TipoDeColumna.Moneda),
        new("Alimentos"),
        new("Estado"),
        new("Retenido", TipoDeColumna.Moneda),
        new("Consignado", TipoDeColumna.Moneda),
        new("Responder antes de", TipoDeColumna.Fecha),
        new("Respuestas", TipoDeColumna.Entero),
        new("_embargo", Clave: "_embargo"),
    ];

    public async Task<Result<TablaExportable>> Handle(GarnishmentsReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var rango = f.ValidarRango(hoy);
        if (rango.IsFailure) return Result.Failure<TablaExportable>(rango.Error);
        GarnishmentStatus? estado = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<GarnishmentStatus>(request.Status, true, out var e) || !Enum.IsDefined(e))
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("status", request.Status, Enum.GetNames<GarnishmentStatus>()));
            estado = e;
        }
        var (desde, hasta) = (f.Desde(hoy), f.Hasta(hoy));
        var embargos = await db.Garnishments.AsNoTracking()
            .Where(g => !g.IsDeleted && g.ReceivedOn >= desde && g.ReceivedOn <= hasta && (estado == null || g.Status == estado))
            .OrderBy(g => g.ReceivedOn).ThenBy(g => g.Id).ToListAsync(ct);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var personasIds = embargos.Select(g => g.PersonId).Distinct().ToList();
        var cuentas = await db.MemberAccounts.AsNoTracking().Where(a => personasIds.Contains(a.HolderPersonId) && !a.IsDeleted)
            .Select(a => new { a.HolderPersonId, a.BranchId, a.CreditLineId }).ToListAsync(ct);
        var fuera = cuentas.Where(c => !alcance.IncluyeOficina(c.BranchId) || !alcance.IncluyeLinea(c.CreditLineId)).Select(c => c.HolderPersonId).ToHashSet();
        embargos = embargos.Where(g => !fuera.Contains(g.PersonId)).ToList();
        var ids = embargos.Select(g => g.Id).ToList();
        var retenido = await db.AccountHolds.AsNoTracking().Where(h => h.GarnishmentId != null && ids.Contains(h.GarnishmentId.Value) && h.ReleasedAt == null && !h.IsDeleted)
            .GroupBy(h => h.GarnishmentId!.Value).Select(x => new { x.Key, Total = x.Sum(h => h.Amount ?? 0m) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var consignado = await db.MemberAccountOperations.AsNoTracking()
            .Where(o => o.GarnishmentId != null && ids.Contains(o.GarnishmentId.Value) && o.Kind == MemberAccountOperationKind.GarnishmentConsignment
                        && o.ReversedByOperationId == null && !o.IsDeleted)
            .GroupBy(o => o.GarnishmentId!.Value).Select(x => new { x.Key, Total = x.Sum(o => o.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var respuestas = await db.GarnishmentResponses.AsNoTracking().Where(r => ids.Contains(r.GarnishmentId) && !r.IsDeleted)
            .GroupBy(r => r.GarnishmentId).Select(x => new { x.Key, Total = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var personas = await LectorDeCuentas.PersonasAsync(db, personasIds, ct);
        var filas = embargos.Select(g => new FilaExportable([g.ReceivedOn, personas.GetValueOrDefault(g.PersonId)?.FullName,
            personas.GetValueOrDefault(g.PersonId)?.DocumentNumber, $"{g.CourtName} ({g.Authority})", g.OrderNumber, g.ProcessNumber, g.ClaimantName, g.Amount,
            g.IsChildSupport ? "Sí" : "No", g.Status.ToString(), retenido.GetValueOrDefault(g.Id), consignado.GetValueOrDefault(g.Id), g.ResponseDueOn,
            respuestas.GetValueOrDefault(g.Id), g.PublicId.ToString()])).ToList();
        var totales = new FilaExportable(["Total", null, null, null, null, null, null, null, null, null, filas.Sum(r => (decimal)r.Valores[10]!),
            filas.Sum(r => (decimal)r.Valores[11]!), null, null, null]);
        return Result.Success(new TablaExportable("Embargos", $"Recibidos del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd}", Columnas, filas, totales,
            ["El plazo de respuesta es informativo: avisa, no bloquea. Los aportes no se embargan (Ley 79 de 1988, art. 49)."]));
    }
}
