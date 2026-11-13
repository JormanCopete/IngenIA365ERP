using IngenIA365ERP.Shared.Models.Compras;
using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Services.Compras;

/// <summary>
/// Devoluciones a proveedor (feature 012, T352; contracts/api.md §14.6): la lista genérica de la clase y el detalle. Guardar,
/// confirmar y anular van por el ciclo común de <see cref="ComprasClient"/> sobre <see cref="Rutas.Devoluciones"/>.
/// </summary>
public sealed partial class ComprasClient
{
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeDocumentoDto>>> ListarDevolucionesAsync(FiltroDeCompras filtro, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ResumenDeDocumentoDto>>(HttpMethod.Get, InventarioClient.ConQuery(Rutas.Devoluciones, Query(filtro)), null, null, ct);

    public Task<ResultadoDeInventario<DocumentoDeCompraDto>> ObtenerDevolucionAsync(Guid id, CancellationToken ct = default) => ObtenerAsync(Rutas.Devoluciones, id, ct);

    /// <summary>
    /// Revisión comercial (2026-11-12): lo que la devolución pide corregir en lo facturado —la nota crédito del proveedor o la nota de ajuste del
    /// documento soporte— con sus líneas y si ya se registró. Es una consulta.
    /// </summary>
    public Task<ResultadoDeInventario<NotaPorDevolucionDto>> NotaPorDevolucionAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<NotaPorDevolucionDto>(HttpMethod.Get, $"{Rutas.Devoluciones}/{id}/credit-proposal", null, null, ct);
}
