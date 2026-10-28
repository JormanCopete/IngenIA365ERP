using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Recaudo;

/// <summary>
/// Cliente tipado de la configuración de Recaudo (feature 015, US1, T0077; contracts/api.md §2): puntos, cajas, cajeros, medios por punto, tipos de
/// documento, conceptos varios, talonarios, suplencias, reglas de modalidad por canal y bóvedas. Sin <c>Authorization</c> a mano; las escrituras llevan
/// <see cref="ClaveDeOperacion"/>. Las oficinas y los usuarios salen de sus rutas comunes (<c>/api/core/branches</c>) y de Recaudo (<c>/api/receipts/users</c>). (nuevo)
/// </summary>
public sealed class ConfiguracionDeRecaudoClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/receipts";

    // ------------------------------------------------------------------------------------------------------------ puntos --

    public Task<ResultadoDeInventario<IReadOnlyList<PuntoDeRecaudoDto>>> PuntosAsync(Guid? oficina = null, bool incluirInactivos = false, CancellationToken ct = default) =>
        Get<IReadOnlyList<PuntoDeRecaudoDto>>(EnvioDeVentas.ConQuery($"{Base}/collection-points",
            EnvioDeVentas.Query(("branch", EnvioDeVentas.Texto(oficina)), ("includeInactive", EnvioDeVentas.Texto(incluirInactivos)))), ct);

    public Task<ResultadoDeInventario<PuntoDeRecaudoDto>> CrearPuntoAsync(CrearPuntoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<PuntoDeRecaudoDto>(HttpMethod.Post, $"{Base}/collection-points", r, clave, ct);

    public Task<ResultadoDeInventario<PuntoDeRecaudoDto>> EditarPuntoAsync(Guid id, EditarPuntoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<PuntoDeRecaudoDto>(HttpMethod.Put, $"{Base}/collection-points/{id}", r, clave, ct);

    public Task<ResultadoDeInventario<PuntoDeRecaudoDto>> InactivarPuntoAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<PuntoDeRecaudoDto>(HttpMethod.Post, $"{Base}/collection-points/{id}/inactivate", null, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<MedioDelPuntoDto>>> MediosDelPuntoAsync(Guid punto, DateOnly? vigentesAl = null, CancellationToken ct = default) =>
        Get<IReadOnlyList<MedioDelPuntoDto>>(EnvioDeVentas.ConQuery($"{Base}/collection-points/{punto}/payment-means",
            EnvioDeVentas.Query(("asOf", EnvioDeVentas.Texto(vigentesAl)))), ct);

    public Task<ResultadoDeInventario<IReadOnlyList<MedioDelPuntoDto>>> OfrecerMedioAsync(Guid punto, OfrecerMedioRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<MedioDelPuntoDto>>(HttpMethod.Post, $"{Base}/collection-points/{punto}/payment-means", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<MedioDelPuntoDto>>> RetirarMedioAsync(Guid punto, Guid medio, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<MedioDelPuntoDto>>(HttpMethod.Post, $"{Base}/collection-points/{punto}/payment-means/{medio}/retire", null, clave, ct);

    // ------------------------------------------------------------------------------------------------------------- cajas --

    public Task<ResultadoDeInventario<IReadOnlyList<CajaDeRecaudoDto>>> CajasAsync(Guid? punto = null, bool incluirInactivas = false, CancellationToken ct = default) =>
        Get<IReadOnlyList<CajaDeRecaudoDto>>(EnvioDeVentas.ConQuery($"{Base}/cash-boxes",
            EnvioDeVentas.Query(("point", EnvioDeVentas.Texto(punto)), ("includeInactive", EnvioDeVentas.Texto(incluirInactivas)))), ct);

    public Task<ResultadoDeInventario<CajaDeRecaudoDto>> CrearCajaAsync(CrearCajaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CajaDeRecaudoDto>(HttpMethod.Post, $"{Base}/cash-boxes", r, clave, ct);

    public Task<ResultadoDeInventario<CajaDeRecaudoDto>> EditarCajaAsync(Guid id, EditarCajaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CajaDeRecaudoDto>(HttpMethod.Put, $"{Base}/cash-boxes/{id}", r, clave, ct);

    public Task<ResultadoDeInventario<CajaDeRecaudoDto>> InactivarCajaAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CajaDeRecaudoDto>(HttpMethod.Post, $"{Base}/cash-boxes/{id}/inactivate", null, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CajeroDto>>> CajerosAsync(Guid caja, CancellationToken ct = default) =>
        Get<IReadOnlyList<CajeroDto>>($"{Base}/cash-boxes/{caja}/cashiers", ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CajeroDto>>> AsignarCajeroAsync(Guid caja, AsignarCajeroRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CajeroDto>>(HttpMethod.Post, $"{Base}/cash-boxes/{caja}/cashiers", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CajeroDto>>> RetirarCajeroAsync(Guid caja, Guid usuario, RetirarCajeroRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CajeroDto>>(HttpMethod.Post, $"{Base}/cash-boxes/{caja}/cashiers/{usuario}/retire", r, clave, ct);

    // ---------------------------------------------------------------------------------------------- tipos y conceptos --

    public Task<ResultadoDeInventario<IReadOnlyList<TipoDeDocumentoDeRecaudoDto>>> TiposDeDocumentoAsync(bool incluirInactivos = true, CancellationToken ct = default) =>
        Get<IReadOnlyList<TipoDeDocumentoDeRecaudoDto>>(EnvioDeVentas.ConQuery($"{Base}/document-types",
            EnvioDeVentas.Query(("includeInactive", EnvioDeVentas.Texto(incluirInactivos)))), ct);

    public Task<ResultadoDeInventario<TipoDeDocumentoDeRecaudoDto>> CrearTipoDeDocumentoAsync(CrearTipoDeDocumentoRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<TipoDeDocumentoDeRecaudoDto>(HttpMethod.Post, $"{Base}/document-types", r, clave, ct);

    public Task<ResultadoDeInventario<TipoDeDocumentoDeRecaudoDto>> EditarTipoDeDocumentoAsync(Guid id, EditarTipoDeDocumentoRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<TipoDeDocumentoDeRecaudoDto>(HttpMethod.Put, $"{Base}/document-types/{id}", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ConceptoVarioDto>>> ConceptosAsync(bool incluirInactivos = true, CancellationToken ct = default) =>
        Get<IReadOnlyList<ConceptoVarioDto>>(EnvioDeVentas.ConQuery($"{Base}/misc-concepts",
            EnvioDeVentas.Query(("includeInactive", EnvioDeVentas.Texto(incluirInactivos)))), ct);

    public Task<ResultadoDeInventario<ConceptoVarioDto>> CrearConceptoAsync(CrearConceptoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConceptoVarioDto>(HttpMethod.Post, $"{Base}/misc-concepts", r, clave, ct);

    public Task<ResultadoDeInventario<ConceptoVarioDto>> EditarConceptoAsync(Guid id, EditarConceptoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConceptoVarioDto>(HttpMethod.Put, $"{Base}/misc-concepts/{id}", r, clave, ct);

    public Task<ResultadoDeInventario<ConceptoVarioDto>> InactivarConceptoAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConceptoVarioDto>(HttpMethod.Post, $"{Base}/misc-concepts/{id}/inactivate", null, clave, ct);

    // ------------------------------------------------------------------------------------------- talonarios y suplencias --

    public Task<ResultadoDeInventario<IReadOnlyList<TalonarioDto>>> TalonariosAsync(Guid? punto = null, CancellationToken ct = default) =>
        Get<IReadOnlyList<TalonarioDto>>(EnvioDeVentas.ConQuery($"{Base}/contingency-books", EnvioDeVentas.Query(("point", EnvioDeVentas.Texto(punto)))), ct);

    public Task<ResultadoDeInventario<TalonarioDto>> RegistrarTalonarioAsync(RegistrarTalonarioRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<TalonarioDto>(HttpMethod.Post, $"{Base}/contingency-books", r, clave, ct);

    public Task<ResultadoDeInventario<TalonarioDto>> AnularTalonarioAsync(Guid id, MotivoDeRecaudoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<TalonarioDto>(HttpMethod.Post, $"{Base}/contingency-books/{id}/void", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<SuplenciaDto>>> SuplenciasAsync(Guid? punto = null, bool soloVigentes = false, CancellationToken ct = default) =>
        Get<IReadOnlyList<SuplenciaDto>>(EnvioDeVentas.ConQuery($"{Base}/supervisor-delegations",
            EnvioDeVentas.Query(("point", EnvioDeVentas.Texto(punto)), ("onlyCurrent", EnvioDeVentas.Texto(soloVigentes)))), ct);

    public Task<ResultadoDeInventario<SuplenciaDto>> RegistrarSuplenciaAsync(RegistrarSuplenciaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<SuplenciaDto>(HttpMethod.Post, $"{Base}/supervisor-delegations", r, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> TerminarSuplenciaAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{Base}/supervisor-delegations/{id}/end", null, clave, ct);

    // ---------------------------------------------------------------------------------------------- modalidad por canal --

    public Task<ResultadoDeInventario<IReadOnlyList<ReglaDeModalidadDto>>> ReglasDeModalidadAsync(bool conHistoria = false, CancellationToken ct = default) =>
        Get<IReadOnlyList<ReglaDeModalidadDto>>(EnvioDeVentas.ConQuery($"{Base}/channel-modality-rules",
            EnvioDeVentas.Query(("includeHistory", EnvioDeVentas.Texto(conHistoria)))), ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ReglaDeModalidadDto>>> FijarReglasDeModalidadAsync(FijarReglasDeModalidadRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ReglaDeModalidadDto>>(HttpMethod.Put, $"{Base}/channel-modality-rules", r, clave, ct);

    // ----------------------------------------------------------------------------------------------------------- bóvedas --

    public Task<ResultadoDeInventario<IReadOnlyList<BovedaDto>>> BovedasAsync(CancellationToken ct = default) =>
        Get<IReadOnlyList<BovedaDto>>($"{Base}/vaults", ct);

    public Task<ResultadoDeInventario<BovedaDto>> CrearBovedaAsync(CrearBovedaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<BovedaDto>(HttpMethod.Post, $"{Base}/vaults", r, clave, ct);

    public Task<ResultadoDeInventario<BovedaDto>> EditarBovedaAsync(Guid id, EditarBovedaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<BovedaDto>(HttpMethod.Put, $"{Base}/vaults/{id}", r, clave, ct);

    public Task<ResultadoDeInventario<BovedaDto>> FijarCustodiosAsync(Guid id, FijarCustodiosRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<BovedaDto>(HttpMethod.Post, $"{Base}/vaults/{id}/custodians", r, clave, ct);

    // ------------------------------------------------------------------------------------------------ listas comunes --

    /// <summary>Las oficinas de la cooperativa (<c>COR_Branches</c>), por <c>PublicId</c>.</summary>
    public async Task<IReadOnlyList<OficinaDeRecaudoItem>> OficinasAsync(CancellationToken ct = default) =>
        await http.TryGetListAsync<OficinaDeRecaudoItem>("/api/core/branches?PageNumber=1&PageSize=500&SortBy=name", ct);

    /// <summary>Los usuarios activos de la cooperativa (<c>GET /api/receipts/users</c>, con <c>Receipts.Setup.View</c>; decisión del dueño del 2026-10-23).</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<PersonaDeLaCooperativaDto>>> UsuariosAsync(CancellationToken ct = default) =>
        Get<PaginaDeInventarioDto<PersonaDeLaCooperativaDto>>("/api/receipts/users", ct);

    // ------------------------------------------------------------------------------------------------------- textos --

    public static string Estado(int estado) => estado == 1 ? "Activo" : "Inactivo";

    public static string ModoDeBase(int modo) => modo == 2 ? "Fondo fijo" : "Base del día";

    public static string ClaseDeDocumento(int clase) => clase switch
    {
        1 => "Recibo de caja",
        2 => "Recibo de banco",
        3 => "Recibo de convenio",
        4 => "Recibo de pagaduría",
        5 => "Recibo en línea",
        6 => "Recibo de contingencia",
        7 => "Egreso de caja",
        8 => "Anulación",
        9 => "Movimiento de caja",
        10 => "Ajuste de sesión",
        _ => "Otra",
    };

    public static string ClaseDeMedio(int clase) => clase switch
    {
        1 => "Efectivo",
        2 => "Tarjeta de crédito",
        3 => "Tarjeta débito",
        6 => "Consignación",
        7 => "Transferencia",
        8 => "Bono",
        9 => "Cheque",
        _ => "Otro",
    };

    public static string EstadoDeTalonario(int estado) => estado switch { 1 => "Activo", 2 => "Agotado", 3 => "Anulado", _ => "" };

    public static string ModoDeBoveda(int modo) => modo == 2 ? "Caja principal como bóveda" : "Bóveda propia";

    // ------------------------------------------------------------- cuentas de transferencia (segunda ronda del 2026-10-27, D-f) --

    public Task<ResultadoDeInventario<IReadOnlyList<CuentaDeTransferenciaDto>>> CuentasDeTransferenciaAsync(Guid? punto = null, CancellationToken ct = default) =>
        Get<IReadOnlyList<CuentaDeTransferenciaDto>>(EnvioDeVentas.ConQuery($"{Base}/transfer-accounts", EnvioDeVentas.Query(("collectionPoint", EnvioDeVentas.Texto(punto)))), ct);

    public Task<ResultadoDeInventario<CuentaDeTransferenciaDto>> HabilitarCuentaDeTransferenciaAsync(HabilitarCuentaDeTransferenciaRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CuentaDeTransferenciaDto>(HttpMethod.Post, $"{Base}/transfer-accounts", r, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> QuitarCuentaDeTransferenciaAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{Base}/transfer-accounts/{id}/disable", null, clave, ct);

    private Task<ResultadoDeInventario<T>> Get<T>(string url, CancellationToken ct) => EnvioDeVentas.EnviarAsync<T>(http, auth, HttpMethod.Get, url, null, null, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
