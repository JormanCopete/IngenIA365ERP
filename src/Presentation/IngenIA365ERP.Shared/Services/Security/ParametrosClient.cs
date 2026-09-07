using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Parámetros de configuración de la cooperativa activa.
///
/// <para>
/// Adjunta el token desde <see cref="CentralAuthClient"/> igual que el resto de
/// los clientes tipados, en vez de depender del <c>AuthBearerHandler</c>. La
/// diferencia importa: ese handler lee el token del almacenamiento seguro, y
/// durante el prerenderizado en el servidor ese almacenamiento es un diccionario
/// en memoria vacío. Las pantallas que llaman a la API en
/// <c>OnInitializedAsync</c> con un HttpClient plano salen sin cabecera de
/// autorización y reciben 401 en la primera pintura.
/// </para>
/// </summary>
public sealed class ParametrosClient(HttpClient http, CentralAuthClient auth)
{
    public async Task<InvitationApiResult<IReadOnlyList<Parametro>>> ListarAsync(
        string? modulo = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(modulo)
            ? "/api/admin/parametros"
            : $"/api/admin/parametros?modulo={Uri.EscapeDataString(modulo)}";

        return await EnviarAsync<IReadOnlyList<Parametro>>(HttpMethod.Get, url, null, ct);
    }

    public async Task<InvitationApiResult<EmptyResponse>> ActualizarAsync(
        Guid publicId, string? valor, CancellationToken ct = default) =>
        await EnviarAsync<EmptyResponse>(
            HttpMethod.Put, $"/api/admin/parametros/{publicId}", new { valor }, ct);

    private async Task<InvitationApiResult<T>> EnviarAsync<T>(
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
            return InvitationApiResult<T>.Failure("Generic.RespuestaInesperada",
                $"El servidor respondió con un formato inesperado: {ex.Message}", 0);
        }
    }
}

public sealed record Parametro(
    Guid PublicId,
    string Clave,
    string? Valor,
    string TipoValor,
    string? Descripcion,
    string? Modulo,
    DateTime? ActualizadoEn,
    string? ActualizadoPor);
