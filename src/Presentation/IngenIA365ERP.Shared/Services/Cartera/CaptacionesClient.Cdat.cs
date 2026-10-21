using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Feature 014, US6 (T179; contracts/api.md §5 y §6 de la 014): las rutas del CDAT, de las solicitudes de tasa negociada y de las reglas de tasa negociada de
/// una línea. Sin <c>Authorization</c> a mano (la pone el handler); toda escritura con su <see cref="ClaveDeOperacion"/>; las simulaciones y las lecturas sin
/// clave. (nuevo)
/// </summary>
public sealed partial class CaptacionesClient
{
    public const string RutaDeCdat = Base + "/term-deposits";
    public const string RutaDeSolicitudesDeTasa = Base + "/negotiated-rate-requests";

    // ------------------------------------------------------------------------------------------------------------- CDAT --

    /// <summary>Los CDAT del alcance con sus filtros, el más reciente primero.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<CdatEnListaDto>>> CdatsAsync(int? estado = null, Guid? linea = null, DateOnly? venceDesde = null,
        DateOnly? venceHasta = null, int? plazoDeEmision = null, Guid? titular = null, bool? negociados = null, int pagina = 1, int tamano = 20,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<CdatEnListaDto>>(HttpMethod.Get, ConQuery(RutaDeCdat, Query(("status", Texto(estado)), ("line", Texto(linea)),
            ("maturityFrom", Texto(venceDesde)), ("maturityTo", Texto(venceHasta)), ("issueTermBand", Texto(plazoDeEmision)), ("personPublicId", Texto(titular)),
            ("negotiated", negociados is { } n ? Texto(n) : null), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    /// <summary>Lo que vence por día y plazo de emisión entre dos fechas, sin titulares.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<VencimientoDelDiaDto>>> CalendarioDeVencimientosAsync(DateOnly desde, DateOnly hasta,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<VencimientoDelDiaDto>>(HttpMethod.Get, ConQuery($"{RutaDeCdat}/maturity-calendar", Query(("from", Texto(desde)), ("to", Texto(hasta)))),
            null, null, ct);

    /// <summary>Un CDAT con sus condiciones vigentes, su historia, sus títulos, la propuesta si espera el fondeo y la solicitud de tasa si espera decisión.</summary>
    public Task<ResultadoDeInventario<CdatDto>> CdatAsync(Guid cdat, CancellationToken ct = default) =>
        Enviar<CdatDto>(HttpMethod.Get, $"{RutaDeCdat}/{cdat}", null, null, ct);

    /// <summary>El plan de intereses de una versión (la vigente si no se pide), con lo pagado.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<LineaDelPlanDeCdatDto>>> PlanDeCdatAsync(Guid cdat, int? version = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<LineaDelPlanDeCdatDto>>(HttpMethod.Get, ConQuery($"{RutaDeCdat}/{cdat}/schedule", Query(("version", Texto(version)))), null, null, ct);

    /// <summary>Simula un CDAT: tasa de la tabla, plan, retención estimada, vencimiento hábil, plazo de emisión, negociación y la huella que la constitución compara.</summary>
    public Task<ResultadoDeInventario<SimulacionDeCdatDto>> SimularCdatAsync(SimularCdatRequest request, CancellationToken ct = default) =>
        Enviar<SimulacionDeCdatDto>(HttpMethod.Post, $"{RutaDeCdat}/simulate", request, null, ct);

    /// <summary>Constituye el CDAT: 201 en apertura (espera el fondeo o la decisión de su tasa) o activo (fondeado por traslado).</summary>
    public Task<ResultadoDeInventario<CdatDto>> ConstituirCdatAsync(ConstituirCdatRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CdatDto>(HttpMethod.Post, RutaDeCdat, request, clave, ct);

    /// <summary>Fondea por traslado un CDAT que espera.</summary>
    public Task<ResultadoDeInventario<CdatDto>> FondearCdatAsync(Guid cdat, FondearCdatRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CdatDto>(HttpMethod.Post, $"{RutaDeCdat}/{cdat}/fund", request, clave, ct);

    /// <summary>Cambia la instrucción al vencimiento (hasta el fin de la gracia).</summary>
    public Task<ResultadoDeInventario<CdatDto>> CambiarInstruccionDeCdatAsync(Guid cdat, InstruccionDeCdatRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CdatDto>(HttpMethod.Put, $"{RutaDeCdat}/{cdat}/maturity-instruction", request, clave, ct);

    /// <summary>Renueva el CDAT con condiciones nuevas (entre el vencimiento y el fin de la gracia).</summary>
    public Task<ResultadoDeInventario<CdatDto>> RenovarCdatAsync(Guid cdat, RenovarCdatRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CdatDto>(HttpMethod.Post, $"{RutaDeCdat}/{cdat}/renew", request, clave, ct);

    /// <summary>Lo que se pagaría al redimir a una fecha, con la penalidad, la retención, el GMF y la huella.</summary>
    public Task<ResultadoDeInventario<VistaPreviaDeRedencionDto>> SimularRedencionAsync(Guid cdat, DateOnly fecha, CancellationToken ct = default) =>
        Enviar<VistaPreviaDeRedencionDto>(HttpMethod.Post, $"{RutaDeCdat}/{cdat}/redeem/simulate", new SimularRedencionRequest(fecha), null, ct);

    /// <summary>Redime el CDAT hoy: 201 aplicada o 202 en espera de un aprobador (atribución con monto).</summary>
    public Task<ResultadoDeInventario<RedencionDeCdatDto>> RedimirCdatAsync(Guid cdat, RedimirCdatRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<RedencionDeCdatDto>(HttpMethod.Post, $"{RutaDeCdat}/{cdat}/redeem", request, clave, ct);

    // ------------------------------------------------------------------------------------------------- tasa negociada --

    /// <summary>Las solicitudes de tasa negociada (quien no tiene el nivel sólo ve las suyas).</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<SolicitudDeTasaNegociadaDto>>> SolicitudesDeTasaAsync(int? estado = null, int pagina = 1, int tamano = 20,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<SolicitudDeTasaNegociadaDto>>(HttpMethod.Get, ConQuery(RutaDeSolicitudesDeTasa, Query(("status", Texto(estado)),
            ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    public Task<ResultadoDeInventario<SolicitudDeTasaNegociadaDto>> AprobarTasaAsync(Guid solicitud, DecidirTasaNegociadaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<SolicitudDeTasaNegociadaDto>(HttpMethod.Post, $"{RutaDeSolicitudesDeTasa}/{solicitud}/approve", request, clave, ct);

    public Task<ResultadoDeInventario<SolicitudDeTasaNegociadaDto>> NegarTasaAsync(Guid solicitud, DecidirTasaNegociadaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<SolicitudDeTasaNegociadaDto>(HttpMethod.Post, $"{RutaDeSolicitudesDeTasa}/{solicitud}/reject", request, clave, ct);

    public Task<ResultadoDeInventario<SolicitudDeTasaNegociadaDto>> RetirarTasaAsync(Guid solicitud, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<SolicitudDeTasaNegociadaDto>(HttpMethod.Post, $"{RutaDeSolicitudesDeTasa}/{solicitud}/withdraw", new DecidirTasaNegociadaRequest(motivo), clave, ct);

    /// <summary>Las reglas de tasa negociada vigentes de una línea a una fecha.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<ReglaDeTasaNegociadaDto>>> ReglasDeTasaAsync(Guid linea, DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ReglaDeTasaNegociadaDto>>(HttpMethod.Get, ConQuery($"{RutaDeLineas}/{linea}/negotiated-rate-rules", Query(("date", Texto(fecha)))),
            null, null, ct);

    /// <summary>Registra una regla (o una versión nueva con el mismo código).</summary>
    public Task<ResultadoDeInventario<ReglaDeTasaNegociadaDto>> RegistrarReglaDeTasaAsync(Guid linea, ReglaDeTasaNegociadaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ReglaDeTasaNegociadaDto>(HttpMethod.Post, $"{RutaDeLineas}/{linea}/negotiated-rate-rules", request, clave, ct);

    /// <summary>Termina una regla desde una fecha.</summary>
    public Task<ResultadoDeInventario<ReglaDeTasaNegociadaDto>> TerminarReglaDeTasaAsync(Guid linea, Guid regla, TerminarReglaDeTasaRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ReglaDeTasaNegociadaDto>(HttpMethod.Post, $"{RutaDeLineas}/{linea}/negotiated-rate-rules/{regla}/end", request, clave, ct);
}
