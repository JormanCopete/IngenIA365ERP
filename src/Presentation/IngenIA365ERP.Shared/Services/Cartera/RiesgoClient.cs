using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado del riesgo y los cierres de Cartera (feature 013, US9, T0713; contracts/api.md §12.1–§12.5): la política SARC (consultar,
/// registrar una versión y validarla con su punto de acta), la calificación informativa y la del crédito, la calificación por análisis (listar,
/// poner y levantar), los cierres mensuales (lista, preparación del mes, orden 202, encabezado, líneas, cambios, cuadre con Contabilidad y
/// reapertura 202), el modelo de deterioro por segmento y la completitud del historial de mora. Las escrituras llevan la
/// <see cref="ClaveDeOperacion"/> de la operación; las consultas no. Sin <c>Authorization</c> a mano. Molde: <see cref="ConciliacionContableClient"/>. (nuevo)
/// </summary>
public sealed class RiesgoClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = GobiernoDeCarteraClient.Base + "/risk";
    public const string RutaDeCierres = Ruta + "/monthly-closes";

    /// <summary>La plantilla <c>lending.arrears-history</c> (K6a): <c>/template.xlsx</c> e <c>/import?mode=</c>, para <c>ImportarPlantilla</c>.</summary>
    public const string RutaDeHistorial = Ruta + "/arrears-history";

    /// <summary>El dueño de los adjuntos del estudio de impacto y del aviso de una propuesta de cambio del modelo (K6a).</summary>
    public const string DuenoDeSoportesDelCambioDelModelo = "LendingImpairmentModelElectionSupport";

    // -------------------------------------------------------------------------------------------- política SARC --

    public Task<ResultadoDeInventario<PoliticaSarcDto>> PoliticaAsync(DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<PoliticaSarcDto>(HttpMethod.Get, ConQuery($"{Ruta}/policy", Query(("date", Texto(fecha)))), null, null, ct);

    /// <summary>Registra una versión en «pendiente de validar»; una celda menos estricta que la norma responde <c>Lending.RiskPolicy.LessStrict</c>.</summary>
    public Task<ResultadoDeInventario<VersionDePoliticaSarcDto>> RegistrarVersionAsync(NuevaVersionDePoliticaSarcRequest pedido, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<VersionDePoliticaSarcDto>(HttpMethod.Post, $"{Ruta}/policy/versions", pedido, clave, ct);

    /// <summary>Valida la versión con el punto del acta del consejo; quien la registró no la valida.</summary>
    public Task<ResultadoDeInventario<VersionDePoliticaSarcDto>> ValidarVersionAsync(Guid version, Guid puntoDeActa, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<VersionDePoliticaSarcDto>(HttpMethod.Post, $"{Ruta}/policy/versions/{version}/validate", new ValidacionDePoliticaSarcRequest(puntoDeActa), clave, ct);

    // ------------------------------------------------------------------------------------------- calificaciones --

    /// <summary>La calificación informativa de los créditos del alcance a la fecha (no se guarda).</summary>
    public Task<ResultadoDeInventario<PaginaDeCarteraDto<CalificacionInformativaDto>>> InformativasAsync(DateOnly? fecha = null, string? categoria = null,
        string? causa = null, Guid? linea = null, Guid? oficina = null, int pagina = 1, int tamano = 20, CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<CalificacionInformativaDto>>(HttpMethod.Get, ConQuery($"{Ruta}/ratings", Query(
            ("date", Texto(fecha)), ("category", categoria), ("cause", causa), ("line", Texto(linea)), ("office", Texto(oficina)),
            ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    /// <summary>La calificación de un crédito: la informativa del día, la oficial del último cierre y el análisis vigente.</summary>
    public Task<ResultadoDeInventario<CalificacionDelCreditoDto>> DelCreditoAsync(Guid credito, DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<CalificacionDelCreditoDto>(HttpMethod.Get, ConQuery($"{GobiernoDeCarteraClient.Base}/loans/{credito}/rating", Query(("date", Texto(fecha)))),
            null, null, ct);

    public Task<ResultadoDeInventario<PaginaDeCarteraDto<CalificacionPorAnalisisDto>>> AnalisisAsync(bool? vigentes = null, int pagina = 1, int tamano = 20,
        CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<CalificacionPorAnalisisDto>>(HttpMethod.Get, ConQuery($"{Ruta}/analysis-ratings", Query(
            ("active", Texto(vigentes)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    /// <summary>Pone una calificación por análisis; igual o mejor que la de las reglas responde <c>Lending.AnalysisRating.NotWorse</c>.</summary>
    public Task<ResultadoDeInventario<CalificacionPorAnalisisDto>> CalificarPorAnalisisAsync(NuevaCalificacionPorAnalisisRequest pedido, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CalificacionPorAnalisisDto>(HttpMethod.Post, $"{Ruta}/analysis-ratings", pedido, clave, ct);

    public Task<ResultadoDeInventario<object>> LevantarAnalisisAsync(Guid analisis, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<object>(HttpMethod.Post, $"{Ruta}/analysis-ratings/{analisis}/lift", new MotivoDeCarteraRequest(motivo), clave, ct);

    // --------------------------------------------------------------------------------------------------- cierres --

    public Task<ResultadoDeInventario<IReadOnlyList<CierreEnListaDto>>> CierresAsync(int? anio = null, string? clase = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CierreEnListaDto>>(HttpMethod.Get, ConQuery(RutaDeCierres, Query(("year", Texto(anio)), ("kind", clase))), null, null, ct);

    /// <summary>Los requisitos del cierre del mes con su detalle; no escribe.</summary>
    public Task<ResultadoDeInventario<PreparacionDelCierreDto>> PreparacionAsync(int anio, int mes, CancellationToken ct = default) =>
        Enviar<PreparacionDelCierreDto>(HttpMethod.Get, $"{RutaDeCierres}/{anio}/{mes}/readiness", null, null, ct);

    /// <summary>Ordena el cierre del mes (202): encola la corrida <c>MonthlyClose</c>; el proceso la corre por tandas.</summary>
    public Task<ResultadoDeInventario<CorridaPedidaDto>> OrdenarCierreAsync(int anio, int mes, string? motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CorridaPedidaDto>(HttpMethod.Post, $"{RutaDeCierres}/{anio}/{mes}/close", new OrdenDeCierreRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<CierreDto>> CierreAsync(Guid cierre, CancellationToken ct = default) =>
        Enviar<CierreDto>(HttpMethod.Get, $"{RutaDeCierres}/{cierre}", null, null, ct);

    public Task<ResultadoDeInventario<PaginaDeCarteraDto<LineaDelCierreDto>>> LineasAsync(Guid cierre, string? categoria = null, int pagina = 1, int tamano = 50,
        CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<LineaDelCierreDto>>(HttpMethod.Get, ConQuery($"{RutaDeCierres}/{cierre}/loans", Query(
            ("category", categoria), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    public Task<ResultadoDeInventario<PaginaDeCarteraDto<CambioDeCalificacionDto>>> CambiosAsync(Guid cierre, int pagina = 1, int tamano = 50,
        CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<CambioDeCalificacionDto>>(HttpMethod.Get, ConQuery($"{RutaDeCierres}/{cierre}/changes", Query(
            ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    /// <summary>El cuadre con Contabilidad al último día del mes del cierre.</summary>
    public Task<ResultadoDeInventario<ConciliacionContableDto>> CuadreAsync(Guid cierre, CancellationToken ct = default) =>
        Enviar<ConciliacionContableDto>(HttpMethod.Get, $"{RutaDeCierres}/{cierre}/reconciliation", null, null, ct);

    /// <summary>Reabre el último cierre vigente (202): encola la corrida <c>CloseReopen</c>, antes del cierre contable del mes.</summary>
    public Task<ResultadoDeInventario<CorridaPedidaDto>> ReabrirAsync(Guid cierre, string motivo, Guid? puntoDeActa, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CorridaPedidaDto>(HttpMethod.Post, $"{RutaDeCierres}/{cierre}/reopen", new ReaperturaDeCierreRequest(motivo, puntoDeActa), clave, ct);

    // --------------------------------------------------------------------------------- modelo e historial de mora --

    public Task<ResultadoDeInventario<IReadOnlyList<ModeloPorSegmentoDto>>> ModeloPorSegmentoAsync(DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ModeloPorSegmentoDto>>(HttpMethod.Get, ConQuery($"{Ruta}/impairment-model", Query(("date", Texto(fecha)))), null, null, ct);

    /// <summary>Las adopciones y los desistimientos voluntarios, el más reciente primero (K6a).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<EleccionDelModeloDto>>> EleccionesAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<EleccionDelModeloDto>>(HttpMethod.Get, $"{Ruta}/impairment-model/elections", null, null, ct);

    /// <summary>Registra la adopción o el desistimiento voluntario con el punto del acta, el estudio y el aviso (K6a); los errores llevan sus datos
    /// (<c>earliestValidFrom</c>, <c>loans[]</c>, <c>segments</c>).</summary>
    public Task<ResultadoDeInventario<EleccionDelModeloDto>> RegistrarEleccionAsync(NuevaEleccionDelModeloRequest pedido, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<EleccionDelModeloDto>(HttpMethod.Post, $"{Ruta}/impairment-model/elections", pedido, clave, ct);

    public Task<ResultadoDeInventario<CompletitudDelHistorialDto>> CompletitudDelHistorialAsync(DateOnly? fecha = null, string? segmento = null,
        CancellationToken ct = default) =>
        Enviar<CompletitudDelHistorialDto>(HttpMethod.Get, ConQuery($"{Ruta}/arrears-history/completeness", Query(
            ("date", Texto(fecha)), ("segment", segmento))), null, null, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
