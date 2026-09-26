using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>
/// Cliente tipado del catálogo tributario de Core (feature 012, T170; contracts/api.md §30), molde
/// <see cref="PersonasClient"/>: devuelve <see cref="InvitationApiResult{T}"/> con el código de error del sobre. La
/// cabecera <c>Authorization</c> la pone el handler de la sesión; aquí sólo se comprueba que haya sesión. Toda escritura
/// lleva la <c>Idempotency-Key</c> de la operación de pantalla (<see cref="ClaveDeOperacion"/>).
/// </summary>
public sealed class ImpuestosClient(HttpClient http, CentralAuthClient auth)
{
    private const string Impuestos = "/api/core/taxes";
    private const string Tarifas = "/api/core/tax-rates";
    private const string Conceptos = "/api/core/withholding-concepts";

    // ------------------------------------------------------------------------------------------- impuestos --

    public Task<InvitationApiResult<IReadOnlyList<ImpuestoDto>>> ListarImpuestosAsync(bool? activos = null, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ImpuestoDto>>(HttpMethod.Get, activos is { } a ? $"{Impuestos}?active={(a ? "true" : "false")}" : Impuestos, null, null, ct);

    public Task<InvitationApiResult<ImpuestoDto>> ObtenerImpuestoAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<ImpuestoDto>(HttpMethod.Get, $"{Impuestos}/{id}", null, null, ct);

    public Task<InvitationApiResult<ImpuestoCreadoDto>> CrearImpuestoAsync(CrearImpuestoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ImpuestoCreadoDto>(HttpMethod.Post, Impuestos, request, clave, ct);

    public Task<InvitationApiResult<EmptyResponse>> EditarImpuestoAsync(Guid id, EditarImpuestoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"{Impuestos}/{id}", request, clave, ct);

    // --------------------------------------------------------------------------------------------- tarifas --

    /// <summary>Las tarifas; <paramref name="soloVigentes"/> = las que rigen hoy; <paramref name="pendientes"/> filtra «pendiente de validar».</summary>
    public Task<InvitationApiResult<IReadOnlyList<TarifaTributariaDto>>> ListarTarifasAsync(Guid? impuesto = null, DateOnly? aLaFecha = null, string? municipio = null,
        bool? pendientes = null, bool? soloVigentes = null, CancellationToken ct = default)
    {
        var filtros = new List<string>();
        if (impuesto is { } i) filtros.Add($"tax={i}");
        if (aLaFecha is { } f) filtros.Add($"asOf={f:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(municipio)) filtros.Add($"municipality={Uri.EscapeDataString(municipio.Trim())}");
        if (pendientes is { } p) filtros.Add($"reviewPending={(p ? "true" : "false")}");
        if (soloVigentes is { } v) filtros.Add($"onlyCurrent={(v ? "true" : "false")}");
        var url = filtros.Count == 0 ? Tarifas : $"{Tarifas}?{string.Join('&', filtros)}";
        return EnviarAsync<IReadOnlyList<TarifaTributariaDto>>(HttpMethod.Get, url, null, null, ct);
    }

    public Task<InvitationApiResult<TarifaTributariaDto>> ObtenerTarifaAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<TarifaTributariaDto>(HttpMethod.Get, $"{Tarifas}/{id}", null, null, ct);

    public Task<InvitationApiResult<TarifaCreadaDto>> CrearTarifaAsync(TarifaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<TarifaCreadaDto>(HttpMethod.Post, Tarifas, request, clave, ct);

    public Task<InvitationApiResult<EmptyResponse>> CorregirTarifaAsync(Guid id, TarifaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"{Tarifas}/{id}", request, clave, ct);

    public Task<InvitationApiResult<EmptyResponse>> CerrarTarifaAsync(Guid id, CerrarTarifaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{Tarifas}/{id}/close", request, clave, ct);

    public Task<InvitationApiResult<EmptyResponse>> MarcarRevisadaAsync(Guid id, MotivoTributarioRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{Tarifas}/{id}/review", request, clave, ct);

    // ------------------------------------------------------------------------------------------- conceptos --

    public Task<InvitationApiResult<IReadOnlyList<ConceptoDeRetencionDto>>> ListarConceptosAsync(bool? activos = null, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ConceptoDeRetencionDto>>(HttpMethod.Get, activos is { } a ? $"{Conceptos}?active={(a ? "true" : "false")}" : Conceptos, null, null, ct);

    public Task<InvitationApiResult<ConceptoCreadoDto>> CrearConceptoAsync(CrearConceptoDeRetencionRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ConceptoCreadoDto>(HttpMethod.Post, Conceptos, request, clave, ct);

    public Task<InvitationApiResult<EmptyResponse>> EditarConceptoAsync(Guid id, EditarConceptoDeRetencionRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"{Conceptos}/{id}", request, clave, ct);

    // ------------------------------------------------------------------------------------------- plantilla --

    /// <summary>La plantilla 1 vacía o con lo que hoy tiene la cooperativa (<c>withData</c>).</summary>
    public async Task<InvitationApiResult<ArchivoDescargado>> PlantillaAsync(bool conDatos, CancellationToken ct = default)
    {
        if (auth.CurrentAccessToken is null)
            return InvitationApiResult<ArchivoDescargado>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{Impuestos}/template.xlsx{(conDatos ? "?withData=true" : string.Empty)}");
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return await CentralAuthApi.ParseAsync<ArchivoDescargado>(resp, ct);
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            var nombre = resp.Content.Headers.ContentDisposition?.FileNameStar
                         ?? resp.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                         ?? "plantilla-impuestos.xlsx";
            return InvitationApiResult<ArchivoDescargado>.Success(
                new ArchivoDescargado(nombre, resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", bytes));
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ArchivoDescargado>.NetworkError(ex.Message);
        }
    }

    // ----------------------------------------------------------------------------------------------- envío --

    private async Task<InvitationApiResult<T>> EnviarAsync<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct)
    {
        if (auth.CurrentAccessToken is null)
            return InvitationApiResult<T>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);

        try
        {
            using var req = new HttpRequestMessage(metodo, url);
            if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo, cuerpo.GetType());
            clave?.Aplicar(req, cuerpo);
            var resp = await http.SendAsync(req, ct);
            var resultado = await CentralAuthApi.ParseAsync<T>(resp, ct);
            if (resultado.IsSuccess) clave?.Exito();
            return resultado;
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
