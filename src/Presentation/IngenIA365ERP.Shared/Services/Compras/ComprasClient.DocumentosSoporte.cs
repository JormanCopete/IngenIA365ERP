using IngenIA365ERP.Shared.Models.Compras;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Services.Compras;

/// <summary>
/// El documento soporte y su nota de ajuste (feature 012, I4, T752, T757; contracts/api.md §14.7): la lista por clase (el documento
/// soporte, o con <c>notas</c> sus notas de ajuste, que se crean en la misma ruta con un tipo de su clase y el documento soporte que
/// corrigen en <c>supplierInvoicePublicId</c>), el detalle y el ciclo común sobre <see cref="Rutas.DocumentosSoporte"/>, y la generación
/// semanal de borradores por proveedor no obligado. Su estado ante la DIAN vive en la bandeja de documentos electrónicos. (nuevo)
/// </summary>
public sealed partial class ComprasClient
{
    /// <summary>La clase de la nota de ajuste del documento soporte, por nombre (<c>DocumentClass.SupportDocumentAdjustmentNote</c>).</summary>
    public const string ClaseNotaDeAjusteDeSoporte = "SupportDocumentAdjustmentNote";

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeFacturaDeProveedorDto>>> ListarDocumentosSoporteAsync(FiltroDeCompras filtro,
        bool notas = false, CancellationToken ct = default)
    {
        var query = Query(filtro);
        if (notas) query = string.IsNullOrEmpty(query) ? $"class={ClaseNotaDeAjusteDeSoporte}" : $"{query}&class={ClaseNotaDeAjusteDeSoporte}";
        return EnviarAsync<PaginaDeInventarioDto<ResumenDeFacturaDeProveedorDto>>(HttpMethod.Get, InventarioClient.ConQuery(Rutas.DocumentosSoporte, query),
            null, null, ct);
    }

    /// <summary>El documento soporte o su nota de ajuste.</summary>
    public Task<ResultadoDeInventario<DocumentoDeCompraDto>> ObtenerDocumentoSoporteAsync(Guid id, CancellationToken ct = default) =>
        ObtenerAsync(Rutas.DocumentosSoporte, id, ct);

    /// <summary>
    /// La generación semanal (<c>DocumentoSoporte.Generacion = Semanal</c>): un borrador por proveedor no obligado con las recepciones de la
    /// semana hasta <paramref name="hasta"/> (nulo = hoy) que no tienen factura ni documento soporte.
    /// </summary>
    public Task<ResultadoDeInventario<IReadOnlyList<DocumentoSoporteSemanalDto>>> GenerarDocumentosSoporteSemanalesAsync(DateOnly? hasta, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<DocumentoSoporteSemanalDto>>(HttpMethod.Post, $"{Rutas.DocumentosSoporte}/weekly", new GenerarSemanalRequest(hasta), clave, ct);
}
