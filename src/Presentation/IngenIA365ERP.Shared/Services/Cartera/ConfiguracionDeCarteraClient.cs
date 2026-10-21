using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de la configuración de Cartera (feature 013, fase 4, T0315; US17; contracts/api.md §3.4, §3.9, §3.10 y §22.2): los parámetros
/// del módulo <c>LND</c> con su historia y sus vigencias, el registro de plantillas de Cartera y la descarga del libro vacío, la bandeja de alertas
/// del módulo y la configuración de sus tipos, y las autorizaciones del titular por finalidad (la ruta es de Cumplimiento, pero la pantalla vive
/// en Cartera, C85). Sin <c>Authorization</c> a mano (la pone el handler de la sesión); toda escritura lleva su <see cref="ClaveDeOperacion"/>,
/// las consultas no. Un 422 <c>Lending.Permission.Required</c> (la clave tiene permiso propio) vuelve como error de negocio con sus permisos
/// (<see cref="GobiernoDeCarteraClient.PermisosQueFaltan"/>). (nuevo)
/// </summary>
public sealed class ConfiguracionDeCarteraClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeParametros = GobiernoDeCarteraClient.Base + "/parameters";
    public const string RutaDePlantillas = GobiernoDeCarteraClient.Base + "/templates";
    public const string RutaDeAlertas = GobiernoDeCarteraClient.Base + "/alerts";
    public const string RutaDeTiposDeAlerta = GobiernoDeCarteraClient.Base + "/alert-types";
    public const string RutaDeAutorizaciones = "/api/compliance/habeas-data/authorizations";

    // ------------------------------------------------------------------------------------------- parámetros --

    /// <summary>Las claves de Cartera con su valor vigente a <paramref name="fecha"/> (hoy si no viene).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<ParametroDeCarteraDto>>> ParametrosAsync(DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ParametroDeCarteraDto>>(HttpMethod.Get, ConQuery(RutaDeParametros, Query(("date", Texto(fecha)))), null, null, ct);

    /// <summary>La historia de una clave, la más reciente primero; con ámbito, sólo la de esa línea.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<VigenciaDeParametroDto>>> HistorialDeParametroAsync(string clave, int? ambito = null, Guid? ambitoId = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<VigenciaDeParametroDto>>(HttpMethod.Get, ConQuery($"{RutaDeParametros}/{Uri.EscapeDataString(clave)}/history", Query(
            ("scopeKind", Texto(ambito)), ("scopePublicId", Texto(ambitoId)))), null, null, ct);

    /// <summary>
    /// Registra una vigencia desde una fecha, con motivo. Fuera de los pisos y techos legales responde 422 con el límite; una clave de una
    /// entrega que no llegó, <c>Lending.Parameter.NotYetAvailable</c>.
    /// </summary>
    public Task<ResultadoDeInventario<VigenciaCreadaDto>> NuevaVigenciaAsync(string clave, NuevaVigenciaDeCarteraRequest request, ClaveDeOperacion operacion,
        CancellationToken ct = default) =>
        Enviar<VigenciaCreadaDto>(HttpMethod.Post, $"{RutaDeParametros}/{Uri.EscapeDataString(clave)}/versions", request, operacion, ct);

    // ------------------------------------------------------------------------------------------- plantillas --

    /// <summary>El registro de plantillas de Cartera con <c>canDownload</c>/<c>canImport</c> de quien pregunta.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<PlantillaDeCarteraDto>>> PlantillasAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<PlantillaDeCarteraDto>>(HttpMethod.Get, RutaDePlantillas, null, null, ct);

    /// <summary>El libro vacío de una plantilla (encabezados en la fila 1, instrucciones en otra hoja).</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarPlantillaAsync(string clave, CancellationToken ct = default) =>
        DescargarAsync(http, auth, $"{RutaDePlantillas}/{Uri.EscapeDataString(clave)}/template.xlsx", $"plantilla-{clave.Replace('.', '-')}.xlsx", ct);

    // ---------------------------------------------------------------------------------------------- alertas --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<AlertaDto>>> AlertasAsync(FiltrosDeAlertas filtros, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<AlertaDto>>(HttpMethod.Get, ConQuery(RutaDeAlertas, Query(
            ("status", filtros.Status), ("typeCode", filtros.TypeCode), ("severity", filtros.Severity), ("from", Texto(filtros.From)),
            ("to", Texto(filtros.To)), ("page", Texto(filtros.Page)), ("pageSize", Texto(filtros.PageSize)))), null, null, ct);

    public Task<ResultadoDeInventario<AlertaDto>> AtenderAlertaAsync(Guid alerta, string nota, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<AlertaDto>(HttpMethod.Post, $"{RutaDeAlertas}/{alerta}/attend", new InventarioClient.NotaDeAlertaRequest(nota), clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<TipoDeAlertaDto>>> TiposDeAlertaAsync(DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<TipoDeAlertaDto>>(HttpMethod.Get, ConQuery(RutaDeTiposDeAlerta, Query(("date", Texto(fecha)))), null, null, ct);

    public Task<ResultadoDeInventario<TipoDeAlertaDto>> NuevaVersionDeTipoDeAlertaAsync(string tipo, VersionDeTipoDeAlertaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<TipoDeAlertaDto>(HttpMethod.Post, $"{RutaDeTiposDeAlerta}/{Uri.EscapeDataString(tipo)}/versions", request, clave, ct);

    // ------------------------------------------------------------------------------------------ autorizaciones --

    /// <summary>Las autorizaciones de un titular por finalidad, con su evidencia, versión de política y origen.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<AutorizacionDeDatosDto>>> AutorizacionesAsync(Guid persona, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<AutorizacionDeDatosDto>>(HttpMethod.Get, ConQuery(RutaDeAutorizaciones, Query(("personPublicId", Texto(persona)))), null, null, ct);

    public Task<ResultadoDeInventario<AutorizacionRegistradaDto>> RegistrarAutorizacionAsync(RegistrarAutorizacionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<AutorizacionRegistradaDto>(HttpMethod.Post, RutaDeAutorizaciones, request, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
