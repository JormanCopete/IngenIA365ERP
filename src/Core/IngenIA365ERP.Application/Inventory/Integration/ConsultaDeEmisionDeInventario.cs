using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// Inventario le dice a la bandeja de documentos electrónicos qué documentos comerciales ve quien consulta y cómo se describen (feature 012, I4,
/// T715): el alcance es el de <see cref="IAlcanceDeInventario"/> —un documento se ve si su bodega o su punto de venta están en el alcance—, y la
/// ruta es la de su pantalla (ventas o documento soporte). La plataforma no lee <c>INV_</c>: llega aquí por
/// <see cref="IConsultaDeFuenteElectronica"/>. (nuevo)
/// </summary>
public sealed class ConsultaDeEmisionDeInventario(IApplicationDbContext db, IAlcanceDeInventario alcance) : IConsultaDeFuenteElectronica
{
    public string SourceModule => FuenteDeEmisionDeInventario.Modulo;

    public async Task<IQueryable<Guid>?> VisiblesAsync(CancellationToken ct)
    {
        var a = await alcance.ObtenerAsync(ct);
        if (a.TodasLasBodegas && a.TodosLosPuntos) return null;
        var bodegas = a.Bodegas.ToList();
        var puntos = a.Puntos.ToList();
        var todasLasBodegas = a.TodasLasBodegas;
        var todosLosPuntos = a.TodosLosPuntos;
        return db.InventoryDocuments.AsNoTracking()
            .Where(d => (d.WarehouseId != null && (todasLasBodegas || bodegas.Contains(d.WarehouseId.Value)))
                        || (d.PointOfSaleId != null && (todosLosPuntos || puntos.Contains(d.PointOfSaleId.Value))))
            .Select(d => d.PublicId);
    }

    public async Task<IReadOnlyDictionary<Guid, OrigenDelDocumentoDto>> DescribirAsync(IReadOnlyCollection<Guid> documentos, CancellationToken ct)
    {
        var filas = await db.InventoryDocuments.AsNoTracking()
            .Where(d => documentos.Contains(d.PublicId))
            .Select(d => new { d.PublicId, d.Class })
            .ToListAsync(ct);
        return filas.ToDictionary(f => f.PublicId, f => new OrigenDelDocumentoDto(SourceModule, f.PublicId, f.Class.ToString(), Ruta(f.Class, f.PublicId)));
    }

    /// <summary>La pantalla del documento comercial.</summary>
    public static string Ruta(DocumentClass clase, Guid publicId) => clase is DocumentClass.SupportDocument or DocumentClass.SupportDocumentAdjustmentNote
        ? $"/compras/documentos-soporte?documento={publicId}"
        : $"/ventas/documentos/{publicId}";
}
