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

    /// <summary>
    /// Cuánto espera el login por las promociones. Son contenido accesorio: si la
    /// API tarda más que esto, el login sigue sin panel y no se entera nadie. El
    /// timeout del HttpClient (100 s por defecto) es para las operaciones que sí
    /// importan; aquí, tres segundos es lo máximo que vale la pena esperar por
    /// publicidad en la pantalla de entrada.
    /// </summary>
    private static readonly TimeSpan EsperaMaximaPublica = TimeSpan.FromSeconds(3);

    public async Task<InvitationApiResult<IReadOnlyList<PromoPublica>>> ListarPublicasAsync(
        CancellationToken ct = default)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(EsperaMaximaPublica);
        try
        {
            var resp = await http.GetAsync("/api/publico/promociones", limite.Token);
            return await CentralAuthApi.ParseAsync<IReadOnlyList<PromoPublica>>(resp, limite.Token);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<IReadOnlyList<PromoPublica>>.NetworkError(ex.Message);
        }
        catch (OperationCanceledException)
        {
            // Agotado el tope (o cancelado quien llamaba): se trata igual que la
            // red caída. Hasta el 2026-09-12 sólo se capturaba HttpRequestException,
            // y una API lenta hacía que la excepción de cancelación subiera hasta el
            // límite de error de Blazor: el login entero mostraba «error inesperado»
            // por culpa de un panel que ni siquiera hace falta para entrar.
            return InvitationApiResult<IReadOnlyList<PromoPublica>>.NetworkError(
                $"las novedades no respondieron en {EsperaMaximaPublica.TotalSeconds:0} s");
        }
    }

    /// <summary>URL de la imagen de una pieza, para usar en un <c>src</c>.</summary>
    /// <remarks>
    /// Es ABSOLUTA, contra la URL base de la API. Una ruta relativa la resuelve
    /// el navegador contra el host que sirvio la pagina —la Web, en otro
    /// puerto— y devuelve 404: la imagen la sirve la API. Se veia el panel
    /// promocional con su titulo y su texto, y un hueco donde iba la imagen.
    /// </remarks>
    public static string RutaImagen(Guid publicId) =>
        $"{Configuration.AppMode.ApiBaseUrl.TrimEnd('/')}/api/publico/promociones/{publicId}/imagen";

    public async Task<InvitationApiResult<IReadOnlyList<PromoAdmin>>> ListarAsync(
        CancellationToken ct = default) =>
        await ConTokenAsync<IReadOnlyList<PromoAdmin>>(HttpMethod.Get, "/api/admin/promociones", null, ct);

    /// <summary>
    /// Devuelve el PublicId de la pieza guardada.
    /// </summary>
    /// <remarks>
    /// El tipo es <see cref="Guid"/> y no un objeto envoltorio porque el
    /// endpoint devuelve <c>Result&lt;Guid&gt;</c> y ErrorEnvelopeFilter emite
    /// el valor tal cual: el cuerpo es una cadena JSON suelta ("fcfe97f1-…"),
    /// no un objeto. Pedirle a ReadFromJsonAsync que lo meta en un record
    /// lanzaba JsonException, que no es HttpRequestException y por lo tanto no
    /// la capturaba nadie: la excepcion llegaba hasta el limite de error de
    /// Blazor y la pantalla mostraba "Ha ocurrido un error inesperado" aunque
    /// el guardado hubiera funcionado.
    /// </remarks>
    public async Task<InvitationApiResult<Guid>> GuardarAsync(
        object cuerpo, CancellationToken ct = default) =>
        await ConTokenAsync<Guid>(HttpMethod.Post, "/api/admin/promociones", cuerpo, ct);

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
        catch (System.Text.Json.JsonException ex)
        {
            // Una respuesta con forma distinta de la esperada es un error de
            // contrato, no un motivo para tumbar la pantalla entera.
            return InvitationApiResult<T>.Failure("Generic.RespuestaInesperada",
                $"El servidor respondio con un formato inesperado: {ex.Message}", 0);
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
