using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// La vista <c>radian-events</c> de <c>/api/reports/inventory</c> (feature 012, T349; FR-050, US9-4; decisiones-transversales
/// §2.12): por factura del proveedor, el proveedor, el número y el CUFE, emisión, vencimiento, forma de pago, estado del registro
/// y la fecha y la fuente del acuse (030) y del recibo del bien (032), y los días que lleva sin eventos. Filtros <c>from</c>/<c>to</c>
/// (sobre la emisión), <c>person</c> (el proveedor) y el propio <c>onlyPending</c>. La columna oculta <c>_documento</c> lleva el
/// PublicId del registro para abrirlo. El alcance se aplica por <see cref="IAlcanceDeInventario"/>: la factura sin bodega es
/// visible si lo es alguna de sus recepciones. (nuevo)
/// </summary>
public sealed record RadianEventsReportQuery(FiltrosDeInformeDeInventario Filtros, bool OnlyPending = false) : IRequest<Result<TablaExportable>>;

public sealed class RadianEventsReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<RadianEventsReportQuery, Result<TablaExportable>>
{
    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Proveedor", TipoDeColumna.Texto),
        new("Factura", TipoDeColumna.Texto),
        new("CUFE", TipoDeColumna.Texto),
        new("Emisión", TipoDeColumna.Fecha),
        new("Vencimiento", TipoDeColumna.Fecha),
        new("Forma de pago", TipoDeColumna.Texto),
        new("Registro", TipoDeColumna.Texto),
        new("Estado", TipoDeColumna.Texto),
        new("Acuse (030)", TipoDeColumna.Texto),
        new("Fecha 030", TipoDeColumna.Fecha),
        new("Fuente 030", TipoDeColumna.Texto),
        new("Recibo del bien (032)", TipoDeColumna.Texto),
        new("Fecha 032", TipoDeColumna.Fecha),
        new("Fuente 032", TipoDeColumna.Texto),
        new("Días sin eventos", TipoDeColumna.Entero),
        new("Documento", TipoDeColumna.Texto, "_documento"),
    ];

    public async Task<Result<TablaExportable>> Handle(RadianEventsReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.InventoryDocuments.AsNoTracking().Where(d => d.Class == DocumentClass.SupplierInvoice && d.Status != DocumentStatus.Draft && d.Status != DocumentStatus.Discarded)
            .DocumentosVisibles(alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking());
        if (f.Person is { } persona) consulta = consulta.Where(d => db.People.Any(p => p.Id == d.CounterpartyPersonId && p.PublicId == persona));

        var filas = await (
                from d in consulta
                join s in db.SupplierInvoiceDetails.AsNoTracking() on d.Id equals s.DocumentId
                join p in db.People.AsNoTracking() on s.SupplierPersonId equals p.Id
                select new
                {
                    d.Id, d.PublicId, d.Prefix, d.Number, d.Status,
                    Proveedor = p.BusinessName ?? (p.FirstName + " " + p.LastName), p.TaxId,
                    s.SupplierPrefix, s.SupplierNumber, s.Cufe, s.IssueDate, s.DueDate, s.IsCredit,
                })
            .ToListAsync(ct);
        if (f.From is { } desde) filas = filas.Where(x => x.IssueDate >= desde).ToList();
        if (f.To is { } hasta) filas = filas.Where(x => x.IssueDate <= hasta).ToList();

        var ids = filas.Select(x => x.Id).ToList();
        var eventos = await db.SupplierInvoiceEvents.AsNoTracking().Where(e => ids.Contains(e.DocumentId)).ToListAsync(ct);
        if (request.OnlyPending)
            filas = filas.Where(x => eventos.Any(e => e.DocumentId == x.Id && e.Status == SupplierInvoiceEventStatus.Pending)).ToList();

        var tabla = filas.OrderBy(x => x.IssueDate).ThenBy(x => x.SupplierNumber, StringComparer.Ordinal).Select(x =>
        {
            var e030 = eventos.FirstOrDefault(e => e.DocumentId == x.Id && e.EventCode == SupplierInvoiceEventCode.Receipt030);
            var e032 = eventos.FirstOrDefault(e => e.DocumentId == x.Id && e.EventCode == SupplierInvoiceEventCode.GoodsReceived032);
            var pendiente = e030?.Status == SupplierInvoiceEventStatus.Pending || e032?.Status == SupplierInvoiceEventStatus.Pending;
            return new FilaExportable(
            [
                $"{x.TaxId} · {x.Proveedor}", $"{x.SupplierPrefix}{x.SupplierNumber}", x.Cufe, x.IssueDate, x.DueDate,
                x.IsCredit ? "Crédito" : "Contado", VistaDeDocumentos.NumeroVisible(x.Prefix, x.Number), x.Status.ToString(),
                Estado(e030?.Status), e030?.EventDate, e030?.Source,
                Estado(e032?.Status), e032?.EventDate, e032?.Source,
                pendiente ? hoy.DayNumber - x.IssueDate.DayNumber : null,
                x.PublicId.ToString(),
            ], Resaltada: pendiente);
        }).ToList();

        return Result.Success(new TablaExportable("Eventos RADIAN de facturas de proveedor", "Acuse de recibo (030) y recibo del bien (032)",
            Columnas, tabla, null,
            ["Hasta la entrega I5 la cooperativa emite los eventos en el portal de la DIAN o del proveedor y el ERP anota quién dijo haberlo hecho."]));
    }

    private static string Estado(SupplierInvoiceEventStatus? estado) => estado switch
    {
        SupplierInvoiceEventStatus.Pending => "Pendiente",
        SupplierInvoiceEventStatus.RegisteredExternally => "Registrado por fuera",
        SupplierInvoiceEventStatus.Emitted => "Emitido",
        SupplierInvoiceEventStatus.Rejected => "Rechazado",
        SupplierInvoiceEventStatus.NotApplicable => "No aplica",
        _ => string.Empty,
    };
}
