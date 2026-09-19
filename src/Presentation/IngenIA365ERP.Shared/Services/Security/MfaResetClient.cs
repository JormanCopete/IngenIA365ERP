using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Feature 003 (US6, FR-119) — solicitar y aprobar reseteos de segundo factor.
///
/// <para>
/// <b>Por qué existe este archivo.</b> Estos tres métodos vivían en
/// <c>AuthClient</c>, el cliente de Fase 0, y tomaban el bearer de un campo que
/// <b>sólo llenaba el login de ese mismo cliente</b>. Ese login dejó de usarse
/// en el cutover a identidad central: la pantalla de aprobaciones seguía
/// llamando a un cliente que nunca tuvo token, así que sus peticiones salían
/// anónimas contra endpoints con <c>RequireAuthorization</c>. No daba un error
/// entendible — devolvía <c>null</c> y la pantalla se quedaba vacía, que es
/// indistinguible de «no hay solicitudes pendientes».
/// </para>
///
/// <para>
/// Ahora el token sale de <see cref="CentralAuthClient"/>, que es quien tiene la
/// sesión de verdad, siguiendo el mismo patrón que el resto de clientes
/// (<c>MembershipsClient</c>, <c>ProfileClient</c>, <c>CooperativasClient</c>…).
/// </para>
/// </summary>
public sealed class MfaResetClient
{
    private readonly HttpClient _http;
    private readonly CentralAuthClient _auth;

    public MfaResetClient(HttpClient http, CentralAuthClient auth)
    {
        _http = http;
        _auth = auth;
    }

    /// <summary>Solicita el reseteo del segundo factor de otra persona.</summary>
    /// <returns><c>null</c> si no hay sesión o si el servidor rechazó la solicitud.</returns>
    public async Task<MfaResetRequestResponse?> RequestMfaResetAsync(
        Guid targetUserPublicId, string reason, Guid? evidenceAttachmentPublicId,
        CancellationToken ct = default)
    {
        using var req = Autenticada(HttpMethod.Post, "/api/auth/mfa/reset/request");
        if (req is null) return null;
        req.Content = JsonContent.Create(new { targetUserPublicId, reason, evidenceAttachmentPublicId });

        var resp = await _http.SendAsync(req, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<MfaResetRequestResponse>(ct)
            : null;
    }

    /// <summary>Aprueba una solicitud. Devuelve el estado resultante, o <c>null</c>.</summary>
    public async Task<string?> ApproveMfaResetAsync(Guid requestPublicId, CancellationToken ct = default)
    {
        using var req = Autenticada(HttpMethod.Post, $"/api/auth/mfa/reset/{requestPublicId}/approve");
        if (req is null) return null;
        req.Content = JsonContent.Create(new { });

        var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var body = await resp.Content.ReadFromJsonAsync<MfaResetApprovalResponse>(ct);
        return body?.Status;
    }

    /// <summary>Listado paginado para quien aprueba (FR-119): sin GUIDs a mano.</summary>
    public async Task<MfaResetRequestListResponse?> ListMfaResetRequestsAsync(
        string? status = "Pending", int page = 1, int pageSize = 20,
        CancellationToken ct = default)
    {
        var url = $"/api/auth/mfa/reset/requests?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(status)) url += $"&status={status}";

        using var req = Autenticada(HttpMethod.Get, url);
        if (req is null) return null;

        var resp = await _http.SendAsync(req, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<MfaResetRequestListResponse>(ct)
            : null;
    }

    /// <summary>
    /// Petición con el bearer de la sesión central, o <c>null</c> si no hay
    /// sesión. Se devuelve null en vez de mandarla sin cabecera: una petición
    /// anónima a un endpoint autenticado vuelve como 401 y acaba pareciendo un
    /// listado vacío, que es exactamente el fallo que esto corrige.
    /// </summary>
    private HttpRequestMessage? Autenticada(HttpMethod metodo, string url)
    {
        var token = _auth.CurrentAccessToken;
        if (string.IsNullOrWhiteSpace(token)) return null;

        var req = new HttpRequestMessage(metodo, url);
        return req;
    }
}

public sealed record MfaResetRequestResponse(Guid RequestPublicId, DateTime ExpiresAt);

public sealed record MfaResetApprovalResponse(string Status);

public sealed record MfaResetRequestListResponse(
    IReadOnlyList<MfaResetRequestSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record MfaResetRequestSummaryDto(
    Guid PublicId,
    string TargetUserName,
    string TargetEmail,
    string RequestedByUserName,
    string Reason,
    DateTime RequestedAt,
    DateTime ExpiresAt,
    string Status,
    bool HasFirstApproval,
    bool HasSecondApproval);
