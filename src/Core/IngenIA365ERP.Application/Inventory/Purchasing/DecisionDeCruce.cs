using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// La decisión de una excepción del cruce a tres vías (feature 012, I5, T795, T796; <c>SourceType = PurchaseMatchLine</c>, sujeto
/// <c>PurchaseMatchException</c>; contracts/api.md §14.9; data-model §9.6, §21), sobre el punto de extensión del motor de aprobaciones
/// de plataforma (T33). (nuevo)
/// <list type="bullet">
/// <item>una línea retenida por <b>cantidad</b> (facturar más de lo recibido) no se aprueba en ningún nivel:
/// <c>Inventory.PurchaseMatch.QuantityNotApprovable</c> y la decisión no queda (T796, decisiones-transversales §3 T42a);</item>
/// <item>la aprobación de una línea la marca <c>Approved</c>; si todavía queda otra retenida de la misma factura, la factura sigue en
/// <c>PendingApproval</c>; si era la última, la factura vuelve a entrar por el flujo canónico (<see cref="ConfirmacionDeDocumento"/>,
/// <c>PorAprobacion</c>) en la transacción del aprobador: la diferencia de precio va al kardex como <c>CostAdjustment</c>
/// <c>PriceDifference</c>, partida entre existencia y vendido por el costeo, con un <c>AjusteDeCostoReconocido</c> por recepción
/// afectada (la estrategia de la factura, E6);</item>
/// <item>el rechazo (o el retiro) la marca <c>Rejected</c>, devuelve la factura a borrador y cancela las otras solicitudes pendientes de
/// la misma factura; las filas quedan como historia hasta el siguiente intento, que las da de baja.</item>
/// </list>
/// <see cref="ConfirmacionDeDocumento"/> se resuelve al aprobar, no en el constructor (el círculo motor → fuentes).
/// </summary>
public sealed class DecisionDeCruce(IApplicationDbContext db, IDateTimeService reloj, IServiceProvider servicios)
    : IFuenteDeAprobacion, IFuenteConAprobador
{
    public string SourceType => ApprovalSourceTypes.PurchaseMatchLine;

    public async Task<Result> AlAprobarElNivelAsync(ApprovalRequest solicitud, int aprobadorUserId, ApprovalMethod metodo, bool esElUltimo, CancellationToken ct)
    {
        var fila = await FilaAsync(solicitud, ct);
        if (fila is null) return Result.Failure(InventoryErrors.DocumentNotFound());
        if (!fila.Aprobable) return Result.Failure(await CantidadNoAprobableAsync(fila, ct));
        return Result.Success();
    }

    public async Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct)
    {
        var fila = await FilaAsync(solicitud, ct);
        if (fila is null || !fila.EstaRetenida) return Result.Failure<EstadoDeFuenteDto>(InventoryErrors.DocumentNotFound());
        if (!fila.Aprobable) return Result.Failure<EstadoDeFuenteDto>(await CantidadNoAprobableAsync(fila, ct));
        fila.Aprobar();

        var factura = await db.InventoryDocuments.FirstAsync(d => d.Id == fila.InvoiceDocumentId, ct);
        var otras = (await db.PurchaseMatchLines
                .Where(m => m.InvoiceDocumentId == fila.InvoiceDocumentId && !m.IsDeleted && m.Status == PurchaseMatchStatus.Held)
                .ToListAsync(ct))
            .Where(m => !m.IsDeleted && m.EstaRetenida && m != fila)
            .ToList();
        if (otras.Count > 0)
            return Result.Success(new EstadoDeFuenteDto(factura.PublicId, factura.Class.ToString(), factura.Status.ToString(),
                VistaDeDocumentos.NumeroVisible(factura.Prefix, factura.Number)));

        var confirmacion = servicios.GetRequiredService<ConfirmacionDeDocumento>();
        var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(factura.PublicId, null, PorAprobacion: true), ct);
        if (confirmada.IsFailure) return Result.Failure<EstadoDeFuenteDto>(confirmada.Error);
        var r = confirmada.Value;
        return Result.Success(new EstadoDeFuenteDto(r.PublicId, factura.Class.ToString(), r.Status.ToString(), r.DisplayNumber));
    }

    public async Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct)
    {
        var fila = await FilaAsync(solicitud, ct);
        if (fila is null) return Result.Failure<EstadoDeFuenteDto>(InventoryErrors.DocumentNotFound());
        if (fila.EstaRetenida) fila.Rechazar();

        var factura = await db.InventoryDocuments.FirstAsync(d => d.Id == fila.InvoiceDocumentId, ct);
        if (factura.Status == DocumentStatus.PendingApproval) factura.DevolverABorrador();

        // Las otras solicitudes del cruce de la misma factura ya no tienen qué aprobar: la factura volvió a borrador.
        var hermanas = await db.PurchaseMatchLines.AsNoTracking()
            .Where(m => m.InvoiceDocumentId == fila.InvoiceDocumentId && !m.IsDeleted && m.PublicId != fila.PublicId)
            .Select(m => m.PublicId).ToListAsync(ct);
        var ahora = reloj.UtcNow;
        foreach (var otra in await db.ApprovalRequests.Where(r => r.SourceType == ApprovalSourceTypes.PurchaseMatchLine && hermanas.Contains(r.SourcePublicId)
                     && r.Status == ApprovalRequestStatus.Pending).ToListAsync(ct))
        {
            otra.Status = ApprovalRequestStatus.Cancelled;
            otra.DecidedAt = ahora;
        }
        return Result.Success(new EstadoDeFuenteDto(factura.PublicId, factura.Class.ToString(), factura.Status.ToString(),
            VistaDeDocumentos.NumeroVisible(factura.Prefix, factura.Number)));
    }

    public async Task<bool> EnAlcanceAsync(ApprovalRequest solicitud, AlcanceDeInventario alcance, CancellationToken ct)
    {
        var factura = await (from m in db.PurchaseMatchLines.AsNoTracking()
                             join d in db.InventoryDocuments.AsNoTracking() on m.InvoiceDocumentId equals d.Id
                             where m.PublicId == solicitud.SourcePublicId
                             select d).FirstOrDefaultAsync(ct);
        if (factura is null) return false;
        var origenes = await FiltroDeAlcance.BodegasDeSusOrigenesAsync(db, factura.Id, ct);
        return FiltroDeAlcance.DocumentoVisible(alcance, factura, origenes);
    }

    public async Task<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>> DescribirAsync(IReadOnlyCollection<Guid> sourcePublicIds, CancellationToken ct)
    {
        var filas = await (from m in db.PurchaseMatchLines.AsNoTracking()
                           join d in db.InventoryDocuments.AsNoTracking() on m.InvoiceDocumentId equals d.Id
                           join l in db.InventoryDocumentLines.AsNoTracking() on m.InvoiceLineId equals l.Id
                           join p in db.Products.AsNoTracking() on l.ProductId equals p.Id
                           where sourcePublicIds.Contains(m.PublicId)
                           select new
                           {
                               m.PublicId, m.Reasons, m.QuantityDifference, m.PriceDifferenceAmount, m.PriceDifferenceRate,
                               l.LineNumber, Producto = p.Code + " " + p.Name, d.Class, d.Prefix, d.Number, d.OperationDate,
                               DocumentPublicId = d.PublicId, d.DocumentTypeId,
                           })
            .ToListAsync(ct);
        var tipos = filas.Select(f => f.DocumentTypeId).Distinct().ToList();
        var deTipo = await db.InventoryDocumentTypes.AsNoTracking().Where(t => tipos.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => new TipoDeOrigenDto(t.Code, t.Name), ct);
        return filas.ToDictionary(f => f.PublicId, f => new OrigenDeAprobacionDto(
            f.PublicId,
            f.Class.ToString(),
            deTipo.GetValueOrDefault(f.DocumentTypeId),
            VistaDeDocumentos.NumeroVisible(f.Prefix, f.Number),
            f.OperationDate,
            null,
            null,
            $"Cruce de compra, línea {f.LineNumber} ({f.Producto}): "
                + (f.Reasons ?? string.Empty).Replace(CruceDeCompra.RazonCantidad, $"facturado de más {f.QuantityDifference:N4}")
                    .Replace(CruceDeCompra.RazonPrecio, $"diferencia de precio {f.PriceDifferenceAmount:N2} ({f.PriceDifferenceRate:P2})")
                    .Replace(",", "; "),
            "/compras/cruce"));
    }

    private async Task<PurchaseMatchLine?> FilaAsync(ApprovalRequest solicitud, CancellationToken ct) =>
        db.PurchaseMatchLines.Local.FirstOrDefault(m => m.PublicId == solicitud.SourcePublicId && !m.IsDeleted)
        ?? await db.PurchaseMatchLines.FirstOrDefaultAsync(m => m.PublicId == solicitud.SourcePublicId && !m.IsDeleted, ct);

    private async Task<Error> CantidadNoAprobableAsync(PurchaseMatchLine fila, CancellationToken ct)
    {
        var numero = await db.InventoryDocumentLines.AsNoTracking().Where(l => l.Id == fila.InvoiceLineId).Select(l => l.LineNumber).FirstOrDefaultAsync(ct);
        return ErroresDeCompras.QuantityNotApprovable(numero, fila.ReceivedNotInvoicedQuantity, fila.InvoicedQuantity);
    }
}
