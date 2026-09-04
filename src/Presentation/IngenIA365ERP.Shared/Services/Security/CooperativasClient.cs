using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Gestión de cooperativas y de sus invitaciones.
///
/// <para>
/// Adjunta el token desde <see cref="CentralAuthClient"/>, no vía
/// <c>AuthBearerHandler</c>. La diferencia no es de estilo: ese handler lee el
/// token del almacenamiento seguro, y en el host servidor ese almacenamiento es
/// un diccionario en memoria que durante el prerenderizado está vacío. Una
/// pantalla que consulte la API en <c>OnInitializedAsync</c> con un HttpClient
/// plano sale sin cabecera de autorización y recibe 401 en la primera pintura.
/// </para>
/// </summary>
public sealed class CooperativasClient(HttpClient http, CentralAuthClient auth)
{
    // ------------------------------------------------------- cooperativas --

    public async Task<InvitationApiResult<PaginaDe<CooperativaResumen>>> ListarAsync(
        string? busqueda = null, bool incluirSuspendidas = false, CancellationToken ct = default)
    {
        var url = $"/api/saas/tenants?search={Uri.EscapeDataString(busqueda ?? string.Empty)}" +
                  $"&includeSuspended={incluirSuspendidas}";
        return await EnviarAsync<PaginaDe<CooperativaResumen>>(HttpMethod.Get, url, null, ct);
    }

    /// <param name="motivo">
    /// Obligatorio y libre. Suspender una cooperativa corta el acceso a todos
    /// sus usuarios; quien lo haga debe dejar dicho por qué, y quien lo lea
    /// después merece algo mejor que un texto fijo.
    /// </param>
    public async Task<InvitationApiResult<EmptyResponse>> SuspenderAsync(
        Guid publicId, string motivo, CancellationToken ct = default) =>
        await EnviarAsync<EmptyResponse>(
            HttpMethod.Post, $"/api/saas/tenants/{publicId}/suspend", new { reason = motivo }, ct);

    public async Task<InvitationApiResult<EmptyResponse>> ReactivarAsync(
        Guid publicId, CancellationToken ct = default) =>
        await EnviarAsync<EmptyResponse>(
            HttpMethod.Post, $"/api/saas/tenants/{publicId}/activate", null, ct);

    // ------------------------------------------------------- invitaciones --

    public async Task<InvitationApiResult<IReadOnlyList<InvitacionResumen>>> ListarInvitacionesAsync(
        Guid tenantPublicId, bool incluirCerradas = false, CancellationToken ct = default) =>
        await EnviarAsync<IReadOnlyList<InvitacionResumen>>(
            HttpMethod.Get,
            $"/api/tenants/{tenantPublicId}/invitations?incluirCerradas={incluirCerradas}",
            null, ct);

    public async Task<InvitationApiResult<ReenvioResultado>> ReenviarInvitacionAsync(
        Guid tenantPublicId, Guid invitacionPublicId, CancellationToken ct = default) =>
        await EnviarAsync<ReenvioResultado>(
            HttpMethod.Post,
            $"/api/tenants/{tenantPublicId}/invitations/{invitacionPublicId}/reenviar",
            null, ct);

    public async Task<InvitationApiResult<EmptyResponse>> RevocarInvitacionAsync(
        Guid invitacionPublicId, CancellationToken ct = default) =>
        await EnviarAsync<EmptyResponse>(
            HttpMethod.Delete, $"/api/invitations/{invitacionPublicId}", null, ct);

    /// <summary>
    /// Emite una invitación nueva. Faltaba: la pantalla de invitaciones listaba,
    /// reenviaba y cancelaba, y el botón «Invitar persona» de Usuarios llevaba a
    /// ella —pero no había desde dónde invitar. El único camino era la API.
    ///
    /// <para>
    /// Dos endpoints según quién invita. El administrador de la cooperativa usa
    /// <c>POST /api/tenants/{id}/invitations</c>, que sólo admite miembros
    /// regulares (FR-025/FR-026). El maestro usa <c>POST /api/saas/invitations</c>,
    /// que además puede nombrar administradora a la persona. La pantalla no decide
    /// el privilegio: lo decide el token, y la API vuelve a comprobarlo.
    /// </para>
    /// </summary>
    public async Task<InvitationApiResult<InvitacionEmitida>> InvitarAsync(
        Guid tenantPublicId, string email, bool comoAdministradora, bool comoMaestro,
        CancellationToken ct = default)
    {
        if (comoMaestro)
        {
            return await EnviarAsync<InvitacionEmitida>(
                HttpMethod.Post, "/api/saas/invitations",
                new { tenantPublicId, email, inviteAsTenantAdmin = comoAdministradora }, ct);
        }

        return await EnviarAsync<InvitacionEmitida>(
            HttpMethod.Post, $"/api/tenants/{tenantPublicId}/invitations",
            new { email }, ct);
    }

    // -------------------------------------------------------------- envio --

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

public sealed record PaginaDe<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);

public sealed record CooperativaResumen(
    Guid PublicId, string Name, string? Nit, string? Subdomain, string PlanType,
    bool IsActive, bool IsSuspended, int BranchCount,
    DateTime? ActivatedAt, DateTime? SuspendedAt);

public sealed record InvitacionResumen(
    Guid PublicId,
    string Email,
    string Estado,
    bool ComoAdministrador,
    DateTime CreadaEn,
    DateTime ExpiraEn,
    bool Expirada,
    DateTime? AceptadaEn);

public sealed record ReenvioResultado(Guid NuevaInvitacionPublicId, string Email, DateTime ExpiraEn);

/// <summary>
/// Lo que devuelven los dos endpoints de emisión. El del maestro trae además
/// <c>inviteAsTenantAdmin</c>, que aquí se ignora: la pantalla ya sabe qué pidió.
/// El token plano no viene nunca (FR-024): sale por correo al destinatario.
/// </summary>
public sealed record InvitacionEmitida(Guid InvitationPublicId, DateTime ExpiresAt);
