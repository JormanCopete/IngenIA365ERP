using IngenIA365ERP.Shared.Services.Core;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Compliance;

/// <summary>
/// Cliente tipado de las listas restrictivas propias (feature 013, fase 2, T0094; contracts/api.md §22.1): las cargas con su versión y
/// estado, y sus entradas. La carga misma es la plantilla <c>compliance.restrictive-lists</c>: la sube <c>ImportarPlantilla</c> sobre
/// <see cref="Ruta"/> (revisión previa y aplicación con su propia <c>Idempotency-Key</c>). Sin <c>Authorization</c> a mano. (nuevo)
/// </summary>
public sealed class ListasRestrictivasClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = "/api/compliance/restrictive-lists";

    public Task<ResultadoDeInventario<IReadOnlyList<CargaDeListaRestrictivaDto>>> CargasAsync(string? lista = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CargaDeListaRestrictivaDto>>(HttpMethod.Get, ConQuery(Ruta, Query(("listCode", lista))), null, null, ct);

    public Task<ResultadoDeInventario<PaginaDePlataformaDto<EntradaDeListaRestrictivaDto>>> EntradasAsync(Guid carga, string? buscar = null, int pagina = 1,
        int tamano = 50, CancellationToken ct = default) =>
        Enviar<PaginaDePlataformaDto<EntradaDeListaRestrictivaDto>>(HttpMethod.Get, ConQuery($"{Ruta}/{carga}/entries",
            Query(("search", buscar), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
