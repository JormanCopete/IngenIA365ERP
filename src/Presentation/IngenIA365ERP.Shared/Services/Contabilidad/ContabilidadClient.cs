using System.Net.Http.Headers;
using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// Cliente tipado del módulo contable (feature 009), mismo molde que <see cref="NominaClient"/>:
/// adjunta el token desde <see cref="CentralAuthClient"/> porque durante el prerenderizado en el
/// servidor el almacenamiento seguro está vacío y un <c>HttpClient</c> plano saldría sin
/// autorización. Cada historia añade aquí sus métodos; los DTOs viven en
/// <c>ContabilidadDtos.cs</c> y son el espejo de <c>contracts/api.md</c>.
/// </summary>
public sealed partial class ContabilidadClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/accounting";

    // ------------------------------------------------------------ configuración y catálogos --

    public Task<InvitationApiResult<ConfiguracionContableDto>> ObtenerConfiguracionAsync(CancellationToken ct = default) =>
        EnviarAsync<ConfiguracionContableDto>(HttpMethod.Get, $"{Base}/setup", null, ct);

    public Task<InvitationApiResult<InicializacionDto>> InicializarAsync(InicializarContabilidadRequest request, CancellationToken ct = default) =>
        EnviarAsync<InicializacionDto>(HttpMethod.Post, $"{Base}/setup/initialize", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> ActualizarConfiguracionAsync(ActualizarConfiguracionRequest request, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"{Base}/setup", request, ct);

    public Task<InvitationApiResult<IReadOnlyList<CatalogoContableDto>>> ListarCatalogosAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CatalogoContableDto>>(HttpMethod.Get, $"{Base}/setup/catalogs", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<EntradaDeCatalogoDto>>> ListarEntradasDeCatalogoAsync(string code, byte? level = null, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<EntradaDeCatalogoDto>>(HttpMethod.Get,
            $"{Base}/setup/catalogs/{Uri.EscapeDataString(code)}/entries{(level is { } l ? $"?level={l}" : string.Empty)}", null, ct);

    public Task<ResultadoContable<ImportacionDeCatalogoDto>> ImportarCatalogoAsync(string nombre, string archivo, byte[] contenido, CancellationToken ct = default) =>
        SubirAsync<ImportacionDeCatalogoDto>($"{Base}/setup/catalogs/import", archivo, contenido, new Dictionary<string, string> { ["name"] = nombre }, ct);

    public Task<InvitationApiResult<EmptyResponse>> ValidarCatalogoAsync(string code, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{Base}/setup/catalogs/{Uri.EscapeDataString(code)}/validate", null, ct);

    public Task<InvitationApiResult<EmptyResponse>> RetirarCatalogoAsync(string code, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Delete, $"{Base}/setup/catalogs/{Uri.EscapeDataString(code)}", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<CuentaNuevaDeCatalogoDto>>> CuentasNuevasDelCatalogoAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CuentaNuevaDeCatalogoDto>>(HttpMethod.Get, $"{Base}/setup/catalogs/updates", null, ct);

    public Task<InvitationApiResult<InicializacionDto>> AdoptarCuentasNuevasAsync(IReadOnlyList<string> codes, CancellationToken ct = default) =>
        EnviarAsync<InicializacionDto>(HttpMethod.Post, $"{Base}/setup/catalogs/updates/adopt", new { Codes = codes }, ct);

    // -------------------------------------------------------------------------- plan de cuentas --

    public Task<InvitationApiResult<IReadOnlyList<CuentaNodoDto>>> ArbolDeCuentasAsync(Guid? parent = null, bool onlyActive = false, bool todo = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CuentaNodoDto>>(HttpMethod.Get,
            $"{Base}/accounts/tree?onlyActive={(onlyActive ? "true" : "false")}&all={(todo ? "true" : "false")}{(parent is { } p ? $"&parent={p}" : string.Empty)}", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<CuentaBuscadaDto>>> BuscarCuentasAsync(string q, string? module = null, bool onlyMovement = true, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CuentaBuscadaDto>>(HttpMethod.Get,
            $"{Base}/accounts/search?q={Uri.EscapeDataString(q)}&onlyMovement={(onlyMovement ? "true" : "false")}{(string.IsNullOrWhiteSpace(module) ? string.Empty : $"&module={Uri.EscapeDataString(module)}")}", null, ct);

    public Task<InvitationApiResult<CuentaDto>> ObtenerCuentaAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<CuentaDto>(HttpMethod.Get, $"{Base}/accounts/{id}", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<EventoDeCuentaDto>>> HistorialDeCuentaAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<EventoDeCuentaDto>>(HttpMethod.Get, $"{Base}/accounts/{id}/history", null, ct);

    public Task<InvitationApiResult<CreadoDto>> CrearCuentaAsync(CuentaRequest request, CancellationToken ct = default) =>
        EnviarAsync<CreadoDto>(HttpMethod.Post, $"{Base}/accounts", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> ActualizarCuentaAsync(Guid id, CuentaRequest request, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"{Base}/accounts/{id}", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> InactivarCuentaAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{Base}/accounts/{id}/deactivate", null, ct);

    public Task<InvitationApiResult<EmptyResponse>> ActivarCuentaAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{Base}/accounts/{id}/activate", null, ct);

    public Task<InvitationApiResult<EmptyResponse>> EliminarCuentaAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Delete, $"{Base}/accounts/{id}", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<ParametrizacionInvalidaDto>>> ParametrizacionesInvalidasAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ParametrizacionInvalidaDto>>(HttpMethod.Get, $"{Base}/accounts/invalid-parameterizations", null, ct);

    // ------------------------------------------------------- tipos de comprobante y de cruce --

    public Task<InvitationApiResult<IReadOnlyList<TipoComprobanteDto>>> ListarTiposDeComprobanteAsync(bool incluirInactivos = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<TipoComprobanteDto>>(HttpMethod.Get, $"{Base}/voucher-types?includeInactive={(incluirInactivos ? "true" : "false")}", null, ct);

    public Task<InvitationApiResult<CreadoDto>> CrearTipoDeComprobanteAsync(TipoComprobanteRequest request, CancellationToken ct = default) =>
        EnviarAsync<CreadoDto>(HttpMethod.Post, $"{Base}/voucher-types", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> ActualizarTipoDeComprobanteAsync(Guid id, TipoComprobanteRequest request, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"{Base}/voucher-types/{id}", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> InactivarTipoDeComprobanteAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{Base}/voucher-types/{id}/deactivate", null, ct);

    public Task<InvitationApiResult<EmptyResponse>> ActivarTipoDeComprobanteAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{Base}/voucher-types/{id}/activate", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<TipoCruceDto>>> ListarTiposDeCruceAsync(bool incluirInactivos = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<TipoCruceDto>>(HttpMethod.Get, $"{Base}/cross-document-types?includeInactive={(incluirInactivos ? "true" : "false")}", null, ct);

    public Task<InvitationApiResult<CreadoDto>> CrearTipoDeCruceAsync(TipoCruceRequest request, CancellationToken ct = default) =>
        EnviarAsync<CreadoDto>(HttpMethod.Post, $"{Base}/cross-document-types", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> ActualizarTipoDeCruceAsync(Guid id, TipoCruceRequest request, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"{Base}/cross-document-types/{id}", request, ct);

    // ----------------------------------------------------------------------------- períodos --

    public Task<InvitationApiResult<EjercicioDto>> ObtenerEjercicioAsync(int year, CancellationToken ct = default) =>
        EnviarAsync<EjercicioDto>(HttpMethod.Get, $"{Base}/periods?year={year}", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<int>>> ListarEjerciciosAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<int>>(HttpMethod.Get, $"{Base}/periods/years", null, ct);

    public Task<InvitationApiResult<EjercicioDto>> AbrirEjercicioAsync(int year, CancellationToken ct = default) =>
        EnviarAsync<EjercicioDto>(HttpMethod.Post, $"{Base}/periods/years", new AbrirEjercicioRequest(year), ct);

    public Task<InvitationApiResult<EmptyResponse>> CerrarPeriodoAsync(int year, int month, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{Base}/periods/{year}/{month}/close", null, ct);

    public Task<InvitationApiResult<EmptyResponse>> ReabrirPeriodoAsync(int year, int month, string motivo, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{Base}/periods/{year}/{month}/reopen", new MotivoRequest(motivo), ct);

    public Task<InvitationApiResult<CierreDeEjercicioDto>> CerrarEjercicioAsync(int year, CancellationToken ct = default) =>
        EnviarAsync<CierreDeEjercicioDto>(HttpMethod.Post, $"{Base}/periods/years/{year}/close", null, ct);

    public Task<InvitationApiResult<EmptyResponse>> ReabrirEjercicioAsync(int year, string motivo, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{Base}/periods/years/{year}/reopen", new MotivoRequest(motivo), ct);

    // -------------------------------------------------------------------------- comprobantes --

    public Task<InvitationApiResult<PaginaDto<ComprobanteResumenDto>>> ListarComprobantesAsync(FiltroDeComprobantes filtro, CancellationToken ct = default)
    {
        var url = $"{Base}/documents?page={filtro.Page}&pageSize={filtro.PageSize}";
        if (filtro.From is { } desde) url += $"&from={desde:yyyy-MM-dd}";
        if (filtro.To is { } hasta) url += $"&to={hasta:yyyy-MM-dd}";
        if (!string.IsNullOrWhiteSpace(filtro.VoucherType)) url += $"&voucherType={Uri.EscapeDataString(filtro.VoucherType)}";
        if (!string.IsNullOrWhiteSpace(filtro.Status)) url += $"&status={Uri.EscapeDataString(filtro.Status)}";
        if (!string.IsNullOrWhiteSpace(filtro.Origin)) url += $"&origin={Uri.EscapeDataString(filtro.Origin)}";
        if (filtro.Number is { } numero) url += $"&number={numero}";
        return EnviarAsync<PaginaDto<ComprobanteResumenDto>>(HttpMethod.Get, url, null, ct);
    }

    public Task<InvitationApiResult<ComprobanteDto>> ObtenerComprobanteAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<ComprobanteDto>(HttpMethod.Get, $"{Base}/documents/{id}", null, ct);

    public Task<InvitationApiResult<ComprobanteDto>> ComprobanteDeOrigenAsync(string module, Guid sourcePublicId, CancellationToken ct = default) =>
        EnviarAsync<ComprobanteDto>(HttpMethod.Get, $"{Base}/documents/by-source?module={Uri.EscapeDataString(module)}&sourcePublicId={sourcePublicId}", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<ComprobanteResumenDto>>> MisBorradoresAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ComprobanteResumenDto>>(HttpMethod.Get, $"{Base}/documents/my-drafts", null, ct);

    public Task<InvitationApiResult<BorradorGuardadoDto>> GuardarBorradorAsync(BorradorRequest request, CancellationToken ct = default) =>
        EnviarAsync<BorradorGuardadoDto>(HttpMethod.Post, $"{Base}/documents/drafts", request, ct);

    public Task<InvitationApiResult<BorradorGuardadoDto>> ActualizarBorradorAsync(Guid id, BorradorRequest request, CancellationToken ct = default) =>
        EnviarAsync<BorradorGuardadoDto>(HttpMethod.Put, $"{Base}/documents/drafts/{id}", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> DescartarBorradorAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Delete, $"{Base}/documents/drafts/{id}", null, ct);

    public Task<InvitationApiResult<ValidacionDto>> ValidarAsync(BorradorRequest request, CancellationToken ct = default) =>
        EnviarAsync<ValidacionDto>(HttpMethod.Post, $"{Base}/documents/validate", request, ct);

    public Task<ResultadoContable<ContabilizadoDto>> ContabilizarAsync(Guid id, CancellationToken ct = default) =>
        EnviarConDatosAsync<ContabilizadoDto>(HttpMethod.Post, $"{Base}/documents/{id}/post", null, ct);

    public Task<ResultadoContable<ReversadoDto>> ReversarAsync(Guid id, ReversionRequest request, CancellationToken ct = default) =>
        EnviarConDatosAsync<ReversadoDto>(HttpMethod.Post, $"{Base}/documents/{id}/reverse", request, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> ImprimirComprobanteAsync(Guid id, CancellationToken ct = default) =>
        DescargarAsync($"{Base}/documents/{id}/print", ct);

    // ------------------------------------------------------------------------------- apertura --

    public Task<InvitationApiResult<ArchivoDescargado>> PlantillaDeAperturaAsync(CancellationToken ct = default) =>
        DescargarAsync($"{Base}/opening/template.xlsx", ct);

    public Task<ResultadoContable<AperturaImportadaDto>> ImportarAperturaAsync(string archivo, byte[] contenido, CancellationToken ct = default) =>
        SubirAsync<AperturaImportadaDto>($"{Base}/opening/import", archivo, contenido, null, ct);

    // -------------------------------------------------------------------------------- común --

    /// <summary>Como <c>EnviarAsync</c>, pero conserva <c>data</c> (errores por línea de un comprobante).</summary>
    private async Task<ResultadoContable<T>> EnviarConDatosAsync<T>(HttpMethod metodo, string url, object? cuerpo, CancellationToken ct)
    {
        var token = auth.CurrentAccessToken;
        if (token is null) return ResultadoContable<T>.SinToken();
        try
        {
            using var req = new HttpRequestMessage(metodo, url);
            if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);
            var resp = await http.SendAsync(req, ct);
            return await ResultadoContable<T>.DesdeAsync(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return ResultadoContable<T>.ErrorDeRed(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return new ResultadoContable<T>(false, default, "Generic.RespuestaInesperada", $"El servidor respondió con un formato inesperado: {ex.Message}", 0, null);
        }
    }



    private async Task<InvitationApiResult<T>> EnviarAsync<T>(HttpMethod metodo, string url, object? cuerpo, CancellationToken ct)
    {
        var token = auth.CurrentAccessToken;
        if (token is null)
            return InvitationApiResult<T>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);

        try
        {
            using var req = new HttpRequestMessage(metodo, url);
            if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);
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

    /// <summary>Un archivo (multipart) más campos de texto; el servidor responde JSON.</summary>
    private async Task<ResultadoContable<T>> SubirAsync<T>(string url, string archivo, byte[] contenido, IReadOnlyDictionary<string, string>? campos, CancellationToken ct)
    {
        var token = auth.CurrentAccessToken;
        if (token is null)
            return ResultadoContable<T>.SinToken();

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            var multipart = new MultipartFormDataContent();
            var bytes = new ByteArrayContent(contenido);
            bytes.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(bytes, "archivo", archivo);
            if (campos is not null)
                foreach (var (clave, valor) in campos) multipart.Add(new StringContent(valor), clave);
            req.Content = multipart;
            var resp = await http.SendAsync(req, ct);
            return await ResultadoContable<T>.DesdeAsync(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return ResultadoContable<T>.ErrorDeRed(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return new ResultadoContable<T>(false, default, "Generic.RespuestaInesperada", $"El servidor respondió con un formato inesperado: {ex.Message}", 0, null);
        }
    }

    private async Task<InvitationApiResult<ArchivoDescargado>> DescargarAsync(string url, CancellationToken ct)
    {
        var token = auth.CurrentAccessToken;
        if (token is null)
            return InvitationApiResult<ArchivoDescargado>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
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
