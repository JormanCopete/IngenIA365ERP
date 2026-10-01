using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// Seguridad, aprobaciones, parámetros y alertas del módulo (feature 012, T429; US12): parámetros con vigencia (§7),
/// políticas de aprobación y montos máximos (§15.1, §15.3), la bandeja de aprobaciones y su decisión (§15.2), el alcance
/// comercial de un usuario (§16.3), las alertas y sus tipos (§16.1, §16.2) y los vendedores (§31). Las consultas no llevan
/// clave; cada escritura lleva la <see cref="ClaveDeOperacion"/> de la acción de la persona (la misma en el reintento). La
/// cabecera <c>Authorization</c> no se pone aquí: la pone el handler de la sesión.
/// </summary>
public sealed partial class InventarioClient
{
    public const string RutaDeParametros = Base + "/parameters";
    public const string RutaDePoliticas = Base + "/approval-policies";
    public const string RutaDeMontos = Base + "/amount-limits";
    public const string RutaDeAprobaciones = Base + "/approvals";
    public const string RutaDeAlcances = Base + "/scopes/users";
    public const string RutaDeAlertas = Base + "/alerts";
    public const string RutaDeTiposDeAlerta = Base + "/alert-types";
    public const string RutaDeVendedores = Base + "/salespeople";

    // ------------------------------------------------------------------------------------------- parámetros --

