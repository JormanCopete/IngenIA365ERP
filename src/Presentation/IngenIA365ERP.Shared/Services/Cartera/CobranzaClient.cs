using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de la cobranza de K4 (feature 013, US12, T0957; contracts/api.md §14.3–§14.9): etapas y sus versiones, reglas de reparto, asignaciones,
/// agenda, gestiones y promesas, acuerdos de pago, cobro jurídico, campañas, gastos de cobranza, avisos previos con su constancia y reclamos sobre el
/// reporte, más la ventana de contacto de la Ley 2300 (que se muestra antes de registrar una gestión). Sin <c>Authorization</c> a mano: la pone el
/// handler; toda escritura lleva su <see cref="ClaveDeOperacion"/>. Las consultas por POST (vista previa de campaña) no escriben y van sin clave. (nuevo)
/// </summary>
public sealed class CobranzaClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeEtapas = GobiernoDeCarteraClient.Base + "/collection-stages";
    public const string RutaDeAsignaciones = GobiernoDeCarteraClient.Base + "/collector-assignments";
    public const string RutaDeAgenda = GobiernoDeCarteraClient.Base + "/agenda";
    public const string RutaDeGestiones = GobiernoDeCarteraClient.Base + "/collection-actions";
    public const string RutaDePromesas = GobiernoDeCarteraClient.Base + "/promises";
    public const string RutaDeAcuerdos = GobiernoDeCarteraClient.Base + "/payment-agreements";
    public const string RutaDeProcesos = GobiernoDeCarteraClient.Base + "/legal-processes";
    public const string RutaDeCampanas = GobiernoDeCarteraClient.Base + "/campaigns";
    public const string RutaDeGastos = GobiernoDeCarteraClient.Base + "/collection-fees";
    public const string RutaDeAvisos = GobiernoDeCarteraClient.Base + "/prior-notices";
    public const string RutaDeReclamos = GobiernoDeCarteraClient.Base + "/credit-report-claims";

    /// <summary>El dueño de adjunto de las actuaciones del cobro jurídico (su dueño es el proceso).</summary>
    public const string DuenoDeActuaciones = "LendingLegalProcessDocument";

    /// <summary>El dueño de adjunto de la guía de una carta de aviso previo.</summary>
    public const string DuenoDeConstancias = "LendingPriorNoticeEvidence";

    /// <summary>El dueño de adjunto del soporte de un reclamo sobre el reporte.</summary>
    public const string DuenoDeSoportesDeReclamo = "LendingCreditReportClaimSupport";

    // ------------------------------------------------------------------------------------------------- etapas --

    public Task<ResultadoDeInventario<VersionDeEtapasDto>> EtapasAsync(DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<VersionDeEtapasDto>(HttpMethod.Get, ConQuery(RutaDeEtapas, Query(("date", Texto(fecha)))), null, null, ct);

    public Task<ResultadoDeInventario<VersionDeEtapasDto>> AgregarVersionDeEtapasAsync(VersionDeEtapasRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<VersionDeEtapasDto>(HttpMethod.Post, $"{RutaDeEtapas}/versions", request, clave, ct);

    // ---------------------------------------------------------------------------------------- reparto y agenda --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<AsignacionDeGestorDto>>> AsignacionesAsync(Guid? gestor = null, Guid? credito = null,
        int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<AsignacionDeGestorDto>>(HttpMethod.Get, ConQuery(RutaDeAsignaciones, Query(("collector", Texto(gestor)),
            ("loanPublicId", Texto(credito)), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<AsignacionDeGestorDto>>> AsignarAsync(AsignarGestorRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<AsignacionDeGestorDto>>(HttpMethod.Post, RutaDeAsignaciones, request, clave, ct);

    /// <summary>Reparte por pedido; con <c>PreviewOnly</c> no escribe y devuelve cómo quedaría.</summary>
    public Task<ResultadoDeInventario<RepartoDto>> RepartirAsync(RepartirRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<RepartoDto>(HttpMethod.Post, $"{RutaDeAsignaciones}/distribute", request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ReglaDeRepartoDto>>> ReglasDeRepartoAsync(DateOnly? fecha = null, bool incluirCerradas = false,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ReglaDeRepartoDto>>(HttpMethod.Get, ConQuery($"{RutaDeAsignaciones}/rules", Query(("date", Texto(fecha)),
            ("includeClosed", Texto((bool?)incluirCerradas)))), null, null, ct);

    public Task<ResultadoDeInventario<ReglaDeRepartoDto>> CrearReglaDeRepartoAsync(ReglaDeRepartoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ReglaDeRepartoDto>(HttpMethod.Post, $"{RutaDeAsignaciones}/rules", request, clave, ct);

    public Task<ResultadoDeInventario<ReglaDeRepartoDto>> CerrarReglaDeRepartoAsync(Guid regla, DateOnly hasta, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ReglaDeRepartoDto>(HttpMethod.Post, $"{RutaDeAsignaciones}/rules/{regla}/close", new FinDeVigenciaRequest(hasta, motivo), clave, ct);

    public Task<ResultadoDeInventario<AgendaDelGestorDto>> AgendaAsync(DateOnly? fecha = null, Guid? gestor = null, CancellationToken ct = default) =>
        Enviar<AgendaDelGestorDto>(HttpMethod.Get, ConQuery(RutaDeAgenda, Query(("date", Texto(fecha)), ("collector", Texto(gestor)))), null, null, ct);

    // ------------------------------------------------------------------------------------ gestiones y promesas --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<GestionDeCobranzaDto>>> GestionesAsync(Guid? credito = null, Guid? gestor = null,
        DateOnly? desde = null, DateOnly? hasta = null, int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<GestionDeCobranzaDto>>(HttpMethod.Get, ConQuery(RutaDeGestiones, Query(("loanPublicId", Texto(credito)),
            ("collector", Texto(gestor)), ("from", Texto(desde)), ("to", Texto(hasta)), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<GestionDeCobranzaDto>> RegistrarGestionAsync(GestionRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<GestionDeCobranzaDto>(HttpMethod.Post, RutaDeGestiones, request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> AnularGestionAsync(Guid gestion, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{RutaDeGestiones}/{gestion}/void", new MotivoDeCarteraRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<PromesaDePagoDto>>> PromesasAsync(int? estado = null, Guid? gestor = null, Guid? credito = null,
        int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<PromesaDePagoDto>>(HttpMethod.Get, ConQuery(RutaDePromesas, Query(("status", Texto(estado)),
            ("collector", Texto(gestor)), ("loanPublicId", Texto(credito)), ("page", Texto(pagina)))), null, null, ct);

    /// <summary>Lo que dice la ventana de contacto (Ley 2300) sobre contactar a <paramref name="persona"/> ahora; no escribe.</summary>
    public Task<ResultadoDeInventario<VentanaDeContactoDto>> VentanaDeContactoAsync(Guid persona, int canal, Guid? credito = null,
        DateTimeOffset? en = null, CancellationToken ct = default) =>
        Enviar<VentanaDeContactoDto>(HttpMethod.Get, ConQuery($"{MensajesAlDeudorClient.RutaDeMensajes}/contact-window", Query(
            ("person", Texto(persona)), ("channel", Texto(canal)), ("purpose", Texto(1)),
            ("at", en?.ToString("o", System.Globalization.CultureInfo.InvariantCulture)), ("loan", Texto(credito)))), null, null, ct);

    // ------------------------------------------------------------------------------------------------ acuerdos --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<AcuerdoDePagoDto>>> AcuerdosAsync(int? estado = null, Guid? credito = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<AcuerdoDePagoDto>>(HttpMethod.Get, ConQuery(RutaDeAcuerdos, Query(("status", Texto(estado)),
            ("loanPublicId", Texto(credito)), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<AcuerdoDePagoDto>> ProponerAcuerdoAsync(AcuerdoDePagoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<AcuerdoDePagoDto>(HttpMethod.Post, RutaDeAcuerdos, request, clave, ct);

    public Task<ResultadoDeInventario<AcuerdoDePagoDto>> AprobarAcuerdoAsync(Guid acuerdo, string? motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<AcuerdoDePagoDto>(HttpMethod.Post, $"{RutaDeAcuerdos}/{acuerdo}/approve", new MotivoOpcionalRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<AcuerdoDePagoDto>> RechazarAcuerdoAsync(Guid acuerdo, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<AcuerdoDePagoDto>(HttpMethod.Post, $"{RutaDeAcuerdos}/{acuerdo}/reject", new MotivoDeCarteraRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<AcuerdoDePagoDto>> CancelarAcuerdoAsync(Guid acuerdo, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<AcuerdoDePagoDto>(HttpMethod.Post, $"{RutaDeAcuerdos}/{acuerdo}/cancel", new MotivoDeCarteraRequest(motivo), clave, ct);

    // ----------------------------------------------------------------------------------------- cobro jurídico --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ProcesoJuridicoDto>>> ProcesosAsync(int? estado = null, Guid? abogado = null, Guid? credito = null,
        int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ProcesoJuridicoDto>>(HttpMethod.Get, ConQuery(RutaDeProcesos, Query(("status", Texto(estado)),
            ("lawyer", Texto(abogado)), ("loanPublicId", Texto(credito)), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ProcesoJuridicoDto>>> AbrirProcesoAsync(AbrirProcesoJuridicoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ProcesoJuridicoDto>>(HttpMethod.Post, RutaDeProcesos, request, clave, ct);

    public Task<ResultadoDeInventario<ProcesoJuridicoDto>> RegistrarActuacionAsync(Guid proceso, ActuacionJuridicaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ProcesoJuridicoDto>(HttpMethod.Post, $"{RutaDeProcesos}/{proceso}/actions", request, clave, ct);

    public Task<ResultadoDeInventario<ProcesoJuridicoDto>> CerrarProcesoAsync(Guid proceso, CerrarProcesoJuridicoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ProcesoJuridicoDto>(HttpMethod.Post, $"{RutaDeProcesos}/{proceso}/close", request, clave, ct);

    /// <summary>K4, bloque D: reanuda a mano un proceso suspendido, con motivo.</summary>
    public Task<ResultadoDeInventario<ProcesoJuridicoDto>> ReanudarProcesoAsync(Guid proceso, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ProcesoJuridicoDto>(HttpMethod.Post, $"{RutaDeProcesos}/{proceso}/resume", new MotivoDeCarteraRequest(motivo), clave, ct);

    // -------------------------------------------------------------------------------------------- campañas --

    public Task<ResultadoDeInventario<IReadOnlyList<CampanaDeMensajesDto>>> CampanasAsync(int? estado = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CampanaDeMensajesDto>>(HttpMethod.Get, ConQuery(RutaDeCampanas, Query(("status", Texto(estado)))), null, null, ct);

    public Task<ResultadoDeInventario<CampanaDeMensajesDto>> CrearCampanaAsync(CampanaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CampanaDeMensajesDto>(HttpMethod.Post, RutaDeCampanas, request, clave, ct);

    /// <summary>Quiénes recibirían la campaña y a quiénes se excluye y por qué; no escribe, va por POST y sin clave.</summary>
    public Task<ResultadoDeInventario<VistaPreviaDeCampanaDto>> VistaPreviaDeCampanaAsync(Guid campana, CancellationToken ct = default) =>
        Enviar<VistaPreviaDeCampanaDto>(HttpMethod.Post, $"{RutaDeCampanas}/{campana}/preview", null, null, ct);

    public Task<ResultadoDeInventario<CampanaDeMensajesDto>> ProgramarCampanaAsync(Guid campana, string? motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CampanaDeMensajesDto>(HttpMethod.Post, $"{RutaDeCampanas}/{campana}/schedule", new MotivoOpcionalRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<CampanaDeMensajesDto>> CancelarCampanaAsync(Guid campana, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CampanaDeMensajesDto>(HttpMethod.Post, $"{RutaDeCampanas}/{campana}/cancel", new MotivoDeCarteraRequest(motivo), clave, ct);

    // ----------------------------------------------------------------------------------------------- gastos --

    public Task<ResultadoDeInventario<IReadOnlyList<GastoPorCausarDto>>> GastosPorCausarAsync(Guid? persona = null, Guid? credito = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<GastoPorCausarDto>>(HttpMethod.Get, ConQuery($"{RutaDeGastos}/pending", Query(("personPublicId", Texto(persona)),
            ("loanPublicId", Texto(credito)))), null, null, ct);

    /// <summary>Causa los gastos ya informados de la persona (sólo los que tienen su aviso enviado o entregado).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<GastoCausadoDto>>> CausarGastosAsync(CausarGastosRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<GastoCausadoDto>>(HttpMethod.Post, $"{RutaDeGastos}/cause", request, clave, ct);

    // ----------------------------------------------------------------------------------------- avisos previos --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<AvisoPrevioDto>>> AvisosAsync(Guid? credito = null, int? estado = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<AvisoPrevioDto>>(HttpMethod.Get, ConQuery(RutaDeAvisos, Query(("loanPublicId", Texto(credito)),
            ("status", Texto(estado)), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<MensajeAlDeudorDto>> EnviarAvisoAsync(Guid aviso, string canal, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<MensajeAlDeudorDto>(HttpMethod.Post, $"{RutaDeAvisos}/{aviso}/send", new EnviarAvisoRequest(canal), clave, ct);

    public Task<ResultadoDeInventario<AvisoPrevioDto>> RegistrarConstanciaAsync(Guid aviso, ConstanciaDeAvisoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<AvisoPrevioDto>(HttpMethod.Post, $"{RutaDeAvisos}/{aviso}/evidence", request, clave, ct);

    // ------------------------------------------------------------------------------------------------ reclamos --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ReclamoDeCentralesDto>>> ReclamosAsync(int? estado = null, Guid? persona = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ReclamoDeCentralesDto>>(HttpMethod.Get, ConQuery(RutaDeReclamos, Query(("status", Texto(estado)),
            ("personPublicId", Texto(persona)), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<ReclamoDeCentralesDto>> RegistrarReclamoAsync(RegistrarReclamoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ReclamoDeCentralesDto>(HttpMethod.Post, RutaDeReclamos, request, clave, ct);

    /// <summary>Prorroga el plazo del reclamo, una sola vez.</summary>
    public Task<ResultadoDeInventario<ReclamoDeCentralesDto>> ProrrogarReclamoAsync(Guid reclamo, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ReclamoDeCentralesDto>(HttpMethod.Post, $"{RutaDeReclamos}/{reclamo}/extend", new MotivoDeCarteraRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<ReclamoDeCentralesDto>> ResponderReclamoAsync(Guid reclamo, ResponderReclamoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ReclamoDeCentralesDto>(HttpMethod.Post, $"{RutaDeReclamos}/{reclamo}/answer", request, clave, ct);

    public Task<ResultadoDeInventario<ReclamoDeCentralesDto>> DesistirReclamoAsync(Guid reclamo, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ReclamoDeCentralesDto>(HttpMethod.Post, $"{RutaDeReclamos}/{reclamo}/withdraw", new MotivoDeCarteraRequest(motivo), clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
