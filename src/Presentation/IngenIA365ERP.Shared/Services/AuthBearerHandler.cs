using System.Net.Http.Headers;

namespace IngenIA365ERP.Shared.Services;

/// <summary>
/// Reads the JWT from secure storage and attaches it as Authorization: Bearer
/// on every outgoing request. Paired with TenantDelegatingHandler, every API
/// call carries both auth and tenant headers without each page wiring them by hand.
/// </summary>
public class AuthBearerHandler : DelegatingHandler
{
    public const string TokenKey = "auth_token";
    private readonly ISecureStorage _storage;

    public AuthBearerHandler(ISecureStorage storage) => _storage = storage;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null)
        {
            var token = await _storage.GetAsync(TokenKey);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
