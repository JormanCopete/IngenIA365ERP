using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IngenIA365ERP.ExternalChannels.Canal;

/// <summary>
/// Feature 021 (T022; research R2): el cliente HTTP del ERP hacia la API interna del canal digital. Cada petición lleva un token de
/// servicio ES256 recién firmado (<c>iss = ingenia365-erp</c>, <c>aud = ingenia365-canal</c>, <c>sub</c> = la cooperativa, 60 s,
/// <c>jti</c> único). La URL y la llave privada llegan por configuración del ambiente (<c>CanalServicio:UrlInterna</c>,
/// <c>CanalServicio:LlavePrivadaErp</c>, <c>CanalServicio:KidErp</c>); sin ellas, <see cref="Configurado"/> es falso y cada operación
/// responde <c>Channel.Satellite.NotConfigured</c> en vez de fallar en silencio.
/// </summary>
public sealed class ClienteDelCanal : ICanalSatelite, IDisposable
{
    public const string NombreDelCliente = "canal-digital";

    private const string Emisor = "ingenia365-erp";
    private const string Audiencia = "ingenia365-canal";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _fabrica;
    private readonly ILogger<ClienteDelCanal> _logger;
    private readonly ECDsa? _llave;
    private readonly SigningCredentials? _firma;
    private readonly string? _url;

    public ClienteDelCanal(IHttpClientFactory fabrica, IConfiguration configuracion, ILogger<ClienteDelCanal> logger)
    {
        _fabrica = fabrica;
        _logger = logger;
        _url = configuracion["CanalServicio:UrlInterna"]?.TrimEnd('/');
        var pem = configuracion["CanalServicio:LlavePrivadaErp"];
        if (!string.IsNullOrWhiteSpace(pem))
        {
            _llave = ECDsa.Create();
            _llave.ImportFromPem(pem);
            var kid = configuracion["CanalServicio:KidErp"] ?? "erp1";
            _firma = new SigningCredentials(new ECDsaSecurityKey(_llave) { KeyId = kid }, SecurityAlgorithms.EcdsaSha256);
        }
    }

    public bool Configurado => _firma is not null && !string.IsNullOrWhiteSpace(_url);

    public Task<Result<string>> ReservarCodigoAsync(Guid cooperativa, string sugerido, CancellationToken ct) =>
        EnviarAsync<string>(cooperativa, HttpMethod.Post, $"/internal/v1/tenants/{cooperativa}/channel-code", new { suggested = sugerido }, ct);

    public Task<Result<VerificacionDeCredencialesDto>> GuardarCredencialesAsync(Guid cooperativa, Guid sitio, CredencialesDePasarela credenciales, CancellationToken ct) =>
        EnviarAsync<VerificacionDeCredencialesDto>(cooperativa, HttpMethod.Put, $"/internal/v1/tenants/{cooperativa}/sites/{sitio}/credentials", new
        {
            gateway = credenciales.Pasarela,
            baseUrl = credenciales.UrlBase,
            login = credenciales.Login,
            secretKey = credenciales.SecretKey,
            paymentMethods = credenciales.MediosDePago,
        }, ct);

    public Task<Result<IReadOnlyList<IntentoDelCanalDto>>> BuscarIntentosAsync(Guid cooperativa, string? tipoDocumento, string? documento, string? referencia, CancellationToken ct) =>
        EnviarAsync<IReadOnlyList<IntentoDelCanalDto>>(cooperativa, HttpMethod.Get,
            $"/internal/v1/tenants/{cooperativa}/attempts?documentType={Uri.EscapeDataString(tipoDocumento ?? "")}&document={Uri.EscapeDataString(documento ?? "")}&reference={Uri.EscapeDataString(referencia ?? "")}",
            null, ct);

    public Task<Result<CuentaDelCanalDto?>> BuscarCuentaAsync(Guid cooperativa, string tipoDocumento, string documento, CancellationToken ct) =>
        EnviarAsync<CuentaDelCanalDto?>(cooperativa, HttpMethod.Get,
            $"/internal/v1/tenants/{cooperativa}/accounts?documentType={Uri.EscapeDataString(tipoDocumento)}&document={Uri.EscapeDataString(documento)}", null, ct);

