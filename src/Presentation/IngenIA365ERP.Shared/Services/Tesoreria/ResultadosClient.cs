using System.Net.Http.Headers;
using System.Security.Cryptography;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

/// <summary>Una línea de la respuesta del banco. (nuevo)</summary>
public sealed record LineaDeRespuestaDto(Guid PublicId, int LineNumber, int? FileLineNumber, string Outcome, string? BankCause, DateOnly BankDate, decimal Amount,
    string AccountNumber, string BeneficiaryTaxId, string? BankReference, bool Discrepancy, string? DiscrepancyDetail, bool Informed, DateTime? ReviewedAt,
    bool? ReviewAccepted, string? ReviewNote, string? OrderNumber);

/// <summary>Una respuesta del banco a un archivo de pago. (nuevo)</summary>
public sealed record RespuestaDelBancoDto(Guid PublicId, Guid FilePublicId, string Source, string? FormatCode, string? FileName, string? Sha256,
    Guid? AttachmentPublicId, DateTime LoadedAt, string LoadedByName, int LineCount, int DiscrepancyCount, int InformedCount,
    IReadOnlyList<LineaDeRespuestaDto> Lines);

/// <summary>Un resultado registrado de una orden. (nuevo)</summary>
public sealed record ResultadoDelGiroDto(Guid PublicId, Guid OrderPublicId, string OrderNumber, string Outcome, DateOnly OccurredOn, decimal Amount,
    string TreasuryReference, string? CheckNumber, string? Reason, string Source, string RecordedByName, DateTime RecordedAt, Guid? SupportAttachmentPublicId,
    Guid? EmittedMessagePublicId, bool Repeated);

public sealed record RevisionDeLineaRequest(bool Accept, string Note);

public sealed record RegistrarResultadoRequest(string Outcome, DateOnly OccurredOn, string? TreasuryReference, string? Reason, string? CheckNumber,
    Guid? SupportAttachmentPublicId);

public sealed record ResultadoEnBloque(Guid OrderPublicId, string Outcome, DateOnly OccurredOn, string? TreasuryReference, string? Reason, string? CheckNumber);

public sealed record RegistrarResultadosEnBloqueRequest(IReadOnlyList<ResultadoEnBloque> Items, Guid? SupportAttachmentPublicId);

/// <summary>
/// Cliente tipado de los resultados del giro (feature 015, US11, T0162; contracts/api.md §10): cargar la respuesta del banco a un archivo (multipart), revisar las líneas
/// que discrepan, registrar el resultado a mano uno a uno o en bloque con su soporte, y marcar el archivo rechazado por el banco. (nuevo)
/// </summary>
public sealed class ResultadosClient(HttpClient http, CentralAuthClient auth)
{
    private const string Archivos = "/api/treasury/payment-files";
    private const string Ordenes = "/api/treasury/payment-orders";

    /// <summary>El dueño de los soportes del registro manual (la orden o el lote).</summary>
    public const string DuenoDeSoportes = "TreasuryResultSupport";

    public async Task<ResultadoDeInventario<RespuestaDelBancoDto>> CargarRespuestaAsync(Guid archivo, string nombre, byte[] contenido, ClaveDeOperacion clave,
        CancellationToken ct = default)
    {
        if (auth.CurrentAccessToken is null) return ResultadoDeInventario<RespuestaDelBancoDto>.SinToken();
        var url = $"{Archivos}/{archivo}/responses";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            var multipart = new MultipartFormDataContent();
            var bytes = new ByteArrayContent(contenido);
            bytes.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            multipart.Add(bytes, "archivo", nombre);
            req.Content = multipart;
            clave.Aplicar(req, new { url, nombre, sha256 = Convert.ToHexString(SHA256.HashData(contenido)) });
            using var resp = await http.SendAsync(req, ct);
            var resultado = await ResultadoDeInventario<RespuestaDelBancoDto>.DesdeAsync(resp, ct);
            if (resultado.IsSuccess) clave.Exito();
            return resultado;
        }
        catch (HttpRequestException ex)
        {
            return ResultadoDeInventario<RespuestaDelBancoDto>.ErrorDeRed(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return ResultadoDeInventario<RespuestaDelBancoDto>.FormatoInesperado(ex.Message);
        }
    }

    public Task<ResultadoDeInventario<IReadOnlyList<RespuestaDelBancoDto>>> RespuestasAsync(Guid archivo, CancellationToken ct = default) =>
        Get<IReadOnlyList<RespuestaDelBancoDto>>($"{Archivos}/{archivo}/responses", ct);

    public Task<ResultadoDeInventario<RespuestaDelBancoDto>> RevisarLineaAsync(Guid archivo, Guid respuesta, Guid linea, RevisionDeLineaRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<RespuestaDelBancoDto>(http, auth, HttpMethod.Post, $"{Archivos}/{archivo}/responses/{respuesta}/lines/{linea}/review", r, clave, ct);

    public Task<ResultadoDeInventario<ArchivoDePagoDto>> RechazadoPorElBancoAsync(Guid archivo, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<ArchivoDePagoDto>(http, auth, HttpMethod.Post, $"{Archivos}/{archivo}/rejected-by-bank", new MotivoRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ResultadoDelGiroDto>>> ResultadosAsync(Guid orden, CancellationToken ct = default) =>
        Get<IReadOnlyList<ResultadoDelGiroDto>>($"{Ordenes}/{orden}/results", ct);

    public Task<ResultadoDeInventario<ResultadoDelGiroDto>> RegistrarAsync(Guid orden, RegistrarResultadoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<ResultadoDelGiroDto>(http, auth, HttpMethod.Post, $"{Ordenes}/{orden}/results", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ResultadoDelGiroDto>>> RegistrarEnBloqueAsync(RegistrarResultadosEnBloqueRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<IReadOnlyList<ResultadoDelGiroDto>>(http, auth, HttpMethod.Post, $"{Ordenes}/results/bulk", r, clave, ct);

    private Task<ResultadoDeInventario<T>> Get<T>(string url, CancellationToken ct) => EnvioDeVentas.EnviarAsync<T>(http, auth, HttpMethod.Get, url, null, null, ct);
}
