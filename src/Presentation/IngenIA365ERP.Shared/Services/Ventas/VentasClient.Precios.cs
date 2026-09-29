using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>Listas de precios, la resolución del precio y los topes de descuento (contracts/api.md §19.1–§19.3). (nuevo)</summary>
public sealed partial class VentasClient
{
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ListaDePreciosDto>>> ListarListasAsync(string? buscar = null, bool? activas = null, DateOnly? vigentesAl = null,
        int pagina = 1, int tamano = 20, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ListaDePreciosDto>>(HttpMethod.Get, Q(Rutas.Listas,
            ("asOf", Texto(vigentesAl)), ("active", Texto(activas)), ("search", buscar), ("page", Texto(pagina)), ("pageSize", Texto(tamano))), null, null, ct);

    /// <summary>La lista con sus precios paginados (y filtrados por producto).</summary>
    public Task<ResultadoDeInventario<DetalleDeListaDto>> ObtenerListaAsync(Guid id, string? buscar = null, int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        Enviar<DetalleDeListaDto>(HttpMethod.Get, Q($"{Rutas.Listas}/{id}", ("search", buscar), ("page", Texto(pagina)), ("pageSize", Texto(tamano))), null, null, ct);

    public async Task<ResultadoDeInventario<Guid>> GuardarListaAsync(Guid? id, ListaDePreciosRequest request, ClaveDeOperacion clave, CancellationToken ct = default)
    {
        if (id is { } existente)
        {
            var r = await Enviar<ListaDePreciosDto>(HttpMethod.Put, $"{Rutas.Listas}/{existente}", request, clave, ct);
            return new(r.IsSuccess, existente, r.ErrorCode, r.ErrorMessage, r.StatusCode, r.Data, r.Repetida);
        }
        var creada = await Enviar<ListaCreadaDto>(HttpMethod.Post, Rutas.Listas, request, clave, ct);
        return new(creada.IsSuccess, creada.Value?.PriceListPublicId ?? Guid.Empty, creada.ErrorCode, creada.ErrorMessage, creada.StatusCode, creada.Data, creada.Repetida);
    }

    /// <summary>Crea o cambia precios por (producto, unidad) y retira productos; todo con motivo.</summary>
    public Task<ResultadoDeInventario<ResultadoDePreciosDto>> FijarPreciosAsync(Guid lista, PreciosDeListaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResultadoDePreciosDto>(HttpMethod.Put, $"{Rutas.Listas}/{lista}/items", request, clave, ct);

    /// <summary>El precio que ganaría hoy (o a <paramref name="fecha"/>) con la lista que ganó y las candidatas.</summary>
    public Task<ResultadoDeInventario<PrecioResueltoDto>> ResolverPrecioAsync(Guid producto, Guid? unidad = null, Guid? persona = null, Guid? canal = null,
        Guid? sucursal = null, DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<PrecioResueltoDto>(HttpMethod.Get, Q($"{Rutas.Precios}/resolve",
            ("product", Texto(producto)), ("unit", Texto(unidad)), ("person", Texto(persona)), ("salesChannel", Texto(canal)), ("branch", Texto(sucursal)),
            ("date", Texto(fecha))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<TopeDeDescuentoDto>>> ListarTopesAsync(Guid? rol = null, DateOnly? vigentesAl = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<TopeDeDescuentoDto>>(HttpMethod.Get, Q(Rutas.Topes, ("role", Texto(rol)), ("asOf", Texto(vigentesAl))), null, null, ct);

    /// <summary>El tope que aplica al usuario: el mayor de sus roles.</summary>
    public Task<ResultadoDeInventario<MiTopeDto>> MiTopeAsync(CancellationToken ct = default) =>
        Enviar<MiTopeDto>(HttpMethod.Get, $"{Rutas.Topes}/mine", null, null, ct);

    // ---------------------------------------------------------------------------------- promociones (I6) --

    /// <summary>Las promociones (§19.4): vigentes a <paramref name="vigentesAl"/> y activas o no. Consulta: sin clave.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<PromocionDto>>> ListarPromocionesAsync(DateOnly? vigentesAl = null, bool? activas = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<PromocionDto>>(HttpMethod.Get, Q(Rutas.Promociones, ("asOf", Texto(vigentesAl)), ("active", Texto(activas))), null, null, ct);

    public Task<ResultadoDeInventario<PromocionDto>> ObtenerPromocionAsync(Guid id, CancellationToken ct = default) =>
        Enviar<PromocionDto>(HttpMethod.Get, $"{Rutas.Promociones}/{id}", null, null, ct);

    public Task<ResultadoDeInventario<PromocionDto>> CrearPromocionAsync(PromocionRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<PromocionDto>(HttpMethod.Post, Rutas.Promociones, request, clave, ct);

    /// <summary>Ya aplicada en un documento confirmado, cambiar algo más que nombre, fin y activo responde <c>Inventory.Promotion.InUse</c>.</summary>
    public Task<ResultadoDeInventario<PromocionDto>> EditarPromocionAsync(Guid id, EdicionDePromocionRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<PromocionDto>(HttpMethod.Put, $"{Rutas.Promociones}/{id}", request, clave, ct);

    public Task<ResultadoDeInventario<TopeCreadoDto>> CrearTopeAsync(TopeRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<TopeCreadoDto>(HttpMethod.Post, Rutas.Topes, request, clave, ct);
}
