using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>
/// Cliente tipado del perfil regulatorio de la cooperativa (feature 013, fase 2, T0094; contracts/api.md §21.4): la versión vigente a una
/// fecha (con <c>recordedAsOf</c> para lo que se sabía entonces) y el registro de una versión nueva o de su corrección. Sin
/// <c>Authorization</c> a mano; la escritura lleva <c>Idempotency-Key</c>. (nuevo)
/// </summary>
public sealed class PerfilRegulatorioClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = "/api/core/regulatory-profile";

    public Task<ResultadoDeInventario<PerfilRegulatorioDto>> ObtenerAsync(DateOnly? fecha = null, DateTime? informacionHasta = null, CancellationToken ct = default) =>
        Enviar<PerfilRegulatorioDto>(HttpMethod.Get, ConQuery(Ruta, Query(("date", Texto(fecha)),
            ("recordedAsOf", informacionHasta?.ToUniversalTime().ToString("o")))), null, null, ct);

    public Task<ResultadoDeInventario<VersionDePerfilCreadaDto>> RegistrarVersionAsync(VersionDePerfilRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<VersionDePerfilCreadaDto>(HttpMethod.Post, $"{Ruta}/versions", request, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