    public Task<ResultadoDeInventario<IReadOnlyList<ParametroDto>>> ParametrosAsync(string? modulo = null, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ParametroDto>>(HttpMethod.Get, ConQuery(RutaDeParametros, Query(("module", modulo))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<VigenciaDeParametroDto>>> HistorialDeParametroAsync(string modulo, string clave, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<VigenciaDeParametroDto>>(HttpMethod.Get, $"{RutaDeParametros}/{Uri.EscapeDataString(modulo)}/{Uri.EscapeDataString(clave)}/history", null, null, ct);

    /// <summary>
    /// Registra una vigencia nueva. Sobre la cadena de ventas con un tipo fiscal y sin confirmación responde 422
    /// <c>Inventory.PostingMode.FiscalRequiresConfirmation</c> con <c>data.fiscalDocumentTypes</c>.
    /// </summary>
    public Task<ResultadoDeInventario<VigenciaCreadaDto>> NuevaVigenciaAsync(string modulo, string clave, NuevaVigenciaDeParametroRequest request,
        ClaveDeOperacion operacion, CancellationToken ct = default) =>
        EnviarAsync<VigenciaCreadaDto>(HttpMethod.Post, $"{RutaDeParametros}/{Uri.EscapeDataString(modulo)}/{Uri.EscapeDataString(clave)}/versions", request, operacion, ct);

    // ---------------------------------------------------------------------------------- políticas y montos --

    public Task<ResultadoDeInventario<IReadOnlyList<PoliticaDeAprobacionDto>>> PoliticasDeAprobacionAsync(bool conHistoria = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<PoliticaDeAprobacionDto>>(HttpMethod.Get, ConQuery(RutaDePoliticas, Query(("includeHistory", conHistoria ? "true" : null))), null, null, ct);

    public Task<ResultadoDeInventario<PoliticaDeAprobacionDto>> GuardarPoliticaDeAprobacionAsync(GuardarPoliticaDeAprobacionRequest request, ClaveDeOperacion operacion,
        CancellationToken ct = default) =>
        EnviarAsync<PoliticaDeAprobacionDto>(HttpMethod.Post, RutaDePoliticas, request, operacion, ct);

    /// <summary>Los montos máximos; con <paramref name="aLaFecha"/> sólo los vigentes, sin ella toda la historia.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<MontoMaximoDto>>> MontosMaximosAsync(DateOnly? aLaFecha = null, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<MontoMaximoDto>>(HttpMethod.Get, ConQuery(RutaDeMontos, Query(("asOf", aLaFecha?.ToString("yyyy-MM-dd")))), null, null, ct);

    public Task<ResultadoDeInventario<MontoMaximoDto>> FijarMontoMaximoAsync(FijarMontoMaximoRequest request, ClaveDeOperacion operacion, CancellationToken ct = default) =>
        EnviarAsync<MontoMaximoDto>(HttpMethod.Post, RutaDeMontos, request, operacion, ct);

    /// <summary>
    /// Los roles de la cooperativa para fijar montos (<c>GET /api/admin/roles</c>, exige <c>Security.Roles.View</c>); sin él
    /// la pestaña de montos sólo muestra lo ya fijado.
    /// </summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<RolDeCooperativaDto>>> RolesAsync(CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<RolDeCooperativaDto>>(HttpMethod.Get, "/api/admin/roles?page=1&pageSize=200", null, null, ct);

    /// <summary>Los permisos que admiten monto máximo (contracts/api.md §1.3, FR-009).</summary>
    public static IReadOnlyList<string> PermisosLimitables { get; } =
        ["Inventory.Purchases.Confirm", "Inventory.Adjustments.Confirm", "Inventory.Sales.Confirm", "Inventory.Sales.SellOnCredit"];

    // ------------------------------------------------------------------------------------------ aprobaciones --

    /// <summary><paramref name="mias"/>: lo que puedo decidir ahora; si no, el seguimiento de mi alcance.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<SolicitudDeAprobacionDto>>> AprobacionesAsync(bool mias, int pagina = 1, int tamano = 50,
        CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<SolicitudDeAprobacionDto>>(HttpMethod.Get,
            ConQuery(RutaDeAprobaciones, Query(("mine", mias ? "true" : "false"), ("page", pagina.ToString()), ("pageSize", tamano.ToString()))), null, null, ct);

    public Task<ResultadoDeInventario<SolicitudDeAprobacionDto>> AprobacionAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<SolicitudDeAprobacionDto>(HttpMethod.Get, $"{RutaDeAprobaciones}/{id}", null, null, ct);

    /// <summary>
    /// Decide el nivel pendiente. Con <c>expectedContentSha256</c> viejo: 422 <c>Approvals.Request.ContentChanged</c>; un
    /// 422 <c>Approvals.Presence.Invalid</c> es un error de negocio, no de sesión.
    /// </summary>
    public Task<ResultadoDeInventario<ResultadoDeDecisionDto>> DecidirAprobacionAsync(Guid id, DecidirAprobacionRequest request, ClaveDeOperacion operacion,
        CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeDecisionDto>(HttpMethod.Post, $"{RutaDeAprobaciones}/{id}/decide", request, operacion, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> RetirarAprobacionAsync(Guid id, string motivo, ClaveDeOperacion operacion, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{RutaDeAprobaciones}/{id}/withdraw", new MotivoDeInventarioRequest(motivo), operacion, ct);

    // ----------------------------------------------------------------------------------------------- alcance --

    public Task<ResultadoDeInventario<AlcanceComercialDto>> AlcanceDeUsuarioAsync(Guid usuario, CancellationToken ct = default) =>
        EnviarAsync<AlcanceComercialDto>(HttpMethod.Get, $"{RutaDeAlcances}/{usuario}", null, null, ct);

    /// <summary>Reemplaza las bodegas asignadas; dos por defecto: 422 <c>Inventory.Scope.DefaultDuplicate</c>.</summary>
    public Task<ResultadoDeInventario<AlcanceComercialDto>> FijarAlcanceAsync(Guid usuario, FijarAlcanceRequest request, ClaveDeOperacion operacion,
        CancellationToken ct = default) =>
        EnviarAsync<AlcanceComercialDto>(HttpMethod.Put, $"{RutaDeAlcances}/{usuario}", request, operacion, ct);

    // ---------------------------------------------------------------------------------------------- alertas --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<AlertaDto>>> AlertasAsync(FiltrosDeAlertas filtros, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<AlertaDto>>(HttpMethod.Get, ConQuery(RutaDeAlertas, Query(
            ("status", filtros.Status),
            ("typeCode", filtros.TypeCode),
            ("severity", filtros.Severity),
            ("from", filtros.From?.ToString("yyyy-MM-dd")),
            ("to", filtros.To?.ToString("yyyy-MM-dd")),
            ("page", filtros.Page.ToString()),
            ("pageSize", filtros.PageSize.ToString()))), null, null, ct);

    public Task<ResultadoDeInventario<AlertaDto>> AtenderAlertaAsync(Guid id, string nota, ClaveDeOperacion operacion, CancellationToken ct = default) =>
        EnviarAsync<AlertaDto>(HttpMethod.Post, $"{RutaDeAlertas}/{id}/attend", new NotaDeAlertaRequest(nota), operacion, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<TipoDeAlertaDto>>> TiposDeAlertaAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<TipoDeAlertaDto>>(HttpMethod.Get, RutaDeTiposDeAlerta, null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<TipoDeAlertaDto>>> HistorialDeTipoDeAlertaAsync(string tipo, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<TipoDeAlertaDto>>(HttpMethod.Get, $"{RutaDeTiposDeAlerta}/{Uri.EscapeDataString(tipo)}/history", null, null, ct);

    public Task<ResultadoDeInventario<TipoDeAlertaDto>> NuevaVersionDeTipoDeAlertaAsync(string tipo, VersionDeTipoDeAlertaRequest request, ClaveDeOperacion operacion,
        CancellationToken ct = default) =>
        EnviarAsync<TipoDeAlertaDto>(HttpMethod.Post, $"{RutaDeTiposDeAlerta}/{Uri.EscapeDataString(tipo)}/versions", request, operacion, ct);

    // ------------------------------------------------------------------------------------------- vendedores --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<VendedorDto>>> VendedoresAsync(string? buscar, bool conRetirados, int pagina = 1, int tamano = 50,
        CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<VendedorDto>>(HttpMethod.Get, ConQuery(RutaDeVendedores, Query(
            ("search", buscar),
            ("includeRetired", conRetirados ? "true" : null),
            ("page", pagina.ToString()),
            ("pageSize", tamano.ToString()))), null, null, ct);

    /// <summary>Da el rol (o restaura el retirado con el mismo identificador): <c>{ salespersonPublicId, restored }</c>.</summary>
    public Task<ResultadoDeInventario<VendedorCreadoDto>> CrearVendedorAsync(CrearVendedorRequest request, ClaveDeOperacion operacion, CancellationToken ct = default) =>
        EnviarAsync<VendedorCreadoDto>(HttpMethod.Post, RutaDeVendedores, request, operacion, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> ActualizarVendedorAsync(Guid id, ActualizarVendedorRequest request, ClaveDeOperacion operacion,
        CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"{RutaDeVendedores}/{id}", request, operacion, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> RetirarVendedorAsync(Guid id, string motivo, ClaveDeOperacion operacion, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{RutaDeVendedores}/{id}/retire", new MotivoDeInventarioRequest(motivo), operacion, ct);

    /// <summary>El cuerpo de <c>attend</c> (§16.1).</summary>
    public sealed record NotaDeAlertaRequest(string Note);
}
