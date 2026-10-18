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
