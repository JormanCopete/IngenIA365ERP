using System.Net.Http.Headers;
using IngenIA365ERP.Shared.Services.Contabilidad;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Reportes;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Implantacion;

/// <summary>
/// Cliente del cotejo con cifras de referencia (<c>/api/implantacion/cotejo/{opcion}</c>; nuevo). El archivo se sube en cada consulta: la API no
/// lo guarda, así que cambiar de resumen a detalle o exportar vuelve a mandarlo. La cabecera de sesión la pone el handler, como en todo cliente.
/// </summary>
public sealed class CotejoClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/implantacion/cotejo";

    /// <summary>La plantilla de la opción (.xlsx).</summary>
    public async Task<InvitationApiResult<ArchivoDescargado>> PlantillaAsync(string opcion, CancellationToken ct = default)
    {
        if (auth.CurrentAccessToken is null)
            return InvitationApiResult<ArchivoDescargado>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{Base}/{opcion}/template.xlsx");
            return await ArchivoAsync(await http.SendAsync(req, ct), ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ArchivoDescargado>.NetworkError(ex.Message);
        }
    }

    /// <summary>El cotejo en pantalla (json). Si el archivo tiene errores, <see cref="ResultadoContable{T}.ErroresDeFila"/> los trae con fila y columna.</summary>
    public async Task<ResultadoContable<TablaReporteDto>> CotejarAsync(string opcion, string archivo, byte[] contenido, ConsultaDeCotejo consulta,
        CancellationToken ct = default)
    {
        if (auth.CurrentAccessToken is null) return ResultadoContable<TablaReporteDto>.SinToken();
        try
        {
            using var req = Peticion(opcion, archivo, contenido, consulta, null);
            return await ResultadoContable<TablaReporteDto>.DesdeAsync(await http.SendAsync(req, ct), ct);
        }
        catch (HttpRequestException ex)
        {
            return ResultadoContable<TablaReporteDto>.ErrorDeRed(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return new ResultadoContable<TablaReporteDto>(false, default, "Generic.RespuestaInesperada", $"El servidor respondió con un formato inesperado: {ex.Message}", 0, null);
        }
    }

    /// <summary>El mismo cotejo como archivo (<paramref name="formato"/> xlsx, pdf o docx).</summary>
    public async Task<InvitationApiResult<ArchivoDescargado>> ExportarAsync(string opcion, string archivo, byte[] contenido, ConsultaDeCotejo consulta,
        string formato, CancellationToken ct = default)
    {
        if (auth.CurrentAccessToken is null)
            return InvitationApiResult<ArchivoDescargado>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
        try
        {
            using var req = Peticion(opcion, archivo, contenido, consulta, formato);
            return await ArchivoAsync(await http.SendAsync(req, ct), ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ArchivoDescargado>.NetworkError(ex.Message);
        }
    }

    private static HttpRequestMessage Peticion(string opcion, string archivo, byte[] contenido, ConsultaDeCotejo c, string? formato)
    {
        var q = new List<string>
        {
            $"asOf={c.Corte:yyyy-MM-dd}",
            $"level={(c.Detalle ? "detalle" : "resumen")}",
            $"onlyDifferences={(c.SoloDiferencias ? "true" : "false")}",
            $"debitMinusCredit={(c.DebitoMenosCredito ? "true" : "false")}",
            $"tolerance={c.Tolerancia.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
        };
        if (!string.IsNullOrWhiteSpace(c.Grupo)) q.Add($"group={Uri.EscapeDataString(c.Grupo)}");
        if (!string.IsNullOrWhiteSpace(formato)) q.Add($"format={Uri.EscapeDataString(formato)}");
        var req = new HttpRequestMessage(HttpMethod.Post, $"{Base}/{opcion}?{string.Join('&', q)}");
        var multipart = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(contenido);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(bytes, "archivo", archivo);
        req.Content = multipart;
        return req;
    }

    private static async Task<InvitationApiResult<ArchivoDescargado>> ArchivoAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (!resp.IsSuccessStatusCode) return await CentralAuthApi.ParseAsync<ArchivoDescargado>(resp, ct);
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        var nombre = resp.Content.Headers.ContentDisposition?.FileNameStar ?? resp.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "cotejo";
        return InvitationApiResult<ArchivoDescargado>.Success(new ArchivoDescargado(nombre, resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", bytes));
    }
}

/// <summary>Lo que se pide al cotejar además del archivo.</summary>
public sealed record ConsultaDeCotejo(DateOnly Corte, bool Detalle, string? Grupo, bool SoloDiferencias, bool DebitoMenosCredito, decimal Tolerancia);
