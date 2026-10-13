using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de las consultas del crédito (feature 013, US4, T0507; contracts/api.md §8.1): la lista dentro del alcance, el detalle, el
/// estado de cuenta y la liquidación a una fecha D con la información hasta R, el plan con sus versiones, las operaciones con su comprobante y
/// el libro. Todo es lectura: sin clave. Sin <c>Authorization</c> a mano (lo pone el handler). (nuevo)
/// </summary>
public sealed class CreditosClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeCreditos = GobiernoDeCarteraClient.Base + "/loans";

    /// <summary>
    /// La búsqueda de créditos: número o deudor (<paramref name="buscar"/>), estado (<c>Active</c>, <c>PendingDisbursement</c>, <c>PaidOff</c>…),
    /// línea, oficina, modalidad y mora mínima. Sólo lo del alcance de quien consulta.
    /// </summary>
    public Task<ResultadoDeInventario<PaginaDeCarteraDto<CreditoEnListaDto>>> ListarAsync(string? buscar = null, string? estado = null, Guid? linea = null,
        Guid? oficina = null, string? modalidad = null, int? moraMinima = null, int pagina = 1, int tamano = 20, CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<CreditoEnListaDto>>(HttpMethod.Get, ConQuery(RutaDeCreditos, Query(
            ("search", string.IsNullOrWhiteSpace(buscar) ? null : buscar.Trim()), ("status", estado), ("line", Texto(linea)), ("office", Texto(oficina)),
            ("modality", modalidad), ("minDaysPastDue", Texto(moraMinima)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), ct);

    public Task<ResultadoDeInventario<CreditoDto>> CreditoAsync(Guid credito, CancellationToken ct = default) =>
        Enviar<CreditoDto>(HttpMethod.Get, $"{RutaDeCreditos}/{credito}", ct);

    /// <summary>El estado de cuenta a <paramref name="fecha"/> (D) con la información hasta <paramref name="informacionHasta"/> (R).</summary>
    public Task<ResultadoDeInventario<EstadoDeCuentaDto>> EstadoDeCuentaAsync(Guid credito, DateOnly? fecha = null, DateTime? informacionHasta = null,
        CancellationToken ct = default) =>
        Enviar<EstadoDeCuentaDto>(HttpMethod.Get, ConQuery($"{RutaDeCreditos}/{credito}/statement", Query(
            ("date", Texto(fecha)), ("recordedAsOf", Instante(informacionHasta)))), ct);

    /// <summary>La liquidación a D con la información hasta R (lectura auditada; exige <c>Lending.Payoffs.Read</c>).</summary>
    public Task<ResultadoDeInventario<LiquidacionDto>> LiquidacionAsync(Guid credito, DateOnly? fecha = null, DateTime? informacionHasta = null,
        CancellationToken ct = default) =>
        Enviar<LiquidacionDto>(HttpMethod.Get, ConQuery($"{RutaDeCreditos}/{credito}/settlement", Query(
            ("date", Texto(fecha)), ("recordedAsOf", Instante(informacionHasta)))), ct);

    /// <summary>Una versión del plan con sus cuotas; sin versión, la vigente.</summary>
    public Task<ResultadoDeInventario<PlanDelCreditoDto>> PlanAsync(Guid credito, int? version = null, CancellationToken ct = default) =>
        Enviar<PlanDelCreditoDto>(HttpMethod.Get, ConQuery($"{RutaDeCreditos}/{credito}/schedule", Query(("version", Texto(version)))), ct);

    public Task<ResultadoDeInventario<IReadOnlyList<VersionDelPlanDto>>> VersionesDelPlanAsync(Guid credito, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<VersionDelPlanDto>>(HttpMethod.Get, $"{RutaDeCreditos}/{credito}/schedule/versions", ct);

    public Task<ResultadoDeInventario<PaginaDeCarteraDto<OperacionDelCreditoDto>>> OperacionesAsync(Guid credito, int pagina = 1, int tamano = 20,
        CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<OperacionDelCreditoDto>>(HttpMethod.Get, ConQuery($"{RutaDeCreditos}/{credito}/operations", Query(
            ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), ct);

    /// <summary>El comprobante de aplicación de una operación: su distribución y los mensajes que dejó.</summary>
    public Task<ResultadoDeInventario<ComprobanteDeOperacionDto>> OperacionAsync(Guid credito, Guid operacion, CancellationToken ct = default) =>
        Enviar<ComprobanteDeOperacionDto>(HttpMethod.Get, $"{RutaDeCreditos}/{credito}/operations/{operacion}", ct);

    /// <summary>El libro del crédito (sólo lectura), por fecha valor y concepto.</summary>
    public Task<ResultadoDeInventario<PaginaDeCarteraDto<FilaDelLibroDto>>> LibroAsync(Guid credito, DateOnly? desde = null, DateOnly? hasta = null,
        string? concepto = null, int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<FilaDelLibroDto>>(HttpMethod.Get, ConQuery($"{RutaDeCreditos}/{credito}/transactions", Query(
            ("from", Texto(desde)), ("to", Texto(hasta)), ("concept", concepto), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), ct);

    /// <summary>R viaja como instante UTC con su zona (<c>…Z</c>), así la API no lo toma como hora local.</summary>
    public static string? Instante(DateTime? r) => r is { } v
        ? (v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc))
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture)
        : null;

    /// <summary>
    /// Feature 013, US6 (T0601; api.md §8.2): confirma por excepción la obligación de una venta con validación fallida, con motivo y su
    /// <see cref="ClaveDeOperacion"/>. Por encima de la atribución operativa el crédito pasa a decisión: la respuesta lo dice.
    /// </summary>
    public Task<ResultadoDeInventario<CreditoDto>> ConfirmarValidacionFallidaAsync(Guid credito, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<CreditoDto>(http, auth, HttpMethod.Post, $"{RutaDeCreditos}/{credito}/validation-failure/confirm", new MotivoDeCarteraRequest(motivo),
            clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, null, null, ct);
}
