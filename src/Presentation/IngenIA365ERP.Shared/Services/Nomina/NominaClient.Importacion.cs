using System.Net.Http.Headers;
using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>US6: importación de novedades desde archivo y novedades recurrentes (contracts/api.md §3).</summary>
public sealed partial class NominaClient
{
    public Task<InvitationApiResult<ArchivoDescargado>> PlantillaImportacionAsync(CancellationToken ct = default) =>
        DescargarAsync("/api/payroll/novelties/import-template", ct);

    /// <summary>Sube el archivo. Un 422 trae el mismo DTO con <c>Applied = 0</c> y la lista de errores: no es un fallo de transporte, es el resultado.</summary>
    public async Task<InvitationApiResult<ResultadoImportacionDto>> ImportarNovedadesAsync(Guid periodoId, Stream archivo, string nombre, CancellationToken ct = default)
    {
        var token = auth.CurrentAccessToken;
        if (token is null)
            return InvitationApiResult<ResultadoImportacionDto>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
        try
        {
            using var form = new MultipartFormDataContent();
            var contenido = new StreamContent(archivo);
            contenido.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            form.Add(contenido, "file", nombre);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payroll/pay-periods/{periodoId}/novelties/import") { Content = form };
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode || (int)resp.StatusCode == 422)
            {
                var dto = await resp.Content.ReadFromJsonAsync<ResultadoImportacionDto>(cancellationToken: ct);
                return dto is null
                    ? InvitationApiResult<ResultadoImportacionDto>.Failure("Generic.RespuestaInesperada", "El servidor no devolvió el resultado de la importación.", (int)resp.StatusCode)
                    : InvitationApiResult<ResultadoImportacionDto>.Success(dto);
            }
            return await CentralAuthApi.ParseAsync<ResultadoImportacionDto>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ResultadoImportacionDto>.NetworkError(ex.Message);
        }
    }

    public Task<InvitationApiResult<IReadOnlyList<RecurrenteDto>>> ListarRecurrentesAsync(Guid? empleadoId = null, bool? activas = true, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (empleadoId is { } e) q.Add($"employeeId={e}");
        if (activas is { } a) q.Add($"active={a.ToString().ToLowerInvariant()}");
        var url = "/api/payroll/recurring-novelties" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        return EnviarAsync<IReadOnlyList<RecurrenteDto>>(HttpMethod.Get, url, null, ct);
    }

    public Task<InvitationApiResult<CreadoDto>> CrearRecurrenteAsync(CrearRecurrenteRequest request, CancellationToken ct = default) =>
        EnviarAsync<CreadoDto>(HttpMethod.Post, "/api/payroll/recurring-novelties", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> DesactivarRecurrenteAsync(Guid recurrenteId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"/api/payroll/recurring-novelties/{recurrenteId}/deactivate", new { Reason = motivo }, ct);
}

public sealed record ErrorImportacionDto(int Row, string Column, string Message);

public sealed record ResultadoImportacionDto(int Applied, Guid BatchId, IReadOnlyList<ErrorImportacionDto> Errors)
{
    public bool TieneErrores => Errors.Count > 0;
}

public sealed record RecurrenteDto(
    Guid PublicId, Guid EmployeePublicId, string EmployeeName, string Document, string ConceptCode, string ConceptName, string Nature,
    decimal? Quantity, decimal? Amount, DateTime StartDate, DateTime? EndDate, int? TotalInstallments, int InstallmentsIssued,
    bool IsActive, string? Notes, string? DeactivationReason, DateTime CreatedAt, string? CreatedBy, string ApplyOn = "EveryPeriod")
{
    public string AplicaEnTexto => ApplyOn switch
    {
        "FirstOfMonth" => "Primero del mes",
        "LastOfMonth" => "Último del mes",
        _ => "Cada período",
    };
    public string CantidadOValor => Quantity is { } q ? q.ToString("0.##") : Amount is { } a ? a.ToString("N0") : string.Empty;
    public string Cuotas => TotalInstallments is { } t ? $"{InstallmentsIssued} de {t}" : $"{InstallmentsIssued} · sin límite";
    public string Vigencia => EndDate is { } e ? $"{StartDate:dd/MM/yyyy} – {e:dd/MM/yyyy}" : $"desde {StartDate:dd/MM/yyyy}";
}

public sealed record CrearRecurrenteRequest(Guid EmployeePublicId, string ConceptCode, decimal? Quantity, decimal? Amount, DateTime StartDate, DateTime? EndDate, int? TotalInstallments, string? Notes, string ApplyOn = "EveryPeriod");
