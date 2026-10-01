using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing.Common;

/// <summary>
/// La misma factura del proveedor no se registra dos veces (feature 012, T337; data-model §9.2; api.md §14.4). Entre los
/// registros no liberados (<c>IsReleased = 0</c>): (proveedor, clase, prefijo, número) y el CUFE. La regla se comprueba al
/// guardar (<see cref="BuscarAsync"/>, nombrando el registro existente) y la garantizan los dos índices únicos filtrados de
/// <c>INV_SupplierInvoiceDetails</c>; si dos personas registran la misma a la vez, el segundo <c>SaveChanges</c> choca con
/// el índice y <see cref="TraducirAsync"/> lo convierte en el mismo error, nombrando el que ganó. (nuevo)
/// </summary>
public static class ColisionDeFacturaDeProveedor
{
    /// <summary>El índice de (proveedor, clase, prefijo, número).</summary>
    public const string IndiceDelNumero = "UK_INV_SupplierInvoiceDetails_Supplier_Class_Number";

    /// <summary>El índice del CUFE.</summary>
    public const string IndiceDelCufe = "UK_INV_SupplierInvoiceDetails_Cufe";

    /// <summary>
    /// El registro vivo con el mismo número del mismo proveedor, o con el mismo CUFE, distinto de <paramref name="detalle"/>.
    /// Nulo si no hay.
    /// </summary>
    public static async Task<Error?> BuscarAsync(IApplicationDbContext db, SupplierInvoiceDetail detalle, CancellationToken ct)
    {
        var propio = detalle.DocumentId;
        var mismoNumero = await db.SupplierInvoiceDetails.AsNoTracking()
            .Where(d => !d.IsReleased && d.DocumentId != propio && d.SupplierPersonId == detalle.SupplierPersonId
                && d.DocumentClass == detalle.DocumentClass && d.SupplierPrefix == detalle.SupplierPrefix && d.SupplierNumber == detalle.SupplierNumber)
            .Select(d => d.DocumentId).FirstOrDefaultAsync(ct);
        if (mismoNumero != 0) return await DuplicadoAsync(db, mismoNumero, detalle.NumeroVisible, ct);

        if (detalle.Cufe is { } cufe)
        {
            var mismoCufe = await db.SupplierInvoiceDetails.AsNoTracking()
                .Where(d => !d.IsReleased && d.DocumentId != propio && d.Cufe == cufe)
                .Select(d => d.DocumentId).FirstOrDefaultAsync(ct);
            if (mismoCufe != 0) return await CufeDuplicadoAsync(db, mismoCufe, ct);
        }
        return null;
    }

    /// <summary>¿La excepción es la violación de uno de los dos índices?</summary>
    public static bool Es(DbUpdateException ex) => Indice(ex) is not null;

    /// <summary>El error de la carrera, nombrando el registro que quedó; nulo si la excepción es de otro índice.</summary>
    public static async Task<Error?> TraducirAsync(IApplicationDbContext db, DbUpdateException ex, SupplierInvoiceDetail detalle, CancellationToken ct)
    {
        var indice = Indice(ex);
        if (indice is null) return null;
        db.DescartarCambios();
        return await BuscarAsync(db, detalle, ct)
               ?? (indice == IndiceDelCufe ? ErroresDeCompras.CufeDuplicate(Guid.Empty, null) : ErroresDeCompras.Duplicate(Guid.Empty, null, detalle.NumeroVisible));
    }

    private static string? Indice(DbUpdateException ex)
    {
        for (Exception? actual = ex; actual is not null; actual = actual.InnerException)
        {
            if (actual.Message.Contains(IndiceDelNumero, StringComparison.OrdinalIgnoreCase)) return IndiceDelNumero;
            if (actual.Message.Contains(IndiceDelCufe, StringComparison.OrdinalIgnoreCase)) return IndiceDelCufe;
        }
        return null;
    }

    private static async Task<Error> DuplicadoAsync(IApplicationDbContext db, int documentoId, string numero, CancellationToken ct)
    {
        var d = await db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == documentoId)
            .Select(x => new { x.PublicId, x.Prefix, x.Number }).FirstAsync(ct);
        return ErroresDeCompras.Duplicate(d.PublicId, VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number), numero);
    }

    private static async Task<Error> CufeDuplicadoAsync(IApplicationDbContext db, int documentoId, CancellationToken ct)
    {
        var d = await db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == documentoId)
            .Select(x => new { x.PublicId, x.Prefix, x.Number }).FirstAsync(ct);
        return ErroresDeCompras.CufeDuplicate(d.PublicId, VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number));
    }

    /// <summary>¿La clase lleva el documento del proveedor? (factura y nota).</summary>
    public static bool LlevaDocumentoDelProveedor(DocumentClass clase) => clase is DocumentClass.SupplierInvoice or DocumentClass.SupplierNote;
}