    public async Task<Result> DesbloquearCuentaAsync(Guid cooperativa, Guid cuenta, CancellationToken ct) =>
        await EnviarAsync<object?>(cooperativa, HttpMethod.Post, $"/internal/v1/tenants/{cooperativa}/accounts/{cuenta}/unlock", new { }, ct);

    public async Task<Result> CerrarSesionesAsync(Guid cooperativa, Guid cuenta, CancellationToken ct) =>
        await EnviarAsync<object?>(cooperativa, HttpMethod.Post, $"/internal/v1/tenants/{cooperativa}/accounts/{cuenta}/sessions/revoke", new { }, ct);

    public Task<Result<ReversoEnPasarelaDto>> ReversarAsync(Guid cooperativa, string referencia, string solicitadoPor, string motivo, CancellationToken ct) =>
        EnviarAsync<ReversoEnPasarelaDto>(cooperativa, HttpMethod.Post, $"/internal/v1/tenants/{cooperativa}/attempts/{Uri.EscapeDataString(referencia)}/reverse",
            new { requestedBy = solicitadoPor, reason = motivo }, ct);

    public Task<Result<MetricasDelCanalDto>> MetricasAsync(Guid cooperativa, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        EnviarAsync<MetricasDelCanalDto>(cooperativa, HttpMethod.Get, $"/internal/v1/tenants/{cooperativa}/metrics?from={desde:yyyy-MM-dd}&to={hasta:yyyy-MM-dd}", null, ct);

    public async Task<Result> SincronizarAsync(Guid cooperativa, CancellationToken ct) =>
        await EnviarAsync<object?>(cooperativa, HttpMethod.Post, $"/internal/v1/tenants/{cooperativa}/sync", new { }, ct);

    private async Task<Result<T>> EnviarAsync<T>(Guid cooperativa, HttpMethod metodo, string ruta, object? cuerpo, CancellationToken ct)
    {
        if (!Configurado)
            return Result.Failure<T>("Channel.Satellite.NotConfigured", "El canal digital no está configurado en este ambiente (URL interna o llave del ERP).");

        using var peticion = new HttpRequestMessage(metodo, _url + ruta);
        peticion.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token(cooperativa));
        if (cuerpo is not null) peticion.Content = JsonContent.Create(cuerpo, options: Json);

        try
        {
            using var respuesta = await _fabrica.CreateClient(NombreDelCliente).SendAsync(peticion, ct);
            if (respuesta.IsSuccessStatusCode)
            {
                if (respuesta.Content.Headers.ContentLength == 0 || typeof(T) == typeof(object))
                    return Result.Success<T>(default!);
                var valor = await respuesta.Content.ReadFromJsonAsync<T>(Json, ct);
                return Result.Success(valor!);
            }

            var error = await LeerErrorAsync(respuesta, ct);
            _logger.LogWarning("[Canal.RespuestaDeError] {Metodo} {Ruta} respondió {Estado} {Codigo}.", metodo, ruta, (int)respuesta.StatusCode, error.Code);
            return Result.Failure<T>(error);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Canal.NoDisponible] No se pudo llamar al canal: {Metodo} {Ruta}.", metodo, ruta);
            return Result.Failure<T>("Channel.Satellite.Unavailable", "El canal digital no respondió. Inténtalo de nuevo en unos minutos.");
        }
    }

    private string Token(Guid cooperativa)
    {
        var ahora = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Emisor,
            Audience = Audiencia,
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = ahora.AddSeconds(60),
            SigningCredentials = _firma,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = cooperativa.ToString(),
                [JwtRegisteredClaimNames.Jti] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            },
        });
    }

    private async Task<Error> LeerErrorAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        try
        {
            var sobre = await respuesta.Content.ReadFromJsonAsync<SobreDeError>(Json, ct);
            if (sobre is { Code: { Length: > 0 } codigo }) return new Error(codigo, sobre.Message ?? "El canal rechazó la operación.");
        }
        catch (JsonException ex)
        {
            // El cuerpo no es el sobre del canal: se informa por el estado HTTP, abajo.
            _logger.LogWarning(ex, "[Canal.RespuestaSinSobre] El canal respondió {Estado} sin el sobre de error.", (int)respuesta.StatusCode);
        }
        return new Error("Channel.Satellite.Error", $"El canal respondió {(int)respuesta.StatusCode}.");
    }

    private sealed record SobreDeError(string? Code, string? Message);

    public void Dispose() => _llave?.Dispose();
}
