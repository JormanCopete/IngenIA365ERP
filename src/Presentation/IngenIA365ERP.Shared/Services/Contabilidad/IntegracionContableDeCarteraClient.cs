using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// El cliente del lado contable de la integración con Cartera (feature 013, US7, T0663; contracts/api.md §23; contracts/contabilidad.md
/// §3.7, §10, §15, §21; enmienda E-01 de la 009). Molde: <see cref="IntegracionContableClient"/> de Inventario: la matriz con su catálogo,
/// sus versiones y «¿a qué cuenta va?», el mapeo de tipos de comprobante, la completitud, los lotes de hechos de Cartera con su vista previa
/// y su orden, los recibos y los indicadores regulatorios. (nuevo)
///
/// <list type="bullet">
/// <item>La matriz, el mapeo y los indicadores siguen la mecánica de la 009: <b>sin</b> clave de operación. Sólo ordenar un lote lleva
/// <c>Idempotency-Key</c> (<see cref="ClaveDeOperacion"/>, que la pantalla crea con la vista previa y conserva en el reintento). La
/// plantilla de la matriz se descarga, se revisa y se aplica con el componente único <c>ImportarPlantilla</c> sobre
/// <see cref="RutaDeReglas"/>: esa importación lleva su propia clave.</item>
/// <item>Los filtros de enum viajan por <b>nombre</b> y las respuestas los traen como número; las etiquetas las ponen
/// <see cref="TextosDeContabilidadDeCartera"/> y <see cref="TextosDeInventario"/>.</item>
/// <item>La cabecera <c>Authorization</c> la pone el handler de la sesión (<c>ElTokenDeSesionLoPoneElHandler</c>).</item>
/// <item>Las respuestas son <see cref="ResultadoDeInventario{T}"/>, que conserva el <c>data</c> del error
/// (<c>Accounting.LendingBatch.AlreadyRunning</c> trae el lote en curso).</item>
/// </list>
/// </summary>
public sealed class IntegracionContableDeCarteraClient(HttpClient http, CentralAuthClient auth)
{
    public const string Base = "/api/accounting/lending";
    public const string RutaDeReglas = Base + "/rules";
    public const string RutaDeIndicadores = "/api/accounting/regulatory-indicators";

    // -------------------------------------------------------------------------------------------- matriz --

