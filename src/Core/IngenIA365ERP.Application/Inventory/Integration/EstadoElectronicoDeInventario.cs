using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// El estado electrónico de un documento comercial de Inventario, visto desde Inventario (feature 012, I4, T738, T743; FR-066; api.md §18.2,
/// §18.3, §20.3). La plataforma no lee <c>INV_</c>; Inventario sí lee su propio documento electrónico por <c>SourceModule = "INV"</c> y
/// <c>SourceDocumentPublicId</c>. Las reglas de corrección que dependen de él viven aquí para que anular y hacer nota digan lo mismo. (nuevo)
/// </summary>
public static class EstadoElectronicoDeInventario
{
    /// <summary>El documento electrónico del comercial <paramref name="comercial"/>, sin seguimiento; nulo si no emite o todavía no se registró.</summary>
    public static Task<ElectronicDocument?> DeAsync(IApplicationDbContext db, Guid comercial, CancellationToken ct) =>
        db.ElectronicDocuments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SourceModule == FuenteDeEmisionDeInventario.Modulo && e.SourceDocumentPublicId == comercial, ct);

    /// <summary>¿Validado por la DIAN (con o sin notificaciones)?</summary>
    public static bool Validado(ElectronicDocumentStatus estado) =>
        estado is ElectronicDocumentStatus.Validated or ElectronicDocumentStatus.ValidatedWithNotices;

    /// <summary>
    /// Lo que impide corregir (anular o hacer nota sobre) un fiscal electrónico por su estado: enviado sin respuesta →
    /// <c>ElectronicInvoicing.Document.AwaitingResponse</c>; rechazado → <c>Inventory.Document.FiscalUseCorrection</c> con la ruta de los casos
    /// a, b y c (un rechazado no está expedido). Nulo en los demás (validado, en contingencia, pendiente de transmitir, o sin documento).
    /// </summary>
    public static async Task<Error?> CorreccionImpedidaAsync(IApplicationDbContext db, Guid comercial, CancellationToken ct)
    {
        var electronico = await DeAsync(db, comercial, ct);
        return electronico?.Status switch
        {
            ElectronicDocumentStatus.Sent => ErroresDeVentas.AwaitingResponse(electronico.PublicId),
            ElectronicDocumentStatus.Rejected => ErroresDeVentas.FiscalUseCorrectionRejected(electronico.PublicId),
            _ => null,
        };
    }
}
