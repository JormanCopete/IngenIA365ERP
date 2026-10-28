using System.Net.Http.Json;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Feature 017 (US6, US7): las rutas de soporte de identidades (<c>/api/saas/identities</c> y
/// <c>/api/saas/identity-cases</c>). Sólo el maestro. El token lo pone el handler.
/// </summary>
public sealed class IdentidadesClient(HttpClient http)
{
    public async Task<InvitationApiResult<List<IdentidadResumen>>> BuscarAsync(string q, CancellationToken ct = default) =>
        await GetAsync<List<IdentidadResumen>>($"/api/saas/identities?q={Uri.EscapeDataString(q)}", ct);

    public async Task<InvitationApiResult<IdentidadDetalle>> DetalleAsync(Guid id, CancellationToken ct = default) =>
        await GetAsync<IdentidadDetalle>($"/api/saas/identities/{id}", ct);

    public async Task<InvitationApiResult<List<CasoDeIdentidad>>> CasosAsync(string? estado, CancellationToken ct = default) =>
        await GetAsync<List<CasoDeIdentidad>>($"/api/saas/identity-cases{(estado is null ? "" : $"?status={estado}")}", ct);

    public Task<InvitationApiResult<CorreccionHecha>> CorregirAsync(Guid id, object cuerpo, CancellationToken ct = default) =>
        PostAsync<CorreccionHecha>($"/api/saas/identities/{id}/correct-document", cuerpo, ct);

    public Task<InvitationApiResult<CorreoCambiado>> CambiarCorreoAsync(Guid id, string correo, string motivo, CancellationToken ct = default) =>
        PostAsync<CorreoCambiado>($"/api/saas/identities/{id}/change-email", new { newEmail = correo, reason = motivo }, ct);

    public Task<InvitationApiResult<FusionHecha>> FusionarAsync(Guid origen, Guid destino, string motivo, CancellationToken ct = default) =>
        PostAsync<FusionHecha>("/api/saas/identities/merge", new { sourceId = origen, targetId = destino, reason = motivo }, ct);

    public Task<InvitationApiResult<CasoDeIdentidad>> DescartarAsync(Guid caso, string motivo, CancellationToken ct = default) =>
        PostAsync<CasoDeIdentidad>($"/api/saas/identity-cases/{caso}/dismiss", new { reason = motivo }, ct);

    private async Task<InvitationApiResult<T>> GetAsync<T>(string ruta, CancellationToken ct)
    {
        try
        {
            using var resp = await http.GetAsync(ruta, ct);
            return await CentralAuthApi.ParseAsync<T>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<T>.NetworkError(ex.Message);
        }
    }

    private async Task<InvitationApiResult<T>> PostAsync<T>(string ruta, object cuerpo, CancellationToken ct)
    {
        try
        {
            using var resp = await http.PostAsJsonAsync(ruta, cuerpo, ct);
            return await CentralAuthApi.ParseAsync<T>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<T>.NetworkError(ex.Message);
        }
    }
}

public sealed record IdentidadResumen(
    Guid Id, string Email, string? DocumentType, string? DocumentNumber, string? DocumentUsername,
    string Nombre, string Status, bool TwoFactorEnabled, bool IsGlobalMasterAdmin, List<MembresiaDeIdentidad> Memberships);

public sealed record MembresiaDeIdentidad(Guid TenantId, string TenantName, string Status, bool IsTenantAdmin);

public sealed record IdentidadDetalle(IdentidadResumen Identidad, List<CambioDeDocumento> History, List<CasoDeIdentidad> Cases);

public sealed record CambioDeDocumento(DateTime OccurredAt, string Reason, string ActorKind, string? OldUsername, string NewUsername, string? Notes);

public sealed record CasoDeIdentidad(
    Guid PublicId, string Kind, string Status, Guid CentralUserId, string? Email,
    Guid? OtherCentralUserId, string? OtherEmail, string? Detail, DateTime CreatedAt, string? Resolution);

public sealed record CorreccionHecha(Guid CentralUserId, string DocumentUsername, string? PreviousUsername);
public sealed record CorreoCambiado(Guid CentralUserId, string Email);
public sealed record FusionHecha(Guid CasePublicId, string Status, List<Guid> PendingTenants);
