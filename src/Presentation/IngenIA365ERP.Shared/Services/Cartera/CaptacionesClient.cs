using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de captaciones y aportes (feature 014, T057; contracts/api.md de la 014): nace con la activación <b>sin carga</b> de aportes o de
/// captaciones (§16, <c>Lending.Deposits.Activate</c>) y cada historia le suma sus rutas (líneas, cuentas, CDAT, aportes, retiro del asociado…). Sin
/// <c>Authorization</c> a mano: la pone el handler; toda escritura con su <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed class CaptacionesClient(HttpClient http, CentralAuthClient auth)
{
    public const string Base = GobiernoDeCarteraClient.Base;
    public const string RutaDeActivacion = Base + "/deposits/activation";
    public const string RutaDeLineas = Base + "/lines";
    public const string RutaDeCuentas = Base + "/accounts";
    public const string RutaDeExenciones = Base + "/gmf-exemptions";
    public const string RutaDeAportes = Base + "/contributions";

    /// <summary>El dueño de los soportes de la titularidad de una cuenta (representación, beneficiarios, firma, origen de fondos): el titular.</summary>
    public const string DuenoDeSoportesDeTitularidad = "LendingAccountPartySupport";

    /// <summary>Las naturalezas que se activan aparte de los créditos (<c>LineNature</c>).</summary>
    public const string Captaciones = "Deposit";
    public const string Aportes = "Contribution";

    /// <summary>
    /// Activa aportes o captaciones sin carga (cooperativa sin historia, FR-117) desde <see cref="ActivarCaptacionesRequest.ActivationDate"/>.
    /// Responde 422 con <c>Lending.Activation.LoansFirst</c>, <c>.RequirementsMissing</c> (<c>data.missing</c>), <c>.AlreadyActivated</c> o
    /// <c>Lending.Savings.DeliveryNotAvailable</c>.
    /// </summary>
    public Task<ResultadoDeInventario<CaptacionesActivadasDto>> ActivarAsync(ActivarCaptacionesRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CaptacionesActivadasDto>(HttpMethod.Post, RutaDeActivacion, request, clave, ct);

    // ------------------------------------------------------------------------------------------ líneas (US1, T070) --

    /// <summary>
    /// El catálogo común de líneas (api.md §3): de la naturaleza pedida (<c>Deposit</c>, <c>Contribution</c> o <c>Placement</c>), sólo las del alcance y
    /// de las naturalezas que quien pregunta puede ver.
    /// </summary>
    public Task<ResultadoDeInventario<IReadOnlyList<LineaDelCatalogoDto>>> LineasAsync(int? naturaleza = null, bool incluirCerradas = false,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<LineaDelCatalogoDto>>(HttpMethod.Get, ConQuery(RutaDeLineas, Query(("nature", Texto(naturaleza)),
            ("includeClosed", Texto(incluirCerradas)))), null, null, ct);

    /// <summary>La línea con la versión vigente a <paramref name="fecha"/> (hoy si no viene) según su naturaleza y su historial.</summary>
    public Task<ResultadoDeInventario<LineaDeCaptacionDto>> LineaAsync(Guid linea, DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<LineaDeCaptacionDto>(HttpMethod.Get, ConQuery($"{RutaDeLineas}/{linea}", Query(("date", Texto(fecha)))), null, null, ct);

    /// <summary>
    /// Crea una línea de captación con su primera versión. Responde 422 con <c>Catalogo.CodigoDuplicado</c>, el perfil que no autoriza captar, la
    /// entrega que todavía no llega o la versión mal formada (<c>data.errors[].field</c>), y no guarda nada.
    /// </summary>
    public Task<ResultadoDeInventario<LineaDeCaptacionDto>> CrearCaptacionAsync(CrearLineaDeCaptacionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<LineaDeCaptacionDto>(HttpMethod.Post, $"{RutaDeLineas}/deposit", request, clave, ct);

    /// <summary>Crea la línea de aportes con su primera versión (en un fondo de empleados, la cuota no supera el tope legal).</summary>
    public Task<ResultadoDeInventario<LineaDeCaptacionDto>> CrearAportesAsync(CrearLineaDeAportesRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<LineaDeCaptacionDto>(HttpMethod.Post, $"{RutaDeLineas}/contribution", request, clave, ct);

    /// <summary>Una versión nueva de captación desde una fecha (nunca antes de hoy; un cambio de tasa a la vista espera el aviso a los titulares).</summary>
    public Task<ResultadoDeInventario<VersionDeCaptacionDto>> NuevaVersionDeCaptacionAsync(Guid linea, VersionDeCaptacionRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<VersionDeCaptacionDto>(HttpMethod.Post, $"{RutaDeLineas}/{linea}/deposit-versions", request, clave, ct);

    /// <summary>Una versión nueva de aportes desde una fecha.</summary>
    public Task<ResultadoDeInventario<VersionDeAportesDto>> NuevaVersionDeAportesAsync(Guid linea, VersionDeAportesRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<VersionDeAportesDto>(HttpMethod.Post, $"{RutaDeLineas}/{linea}/contribution-versions", request, clave, ct);

    /// <summary>Cierra la línea a cuentas nuevas, con motivo; las cuentas vivas siguen.</summary>
    public Task<ResultadoDeInventario<LineaDeCaptacionDto>> CerrarLineaAsync(Guid linea, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<LineaDeCaptacionDto>(HttpMethod.Post, $"{RutaDeLineas}/{linea}/close", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>Reabre una línea cerrada o activa una en borrador, revalidando el perfil, la entrega y la única línea de aportes activa.</summary>
    public Task<ResultadoDeInventario<LineaDeCaptacionDto>> ReabrirLineaAsync(Guid linea, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<LineaDeCaptacionDto>(HttpMethod.Post, $"{RutaDeLineas}/{linea}/reopen", new MotivoDeCarteraRequest(motivo), clave, ct);

    // ----------------------------------------------------------------------------------------- cuentas (US2, T079) --

    /// <summary>Las cuentas del alcance (api.md §4): por búsqueda (número, documento o nombre), naturaleza, estado y titular; la más reciente primero.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<CuentaEnListaDto>>> CuentasAsync(string? buscar = null, int? naturaleza = null, int? estado = null,
        Guid? titular = null, int pagina = 1, int tamano = 20, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<CuentaEnListaDto>>(HttpMethod.Get, ConQuery(RutaDeCuentas, Query(("search", buscar), ("nature", Texto(naturaleza)),
            ("status", Texto(estado)), ("personPublicId", Texto(titular)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    /// <summary>Una cuenta con sus partes, beneficiarios, marca del GMF, saldos y lo que la retiene en apertura.</summary>
    public Task<ResultadoDeInventario<CuentaDto>> CuentaAsync(Guid cuenta, CancellationToken ct = default) =>
        Enviar<CuentaDto>(HttpMethod.Get, $"{RutaDeCuentas}/{cuenta}", null, null, ct);

    /// <summary>
    /// Abre una cuenta. Responde 201 en «activa» o «en apertura» (con el aviso <c>Lending.Screening.Blocked</c> si la retienen las listas), o 422 con
    /// el motivo (no asociado, beneficiarios que no suman 100 %, representante, cuenta exenta ya marcada…).
    /// </summary>
    public Task<ResultadoDeInventario<CuentaDto>> AbrirCuentaAsync(AbrirCuentaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CuentaDto>(HttpMethod.Post, RutaDeCuentas, request, clave, ct);

    /// <summary>Aprueba la apertura retenida (otra persona que quien la abrió, y con las coincidencias liberadas por el oficial).</summary>
    public Task<ResultadoDeInventario<CuentaDto>> AprobarAperturaAsync(Guid cuenta, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CuentaDto>(HttpMethod.Post, $"{RutaDeCuentas}/{cuenta}/approve-opening", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>Las partes vigentes a una fecha y, si se pide, toda la historia.</summary>
    public Task<ResultadoDeInventario<PartesDeCuentaDto>> PartesAsync(Guid cuenta, bool conHistoria = false, CancellationToken ct = default) =>
        Enviar<PartesDeCuentaDto>(HttpMethod.Get, ConQuery($"{RutaDeCuentas}/{cuenta}/parties", Query(("includeHistory", conHistoria ? "true" : null))),
            null, null, ct);

    /// <summary>Cambia las partes (el conjunto completo): lo que no viene igual se cierra la víspera y lo nuevo rige desde hoy.</summary>
    public Task<ResultadoDeInventario<PartesDeCuentaDto>> CambiarPartesAsync(Guid cuenta, CambiarPartesRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<PartesDeCuentaDto>(HttpMethod.Put, $"{RutaDeCuentas}/{cuenta}/parties", request, clave, ct);

    /// <summary>Levanta la marca «actualizar datos» tras la mayoría de edad del titular.</summary>
    public Task<ResultadoDeInventario<CuentaDto>> ConfirmarActualizacionDeDatosAsync(Guid cuenta, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CuentaDto>(HttpMethod.Post, $"{RutaDeCuentas}/{cuenta}/data-update", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>Las marcas de exención del GMF de una persona (vigentes o todas).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<ExencionDeGmfDto>>> ExencionesAsync(Guid? persona = null, bool? vigentes = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ExencionDeGmfDto>>(HttpMethod.Get, ConQuery(RutaDeExenciones, Query(("personPublicId", Texto(persona)),
            ("active", vigentes is null ? null : vigentes.Value ? "true" : "false"))), null, null, ct);

    /// <summary>Marca exenta del GMF una cuenta de ahorro (o reemplaza la vigente del titular si se pide).</summary>
    public Task<ResultadoDeInventario<ExencionDeGmfDto>> MarcarExencionAsync(MarcarExencionRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ExencionDeGmfDto>(HttpMethod.Post, RutaDeExenciones, request, clave, ct);

    /// <summary>Termina una marca de exención (el último día exento, hoy si no se dice).</summary>
    public Task<ResultadoDeInventario<ExencionDeGmfDto>> TerminarExencionAsync(Guid marca, TerminarExencionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ExencionDeGmfDto>(HttpMethod.Post, $"{RutaDeExenciones}/{marca}/end", request, clave, ct);

    // ----------------------------------------------------------------------------------------- aportes (US3, T094) --

    /// <summary>El resumen de aportes de una persona a una fecha: el mismo saldo de la consulta de aportes, por clase, con las cuotas vencidas.</summary>
    public Task<ResultadoDeInventario<ResumenDeAportesDto>> ResumenDeAportesAsync(Guid persona, DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<ResumenDeAportesDto>(HttpMethod.Get, ConQuery($"{RutaDeAportes}/summary", Query(("personPublicId", Texto(persona)), ("date", Texto(fecha)))),
            null, null, ct);

    /// <summary>Las cuotas exigibles del alcance por persona, estado (Pending, PartiallyPaid, Paid, NotDeducted, Cancelled) y período.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<CuotaDeCuentaDto>>> CuotasDeCuentasAsync(Guid? persona = null, string? estado = null,
        string? periodo = null, int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<CuotaDeCuentaDto>>(HttpMethod.Get, ConQuery($"{RutaDeAportes}/installments", Query(("personPublicId", Texto(persona)),
            ("status", estado), ("period", periodo), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    /// <summary>Las devoluciones (<see cref="TextosDeAportes.Devolucion"/>) o las cesiones (<see cref="TextosDeAportes.Cesion"/>) del alcance por estado.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<SolicitudDeAportesDto>>> SolicitudesDeAportesAsync(int clase, string? estado = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<SolicitudDeAportesDto>>(HttpMethod.Get, ConQuery($"{RutaDeAportes}/{Ruta(clase)}", Query(("status", estado),
            ("page", Texto(pagina)))), null, null, ct);

    /// <summary>Pide una devolución de aportes sin retiro: queda por aprobar por otra persona.</summary>
    public Task<ResultadoDeInventario<SolicitudDeAportesDto>> PedirDevolucionAsync(DevolucionDeAportesRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<SolicitudDeAportesDto>(HttpMethod.Post, $"{RutaDeAportes}/refunds", request, clave, ct);

    /// <summary>Pide una cesión de aportes a otro asociado: queda por aprobar por otra persona.</summary>
    public Task<ResultadoDeInventario<SolicitudDeAportesDto>> PedirCesionAsync(CesionDeAportesRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<SolicitudDeAportesDto>(HttpMethod.Post, $"{RutaDeAportes}/assignments", request, clave, ct);

    /// <summary>Aprueba una solicitud (otra persona que quien la pidió): la devolución por orden de giro; la cesión, como traslado entre cuentas.</summary>
    public Task<ResultadoDeInventario<SolicitudDeAportesDto>> AprobarSolicitudDeAportesAsync(SolicitudDeAportesDto solicitud, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        solicitud.Kind == TextosDeAportes.Devolucion
            ? Enviar<SolicitudDeAportesDto>(HttpMethod.Post, $"{RutaDeAportes}/refunds/{solicitud.PublicId}/approve",
                new AprobarDevolucionDeAportesRequest("DisbursementOrder", motivo), clave, ct)
            : Enviar<SolicitudDeAportesDto>(HttpMethod.Post, $"{RutaDeAportes}/assignments/{solicitud.PublicId}/approve", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>Niega una solicitud con motivo.</summary>
    public Task<ResultadoDeInventario<SolicitudDeAportesDto>> NegarSolicitudDeAportesAsync(SolicitudDeAportesDto solicitud, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<SolicitudDeAportesDto>(HttpMethod.Post, $"{RutaDeAportes}/{Ruta(solicitud.Kind)}/{solicitud.PublicId}/reject", new MotivoDeCarteraRequest(motivo),
            clave, ct);

    private static string Ruta(int clase) => clase == TextosDeAportes.Devolucion ? "refunds" : "assignments";

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}

/// <summary>El cuerpo de <c>POST /api/lending/deposits/activation</c>: <c>nature</c> es <c>Deposit</c> o <c>Contribution</c>. (nuevo)</summary>
public sealed record ActivarCaptacionesRequest(
    string Nature,
    DateOnly ActivationDate,
    string Reason,
    bool AcknowledgeWarnings = false,
    Guid? MigrationLoadPublicId = null,
    Guid? SessionItemPublicId = null);

/// <summary>Lo que responde la activación de aportes o captaciones. (nuevo)</summary>
public sealed record CaptacionesActivadasDto(
    Guid ActivationPublicId,
    string Nature,
    DateOnly ActivationDate,
    int Accounts,
    int OpeningTransactions,
    IReadOnlyList<MensajeEmitidoDto> Messages,
    IReadOnlyList<RequisitoDeActivacionDto> Requirements,
    IReadOnlyList<AvisoDeActivacionDto> Warnings);
