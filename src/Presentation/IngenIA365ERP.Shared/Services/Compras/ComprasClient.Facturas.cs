using IngenIA365ERP.Shared.Models.Compras;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Services.Compras;

/// <summary>
/// Facturas y notas del proveedor (feature 012, T352; contracts/api.md §14.4, §14.5, §14.8): listas con el documento del
/// proveedor y los eventos, el prellenado desde el XML (consulta: el archivo no se guarda) y los eventos RADIAN registrados por
/// fuera.
/// </summary>
public sealed partial class ComprasClient
{
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeFacturaDeProveedorDto>>> ListarFacturasAsync(FiltroDeCompras filtro, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ResumenDeFacturaDeProveedorDto>>(HttpMethod.Get, InventarioClient.ConQuery(Rutas.Facturas, Query(filtro)), null, null, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeFacturaDeProveedorDto>>> ListarNotasAsync(FiltroDeCompras filtro, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ResumenDeFacturaDeProveedorDto>>(HttpMethod.Get, InventarioClient.ConQuery(Rutas.Notas, Query(filtro)), null, null, ct);

    public Task<ResultadoDeInventario<DocumentoDeCompraDto>> ObtenerFacturaAsync(Guid id, CancellationToken ct = default) => ObtenerAsync(Rutas.Facturas, id, ct);

    /// <summary>Lee el XML (UBL 2.1, AttachedDocument o ZIP) en el servidor y devuelve lo que se puede prellenar. No se guarda.</summary>
    public Task<ResultadoDeInventario<PrellenadoDeFacturaDto>> PrellenarAsync(string nombre, byte[] contenido, CancellationToken ct = default) =>
        LeerArchivoAsync<PrellenadoDeFacturaDto>($"{Rutas.Facturas}/prefill", nombre, contenido, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<EventoRadianDto>>> EventosRadianAsync(Guid factura, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<EventoRadianDto>>(HttpMethod.Get, $"{Rutas.Facturas}/{factura}/radian-events", null, null, ct);

    /// <summary>Registra (o corrige) que la cooperativa emitió el evento por fuera (<c>Inventory.Purchases.RegisterRadianEvent</c>).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<EventoRadianDto>>> RegistrarEventoRadianAsync(Guid factura, RegistrarEventoRadianRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<EventoRadianDto>>(HttpMethod.Post, $"{Rutas.Facturas}/{factura}/radian-events", request, clave, ct);
}
