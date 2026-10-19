using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado del gobierno de Cartera (feature 013, fase 4, T0315; US17; contracts/api.md §3.5–§3.8, §5, §14.4): la matriz de atribuciones
/// versionada y su vista previa, los montos de las atribuciones operativas, los órganos con sus reglas y miembros, las sesiones y sus actas
/// (asistencia, puntos, votos con presencia, cierre y PDF), los vinculados con su plantilla, el alcance de Cartera de un usuario y los reclamos
/// a centrales (sólo lectura en K1). Envío común de <see cref="EnvioDeVentas"/>:
///
/// <list type="bullet">
/// <item>La cabecera <c>Authorization</c> la pone el handler de la sesión (<c>ElTokenDeSesionLoPoneElHandler</c>), nunca este cliente.</item>
/// <item>Toda escritura lleva la <see cref="ClaveDeOperacion"/> de la acción de la persona —la misma en el reintento, otra tras el éxito—; las
/// consultas (y las dos consultas por POST: la vista previa) no la llevan.</item>
/// <item>Un 422 <c>Lending.Permission.Required</c> (votar por otro miembro sin <c>RecordVotes</c>, una clave con permiso propio) vuelve como
/// error de negocio con <c>data.permissionCodes</c> (<see cref="PermisosQueFaltan"/>), no como un problema de sesión.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class GobiernoDeCarteraClient(HttpClient http, CentralAuthClient auth)
{
    public const string Base = "/api/lending";
    public const string RutaDeMatrices = Base + "/attribution-matrices";
    public const string RutaDeMontos = Base + "/amount-limits";
    public const string RutaDeOrganos = Base + "/governing-bodies";
    public const string RutaDeSesiones = Base + "/sessions";
    public const string RutaDeVinculados = Base + "/related-parties";
    public const string RutaDeAlcances = Base + "/scopes/users";
    public const string RutaDeReclamos = Base + "/credit-report-claims";

    /// <summary>El código del 422 cuando falta un permiso que depende del cuerpo (api.md §1.4).</summary>
    public const string PermisoRequerido = "Lending.Permission.Required";

    /// <summary>Los códigos de atribución operativa que admiten monto (api.md §1.4, T63).</summary>
    public static IReadOnlyList<string> PermisosLimitables { get; } =
    [
        "Lending.Disbursements.Execute", "Lending.CreditLimits.Assign", "Lending.ValidationFailures.Confirm", "Lending.Payments.BackdateBeforeClose",
        // Feature 014 (D53, 2026-10-18): el retiro y la consignación en efectivo de captaciones.
        "Lending.Accounts.Withdraw", "Lending.Accounts.Deposit",
    ];

    /// <summary>Los permisos que faltan de un 422 <see cref="PermisoRequerido"/> (<c>data.permissionCodes</c>); vacío en cualquier otro error.</summary>
    public static IReadOnlyList<string> PermisosQueFaltan<T>(ResultadoDeInventario<T> resultado) =>
        resultado.ErrorCode == PermisoRequerido ? resultado.Dato<List<string>>("permissionCodes") ?? [] : [];

    // ------------------------------------------------------------------------------------------ atribuciones --

    /// <summary>La versión vigente a <paramref name="fecha"/> (hoy si no viene) o, con <paramref name="conHistoria"/>, todas.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<MatrizDeAtribucionesDto>>> MatricesAsync(DateOnly? fecha = null, bool conHistoria = false,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<MatrizDeAtribucionesDto>>(HttpMethod.Get, ConQuery(RutaDeMatrices, Query(("date", Texto(fecha)),
            ("includeHistory", conHistoria ? "true" : null))), null, null, ct);

    public Task<ResultadoDeInventario<MatrizDeAtribucionesDto>> MatrizAsync(Guid matriz, CancellationToken ct = default) =>
        Enviar<MatrizDeAtribucionesDto>(HttpMethod.Get, $"{RutaDeMatrices}/{matriz}", null, null, ct);

    /// <summary>Publica una versión COMPLETA; la anterior rige hasta la víspera.</summary>
    public Task<ResultadoDeInventario<MatrizDeAtribucionesDto>> PublicarMatrizAsync(PublicarMatrizRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<MatrizDeAtribucionesDto>(HttpMethod.Post, RutaDeMatrices, request, clave, ct);

    /// <summary>«¿A qué nivel iría?»: es una consulta por POST y no lleva clave.</summary>
    public Task<ResultadoDeInventario<VistaPreviaDeAtribucionDto>> VistaPreviaDeAtribucionAsync(VistaPreviaDeAtribucionRequest request,
        CancellationToken ct = default) =>
        Enviar<VistaPreviaDeAtribucionDto>(HttpMethod.Post, $"{RutaDeMatrices}/preview", request, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<MontoMaximoDto>>> MontosAsync(Guid? rol = null, string? permiso = null, DateOnly? fecha = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<MontoMaximoDto>>(HttpMethod.Get, ConQuery(RutaDeMontos, Query(("rolePublicId", Texto(rol)), ("permissionCode", permiso),
            ("date", Texto(fecha)))), null, null, ct);

    /// <summary>Fija el monto de una atribución operativa; <c>MaxAmount</c> nulo = sin límite.</summary>
    public Task<ResultadoDeInventario<MontoMaximoDto>> FijarMontoAsync(FijarMontoMaximoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<MontoMaximoDto>(HttpMethod.Post, RutaDeMontos, request, clave, ct);

    // -------------------------------------------------------- políticas de aprobación de captaciones (D53, 2026-10-18) --

    public const string RutaDePoliticasDeCuentas = Base + "/approval-policies";

    /// <summary>Los sujetos de aprobación de las cuentas de captación: el retiro y la consignación en efectivo sobre la atribución.</summary>
    public static IReadOnlyList<(string Codigo, string Texto)> SujetosDeCuentas { get; } =
        [("AccountWithdrawal", "Retiro sobre la atribución"), ("AccountCashDeposit", "Consignación en efectivo sobre la atribución")];

    /// <summary>Las políticas (niveles con umbral y permiso) de los dos sujetos de cuentas, vigentes o, con <paramref name="conHistoria"/>, todas.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<PoliticaDeAprobacionDto>>> PoliticasDeCuentasAsync(bool conHistoria = false, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<PoliticaDeAprobacionDto>>(HttpMethod.Get, ConQuery(RutaDePoliticasDeCuentas, Query(("includeHistory", conHistoria ? "true" : null))),
            null, null, ct);

    /// <summary>Registra una versión nueva de la política de un sujeto de cuentas desde una fecha.</summary>
    public Task<ResultadoDeInventario<PoliticaDeAprobacionDto>> GuardarPoliticaDeCuentasAsync(GuardarPoliticaDeAprobacionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<PoliticaDeAprobacionDto>(HttpMethod.Post, RutaDePoliticasDeCuentas, request, clave, ct);

    /// <summary>Los roles de la cooperativa (<c>GET /api/admin/roles</c>, exige <c>Security.Roles.View</c>).</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<RolDeCooperativaDto>>> RolesAsync(CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<RolDeCooperativaDto>>(HttpMethod.Get, "/api/admin/roles?page=1&pageSize=200", null, null, ct);

    // ---------------------------------------------------------------------------------------------- órganos --

    public Task<ResultadoDeInventario<IReadOnlyList<OrganoEnListaDto>>> OrganosAsync(bool conInactivos = false, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<OrganoEnListaDto>>(HttpMethod.Get, ConQuery(RutaDeOrganos, Query(("includeInactive", conInactivos ? "true" : null))), null, null, ct);

    public Task<ResultadoDeInventario<OrganoDto>> OrganoAsync(Guid organo, DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<OrganoDto>(HttpMethod.Get, ConQuery($"{RutaDeOrganos}/{organo}", Query(("date", Texto(fecha)))), null, null, ct);

    public Task<ResultadoDeInventario<OrganoDto>> CrearOrganoAsync(CrearOrganoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<OrganoDto>(HttpMethod.Post, RutaDeOrganos, request, clave, ct);

    public Task<ResultadoDeInventario<ReglasDeOrganoDto>> NuevasReglasAsync(Guid organo, ReglasDeOrganoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ReglasDeOrganoDto>(HttpMethod.Post, $"{RutaDeOrganos}/{organo}/rules", request, clave, ct);

    public Task<ResultadoDeInventario<MiembroDeOrganoDto>> NombrarMiembroAsync(Guid organo, NombrarMiembroRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<MiembroDeOrganoDto>(HttpMethod.Post, $"{RutaDeOrganos}/{organo}/members", request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> TerminarMiembroAsync(Guid organo, Guid miembro, FinDeVigenciaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{RutaDeOrganos}/{organo}/members/{miembro}/end", request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> InactivarOrganoAsync(Guid organo, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{RutaDeOrganos}/{organo}/deactivate", new MotivoDeCarteraRequest(motivo), clave, ct);

    // --------------------------------------------------------------------------------------------- sesiones --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<SesionEnListaDto>>> SesionesAsync(FiltrosDeSesiones filtros, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<SesionEnListaDto>>(HttpMethod.Get, ConQuery(RutaDeSesiones, Query(
            ("body", Texto(filtros.Body)), ("status", Texto(filtros.Status)), ("from", Texto(filtros.From)), ("to", Texto(filtros.To)),
            ("page", Texto(filtros.Page)), ("pageSize", Texto(filtros.PageSize)))), null, null, ct);

    /// <summary>Los puntos de actas cerradas (el selector de «punto de acta»).</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<PuntoDeActaCerradaDto>>> PuntosDeActasCerradasAsync(string? sujeto = null, Guid? organo = null,
        Guid? sujetoId = null, int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<PuntoDeActaCerradaDto>>(HttpMethod.Get, ConQuery($"{RutaDeSesiones}/items", Query(
            ("subject", sujeto), ("body", Texto(organo)), ("subjectPublicId", Texto(sujetoId)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))),
            null, null, ct);

    /// <summary>El acta; su lectura queda auditada.</summary>
    public Task<ResultadoDeInventario<SesionDto>> SesionAsync(Guid sesion, CancellationToken ct = default) =>
        Enviar<SesionDto>(HttpMethod.Get, $"{RutaDeSesiones}/{sesion}", null, null, ct);

    /// <summary>El PDF del acta: la sellada, o el borrador marcado «SIN CERRAR». Es una lectura: sin clave.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> ActaEnPdfAsync(Guid sesion, CancellationToken ct = default) =>
        DescargarAsync(http, auth, $"{RutaDeSesiones}/{sesion}/minutes.pdf", "acta.pdf", ct);

    public Task<ResultadoDeInventario<SesionDto>> ConvocarAsync(ConvocarSesionRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<SesionDto>(HttpMethod.Post, RutaDeSesiones, request, clave, ct);

    public Task<ResultadoDeInventario<SesionDto>> RegistrarAsistenciaAsync(Guid sesion, AsistenciaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<SesionDto>(HttpMethod.Post, $"{RutaDeSesiones}/{sesion}/attendance", request, clave, ct);

    public Task<ResultadoDeInventario<PuntoDeSesionDto>> AgregarPuntoAsync(Guid sesion, PuntoDeSesionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<PuntoDeSesionDto>(HttpMethod.Post, $"{RutaDeSesiones}/{sesion}/items", request, clave, ct);

    public Task<ResultadoDeInventario<PuntoDeSesionDto>> CorregirPuntoAsync(Guid sesion, Guid punto, CorreccionDePuntoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<PuntoDeSesionDto>(HttpMethod.Put, $"{RutaDeSesiones}/{sesion}/items/{punto}", request, clave, ct);

    /// <summary>Retira un punto sin votos.</summary>
    public Task<ResultadoDeInventario<EmptyResponse>> RetirarPuntoAsync(Guid sesion, Guid punto, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Delete, $"{RutaDeSesiones}/{sesion}/items/{punto}", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>El desafío de presencia de un miembro (passkey o TOTP; opciones en base64url para <c>webauthn.js</c>).</summary>
    public Task<ResultadoDeInventario<DesafioDeVotoDto>> DesafioDePresenciaAsync(Guid sesion, Guid persona, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DesafioDeVotoDto>(HttpMethod.Post, $"{RutaDeSesiones}/{sesion}/presence-challenge", new DesafioDeSesionRequest(persona), clave, ct);

    /// <summary>
    /// Registra un voto. Sin <c>Lending.Sessions.RecordVotes</c> (el de otro miembro) ni <c>.Vote</c> (el propio): 422
    /// <see cref="PermisoRequerido"/>; una presencia fallida es 422 <c>Approvals.Presence.*</c>. Ninguno es un problema de sesión.
    /// </summary>
    public Task<ResultadoDeInventario<VotoDto>> VotarAsync(Guid sesion, Guid punto, VotoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<VotoDto>(HttpMethod.Post, $"{RutaDeSesiones}/{sesion}/items/{punto}/votes", request, clave, ct);

    /// <summary>Cierra el acta con la huella que se revisó; si cambió, 422 y nada queda.</summary>
    public Task<ResultadoDeInventario<SesionDto>> CerrarAsync(Guid sesion, string huellaEsperada, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<SesionDto>(HttpMethod.Post, $"{RutaDeSesiones}/{sesion}/close", new CierreDeSesionRequest(huellaEsperada), clave, ct);

    // -------------------------------------------------------------------------------------------- vinculados --

    /// <summary>Los vínculos (trae documentos: la lectura queda auditada).</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<VinculadoDto>>> VinculadosAsync(Guid? persona = null, int? fundamento = null, DateOnly? fecha = null,
        int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<VinculadoDto>>(HttpMethod.Get, ConQuery(RutaDeVinculados, Query(
            ("personPublicId", Texto(persona)), ("basis", Texto(fundamento)), ("date", Texto(fecha)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))),
            null, null, ct);

    public Task<ResultadoDeInventario<VinculadoDto>> RegistrarVinculoAsync(RegistrarVinculoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<VinculadoDto>(HttpMethod.Post, RutaDeVinculados, request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> TerminarVinculoAsync(Guid vinculo, FinDeVigenciaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{RutaDeVinculados}/{vinculo}/end", request, clave, ct);

    // ----------------------------------------------------------------------------------------------- alcance --

    public Task<ResultadoDeInventario<AlcanceDeCarteraDto>> AlcanceDeUsuarioAsync(Guid usuario, CancellationToken ct = default) =>
        Enviar<AlcanceDeCarteraDto>(HttpMethod.Get, $"{RutaDeAlcances}/{usuario}", null, null, ct);

    /// <summary>Reemplaza en bloque las líneas que opera el usuario (a lo sumo una por defecto).</summary>
    public Task<ResultadoDeInventario<AlcanceDeCarteraDto>> FijarLineasAsync(Guid usuario, FijarLineasRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<AlcanceDeCarteraDto>(HttpMethod.Put, $"{RutaDeAlcances}/{usuario}", request, clave, ct);

    // ---------------------------------------------------------------------------------------------- reclamos --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ReclamoDeCentralesDto>>> ReclamosAsync(int? estado = null, Guid? persona = null, int pagina = 1,
        int tamano = 50, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ReclamoDeCentralesDto>>(HttpMethod.Get, ConQuery(RutaDeReclamos, Query(
            ("status", Texto(estado)), ("personPublicId", Texto(persona)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
