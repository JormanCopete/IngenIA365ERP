using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Auditoria;

/// <summary>
/// La verificación de integridad de la auditoría (feature 012, T429, T435; FR-008, US12-5; contracts/api.md §29,
/// <c>POST /api/audit/integrity/verify</c>, permiso <c>AuditLog.VerifyIntegrity</c>): recalcula la cadena de sellos de la
/// cooperativa en un rango y devuelve los hallazgos (<c>Altered</c>, <c>Deleted</c>, <c>Interleaved</c>,
/// <c>AnchorInvalid</c>, <c>PurgedByRetention</c>). Es una consulta aunque vaya por POST: no lleva clave. La cabecera
/// <c>Authorization</c> la pone el handler de la sesión. (nuevo)
/// </summary>
public sealed class IntegridadDeAuditoriaClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = "/api/audit/integrity/verify";

    public async Task<ResultadoDeInventario<VerificacionDeAuditoriaDto>> VerificarAsync(DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        if (auth.CurrentAccessToken is null) return ResultadoDeInventario<VerificacionDeAuditoriaDto>.SinToken();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Ruta)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new VerificarAuditoriaRequest(desde, hasta, null)),
            };
            using var resp = await http.SendAsync(req, ct);
            return await ResultadoDeInventario<VerificacionDeAuditoriaDto>.DesdeAsync(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return ResultadoDeInventario<VerificacionDeAuditoriaDto>.ErrorDeRed(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return ResultadoDeInventario<VerificacionDeAuditoriaDto>.FormatoInesperado(ex.Message);
        }
    }

    private sealed record VerificarAuditoriaRequest(DateTime From, DateTime To, string? Stream);
}

/// <summary>El resultado de la verificación (§29).</summary>
public sealed record VerificacionDeAuditoriaDto(
    string Stream,
    long? FromSeq,
    long? ToSeq,
    int Checked,
    int AnchorsChecked,
    IReadOnlyList<HallazgoDeAuditoriaDto> Incidents);

/// <summary>Un hallazgo: su clase, la posición en la cadena, el evento y su fecha.</summary>
public sealed record HallazgoDeAuditoriaDto(string Kind, long Seq, string? EventId, DateTime? OccurredAt);
