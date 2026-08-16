using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Contenido promocional del login. La lectura pública NO adjunta token: se
/// invoca antes de iniciar sesión.
/// </summary>
public sealed class PromocionesClient(HttpClient http, CentralAuthClient auth)
{
    /// <summary>
    /// Tope de la imagen, en bytes. Espejo de PromoContenido.MaximoBytesImagen
    /// del dominio: Shared es capa de presentación y no referencia Domain, así
    /// que el valor se repite en vez de acoplar las capas. El servidor vuelve a
    /// comprobarlo; esto es sólo para no subir lo que va a rechazarse.
    /// </summary>
    public const int MaximoBytesImagen = 400 * 1024;

    public async Task<InvitationApiResult<IReadOnlyList<PromoPublica>>> ListarPublicasAsync(
        CancellationToken ct = default)
    {
        try
        {
            var resp = await http.GetAsync("/api/publico/promociones", ct);
            return await CentralAuthApi.ParseAsync<IReadOnlyList<PromoPublica>>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<IReadOnlyList<PromoPublica>>.NetworkError(ex.Message);
        }
    }

    /// <summary>Ruta de la imagen de una pieza, para usar en un <c>src</c>.</summary>
    public static string RutaImagen(Guid publicId) =>
        $"/api/publico/promociones/{publicId}/imagen";

    public async Task<InvitationApiResult<IReadOnlyList<PromoAdmin>>> ListarAsync(
        CancellationToken ct = default) =>
        await ConTokenAsync<IReadOnlyList<PromoAdmin>>(HttpMethod.Get, "/api/admin/promociones", null, ct);

    public async Task<InvitationApiResult<GuardarPromoRespuesta>> GuardarAsync(
        object cuerpo, CancellationToken ct = default) =>
        await ConTokenAsync<GuardarPromoRespuesta>(HttpMethod.Post, "/api/admin/promociones", cuerpo, ct);

    public async Task<InvitationApiResult<EmptyResponse>> EliminarAsync(
        Guid publicId, CancellationToken ct = default) =>
        await ConTokenAsync<EmptyResponse>(
            HttpMethod.Delete, $"/api/admin/promociones/{publicId}", null, ct);

    private async Task<InvitationApiResult<T>> ConTokenAsync<T>(
        HttpMethod metodo, string url, object? cuerpo, CancellationToken ct)
    {
        var token = auth.CurrentAccessToken;
        if (token is null)
            return InvitationApiResult<T>.Failure(
                "Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);

        try
        {
            using var req = new HttpRequestMessage(metodo, url);
            if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await http.SendAsync(req, ct);
            return await CentralAuthApi.ParseAsync<T>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<T>.NetworkError(ex.Message);
        }
    }
}

public sealed record PromoPublica(
    Guid PublicId,
    string Titulo,
    string? Texto,
    string? TextoEnlace,
    string? Enlace,
    bool TieneImagen,
    string? ImagenTextoAlternativo);

public sealed record PromoAdmin(
    Guid PublicId,
    string Titulo,
    string? Texto,
    string? TextoEnlace,
    string? Enlace,
    bool TieneImagen,
    string? ImagenTextoAlternativo,
    int Orden,
    bool Publicado,
    DateTime? VigenteDesde,
    DateTime? VigenteHasta);

public sealed record GuardarPromoRespuesta(Guid PublicId);
