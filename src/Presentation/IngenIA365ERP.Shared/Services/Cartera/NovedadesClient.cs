using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de K3 (feature 013, US11, T0891; contracts/api.md §11.1, §11.2, §11.4): novedades (lista, detalle, pedir, simular —por POST y sin
/// clave: no escribe—, radicar, consentimientos, decisiones, aplicar, anular), condonaciones (lista, detalle, pedir, decidir, aplicar, anular), eventos
/// del deudor, reclamaciones al seguro y procesos de insolvencia. Sin <c>Authorization</c> a mano: la pone el handler; toda escritura con su
/// <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed class NovedadesClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeNovedades = GobiernoDeCarteraClient.Base + "/novelties";
    public const string RutaDeCondonaciones = GobiernoDeCarteraClient.Base + "/waivers";
    public const string RutaDeEventos = GobiernoDeCarteraClient.Base + "/debtor-events";
    public const string RutaDeReclamaciones = GobiernoDeCarteraClient.Base + "/insurance-claims";
    public const string RutaDeInsolvencias = GobiernoDeCarteraClient.Base + "/insolvency-proceedings";

    /// <summary>El dueño de adjunto de la aceptación firmada de un consentimiento (su dueño es la novedad).</summary>
    public const string DuenoDeConsentimientos = "LendingNoveltyConsent";

    /// <summary>El dueño de adjunto del soporte de un evento del deudor, de su insolvencia o de su reclamación (su dueño es la persona).</summary>
    public const string DuenoDeSoportesDelDeudor = "LendingDebtorEventSupport";

    // ----------------------------------------------------------------------------------------------- novedades --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<NovedadResumenDto>>> NovedadesAsync(string? estado = null, string? causa = null,
        Guid? credito = null, int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<NovedadResumenDto>>(HttpMethod.Get, ConQuery(RutaDeNovedades, Query(("status", estado), ("cause", causa),
            ("loanPublicId", Texto(credito)), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<NovedadDeCreditoDto>> NovedadAsync(Guid novedad, CancellationToken ct = default) =>
        Enviar<NovedadDeCreditoDto>(HttpMethod.Get, $"{RutaDeNovedades}/{novedad}", null, null, ct);

    /// <summary>Pide la novedad (queda en borrador con la causa calculada).</summary>
    public Task<ResultadoDeInventario<NovedadDeCreditoDto>> PedirAsync(NovedadRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<NovedadDeCreditoDto>(HttpMethod.Post, RutaDeNovedades, request, clave, ct);

    /// <summary>Lo que dejaría la novedad: no escribe, va por POST y sin clave.</summary>
    public Task<ResultadoDeInventario<SimulacionDeNovedadDto>> SimularAsync(Guid novedad, CancellationToken ct = default) =>
        Enviar<SimulacionDeNovedadDto>(HttpMethod.Post, $"{RutaDeNovedades}/{novedad}/simulate", null, null, ct);

    /// <summary>Radica la novedad: la vuelve a simular, sella la huella y la enruta por la matriz de atribuciones.</summary>
    public Task<ResultadoDeInventario<NovedadDeCreditoDto>> RadicarAsync(Guid novedad, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<NovedadDeCreditoDto>(HttpMethod.Post, $"{RutaDeNovedades}/{novedad}/submit", null, clave, ct);

    public Task<ResultadoDeInventario<ConsentimientoDeNovedadDto>> ConsentimientoAsync(Guid novedad, ConsentimientoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ConsentimientoDeNovedadDto>(HttpMethod.Post, $"{RutaDeNovedades}/{novedad}/consents", request, clave, ct);

    public Task<ResultadoDeInventario<DecisionRegistradaDto>> DecidirNovedadAsync(Guid novedad, DecisionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DecisionRegistradaDto>(HttpMethod.Post, $"{RutaDeNovedades}/{novedad}/decisions", request, clave, ct);

    public Task<ResultadoDeInventario<NovedadAplicadaDto>> AplicarNovedadAsync(Guid novedad, AplicarNovedadRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<NovedadAplicadaDto>(HttpMethod.Post, $"{RutaDeNovedades}/{novedad}/apply", request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> AnularNovedadAsync(Guid novedad, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{RutaDeNovedades}/{novedad}/cancel", new MotivoDeCarteraRequest(motivo), clave, ct);

    // ------------------------------------------------------------------------------------------- condonaciones --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<CondonacionDto>>> CondonacionesAsync(string? estado = null, Guid? credito = null,
        int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<CondonacionDto>>(HttpMethod.Get, ConQuery(RutaDeCondonaciones, Query(("status", estado),
            ("loanPublicId", Texto(credito)), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<CondonacionDto>> CondonacionAsync(Guid condonacion, CancellationToken ct = default) =>
        Enviar<CondonacionDto>(HttpMethod.Get, $"{RutaDeCondonaciones}/{condonacion}", null, null, ct);

    /// <summary>Pide la condonación y la radica con su enrutamiento.</summary>
    public Task<ResultadoDeInventario<CondonacionDto>> PedirCondonacionAsync(CondonacionRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CondonacionDto>(HttpMethod.Post, RutaDeCondonaciones, request, clave, ct);

    public Task<ResultadoDeInventario<DecisionRegistradaDto>> DecidirCondonacionAsync(Guid condonacion, DecisionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DecisionRegistradaDto>(HttpMethod.Post, $"{RutaDeCondonaciones}/{condonacion}/decisions", request, clave, ct);

    public Task<ResultadoDeInventario<CondonacionAplicadaDto>> AplicarCondonacionAsync(Guid condonacion, ClaveDeOperacion clave, bool reconocerAvisos = false,
        CancellationToken ct = default) =>
        Enviar<CondonacionAplicadaDto>(HttpMethod.Post, $"{RutaDeCondonaciones}/{condonacion}/apply", new AplicarCondonacionRequest(reconocerAvisos), clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> AnularCondonacionAsync(Guid condonacion, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{RutaDeCondonaciones}/{condonacion}/cancel", new MotivoDeCarteraRequest(motivo), clave, ct);

    // ------------------------------------------------------------------ eventos del deudor, reclamaciones, insolvencia --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<EventoDelDeudorDto>>> EventosAsync(Guid? persona = null, string? clase = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<EventoDelDeudorDto>>(HttpMethod.Get, ConQuery(RutaDeEventos, Query(("personPublicId", Texto(persona)),
            ("kind", clase), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<EventoDelDeudorDto>> RegistrarEventoAsync(EventoDelDeudorRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<EventoDelDeudorDto>(HttpMethod.Post, RutaDeEventos, request, clave, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ReclamacionAlSeguroDto>>> ReclamacionesAsync(Guid? credito = null, string? estado = null,
        int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ReclamacionAlSeguroDto>>(HttpMethod.Get, ConQuery(RutaDeReclamaciones, Query(("loanPublicId", Texto(credito)),
            ("status", estado), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<ReclamacionAlSeguroDto>> CambiarReclamacionAsync(Guid reclamacion, EstadoDeReclamacionRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ReclamacionAlSeguroDto>(HttpMethod.Post, $"{RutaDeReclamaciones}/{reclamacion}/status", request, clave, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ProcesoDeInsolvenciaDto>>> InsolvenciasAsync(Guid? persona = null, string? estado = null,
        int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ProcesoDeInsolvenciaDto>>(HttpMethod.Get, ConQuery(RutaDeInsolvencias, Query(("personPublicId", Texto(persona)),
            ("status", estado), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<ProcesoDeInsolvenciaDto>> AbrirInsolvenciaAsync(InsolvenciaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ProcesoDeInsolvenciaDto>(HttpMethod.Post, RutaDeInsolvencias, request, clave, ct);

    public Task<ResultadoDeInventario<ProcesoDeInsolvenciaDto>> RegistrarFracasoAsync(Guid proceso, FracasoDeInsolvenciaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ProcesoDeInsolvenciaDto>(HttpMethod.Post, $"{RutaDeInsolvencias}/{proceso}/failure", request, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
