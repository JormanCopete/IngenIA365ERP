using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Channel.Common;

/// <summary>
/// Feature 021 (T022; contracts/canal-api.md §C): el puerto del ERP hacia la API interna del canal digital (<c>/internal/v1</c>).
/// Lo implementa Infrastructure con un token de servicio firmado por el ERP. El ERP nunca guarda las credenciales de la pasarela:
/// las reenvía por aquí (research R10).
/// </summary>
public interface ICanalSatelite
{
    /// <summary>¿Hay URL y llave para hablar con el canal en este ambiente?</summary>
    bool Configurado { get; }

    /// <summary>Reserva el código de 3 letras de la cooperativa en el canal (único en la plataforma, R12).</summary>
    Task<Result<string>> ReservarCodigoAsync(Guid cooperativa, string sugerido, CancellationToken ct);

    /// <summary>Envía las credenciales de un sitio de pasarela; el canal las cifra y las verifica.</summary>
    Task<Result<VerificacionDeCredencialesDto>> GuardarCredencialesAsync(Guid cooperativa, Guid sitio, CredencialesDePasarela credenciales, CancellationToken ct);

    /// <summary>Intentos de pago por documento o referencia (atención, FR-058).</summary>
    Task<Result<IReadOnlyList<IntentoDelCanalDto>>> BuscarIntentosAsync(Guid cooperativa, string? tipoDocumento, string? documento, string? referencia, CancellationToken ct);

    /// <summary>La cuenta del canal de una persona habilitada en la cooperativa (FR-058).</summary>
    Task<Result<CuentaDelCanalDto?>> BuscarCuentaAsync(Guid cooperativa, string tipoDocumento, string documento, CancellationToken ct);

    Task<Result> DesbloquearCuentaAsync(Guid cooperativa, Guid cuenta, CancellationToken ct);

    Task<Result> CerrarSesionesAsync(Guid cooperativa, Guid cuenta, CancellationToken ct);

    /// <summary>
    /// Feature 021 (T116, T118; FR-052, FR-053): con <c>Mode = "gateway"</c>, pide al canal reversar en la pasarela; el canal vuelve a mirar el corte con su
    /// reloj y, pasado, responde 409 <c>Canal.Reverso.FueraDeCorte</c>. Con <c>"treasury"</c>, sólo marca el intento como devuelto por Tesorería para que el
    /// asociado lo vea en su historial. Repetirlo sobre un intento ya reversado responde lo mismo.
    /// </summary>
    Task<Result<ReversoEnPasarelaDto>> ReversarAsync(Guid cooperativa, string referencia, PedidoDeReversoAlCanal pedido, CancellationToken ct);

    Task<Result<MetricasDelCanalDto>> MetricasAsync(Guid cooperativa, DateOnly desde, DateOnly hasta, CancellationToken ct);

    /// <summary>Fuerza el arrastre de habilitaciones (después de una carga masiva).</summary>
    Task<Result> SincronizarAsync(Guid cooperativa, CancellationToken ct);
}

/// <summary>
/// Las credenciales de un sitio de pasarela en tránsito. Nunca se guardan en el ERP ni se escriben en un log: <see cref="ToString"/>
/// no muestra el secreto.
/// </summary>
public sealed record CredencialesDePasarela(string Pasarela, string UrlBase, string Login, string SecretKey, IReadOnlyList<string> MediosDePago)
{
    public override string ToString() => $"CredencialesDePasarela {{ Pasarela = {Pasarela}, UrlBase = {UrlBase}, Login = …{Ultimos4(Login)}, SecretKey = [oculto] }}";

    public static string Ultimos4(string valor) => valor.Length <= 4 ? valor : valor[^4..];
}

/// <summary>El resultado de verificar las credenciales contra la pasarela.</summary>
/// <remarks>Sin <see cref="VerificadaEn"/> y sin activar: la verificación quedó pendiente (la pasarela no tiene todavía cómo verificarse).</remarks>
public sealed record VerificacionDeCredencialesDto(bool Activa, DateTime? VerificadaEn, string LoginLast4, string? Motivo);

/// <summary>
/// Un intento de pago visto desde la atención de la cooperativa. Feature 021 (T112): trae también el destino, los conceptos pagados y la aprobación de la
/// pasarela, para que la conciliación aplique un aprobado que nunca se avisó.
/// </summary>
public sealed record IntentoDelCanalDto(
    string Reference,
    Guid PersonPublicId,
    string Status,
    decimal Total,
    DateTime CreatedAt,
    DateTime? FinishedAt,
    string? Authorization,
    string? PaymentMethodName,
    string ErpOutcome,
    Guid? ErpReceiptPublicId,
    Guid? DestinationPublicId = null,
    IReadOnlyList<Service.ItemDelAvisoDto>? Items = null,
    Service.AprobacionDeLaPasarelaDto? Gateway = null,
    DateTimeOffset? ApprovedAt = null,
    Guid? AccountId = null,
    DateTime? ReversedAt = null,
    string? ReversalKind = null);

/// <summary>La cuenta del canal de una persona.</summary>
public sealed record CuentaDelCanalDto(
    Guid AccountId,
    string Status,
    DateTime? ActivatedAt,
    DateTime? LastLoginAt,
    DateTime? LockedUntil,
    string? EmailMasked,
    string? MobileMasked,
    IReadOnlyList<DispositivoDelCanalDto> Devices);

public sealed record DispositivoDelCanalDto(Guid DeviceId, string Kind, string? Label, DateTime? LastSeenAt, bool Revoked);

/// <summary>Lo que el ERP manda al canal para reversar: quién (PublicId del usuario), por qué, el corte del destino (HH:mm, hora de Colombia) y el modo.</summary>
public sealed record PedidoDeReversoAlCanal(Guid RequestedBy, string Reason, string CutoffLocalTime, string Mode)
{
    public const string PorLaPasarela = "gateway";
    public const string PorTesoreria = "treasury";
}

/// <summary>
/// El resultado del reverso en el canal. <c>Reversed = false</c> con <c>Reason</c>: la pasarela no lo reversó. <c>Mode</c> y <c>ReversedAt</c>: cómo y cuándo
/// quedó reversado el intento (un intento ya reversado los repite).
/// </summary>
public sealed record ReversoEnPasarelaDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("reversed")] bool Reversado,
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Motivo,
    [property: System.Text.Json.Serialization.JsonPropertyName("mode")] string? Modo = null,
    [property: System.Text.Json.Serialization.JsonPropertyName("reversedAt")] DateTime? ReversadoEn = null);

public sealed record MetricasDelCanalDto(
    int CuentasActivas,
    int Ingresos,
    int IntentosAprobados,
    int IntentosRechazados,
    int IntentosPendientes,
    decimal ValorAprobado,
    IReadOnlyDictionary<string, int> RechazosPorMotivo);
