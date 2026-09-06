using System.Net.Http.Headers;
using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>
/// Cliente tipado del módulo de nómina (feature 005, D-15). Adjunta el token desde
/// <see cref="CentralAuthClient"/> como <see cref="ParametrosClient"/>: durante el
/// prerenderizado en el servidor el almacenamiento seguro está vacío y un
/// <c>HttpClient</c> plano saldría sin autorización en la primera pintura.
/// Cada historia de la feature añade aquí sus métodos; los DTOs viven en
/// <c>NominaDtos.cs</c>.
/// </summary>
public sealed partial class NominaClient(HttpClient http, CentralAuthClient auth)
{
    // ------------------------------------------------------------------ planes --

    public Task<InvitationApiResult<IReadOnlyList<PlanNominaDto>>> ListarPlanesAsync(bool incluirInactivos = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<PlanNominaDto>>(HttpMethod.Get, $"/api/payroll/plans?includeInactive={(incluirInactivos ? "true" : "false")}", null, ct);

    public Task<InvitationApiResult<CreadoDto>> CrearPlanAsync(CrearPlanNominaRequest request, CancellationToken ct = default) =>
        EnviarAsync<CreadoDto>(HttpMethod.Post, "/api/payroll/plans", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> ActualizarPlanAsync(Guid planId, ActualizarPlanNominaRequest request, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"/api/payroll/plans/{planId}", new { PublicId = planId, request.Name, request.IsActive }, ct);

    public Task<InvitationApiResult<EmptyResponse>> CambiarPlanDeEmpleadoAsync(Guid empleadoId, CambiarPlanEmpleadoRequest request, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"/api/payroll/employees/{empleadoId}/plan",
            new { EmployeePublicId = empleadoId, request.PlanPublicId, request.EffectiveFrom }, ct);

    // ---------------------------------------------------------------- períodos --

    public Task<InvitationApiResult<PaginaDto<PeriodoPagoDto>>> ListarPeriodosAsync(
        Guid? planId = null, string? estado = null, int pagina = 1, int tamano = 50, CancellationToken ct = default)
    {
        var url = $"/api/payroll/pay-periods?Pagination.PageNumber={pagina}&Pagination.PageSize={tamano}";
        if (planId is { } p) url += $"&PlanId={p}";
        if (!string.IsNullOrWhiteSpace(estado)) url += $"&Status={Uri.EscapeDataString(estado)}";
        return EnviarAsync<PaginaDto<PeriodoPagoDto>>(HttpMethod.Get, url, null, ct);
    }

    public Task<InvitationApiResult<PeriodoPagoDto>> ObtenerPeriodoAsync(Guid periodoId, CancellationToken ct = default) =>
        EnviarAsync<PeriodoPagoDto>(HttpMethod.Get, $"/api/payroll/pay-periods/{periodoId}", null, ct);

    public Task<InvitationApiResult<Guid>> CrearPeriodoAsync(CrearPeriodoPagoRequest request, CancellationToken ct = default) =>
        EnviarAsync<Guid>(HttpMethod.Post, "/api/payroll/pay-periods", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> ActualizarPeriodoAsync(ActualizarPeriodoPagoRequest request, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"/api/payroll/pay-periods/{request.PublicId}", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> EliminarPeriodoAsync(Guid periodoId, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Delete, $"/api/payroll/pay-periods/{periodoId}", null, ct);

    // ------------------------------------------------------------------ común --

    private async Task<InvitationApiResult<T>> EnviarAsync<T>(HttpMethod metodo, string url, object? cuerpo, CancellationToken ct)
    {
        var token = auth.CurrentAccessToken;
        if (token is null)
            return InvitationApiResult<T>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);

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

    /// <summary>Descarga binaria (PDF, CSV) con el mismo token.</summary>
    private async Task<InvitationApiResult<ArchivoDescargado>> DescargarAsync(string url, CancellationToken ct)
    {
        var token = auth.CurrentAccessToken;
        if (token is null)
            return InvitationApiResult<ArchivoDescargado>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return await CentralAuthApi.ParseAsync<ArchivoDescargado>(resp, ct);
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            var nombre = resp.Content.Headers.ContentDisposition?.FileNameStar
                         ?? resp.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                         ?? "archivo";
            return InvitationApiResult<ArchivoDescargado>.Success(
                new ArchivoDescargado(nombre, resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", bytes));
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ArchivoDescargado>.NetworkError(ex.Message);
        }
    }
}

public sealed record CreadoDto(Guid PublicId);

public sealed record ArchivoDescargado(string Nombre, string TipoContenido, byte[] Contenido);
