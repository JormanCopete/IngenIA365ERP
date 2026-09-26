using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Transfers;

/// <summary>
/// La fuente de aprobación de las diferencias de traslado (feature 012, US10, T371, T372; <c>SourceType = TransferDiscrepancy</c>;
/// contracts/api.md §11, §15.2). La solicitud la abre <c>ResolveTransferDiscrepancyCommand</c> con la diferencia como origen; la
/// última aprobación confirma el documento que la resuelve y la resuelve (<see cref="CierreDeDiferencias"/>), en la transacción del
/// aprobador; el rechazo o el retiro descartan ese documento y la devuelven a pendiente. Está al alcance de quien ve el destino o
/// el origen del traslado. (nuevo)
/// </summary>
public sealed class FuenteDeAprobacionDeDiferencia(IApplicationDbContext db, CierreDeDiferencias cierre) : IFuenteDeAprobacion
{
    public string SourceType => ApprovalSourceTypes.TransferDiscrepancy;

    public async Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct)
    {
        var diferencia = await cierre.BuscarAsync(solicitud.SourcePublicId, ct);
        if (diferencia is null) return Result.Failure<EstadoDeFuenteDto>(ErroresDeTraslados.DiscrepancyNotFound());
        var confirmada = await cierre.AlAprobarAsync(diferencia, ct);
        if (confirmada.IsFailure) return Result.Failure<EstadoDeFuenteDto>(confirmada.Error);
        var r = confirmada.Value;
        var clase = await db.InventoryDocuments.AsNoTracking().Where(d => d.PublicId == r.PublicId).Select(d => d.Class).FirstAsync(ct);
        return Result.Success(new EstadoDeFuenteDto(r.PublicId, clase.ToString(), r.Status.ToString(), r.DisplayNumber));
    }

    public async Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct)
    {
        var diferencia = await cierre.BuscarAsync(solicitud.SourcePublicId, ct);
        if (diferencia is null) return Result.Failure<EstadoDeFuenteDto>(ErroresDeTraslados.DiscrepancyNotFound());
        var documentoId = diferencia.ResolutionDocumentId;
        var devuelta = await cierre.AlRechazarAsync(diferencia, motivo, ct);
        if (devuelta.IsFailure) return Result.Failure<EstadoDeFuenteDto>(devuelta.Error);
        var documento = documentoId is int id ? await db.InventoryDocuments.FirstOrDefaultAsync(d => d.Id == id, ct) : null;
        return Result.Success(new EstadoDeFuenteDto(documento?.PublicId ?? diferencia.PublicId, documento?.Class.ToString(),
            documento?.Status.ToString(), documento is null ? null : VistaDeDocumentos.NumeroVisible(documento.Prefix, documento.Number)));
    }

    public async Task<bool> EnAlcanceAsync(ApprovalRequest solicitud, AlcanceDeInventario alcance, CancellationToken ct)
    {
        if (alcance.TodasLasBodegas) return true;
        var bodegas = await db.TransferDiscrepancies.AsNoTracking().Where(d => d.PublicId == solicitud.SourcePublicId)
            .Join(db.InventoryDocuments.AsNoTracking(), d => d.DispatchDocumentId, x => x.Id, (d, x) => new { x.WarehouseId, x.DestinationWarehouseId })
            .FirstOrDefaultAsync(ct);
        return bodegas is not null
            && ((bodegas.DestinationWarehouseId is int destino && alcance.IncluyeBodega(destino))
                || (bodegas.WarehouseId is int origen && alcance.IncluyeBodega(origen)));
    }

    public async Task<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>> DescribirAsync(IReadOnlyCollection<Guid> sourcePublicIds, CancellationToken ct)
    {
        var filas = await db.TransferDiscrepancies.AsNoTracking().Where(d => sourcePublicIds.Contains(d.PublicId))
            .Join(db.InventoryDocuments.AsNoTracking(), d => d.DispatchDocumentId, x => x.Id, (d, x) => new { Diferencia = d, Despacho = x })
            .ToListAsync(ct);
        var productos = filas.Select(f => f.Diferencia.ProductId).Distinct().ToList();
        var codigos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => productos.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        var bodegaIds = filas.Select(f => f.Despacho.DestinationWarehouseId).OfType<int>().Distinct().ToList();
        var bodegas = await db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => bodegaIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.PublicId, ct);
        var documentos = filas.Select(f => f.Diferencia.ResolutionDocumentId).OfType<int>().Distinct().ToList();
        var resolventes = await db.InventoryDocuments.AsNoTracking().Include(d => d.DocumentType).Where(d => documentos.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);

        return filas.ToDictionary(f => f.Diferencia.PublicId, f =>
        {
            var d = f.Diferencia;
            var resolvente = d.ResolutionDocumentId is int r ? resolventes.GetValueOrDefault(r) : null;
            var numero = VistaDeDocumentos.NumeroVisible(f.Despacho.Prefix, f.Despacho.Number);
            return new OrigenDeAprobacionDto(
                d.PublicId,
                resolvente?.Class.ToString() ?? ApprovalSubjects.TransferDiscrepancy,
                resolvente?.DocumentType is { } t ? new TipoDeOrigenDto(t.Code, t.Name) : null,
                numero,
                resolvente?.OperationDate ?? f.Despacho.OperationDate,
                f.Despacho.DestinationWarehouseId is int b && bodegas.TryGetValue(b, out var bodega) ? bodega : null,
                null,
                $"{(d.Kind == Domain.Enums.Inventory.TransferDiscrepancyKind.Shortage ? "Faltante" : "Sobrante")} de {d.QuantityBase:0.####} "
                    + $"{codigos.GetValueOrDefault(d.ProductId)} en el traslado {numero}: {d.Resolution}. {d.ResolutionReason}",
                $"/inventario/traslados/{f.Despacho.PublicId}");
        });
    }
}
