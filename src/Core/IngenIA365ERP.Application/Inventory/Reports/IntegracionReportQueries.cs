using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// La vista <c>messages</c> de <c>/api/reports/inventory</c> (feature 012, US7, T524; FR-080; contracts/api.md §27): una entrega de
/// la bandeja por fila —número y clase del documento de origen, tipo y versión del mensaje, destino, estado, modo, fecha de
/// operación, lote, intentos, último error, cuándo se procesó y el comprobante— con las ocultas <c>_mensaje</c> y <c>_documento</c>
/// para abrir el mensaje en la bandeja y el documento. Lee <b>sólo</b> <c>COR_Integration*</c> (el resultado viene de la entrega,
/// nunca de los libros, FR-014), con el alcance de bodega de la bandeja (<see cref="VistaDeMensajes.Visibles"/>). Filtros comunes
/// <c>from</c>/<c>to</c> (fecha de operación) y <c>warehouse</c> (la bodega del sobre); propios <c>status</c>, <c>destination</c>,
/// <c>type</c> y <c>batch</c>. (nuevo)
/// </summary>
public sealed record MessagesReportQuery(
    FiltrosDeInformeDeInventario Filtros,
    DeliveryStatus? Status = null,
    string? Destination = null,
    string? Type = null,
    Guid? Batch = null) : IRequest<Result<TablaExportable>>;

public sealed class MessagesReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<MessagesReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeInventario Vista = new(
        "messages", "Mensajes a otros módulos", "Los mensajes que Inventario emitió a Contabilidad y Cartera, con su estado de entrega, lote, error y comprobante.",
        "mensajes-de-integracion", ["from", "to", "warehouse"], ["status", "destination", "type", "batch"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Número", TipoDeColumna.Texto),
        new("Tipo", TipoDeColumna.Texto),
        new("Versión", TipoDeColumna.Entero),
        new("Destino", TipoDeColumna.Texto),
        new("Estado", TipoDeColumna.Texto),
        new("Modo", TipoDeColumna.Texto),
        new("Documento", TipoDeColumna.Texto),
        new("Fecha de operación", TipoDeColumna.Fecha),
        new("Lote", TipoDeColumna.Texto),
        new("Intentos", TipoDeColumna.Entero),
        new("Último error", TipoDeColumna.Texto),
        new("Procesado", TipoDeColumna.Texto),
        new("Comprobante", TipoDeColumna.Texto),
        new("Mensaje", TipoDeColumna.Texto, "_mensaje"),
        new("Documento", TipoDeColumna.Texto, "_documento"),
    ];

    public async Task<Result<TablaExportable>> Handle(MessagesReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var desde = f.Desde(hoy);
        var hasta = f.Hasta(hoy);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        var consulta = VistaDeMensajes.PorDestinoYEstado(VistaDeMensajes.Visibles(db, alcance), request.Destination, request.Status)
            .Where(d => d.Message!.OperationDate >= desde && d.Message.OperationDate <= hasta);
        if (!string.IsNullOrWhiteSpace(request.Type)) consulta = consulta.Where(d => d.Message!.Type == request.Type);
        string? bodegaFiltrada = null;
        if (f.Warehouse is { } wp)
        {
            var bodega = await db.Warehouses.AsNoTracking().Where(w => w.PublicId == wp).Select(w => new { w.Id, w.Code }).FirstOrDefaultAsync(ct);
            if (bodega is null || !alcance.IncluyeBodega(bodega.Id)) return Result.Failure<TablaExportable>(ErroresDeAlcance.BodegaInexistente());
            consulta = consulta.Where(d => d.Message!.WarehouseCode == bodega.Code);
            bodegaFiltrada = bodega.Code;
        }
        if (request.Batch is { } lotePublico)
        {
            var loteId = await db.IntegrationBatches.AsNoTracking().Where(b => b.PublicId == lotePublico).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(d => d.BatchId == loteId);
        }

        var entregas = await consulta.Include(d => d.Message).OrderBy(d => d.Message!.OperationDate).ThenBy(d => d.MessageId).ToListAsync(ct);
        var loteIds = entregas.Select(e => e.BatchId).OfType<int>().Distinct().ToList();
        var lotes = await db.IntegrationBatches.AsNoTracking().Where(b => loteIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Number, ct);

        var filas = entregas.Select(e =>
        {
            var m = e.Message!;
            string? comprobante = e.ResultVoucherTypeCode is null ? ReferenciasDeResultado.SinComprobante(e.ResultReference)?.ToString()
                : $"{e.ResultVoucherTypeCode} {e.ResultVoucherNumber}".Trim();
            return new FilaExportable(
            [
                m.OriginNumber, m.Type, (int)m.Version, e.Destination, e.Status.ToString(), e.Mode.ToString(),
                string.Join(' ', new[] { m.OriginDocumentClass, m.OriginDocumentTypeCode }.Where(x => !string.IsNullOrWhiteSpace(x))),
                m.OperationDate, e.BatchId is int l && lotes.TryGetValue(l, out var numero) ? numero.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
                e.Attempts, e.LastErrorCode is null ? null : $"{e.LastErrorCode}: {e.LastErrorMessage}",
                e.ProcessedAt?.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), comprobante,
                m.PublicId.ToString(), m.OriginKind == MessageOriginKind.Document ? m.OriginPublicId.ToString() : null,
            ], Resaltada: e.Status is DeliveryStatus.Rejected or DeliveryStatus.ValidationFailed);
        }).ToList();

        var subtitulo = $"Del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd}" + (bodegaFiltrada is null ? string.Empty : $" · Bodega {bodegaFiltrada}");
        return Result.Success(new TablaExportable("Mensajes a otros módulos", subtitulo, Columnas, filas, null,
        [
            "Un mensaje con destino Contabilidad y Cartera sale una vez por destino. El comprobante lo informa la entrega; Inventario no lee los libros.",
        ]));
    }
}

