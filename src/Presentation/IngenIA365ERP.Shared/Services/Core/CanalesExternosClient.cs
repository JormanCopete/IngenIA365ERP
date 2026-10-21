using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>
/// Cliente tipado de los canales externos de la cooperativa (feature 013, fase 2, T0094; contracts/api.md §21.2): qué canal atiende cada
/// clase (SMS, WhatsApp, depósito de valores, centrales, listas), su vigencia y la verificación de la credencial. La credencial nunca
/// viaja: vive en el secreto del ambiente y aquí sólo se nombra su clave. Sin <c>Authorization</c> a mano; las escrituras llevan
/// <c>Idempotency-Key</c>. (nuevo)
/// </summary>
public sealed class CanalesExternosClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = "/api/core/external-channels";

    /// <summary>Las vigencias; <paramref name="clase"/> es el número de <c>ExternalChannelKind</c> y viaja por nombre.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<CanalExternoDto>>> ListarAsync(int? clase = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CanalExternoDto>>(HttpMethod.Get,
            ConQuery(Ruta, Query(("kind", clase is { } c ? TextosDePlataforma.NombreDeClaseDeCanal(c) : null))), null, null, ct);

    public Task<ResultadoDeInventario<CanalExternoDto>> ConfigurarAsync(ConfiguracionDeCanalRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CanalExternoDto>(HttpMethod.Post, Ruta, request, clave, ct);

    public Task<ResultadoDeInventario<VerificacionDeCanalDto>> VerificarAsync(Guid canal, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<VerificacionDeCanalDto>(HttpMethod.Post, $"{Ruta}/{canal}/verify", null, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
