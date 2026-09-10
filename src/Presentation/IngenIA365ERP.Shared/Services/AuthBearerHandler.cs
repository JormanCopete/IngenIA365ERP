using System.Net.Http.Headers;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services;

/// <summary>
/// Reads the JWT from secure storage and attaches it as Authorization: Bearer
/// on every outgoing request. Paired with TenantDelegatingHandler, every API
/// call carries both auth and tenant headers without each page wiring them by hand.
///
/// <para>
/// Con <see cref="RenovacionDeSesionHandler"/> delante, normalmente la cabecera ya
/// viene puesta y este handler no hace nada. Sigue existiendo como red: un host que
/// no registre el renovador conserva el comportamiento de siempre.
/// </para>
/// </summary>
public class AuthBearerHandler : DelegatingHandler
{
    public const string TokenKey = "auth_token";
    private readonly ISecureStorage _storage;

    public AuthBearerHandler(ISecureStorage storage) => _storage = storage;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // El canje del refresh sale sin sesión a propósito: adjuntarle el access
        // vencido no aporta nada y confunde el diagnóstico en el servidor.
        if (request.Options.TryGetValue(RenovadorDeSesion.SinSesion, out var sinSesion) && sinSesion)
            return await base.SendAsync(request, cancellationToken);

        if (request.Headers.Authorization is null)
        {
            var token = await _storage.GetAsync(TokenKey);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
