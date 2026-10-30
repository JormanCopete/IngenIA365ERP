using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Recaudo;

/// <summary>Cliente del alcance de Recaudo de un usuario (feature 015, US12, T0063; contracts/api.md §2). (nuevo)</summary>
public sealed class AlcanceDeRecaudoClient(HttpClient http, CentralAuthClient auth)
{
    public Task<ResultadoDeInventario<AlcanceDeRecaudoDto>> ObtenerAsync(Guid usuario, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<AlcanceDeRecaudoDto>(http, auth, HttpMethod.Get, $"/api/receipts/users/{usuario}/scope", null, null, ct);

    public Task<ResultadoDeInventario<AlcanceDeRecaudoDto>> FijarAsync(Guid usuario, FijarAlcanceDeRecaudoRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<AlcanceDeRecaudoDto>(http, auth, HttpMethod.Put, $"/api/receipts/users/{usuario}/scope", r, clave, ct);
}
