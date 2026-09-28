using IngenIA365ERP.Shared.Services.Adjuntos;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.FacturacionElectronica;

/// <summary>
/// Cliente tipado de facturación electrónica (feature 012, I4, T752; contracts/api.md §24), en el molde de <c>VentasClient</c>: la
/// configuración de emisión y su credencial, las resoluciones de numeración, la preparación (<c>readiness</c>), la bandeja de documentos
/// electrónicos con sus reintentos, consultas y casos a/b/c, las contingencias y el enlace firmado de los artefactos. El canal —simulado o
/// el del proveedor— no se ve desde aquí: la pantalla habla sólo con la API. (nuevo)
/// <list type="bullet">
/// <item>La cabecera <c>Authorization</c> la pone el handler de la sesión (<c>ElTokenDeSesionLoPoneElHandler</c>).</item>
/// <item>Todo POST y PUT lleva <c>Idempotency-Key</c>: las escrituras, la de la operación de pantalla (<see cref="ClaveDeOperacion"/>); el enlace
/// de descarga, que no escribe, una propia de cada pedido.</item>
/// <item>Los enums viajan por nombre (<see cref="TextosDeFacturacionElectronica"/>) y vuelven como número.</item>
/// </list>
/// </summary>
public sealed class FacturacionElectronicaClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/electronic-invoicing";

    /// <summary>Las rutas de §24.</summary>
    public static class Rutas
    {
        public const string Configuracion = Base + "/settings";
        public const string Preparacion = Base + "/readiness";
        public const string Resoluciones = Base + "/resolutions";
        public const string Documentos = Base + "/documents";
        public const string Contingencias = Base + "/contingencies";
    }

    /// <summary>El dueño de los adjuntos de un evento de contingencia (constancias y evidencias; <c>AdjuntosDeModulo.EventoDeContingencia</c>).</summary>
    public const string DuenoDeEvidencias = "DianContingencyEvent";

    // --------------------------------------------------------------------------------- configuración (§24.1) --

    public Task<ResultadoDeInventario<ConfiguracionDeEmisionDto>> ConfiguracionAsync(CancellationToken ct = default) =>
        Enviar<ConfiguracionDeEmisionDto>(HttpMethod.Get, Rutas.Configuracion, null, null, ct);

    /// <summary>Una vigencia nueva de la configuración: cierra la anterior la víspera.</summary>
    public Task<ResultadoDeInventario<VigenciaDeEmisionDto>> ConfigurarAsync(ConfiguracionDeEmisionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<VigenciaDeEmisionDto>(HttpMethod.Post, Rutas.Configuracion, request, clave, ct);

    /// <summary>Prueba la credencial del canal (nulo = el de la vigencia actual); sólo sella la verificación.</summary>
    public Task<ResultadoDeInventario<VerificacionDeCredencialDto>> VerificarCredencialAsync(string? canal, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<VerificacionDeCredencialDto>(HttpMethod.Post, $"{Rutas.Configuracion}/verify-credential", new { channelCode = canal }, clave, ct);

    // ---------------------------------------------------------------------------------- preparación (§24.3) --

    /// <summary>El veredicto de la preparación y lo que falta con quién lo corrige. Es una consulta.</summary>
    public Task<ResultadoDeInventario<PreparacionDianDto>> PreparacionAsync(DateOnly? asOf = null, Guid? documentType = null, Guid? cashRegister = null,
        CancellationToken ct = default) =>
        Enviar<PreparacionDianDto>(HttpMethod.Get, Q(Rutas.Preparacion, ("asOf", Texto(asOf)), ("documentType", Texto(documentType)),
            ("cashRegister", Texto(cashRegister))), null, null, ct);

    // --------------------------------------------------------------------------------- resoluciones (§24.2) --

    public Task<ResultadoDeInventario<IReadOnlyList<ResolucionDianDto>>> ResolucionesAsync(string? kind = null, string? environment = null,
        string? status = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ResolucionDianDto>>(HttpMethod.Get, Q(Rutas.Resoluciones, ("kind", kind), ("environment", environment), ("status", status)),
            null, null, ct);

    public Task<ResultadoDeInventario<DetalleDeResolucionDto>> ResolucionAsync(Guid id, CancellationToken ct = default) =>
        Enviar<DetalleDeResolucionDto>(HttpMethod.Get, $"{Rutas.Resoluciones}/{id}", null, null, ct);

    public Task<ResultadoDeInventario<ResolucionDianDto>> RegistrarResolucionAsync(ResolucionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResolucionDianDto>(HttpMethod.Post, Rutas.Resoluciones, request, clave, ct);

    /// <summary>Corrige una resolución sin números emitidos; con números, sólo retirarla (<c>validTo</c> hacia atrás).</summary>
    public Task<ResultadoDeInventario<ResolucionDianDto>> CorregirResolucionAsync(Guid id, ResolucionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResolucionDianDto>(HttpMethod.Put, $"{Rutas.Resoluciones}/{id}", request, clave, ct);

    /// <summary>Asocia el prefijo a un canal desde una fecha; la clave técnica vuelve enmascarada.</summary>
    public Task<ResultadoDeInventario<ResolucionDianDto>> AsociarACanalAsync(Guid id, AsociacionACanalRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResolucionDianDto>(HttpMethod.Post, $"{Rutas.Resoluciones}/{id}/channels", request, clave, ct);

    // ----------------------------------------------------------------------------------- documentos (§24.4) --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<DocumentoElectronicoDto>>> DocumentosAsync(FiltroDeDocumentosElectronicos f,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<DocumentoElectronicoDto>>(HttpMethod.Get, Q(Rutas.Documentos,
            ("status", f.Status), ("kind", f.Kind), ("from", Texto(f.From)), ("to", Texto(f.To)), ("prefix", f.Prefix), ("number", f.Number),
            ("contingency", Texto(f.Contingency)), ("overdue", Texto(f.Overdue)), ("page", Texto(f.Page)), ("pageSize", Texto(f.PageSize))),
            null, null, ct);

    public Task<ResultadoDeInventario<DetalleDeDocumentoElectronicoDto>> DocumentoAsync(Guid id, CancellationToken ct = default) =>
        Enviar<DetalleDeDocumentoElectronicoDto>(HttpMethod.Get, $"{Rutas.Documentos}/{id}", null, null, ct);

    /// <summary>«Reintentar ahora»: un intento ya, con el arrendamiento de la fila.</summary>
    public Task<ResultadoDeInventario<ResultadoDeTransmisionDto>> ReintentarAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ResultadoDeTransmisionDto>(HttpMethod.Post, $"{Rutas.Documentos}/{id}/retry", null, clave, ct);

    /// <summary>«Consultar a la DIAN»: la consulta de estado; también confirma un rechazo.</summary>
    public Task<ResultadoDeInventario<ResultadoDeTransmisionDto>> ConsultarEstadoAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ResultadoDeTransmisionDto>(HttpMethod.Post, $"{Rutas.Documentos}/{id}/query-status", null, clave, ct);

    public Task<ResultadoDeInventario<ResultadoDeCorreccionDto>> TransmitirPorElCanalVigenteAsync(Guid id, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResultadoDeCorreccionDto>(HttpMethod.Post, $"{Rutas.Documentos}/{id}/transmit-by-current-channel", new MotivoDeFacturacionRequest(motivo), clave, ct);

    // ---------------------------------------------------------------------------------- casos a, b y c (§24.5) --

    /// <summary>Caso a: corregir sin cambio económico; con la huella distinta, 422 <c>EconomicFootprintChanged</c> con <c>data.fields[]</c>.</summary>
    public Task<ResultadoDeInventario<ResultadoDeCorreccionDto>> CorregirCasoAAsync(Guid id, CorreccionCasoARequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResultadoDeCorreccionDto>(HttpMethod.Post, $"{Rutas.Documentos}/{id}/correct", request, clave, ct);

    /// <summary>Preparación del caso b: el borrador de reemplazo de la misma clase, sin número.</summary>
    public Task<ResultadoDeInventario<BorradorDeReemplazoDto>> PrepararReemplazoAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<BorradorDeReemplazoDto>(HttpMethod.Post, $"{Rutas.Documentos}/{id}/replacement-draft", null, clave, ct);

    /// <summary>Caso b: anula sin efecto fiscal y confirma el reemplazo con el mismo número, en una transacción.</summary>
    public Task<ResultadoDeInventario<ResultadoDeCorreccionDto>> ReemplazarAsync(Guid id, ReemplazoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResultadoDeCorreccionDto>(HttpMethod.Post, $"{Rutas.Documentos}/{id}/replace", request, clave, ct);

    /// <summary>Caso c: anula sin reemplazo, con motivo.</summary>
    public Task<ResultadoDeInventario<ResultadoDeCorreccionDto>> CancelarAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ResultadoDeCorreccionDto>(HttpMethod.Post, $"{Rutas.Documentos}/{id}/cancel", new MotivoDeFacturacionRequest(motivo), clave, ct);

    // ------------------------------------------------------------------------------------ artefactos (§24.4) --

    /// <summary>
    /// El enlace firmado de 60 s a un artefacto (<c>Canonical</c>, <c>SignedXml</c>, <c>AttachedDocument</c>, <c>ApplicationResponse</c>,
    /// <c>GraphicRepresentation</c>), con la URL absoluta sobre la API (el almacén local de desarrollo firma rutas relativas).
    /// </summary>
    public async Task<ResultadoDeInventario<EnlaceDeDescargaDto>> EnlaceDeArtefactoAsync(Guid id, string artefacto, int? version = null,
        int? transmission = null, CancellationToken ct = default)
    {
        var r = await Enviar<EnlaceDeDescargaDto>(HttpMethod.Post, Q($"{Rutas.Documentos}/{id}/download-link",
            ("artifact", artefacto), ("version", Texto(version)), ("transmission", Texto(transmission))), null, new ClaveDeOperacion(), ct);
        return r.IsSuccess && r.Value is { Url: { } url } enlace ? r with { Value = enlace with { Url = EnlacesFirmados.Absoluta(http, url) } } : r;
    }

    // --------------------------------------------------------------------------------- contingencias (§24.6) --

    public Task<ResultadoDeInventario<IReadOnlyList<ContingenciaDianDto>>> ContingenciasAsync(bool? abiertas = null, string? tipo = null,
        DateOnly? desde = null, DateOnly? hasta = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ContingenciaDianDto>>(HttpMethod.Get, Q(Rutas.Contingencias, ("isOpen", Texto(abiertas)), ("type", tipo),
            ("from", Texto(desde)), ("to", Texto(hasta))), null, null, ct);

    public Task<ResultadoDeInventario<DetalleDeContingenciaDto>> ContingenciaAsync(Guid id, CancellationToken ct = default) =>
        Enviar<DetalleDeContingenciaDto>(HttpMethod.Get, $"{Rutas.Contingencias}/{id}", null, null, ct);

    /// <summary>Declara una contingencia 03 (la 04 la declara sólo el canal).</summary>
    public Task<ResultadoDeInventario<ContingenciaDianDto>> DeclararContingenciaAsync(DeclaracionDeContingenciaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ContingenciaDianDto>(HttpMethod.Post, Rutas.Contingencias, request, clave, ct);

    /// <summary>Cierra la contingencia con motivo: fija el plazo de transmisión.</summary>
    public Task<ResultadoDeInventario<ContingenciaDianDto>> CerrarContingenciaAsync(Guid id, CierreDeContingenciaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ContingenciaDianDto>(HttpMethod.Post, $"{Rutas.Contingencias}/{id}/close", request, clave, ct);

    // ---------------------------------------------------------------------------------------------- envío --

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);

    private static string Q(string ruta, params (string Nombre, string? Valor)[] pares) => ConQuery(ruta, Query(pares));
}
