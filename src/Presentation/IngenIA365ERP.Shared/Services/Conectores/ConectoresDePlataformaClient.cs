using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Conectores;

/// <summary>
/// Cliente tipado de los conectores externos que define el maestro (feature 020, US7; contracts/api.md §1): <c>/api/saas/connectors</c>.
/// La cabecera <c>Authorization</c> la pone el handler (<c>ElTokenDeSesionLoPoneElHandler</c>).
/// </summary>
public sealed class ConectoresDePlataformaClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/saas/connectors";

    public Task<ResultadoDeInventario<IReadOnlyList<ConectorDePlataformaDto>>> ListarAsync(CancellationToken ct = default) =>
        Ventas.EnvioDeVentas.EnviarAsync<IReadOnlyList<ConectorDePlataformaDto>>(http, auth, HttpMethod.Get, Base, null, null, ct);

    public Task<ResultadoDeInventario<ConectorCreadoDto>> CrearAsync(string codigo, string nombre, string? descripcion, CancellationToken ct = default) =>
        Ventas.EnvioDeVentas.EnviarAsync<ConectorCreadoDto>(http, auth, HttpMethod.Post, Base,
            new { code = codigo, name = nombre, description = descripcion }, null, ct);

    public Task<ResultadoDeInventario<object>> CambiarAsync(Guid id, string nombre, string? descripcion, bool activo, CancellationToken ct = default) =>
        Ventas.EnvioDeVentas.EnviarAsync<object>(http, auth, HttpMethod.Put, $"{Base}/{id}",
            new { name = nombre, description = descripcion, isActive = activo }, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<VersionDeConectorDto>>> VersionesAsync(Guid id, CancellationToken ct = default) =>
        Ventas.EnvioDeVentas.EnviarAsync<IReadOnlyList<VersionDeConectorDto>>(http, auth, HttpMethod.Get, $"{Base}/{id}/versions", null, null, ct);

    /// <param name="definicion">El JSON de la definición tal como está en el editor; lo valida el servidor.</param>
    public Task<ResultadoDeInventario<VersionPublicadaDto>> PublicarAsync(Guid id, string urlBase, string definicion, string? nota, CancellationToken ct = default) =>
        Ventas.EnvioDeVentas.EnviarAsync<VersionPublicadaDto>(http, auth, HttpMethod.Post, $"{Base}/{id}/versions",
            new { baseUrl = urlBase, definition = definicion, note = nota }, null, ct);

    public Task<ResultadoDeInventario<SimulacionDeConectorDto>> SimularAsync(Guid id, int version, IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default) =>
        Ventas.EnvioDeVentas.EnviarAsync<SimulacionDeConectorDto>(http, auth, HttpMethod.Post, $"{Base}/{id}/versions/{version}/simulate",
            new { variables }, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<HabilitacionDeConectorDto>>> CooperativasAsync(Guid id, CancellationToken ct = default) =>
        Ventas.EnvioDeVentas.EnviarAsync<IReadOnlyList<HabilitacionDeConectorDto>>(http, auth, HttpMethod.Get, $"{Base}/{id}/tenants", null, null, ct);

    public Task<ResultadoDeInventario<object>> HabilitarAsync(Guid id, Guid cooperativa, bool habilitado, string motivo, CancellationToken ct = default) =>
        Ventas.EnvioDeVentas.EnviarAsync<object>(http, auth, HttpMethod.Put, $"{Base}/{id}/tenants/{cooperativa}",
            new { enabled = habilitado, reason = motivo }, null, ct);
}

public sealed record ConectorDePlataformaDto(Guid PublicId, string Code, string Name, string? Description, bool IsActive, int? CurrentVersion, int EnabledTenants);

public sealed record ConectorCreadoDto(Guid PublicId);

public sealed record VersionPublicadaDto(int Number);

public sealed record VersionDeConectorDto(int Number, string BaseUrl, string Definition, string Sha256, DateTime PublishedAt, string PublishedBy, string? Note);

public sealed record HabilitacionDeConectorDto(Guid TenantPublicId, string TenantName, bool Enabled, DateTime? ChangedAt, string? ChangedBy, string? Reason);

public sealed record SimulacionDeConectorDto(string Method, string Url, IReadOnlyDictionary<string, string> Headers, string? Body, IReadOnlyList<string> Missing);

/// <summary>La definición con que arranca el editor del primer conector: genérica, con el proveedor de ejemplo (contracts/api.md §1).</summary>
public static class EjemploDeDefinicion
{
    public const string Texto = """
        {
          "auth": { "kind": "LoginToken", "method": "POST", "path": "/v1/login",
            "body": "{\"username\":\"{{cred.username}}\",\"password\":\"{{cred.password}}\"}",
            "tokenPath": "$.token", "expiresInSeconds": 86400, "header": "Authorization", "prefix": "Bearer " },
          "operation": { "method": "POST", "path": "/v1/servicios/activar",
            "body": "{\"movil\":\"{{vehiculo.radioMovil}}\",\"placa\":\"{{vehiculo.placa}}\",\"valor\":{{pago.valor:int}},\"recibo\":\"{{pago.recibo}}\",\"externalID\":\"{{envio.id}}\"}" },
          "credentialFields": [ { "name": "username", "label": "Usuario", "secret": false }, { "name": "password", "label": "Clave", "secret": true } ],
          "responses": [ { "status": 200, "outcome": "Done" }, { "status": 401, "outcome": "ReAuth" }, { "status": 400, "outcome": "ManualReview" },
            { "status": 500, "outcome": "Retry" }, { "status": 0, "outcome": "Retry" } ],
          "maxAttempts": 12, "timeoutSeconds": 30
        }
        """;
}
