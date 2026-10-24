using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

/// <summary>Cliente tipado de la bandeja única de órdenes de pago y su cadena de regiros (feature 015, US10, T0147; contracts/api.md §10). (nuevo)</summary>
public sealed class OrdenesClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/treasury/payment-orders";

    public Task<ResultadoDeInventario<IReadOnlyList<OrdenDePagoDto>>> OrdenesAsync(int? estado = null, int? origen = null, string? buscar = null,
        CancellationToken ct = default) =>
        Get<IReadOnlyList<OrdenDePagoDto>>(EnvioDeVentas.ConQuery(Base, EnvioDeVentas.Query(("state", EnvioDeVentas.Texto(estado)),
            ("origin", EnvioDeVentas.Texto(origen)), ("search", string.IsNullOrWhiteSpace(buscar) ? null : buscar.Trim()))), ct);

    public Task<ResultadoDeInventario<OrdenDePagoDto>> OrdenAsync(Guid orden, CancellationToken ct = default) => Get<OrdenDePagoDto>($"{Base}/{orden}", ct);

    public Task<ResultadoDeInventario<IReadOnlyList<OrdenDePagoDto>>> CadenaAsync(Guid orden, CancellationToken ct = default) =>
        Get<IReadOnlyList<OrdenDePagoDto>>($"{Base}/{orden}/chain", ct);

    public Task<ResultadoDeInventario<OrdenDePagoDto>> RevalidarAsync(Guid orden, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<OrdenDePagoDto>(http, auth, HttpMethod.Post, $"{Base}/{orden}/revalidate", null, clave, ct);

    private Task<ResultadoDeInventario<T>> Get<T>(string url, CancellationToken ct) => EnvioDeVentas.EnviarAsync<T>(http, auth, HttpMethod.Get, url, null, null, ct);
}
