using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de la originación (feature 013, US2, T0409; contracts/api.md §4.1–§4.5): el simulador con su PDF y su envío, las solicitudes
/// (borrador, documentos, radicar, análisis, volver a validar, enrutamiento, anular o desistir), las decisiones y «Mis pendientes», las
/// consultas a centrales con su enlace al informe y el resultado de las listas restrictivas con su liberación. Sin <c>Authorization</c> a mano
/// (la pone el handler de la sesión); toda escritura lleva su <see cref="ClaveDeOperacion"/>; la simulación, su PDF, el enrutamiento y el
/// enlace de descarga van sin clave aunque algunos vayan por POST: no escriben (api.md §1.5). (nuevo)
/// </summary>
public sealed class OriginacionClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeSimulaciones = GobiernoDeCarteraClient.Base + "/simulations";
    public const string RutaDeSolicitudes = GobiernoDeCarteraClient.Base + "/applications";
    public const string RutaDePendientes = GobiernoDeCarteraClient.Base + "/pending-decisions";
    public const string RutaDeCentrales = GobiernoDeCarteraClient.Base + "/bureau-queries";
    public const string RutaDeListas = GobiernoDeCarteraClient.Base + "/screening-results";

    /// <summary>El dueño de los documentos de una solicitud (<c>AdjuntosDeModulo.DocumentoDeSolicitud</c>), para la subida directa.</summary>
    public const string DuenoDeDocumentos = "LoanApplicationDocument";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // ------------------------------------------------------------------------------------------- simulador --

    /// <summary>El plan cuota a cuota sin registrar nada. La usura excedida no es error: <c>usury.passes = false</c> y un aviso.</summary>
    public Task<ResultadoDeInventario<SimulacionDto>> SimularAsync(SimulacionRequest request, CancellationToken ct = default) =>
        Enviar<SimulacionDto>(HttpMethod.Post, RutaDeSimulaciones + "/schedule", request, null, ct);

    /// <summary>La misma simulación en PDF (sin número ni código, con la leyenda de que no constituye aprobación). Sin clave.</summary>
    public async Task<InvitationApiResult<ArchivoDescargado>> SimulacionEnPdfAsync(SimulacionRequest request, CancellationToken ct = default)
    {
        if (auth.CurrentAccessToken is null)
            return InvitationApiResult<ArchivoDescargado>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, RutaDeSimulaciones + "/schedule/pdf") { Content = JsonContent.Create(request) };
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return await CentralAuthApi.ParseAsync<ArchivoDescargado>(resp, ct);
            return InvitationApiResult<ArchivoDescargado>.Success(await ArchivoAsync(resp, "simulacion.pdf", ct));
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ArchivoDescargado>.NetworkError(ex.Message);
        }
    }

    /// <summary>Envía la simulación por correo a la persona (mensaje informativo con el PDF; exige su autorización y su correo).</summary>
    public Task<ResultadoDeInventario<SimulacionEnviadaDto>> EnviarSimulacionAsync(EnviarSimulacionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<SimulacionEnviadaDto>(HttpMethod.Post, RutaDeSimulaciones + "/schedule/send", request, clave, ct);

    // ------------------------------------------------------------------------------------------- solicitudes --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<SolicitudEnListaDto>>> SolicitudesAsync(FiltrosDeSolicitudes? filtros = null,
        CancellationToken ct = default)
    {
        var f = filtros ?? new FiltrosDeSolicitudes();
        return Enviar<PaginaDeInventarioDto<SolicitudEnListaDto>>(HttpMethod.Get, ConQuery(RutaDeSolicitudes, Query(
            ("status", Texto(f.Status)), ("line", Texto(f.Line)), ("office", Texto(f.Office)), ("applicant", Texto(f.Applicant)), ("level", f.Level),
            ("mine", Texto(f.Mine)), ("from", Texto(f.From)), ("to", Texto(f.To)), ("page", Texto(f.Page)), ("pageSize", Texto(f.PageSize)))), null, null, ct);
    }

    public Task<ResultadoDeInventario<SolicitudDto>> SolicitudAsync(Guid id, CancellationToken ct = default) =>
        Enviar<SolicitudDto>(HttpMethod.Get, $"{RutaDeSolicitudes}/{id}", null, null, ct);

    /// <summary>El enrutamiento con la huella que compara la decisión (sin clave).</summary>
    public Task<ResultadoDeInventario<EnrutamientoDeSolicitudDto>> EnrutamientoAsync(Guid id, CancellationToken ct = default) =>
        Enviar<EnrutamientoDeSolicitudDto>(HttpMethod.Get, $"{RutaDeSolicitudes}/{id}/routing", null, null, ct);

    /// <summary>El borrador nuevo (sin número; el número lo da la radicación).</summary>
    public Task<ResultadoDeInventario<SolicitudDto>> CrearAsync(SolicitudRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<SolicitudDto>(HttpMethod.Post, RutaDeSolicitudes, request, clave, ct);

    /// <summary>Corrige el borrador o la devuelta con la versión leída; si cambió, 409 <c>Concurrency.StaleRowVersion</c>.</summary>
    public Task<ResultadoDeInventario<SolicitudDto>> ActualizarAsync(Guid id, SolicitudRequest request, byte[]? version, ClaveDeOperacion clave,
        CancellationToken ct = default)
    {
        var cuerpo = JsonSerializer.SerializeToNode(request, Web)!.AsObject();
        if (version is not null) cuerpo["rowVersion"] = Convert.ToBase64String(version);
        return Enviar<SolicitudDto>(HttpMethod.Put, $"{RutaDeSolicitudes}/{id}", cuerpo, clave, ct);
    }

    /// <summary>Asocia un documento ya subido (dueño <see cref="DuenoDeDocumentos"/>) a un requisito de la línea.</summary>
    public Task<ResultadoDeInventario<DocumentoDeSolicitudDto>> AdjuntarDocumentoAsync(Guid id, DocumentoDeSolicitudRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DocumentoDeSolicitudDto>(HttpMethod.Post, $"{RutaDeSolicitudes}/{id}/documents", request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> QuitarDocumentoAsync(Guid id, Guid documento, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Delete, $"{RutaDeSolicitudes}/{id}/documents/{documento}", null, clave, ct);

    /// <summary>Radica: valida documentos y autorizaciones, corre las validaciones, el puntaje y el enrutamiento, y numera.</summary>
    public Task<ResultadoDeInventario<SolicitudDto>> RadicarAsync(Guid id, byte[]? version, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<SolicitudDto>(HttpMethod.Post, $"{RutaDeSolicitudes}/{id}/submit", new VersionDeSolicitudRequest(version), clave, ct);

    public Task<ResultadoDeInventario<SolicitudDto>> AnalizarAsync(Guid id, AnalisisDeSolicitudRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<SolicitudDto>(HttpMethod.Post, $"{RutaDeSolicitudes}/{id}/analysis", request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ValidacionDeSolicitudDto>>> RepetirValidacionesAsync(Guid id, RepetirValidacionesRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ValidacionDeSolicitudDto>>(HttpMethod.Post, $"{RutaDeSolicitudes}/{id}/checks/rerun", request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> CancelarAsync(Guid id, CancelarSolicitudRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{RutaDeSolicitudes}/{id}/cancel", request, clave, ct);

    // -------------------------------------------------------------------------------------------- decisiones --

    /// <summary>«Mis pendientes»: lo que se puede decidir ahora y lo que está en agenda de los órganos de quien pregunta.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<DecisionPendienteDto>>> PendientesAsync(string? sujeto = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<DecisionPendienteDto>>(HttpMethod.Get, ConQuery(RutaDePendientes, Query(
            ("subject", sujeto), ("page", Texto(pagina)))), null, null, ct);

    /// <summary>Decide una solicitud con la huella que se vio. Un nivel colegiado responde <c>Lending.Attribution.LevelRequired</c>.</summary>
    public Task<ResultadoDeInventario<DecisionRegistradaDto>> DecidirSolicitudAsync(Guid id, DecisionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DecisionRegistradaDto>(HttpMethod.Post, $"{RutaDeSolicitudes}/{id}/decisions", request, clave, ct);

    /// <summary>Decide un cupo que espera decisión; aprobarlo otorga una versión firme (<c>resultingSubjectPublicId</c>).</summary>
    public Task<ResultadoDeInventario<DecisionRegistradaDto>> DecidirCupoAsync(Guid id, DecisionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DecisionRegistradaDto>(HttpMethod.Post, $"{CuposClient.RutaDeCupos}/{id}/decisions", request, clave, ct);

    // --------------------------------------------------------------------------------------------- centrales --

    /// <summary>Consulta a una central con la autorización del titular; la respuesta trae el estado, nunca el contenido.</summary>
    public Task<ResultadoDeInventario<ConsultaACentralRegistradaDto>> ConsultarCentralAsync(ConsultaACentralRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ConsultaACentralRegistradaDto>(HttpMethod.Post, RutaDeCentrales, request, clave, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ConsultaACentralDto>>> ConsultasACentralesAsync(Guid? persona = null, Guid? solicitud = null,
        int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ConsultaACentralDto>>(HttpMethod.Get, ConQuery(RutaDeCentrales, Query(
            ("personPublicId", Texto(persona)), ("applicationPublicId", Texto(solicitud)), ("page", Texto(pagina)))), null, null, ct);

    /// <summary>El detalle con el informe (lectura auditada).</summary>
    public Task<ResultadoDeInventario<ConsultaACentralDto>> ConsultaACentralAsync(Guid id, CancellationToken ct = default) =>
        Enviar<ConsultaACentralDto>(HttpMethod.Get, $"{RutaDeCentrales}/{id}", null, null, ct);

    /// <summary>Un enlace firmado de 60 s al informe (auditado; repetirlo emite otro). Va por POST y sin clave.</summary>
    public Task<ResultadoDeInventario<EnlaceDeCarteraDto>> EnlaceDelInformeAsync(Guid id, CancellationToken ct = default) =>
        Enviar<EnlaceDeCarteraDto>(HttpMethod.Post, $"{RutaDeCentrales}/{id}/download-link", null, null, ct);

    // ---------------------------------------------------------------------------------------- listas restrictivas --

    /// <summary>Los resultados de listas (sólo el oficial): <c>status</c> Blocked, Released o NoMatch.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResultadoDeListasDto>>> ResultadosDeListasAsync(string? estado = null, Guid? persona = null,
        int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ResultadoDeListasDto>>(HttpMethod.Get, ConQuery(RutaDeListas, Query(
            ("status", estado), ("personPublicId", Texto(persona)), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<ResultadoDeListasDto>> ResultadoDeListasAsync(Guid id, CancellationToken ct = default) =>
        Enviar<ResultadoDeListasDto>(HttpMethod.Get, $"{RutaDeListas}/{id}", null, null, ct);

    /// <summary>Libera una coincidencia con motivo; quien radicó o pidió el cupo no puede.</summary>
    public Task<ResultadoDeInventario<ResultadoDeListasDto>> LiberarCoincidenciaAsync(Guid id, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResultadoDeListasDto>(HttpMethod.Post, $"{RutaDeListas}/{id}/release", new MotivoDeCarteraRequest(motivo), clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
