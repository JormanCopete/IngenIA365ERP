using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>
/// La fuente de aprobación de los documentos de inventario (feature 012, T146; <c>SourceType = InventoryDocument</c>;
/// contracts/api.md §15.2). La última aprobación reentra por el flujo canónico de <see cref="ConfirmacionDeDocumento"/>
/// en la transacción del aprobador (existencia, período, numeración se vuelven a comprobar); el rechazo o el retiro
/// devuelven el documento a borrador para corregirlo. (nuevo)
/// </summary>
public sealed class FuenteDeAprobacionDeDocumento(
    IApplicationDbContext db,
    IMaestrosDelDocumento maestros,
    ConfirmacionDeDocumento confirmacion) : IFuenteDeAprobacion
{
    public string SourceType => ApprovalSourceTypes.InventoryDocument;

    public async Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct)
    {
        var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(solicitud.SourcePublicId, null, PorAprobacion: true), ct);
        if (confirmada.IsFailure) return Result.Failure<EstadoDeFuenteDto>(confirmada.Error);
        var r = confirmada.Value;
        var clase = await db.InventoryDocuments.AsNoTracking().Where(d => d.PublicId == r.PublicId).Select(d => d.Class).FirstAsync(ct);
        return Result.Success(new EstadoDeFuenteDto(r.PublicId, clase.ToString(), r.Status.ToString(), r.DisplayNumber));
    }

    public async Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct)
    {
        var documento = await db.InventoryDocuments.FirstOrDefaultAsync(d => d.PublicId == solicitud.SourcePublicId, ct);
        if (documento is null) return Result.Failure<EstadoDeFuenteDto>(InventoryErrors.DocumentNotFound());
        if (documento.Status == DocumentStatus.PendingApproval) documento.DevolverABorrador();
        return Result.Success(new EstadoDeFuenteDto(documento.PublicId, documento.Class.ToString(), documento.Status.ToString(),
            VistaDeDocumentos.NumeroVisible(documento.Prefix, documento.Number)));
    }

    public async Task<bool> EnAlcanceAsync(ApprovalRequest solicitud, AlcanceDeInventario alcance, CancellationToken ct)
    {
        var documento = await db.InventoryDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.PublicId == solicitud.SourcePublicId, ct);
        if (documento is null) return false;
        var origenes = documento.WarehouseId is null && documento.DestinationWarehouseId is null
            ? await FiltroDeAlcance.BodegasDeSusOrigenesAsync(db, documento.Id, ct)
            : [];
        return FiltroDeAlcance.DocumentoVisible(alcance, documento, origenes);
    }

    public async Task<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>> DescribirAsync(IReadOnlyCollection<Guid> sourcePublicIds, CancellationToken ct)
    {
        var documentos = await db.InventoryDocuments.AsNoTracking().Include(d => d.DocumentType)
            .Where(d => sourcePublicIds.Contains(d.PublicId)).ToListAsync(ct);
        var bodegas = (await maestros.BodegasPorIdAsync(documentos.Select(d => d.WarehouseId).OfType<int>().Distinct().ToList(), ct))
            .ToDictionary(b => b.Id);
        return documentos.ToDictionary(d => d.PublicId, d => new OrigenDeAprobacionDto(
            d.PublicId,
            d.Class.ToString(),
            d.DocumentType is null ? null : new TipoDeOrigenDto(d.DocumentType.Code, d.DocumentType.Name),
            VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number),
            d.OperationDate,
            d.WarehouseId is int b && bodegas.TryGetValue(b, out var bodega) ? bodega.PublicId : null,
            null,
            $"{d.DocumentType?.Name ?? d.Class.ToString()}: valor al costo {d.CostTotal:N2}; total {d.Total:N2}",
            // La pantalla de cada grupo la pone su historia (US2 ajustes, US4 saldo inicial…); sin página, sin enlace.
            null));
    }
}
