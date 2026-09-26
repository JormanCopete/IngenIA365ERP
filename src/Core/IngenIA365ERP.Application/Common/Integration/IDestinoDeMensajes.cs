namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Un destino de los mensajes de integración, visto desde el despachador (feature 012, T496; contracts/mensajes.md §12,
/// §13; decisiones-transversales T8, T11, T12). Contabilidad lo implementa en I2 (<c>DestinoContabilidad</c>); Cartera, en
/// IC. Un destino sin implementación registrada se salta, sin intentos ni alertas (el <c>Lending</c> de hoy).
///
/// <para>
/// El consumidor <b>nunca toca tablas de la plataforma</b>: lee por <see cref="IMensajesEntrantes"/> y responde un
/// <see cref="ResultadoDeConsumo"/> por unidad; el despachador lo registra en otro ámbito con
/// <c>RegisterDeliveryResultCommand</c> (T11).
/// </para>
/// </summary>
public interface IDestinoDeMensajes
{
    /// <summary><c>IntegrationDestinations.Accounting</c> o <c>.Lending</c>.</summary>
    string Destino { get; }

    /// <summary>
    /// ¿Acepta esta versión de este tipo? (§12; en Contabilidad, <c>VersionesAceptadas</c>). Una versión no aceptada queda
    /// <c>Rejected</c> con <c>Integration.VersionNotAccepted</c> y nunca se reescribe.
    /// </summary>
    bool Acepta(string type, int version);

    /// <summary>
    /// Consume un trabajo: una unidad sola (en línea, o por lotes con un comprobante por documento) o un grupo resumido
    /// que armó <see cref="PlanearLote"/>. Devuelve un resultado por cada unidad del trabajo, en el mismo orden. Corre en
    /// un ámbito DI propio, dentro de <c>IEjecutorEnCooperativa</c>.
    /// </summary>
    Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(TrabajoDeConsumo trabajo, CancellationToken ct);

    /// <summary>
    /// Cómo se consumen las unidades de una pasada de un lote: una por documento o agrupadas (resumido; en Contabilidad,
    /// <c>AgrupadorDeResumidos</c>). No lee ni escribe nada: recibe lo que el despachador ya leyó.
    /// </summary>
    IReadOnlyList<TrabajoDeConsumo> PlanearLote(IReadOnlyList<MensajeEntrante> entregas);
}

/// <summary>
/// Una <b>unidad de consumo</b> (T11; contracts/mensajes.md §9): los mensajes con el mismo origen, la misma
/// <c>originEventKey</c> y el mismo destino. Nacen en el mismo guardado, tienen las mismas dependencias, se entregan juntos
/// y dan un comprobante. <see cref="IntentosLeidos"/> es el número de intentos de sus entregas cuando se leyeron: con él
/// <c>RegisterDeliveryResultCommand</c> reconoce que otra réplica ya registró este intento y no lo duplica. (nuevo)
/// </summary>
public sealed record UnidadDeConsumo(
    string Destino,
    Guid OriginPublicId,
    string OriginEventKey,
    IReadOnlyList<Guid> MessagePublicIds,
    Guid? BatchPublicId,
    int IntentosLeidos);

/// <summary>
/// Lo que el despachador entrega al destino de una vez: una unidad, o un grupo resumido con su clave (§5.3 de
/// contracts/contabilidad.md). <see cref="ClaveDeGrupo"/> nula = una unidad sola. (nuevo)
/// </summary>
public sealed record TrabajoDeConsumo(string? ClaveDeGrupo, IReadOnlyList<UnidadDeConsumo> Unidades)
{
    public static TrabajoDeConsumo DeUnaUnidad(UnidadDeConsumo unidad) => new(null, [unidad]);
}

/// <summary>El resultado de una unidad de un trabajo. (nuevo)</summary>
public sealed record ResultadoDeUnidad(UnidadDeConsumo Unidad, ResultadoDeConsumo Resultado);

/// <summary>
/// Por qué un mensaje procesado no dejó comprobante (api.md §25.2, <c>result.withoutVoucher</c>). (nuevo)
/// </summary>
public enum MotivoSinComprobante
{
    /// <summary>Un mensaje informativo: deja su recibo, no su comprobante.</summary>
    Informational = 1,

    /// <summary>Un documento cuyo valor contable es cero.</summary>
    ZeroValue = 2,
}

/// <summary>
/// Lo que responde un destino por una unidad (contracts/mensajes.md §13; decisiones-transversales §2, T11). (nuevo en su
/// forma: <c>Processed</c> y <c>AlreadyProcessed</c> admiten el recibo sin comprobante).
/// </summary>
public abstract record ResultadoDeConsumo
{
    private ResultadoDeConsumo()
    {
    }

    /// <summary>
    /// Procesado ahora. En Contabilidad, el comprobante con su tipo y número; los tres nulos y <paramref name="SinComprobante"/>
    /// con valor en un recibo sin comprobante (informativo o valor cero).
    /// </summary>
    public sealed record Processed(
        Guid? AccountingDocumentPublicId,
        string? VoucherTypeCode,
        string? VoucherNumber,
        MotivoSinComprobante? SinComprobante = null) : ResultadoDeConsumo;

    /// <summary>Ya estaba procesado (recibo único del destino, SC-002): la misma referencia, sin un segundo efecto.</summary>
    public sealed record AlreadyProcessed(
        Guid? AccountingDocumentPublicId,
        string? VoucherTypeCode = null,
        string? VoucherNumber = null,
        MotivoSinComprobante? SinComprobante = null) : ResultadoDeConsumo;

    /// <summary>Regla de negocio: queda <c>Rejected</c> con su código y motivo y no se reintenta solo.</summary>
    public sealed record Rejected(string Code, string Reason, string? DataJson = null) : ResultadoDeConsumo;

    /// <summary>Falla transitoria (o el original aún no está): se reintenta con espera creciente.</summary>
    public sealed record Retry(string Reason, string? Code = null) : ResultadoDeConsumo;
}

/// <summary>
/// Los textos de <c>ResultReference</c> de un recibo sin comprobante (data-model §19). Con comprobante, la referencia es su
/// <c>PublicId</c> en formato <c>D</c>. (nuevo)
/// </summary>
public static class ReferenciasDeResultado
{
    public const string Informativo = "informativo";

    public const string ValorCero = "sin comprobante (valor cero)";

    public static string De(Guid? comprobante, MotivoSinComprobante? sinComprobante) => comprobante is { } id
        ? id.ToString("D")
        : sinComprobante == MotivoSinComprobante.ZeroValue ? ValorCero : Informativo;

    /// <summary>El motivo de un recibo sin comprobante, o nulo si la referencia es un comprobante.</summary>
    public static MotivoSinComprobante? SinComprobante(string? referencia) => referencia switch
    {
        Informativo => MotivoSinComprobante.Informational,
        ValorCero => MotivoSinComprobante.ZeroValue,
        _ => null,
    };
}
