using IngenIA365ERP.Shared.Models.Compras;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Services.Compras;

/// <summary>
/// Recepciones y compra directa (feature 012, T352; contracts/api.md §14.2, §14.3): la lista con lo que falta facturar y lo
/// devuelto, el detalle con los saldos por línea, y la compra directa en un paso (recepción y factura en una transacción).
/// </summary>
public sealed partial class ComprasClient
{
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeRecepcionDto>>> ListarRecepcionesAsync(FiltroDeCompras filtro, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ResumenDeRecepcionDto>>(HttpMethod.Get, InventarioClient.ConQuery(Rutas.Recepciones, Query(filtro)), null, null, ct);

    public Task<ResultadoDeInventario<DocumentoDeCompraDto>> ObtenerRecepcionAsync(Guid id, CancellationToken ct = default) =>
        ObtenerAsync(Rutas.Recepciones, id, ct);

    /// <summary>
    /// La compra directa (§14.3): recepción y factura con las mismas líneas. La clave es la de la operación de pantalla: el
    /// reintento con el mismo contenido responde lo mismo sin comprar dos veces.
    /// </summary>
    public Task<ResultadoDeInventario<ResultadoDeCompraDirectaDto>> CompraDirectaAsync(CompraDirectaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeCompraDirectaDto>(HttpMethod.Post, Rutas.CompraDirecta, request, clave, ct);
}
