using System.Net;
using System.Net.Http.Json;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// T065 — Cliente tipado del módulo de invitaciones (US1). Expone los dos
/// endpoints públicos que consume <c>AcceptInvitation.razor</c>:
/// <see cref="PreviewAsync"/> (no consume el token) y <see cref="AcceptAsync"/>
/// (lo consume con XOR de las tres ramas: registro nuevo, credenciales
/// existentes, sesión activa).
///
/// <para>
/// Devuelve <see cref="InvitationApiResult{T}"/> con tres estados: éxito,
/// fallo conocido (<c>code</c> + <c>message</c> del envelope), o fallo de red.
/// La UI muestra el mensaje al usuario sin acoplar a status codes.
/// </para>
/// </summary>
public sealed class InvitationClient
{
    private readonly HttpClient _http;

    public InvitationClient(HttpClient http) => _http = http;

    public async Task<InvitationApiResult<PreviewInvitationResponse>> PreviewAsync(
        string token, CancellationToken ct = default)
    {
        try
        {
            var encoded = Uri.EscapeDataString(token);
            var resp = await _http.GetAsync($"/api/invitations/{encoded}/preview", ct);
            return await ParseAsync<PreviewInvitationResponse>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<PreviewInvitationResponse>.NetworkError(ex.Message);
        }
    }

    public async Task<InvitationApiResult<AcceptInvitationResponse>> AcceptAsync(
        AcceptInvitationBody body, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/invitations/accept", body, ct);
            return await ParseAsync<AcceptInvitationResponse>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<AcceptInvitationResponse>.NetworkError(ex.Message);
        }
    }

    private static async Task<InvitationApiResult<T>> ParseAsync<T>(
        HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode)
        {
            var value = await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            return value is null
                ? InvitationApiResult<T>.Failure("Generic.EmptyResponse",
                    "La respuesta del servidor está vacía.", (int)resp.StatusCode)
                : InvitationApiResult<T>.Success(value);
        }

        // Status code propio: 410 Gone, 409 Conflict, 401 Unauthorized, etc.
        try
        {
            var envelope = await resp.Content.ReadFromJsonAsync<ErrorEnvelope>(cancellationToken: ct);
            return InvitationApiResult<T>.Failure(
                envelope?.Code ?? "Generic.Failure",
                envelope?.Message ?? "Error sin detalles.",
                (int)resp.StatusCode);
        }
        catch
        {
            return InvitationApiResult<T>.Failure(
                "Generic.Failure",
                $"Error HTTP {(int)resp.StatusCode}.",
                (int)resp.StatusCode);
        }
    }

    private sealed record ErrorEnvelope(string? Code, string? Message, string? TraceId);
}

// -------------------- Result wrapper --------------------

public sealed class InvitationApiResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public int? StatusCode { get; }
    public bool IsNetworkError { get; }

    private InvitationApiResult(bool success, T? value, string? code, string? message, int? status, bool network)
    {
        IsSuccess = success;
        Value = value;
        ErrorCode = code;
        ErrorMessage = message;
        StatusCode = status;
        IsNetworkError = network;
    }

    public static InvitationApiResult<T> Success(T value) =>
        new(true, value, null, null, 200, false);

    public static InvitationApiResult<T> Failure(string code, string message, int status) =>
        new(false, default, code, message, status, false);

    public static InvitationApiResult<T> NetworkError(string message) =>
        new(false, default, "Network.Error",
            $"No se pudo contactar al servidor: {message}", null, true);
}

// -------------------- DTOs (espejo del backend) --------------------

public sealed record PreviewInvitationResponse(
    Guid TenantPublicId,
    string TenantName,
    string Email,
    bool IsExistingCentralUser,
    bool InviteAsTenantAdmin,
    DateTime ExpiresAt,
    bool IsValid,
    string? ErrorCode);

public sealed record AcceptInvitationBody(
    string Token,
    NewRegistrationInput? Registration = null,
    ExistingCredentialsInput? ExistingCredentials = null,
    bool UseActiveSession = false);

public sealed record NewRegistrationInput(string Password);
public sealed record ExistingCredentialsInput(string Password);

public sealed record AcceptInvitationResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    Guid CentralUserId,
    Guid ActiveTenantPublicId,
    string ActiveTenantName);