    public Task<ResultadoDeInventario<CatalogoDeReglasDeCarteraDto>> CatalogoAsync(CancellationToken ct = default) =>
        Enviar<CatalogoDeReglasDeCarteraDto>(HttpMethod.Get, $"{RutaDeReglas}/catalog", null, null, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ReglaDeCarteraDto>>> ReglasAsync(FiltroDeReglasDeCartera f, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ReglaDeCarteraDto>>(HttpMethod.Get, ConQuery(RutaDeReglas, Query(
            ("operation", f.Operacion), ("role", f.Rol), ("asOf", Texto(f.ALaFecha)), ("creditLineCode", f.Linea), ("modalityCode", f.Modalidad),
            ("subsegmentCode", f.Subsegmento), ("riskCategory", f.Categoria), ("conceptCode", f.Concepto), ("collectorClass", f.ClaseDeRecaudador),
            ("account", f.Cuenta), ("onlyCurrent", f.SoloVigentes ? "true" : null), ("page", Texto(f.Pagina)), ("pageSize", Texto(f.TamanoDePagina)))),
            null, null, ct);

    public Task<ResultadoDeInventario<ReglaDeCarteraDto>> ReglaAsync(Guid id, CancellationToken ct = default) =>
        Enviar<ReglaDeCarteraDto>(HttpMethod.Get, $"{RutaDeReglas}/{id}", null, null, ct);

    /// <summary>Las versiones de la misma clave, de la más nueva a la más vieja.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<ReglaDeCarteraDto>>> VersionesDeReglaAsync(Guid id, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ReglaDeCarteraDto>>(HttpMethod.Get, $"{RutaDeReglas}/{id}/versions", null, null, ct);

    public Task<ResultadoDeInventario<ReglaGuardadaDeCarteraDto>> CrearReglaAsync(CrearReglaDeCarteraRequest request, CancellationToken ct = default) =>
        Enviar<ReglaGuardadaDeCarteraDto>(HttpMethod.Post, RutaDeReglas, request, null, ct);

    /// <summary>Una versión nueva de la misma clave: cierra la vigente la víspera de <c>ValidFrom</c>.</summary>
    public Task<ResultadoDeInventario<ReglaGuardadaDeCarteraDto>> NuevaVersionDeReglaAsync(Guid id, VersionDeReglaDeCarteraRequest request,
        CancellationToken ct = default) =>
        Enviar<ReglaGuardadaDeCarteraDto>(HttpMethod.Post, $"{RutaDeReglas}/{id}/versions", request, null, ct);

    /// <summary>Cierra la vigencia con motivo; nunca borra.</summary>
    public Task<ResultadoDeInventario<EmptyResponse>> DesactivarReglaAsync(Guid id, DesactivarReglaDeCarteraRequest request, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{RutaDeReglas}/{id}/deactivate", request, null, ct);

    /// <summary>«¿A qué cuenta va esta combinación?»: no es un error que no haya regla, la respuesta lo dice.</summary>
    public Task<ResultadoDeInventario<ResolucionDeReglaDeCarteraDto>> ResolverAsync(ResolverReglaDeCarteraRequest request, CancellationToken ct = default) =>
        Enviar<ResolucionDeReglaDeCarteraDto>(HttpMethod.Post, $"{RutaDeReglas}/resolve", request, null, ct);

    // ---------------------------------------------------------------------------------- tipos de comprobante --

    public Task<ResultadoDeInventario<IReadOnlyList<MapeoDeComprobanteDeCarteraDto>>> MapeosAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<MapeoDeComprobanteDeCarteraDto>>(HttpMethod.Get, $"{Base}/voucher-mappings", null, null, ct);

    /// <summary>Agrega o reemplaza la fila de (hecho, clase de recaudador); el tipo de comprobante tiene que ser de Cartera.</summary>
    public Task<ResultadoDeInventario<MapeoFijadoDeCarteraDto>> FijarMapeoAsync(MapeoDeComprobanteDeCarteraRequest request, CancellationToken ct = default) =>
        Enviar<MapeoFijadoDeCarteraDto>(HttpMethod.Put, $"{Base}/voucher-mappings", request, null, ct);

    // ------------------------------------------------------------------------------------------ completitud --

    public Task<ResultadoDeInventario<CompletitudDeCarteraDto>> CompletitudAsync(DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<CompletitudDeCarteraDto>(HttpMethod.Get, ConQuery($"{Base}/completeness", Query(("date", Texto(fecha)))), null, null, ct);

    // ------------------------------------------------------------------------------------------------ lotes --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<LoteDeIntegracionDto>>> LotesAsync(FiltroDeLotesDeCartera f, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<LoteDeIntegracionDto>>(HttpMethod.Get, ConQuery($"{Base}/batches", Query(
            ("from", Texto(f.Desde)), ("to", Texto(f.Hasta)),
            ("status", TextosDeInventario.Nombre(TextosDeInventario.NombresDeEstadoDeLote, f.Estado)),
            ("trigger", TextosDeInventario.Nombre(TextosDeInventario.NombresDeDisparador, f.Disparador)),
            ("page", Texto(f.Pagina)), ("pageSize", Texto(f.TamanoDePagina)))), null, null, ct);

    /// <summary>El lote con sus hechos, sus comprobantes, sus rechazos y los débitos y créditos de lo que contabilizó.</summary>
    public Task<ResultadoDeInventario<LoteDeCarteraConTotalesDto>> LoteAsync(Guid id, CancellationToken ct = default) =>
        Enviar<LoteDeCarteraConTotalesDto>(HttpMethod.Get, $"{Base}/batches/{id}", null, null, ct);

    /// <summary>Lo que haría un lote manual del alcance, sin numerar ni guardar; trae el corte que se manda al ordenarlo.</summary>
    public Task<ResultadoDeInventario<VistaPreviaDeLoteDeCarteraDto>> VistaPreviaDeLoteAsync(VistaPreviaDeLoteDeCarteraRequest request,
        CancellationToken ct = default) =>
        Enviar<VistaPreviaDeLoteDeCarteraDto>(HttpMethod.Post, $"{Base}/batches/preview", request, null, ct);

    /// <summary>Ordena el lote manual (202): corre en segundo plano con la persona como actor. Con la clave de la operación.</summary>
    public Task<ResultadoDeInventario<LoteOrdenadoDeIntegracionDto>> OrdenarLoteAsync(OrdenDeLoteDeCarteraRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<LoteOrdenadoDeIntegracionDto>(HttpMethod.Post, $"{Base}/batches", request, clave, ct);

    // ---------------------------------------------------------------------------------------------- recibos --

    /// <summary>Los recibos de un comprobante, de un origen, de un hecho, de un lote o de un rango de fechas contables.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ReciboDeCarteraDto>>> RecibosAsync(Guid? comprobante = null, Guid? origen = null,
        Guid? mensaje = null, Guid? lote = null, DateOnly? desde = null, DateOnly? hasta = null, int pagina = 1, int tamano = 50,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ReciboDeCarteraDto>>(HttpMethod.Get, ConQuery($"{Base}/postings", Query(
            ("document", Texto(comprobante)), ("source", Texto(origen)), ("message", Texto(mensaje)), ("batch", Texto(lote)),
            ("from", Texto(desde)), ("to", Texto(hasta)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    // ------------------------------------------------------------------------------------------ indicadores --

    /// <summary>Los indicadores con su vigencia a <paramref name="aLaFecha"/> (hoy si es nula) y los meses de vigencia con que se juzgó.</summary>
    public Task<ResultadoDeInventario<IndicadoresConVigenciaDto>> IndicadoresAsync(string? tipo = null, DateOnly? aLaFecha = null,
        CancellationToken ct = default) =>
        Enviar<IndicadoresConVigenciaDto>(HttpMethod.Get, ConQuery(RutaDeIndicadores, Query(("code", tipo), ("asOf", Texto(aLaFecha)))), null, null, ct);

    /// <summary>Registra un indicador (hecho de sólo inserción); corregir es registrar otro del mismo corte que nombre al anterior.</summary>
    public Task<ResultadoDeInventario<IndicadorRegistradoDto>> RegistrarIndicadorAsync(RegistrarIndicadorRegulatorioRequest request,
        CancellationToken ct = default) =>
        Enviar<IndicadorRegistradoDto>(HttpMethod.Post, RutaDeIndicadores, request, null, ct);

    // ------------------------------------------------------------------------------------------------ envío --

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