/// <summary>
/// La vista <c>accounting-batches</c> de <c>/api/reports/inventory</c> (feature 012, US7, T524; FR-077, FR-080; contracts/api.md §27):
/// un lote de integración por fila —número, disparador, franja programada, estado, rango de fechas, granularidad, documentos,
/// mensajes, comprobantes, rechazados, quién lo pidió, inicio, fin y si va tarde (programado, sin correr pasada su franja más
/// <see cref="ToleranciaPorDefecto"/> minutos)—. Lee sólo <c>COR_IntegrationBatches</c>; con alcance de bodega parcial, sólo los lotes
/// con alguna entrega visible. Filtros comunes <c>from</c>/<c>to</c> (fecha de la solicitud); propios <c>status</c> y <c>trigger</c>.
/// (nuevo)
/// </summary>
public sealed record AccountingBatchesReportQuery(
    FiltrosDeInformeDeInventario Filtros,
    BatchStatus? Status = null,
    BatchTrigger? Trigger = null,
    int LateToleranceMinutes = AccountingBatchesReportQueryHandler.ToleranciaPorDefecto) : IRequest<Result<TablaExportable>>;

public sealed class AccountingBatchesReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<AccountingBatchesReportQuery, Result<TablaExportable>>
{
    /// <summary>La tolerancia técnica por defecto de un lote programado (la de <c>Integration:Dispatcher:LateToleranceMinutes</c>).</summary>
    public const int ToleranciaPorDefecto = 30;

    public static readonly VistaDeInformeDeInventario Vista = new(
        "accounting-batches", "Lotes contables", "Los lotes de integración con Contabilidad: disparador, estado, documentos, comprobantes, rechazos y si van tarde.",
        "lotes-contables", ["from", "to"], ["status", "trigger"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Lote", TipoDeColumna.Entero),
        new("Disparador", TipoDeColumna.Texto),
        new("Programado para", TipoDeColumna.Texto),
        new("Estado", TipoDeColumna.Texto),
        new("Rango", TipoDeColumna.Texto),
        new("Granularidad", TipoDeColumna.Texto),
        new("Documentos", TipoDeColumna.Entero),
        new("Mensajes", TipoDeColumna.Entero),
        new("Comprobantes", TipoDeColumna.Entero),
        new("Rechazados", TipoDeColumna.Entero),
        new("Solicitado por", TipoDeColumna.Texto),
        new("Inicio", TipoDeColumna.Texto),
        new("Fin", TipoDeColumna.Texto),
        new("Tarde", TipoDeColumna.Texto),
    ];

    public async Task<Result<TablaExportable>> Handle(AccountingBatchesReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var desde = f.Desde(hoy).ToDateTime(TimeOnly.MinValue);
        var hasta = f.Hasta(hoy).AddDays(1).ToDateTime(TimeOnly.MinValue);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        var lotes = db.IntegrationBatches.AsNoTracking().Where(b => b.RequestedAt >= desde && b.RequestedAt < hasta);
        if (request.Status is { } estado) lotes = lotes.Where(b => b.Status == estado);
        if (request.Trigger is { } disparador) lotes = lotes.Where(b => b.Trigger == disparador);
        if (!alcance.TodasLasBodegas)
        {
            var visibles = VistaDeMensajes.Visibles(db, alcance);
            lotes = lotes.Where(b => visibles.Any(d => d.BatchId == b.Id));
        }

        var ahoraLocal = reloj.AhoraLocal.DateTime;
        var limite = ahoraLocal.AddMinutes(-request.LateToleranceMinutes);
        static string? Instante(DateTime? t) => t?.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var filas = (await lotes.OrderBy(b => b.Number).ToListAsync(ct)).Select(b =>
        {
            var tarde = b.Trigger == BatchTrigger.Scheduled && b.Status == BatchStatus.Requested && b.ScheduledFor is { } franja && franja < limite;
            return new FilaExportable(
            [
                b.Number, b.Trigger.ToString(), Instante(b.ScheduledFor), b.Status.ToString(),
                b.DateFrom is null ? null : $"{b.DateFrom:yyyy-MM-dd} – {b.DateTo:yyyy-MM-dd}",
                b.Granularity?.ToString() ?? ClavesDeLote.Leer(b.ScheduleKey)?.GranularidadDelLote.ToString(),
                b.DocumentCount, b.MessageCount, b.VoucherCount, b.RejectedCount, b.RequestedByName,
                Instante(b.StartedAt), Instante(b.FinishedAt), tarde ? "Sí" : "No",
            ], Resaltada: tarde || b.Status == BatchStatus.CompletedWithRejections);
        }).ToList();

        return Result.Success(new TablaExportable("Lotes contables", $"Solicitados del {f.Desde(hoy):yyyy-MM-dd} al {f.Hasta(hoy):yyyy-MM-dd}", Columnas, filas, null,
        [
            $"«Tarde»: un lote programado que no corrió pasada su franja más {request.LateToleranceMinutes} minutos (levanta Integracion.LoteNoCorrio).",
        ]));
    }
}
