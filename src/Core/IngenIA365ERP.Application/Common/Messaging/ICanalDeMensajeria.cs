using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Common.Messaging;

// Feature 013 (T0083; T74, E-05; contracts/adaptadores-y-archivos.md §3.2, §4): la mensajería saliente a personas que no son usuarios
// del ERP (deudores, codeudores). NotificationChannels no se amplía y COR_Notifications no se usa para ellas (pregunta I14): esto es
// otro puerto, con el correo de la plataforma, SMS y WhatsApp por proveedor y el canal SIMULADO.

/// <summary>Estado de la entrega de un mensaje (§3.2, fijado allí). Se resuelve por sondeo: no hay webhooks (T75). (nuevo)</summary>
public enum DeliveryState
{
    /// <summary>El proveedor lo aceptó y todavía no informa la entrega.</summary>
    Accepted = 1,

    Delivered = 2,

    Failed = 3,

    /// <summary>El canal no informa entregas (el correo) o el proveedor no sabe.</summary>
    Unknown = 4,
}

/// <summary>
/// Lo que ofrece un canal de mensajería (§3.2): los canales que atiende (<c>Email</c>, <c>Sms</c>, <c>WhatsApp</c>), si admite adjuntos,
/// si exige la plantilla aprobada del proveedor (WhatsApp, pregunta I15), el largo máximo y si consulta la entrega. (nuevo)
/// </summary>
public sealed record CapacidadesDeMensajeria(
    IReadOnlySet<MessageChannel> Canales,
    bool AdmiteAdjuntos,
    bool ExigePlantillaDelProveedor,
    int? LargoMaximo,
    bool ConsultaEntrega)
{
    public bool Atiende(MessageChannel canal) => Canales.Contains(canal);
}

/// <summary>
/// Un mensaje que sale hacia una persona (§3.2). <see cref="MensajePublicId"/> es la referencia ante el proveedor y la clave de idempotencia:
/// enviarlo dos veces no lo duplica («ya existe» no es error). <see cref="DocumentoDelTitular"/> <b>(nuevo, opcional)</b> no viaja al
/// proveedor: el canal <c>SIMULADO</c> decide por su último dígito (§3.7); sin él, por el último dígito del destino. (nuevo)
/// </summary>
public sealed record MensajeSaliente(
    Guid MensajePublicId,
    string Destino,
    string? Asunto,
    string Cuerpo,
    string? ProviderTemplateId,
    IReadOnlyDictionary<string, string>? VariablesDelProveedor,
    IReadOnlyList<ArtefactoExterno> Adjuntos,
    string? DocumentoDelTitular = null)
{
    public override string ToString() => $"MensajeSaliente {{ {MensajePublicId:N}, Adjuntos = {Adjuntos.Count} }}";
}

/// <summary>Lo que devuelve un envío aceptado (§3.2). (nuevo)</summary>
public sealed record EnvioDeMensaje(string ReferenciaExterna, DateTimeOffset AceptadoEn);

/// <summary>La entrega que informa el canal (§3.2). (nuevo)</summary>
public sealed record EstadoDeEntrega(DeliveryState Estado, DateTimeOffset? En, string? Motivo);

/// <summary>
/// Un canal de mensajería saliente (feature 013, T0083; T71, T74; §3.1, §3.2). Cumple las siete reglas de §3.1
/// (<c>ConformidadDeMensajeria</c>): nunca lanza por rechazo ni por transporte; <c>ChannelUnavailable</c> si no salió e
/// <c>InProcess</c> si pudo haber llegado; «ya existe» no es error; sin reintentos HTTP automáticos al enviar. (nuevo)
/// </summary>
public interface ICanalDeMensajeria
{
    /// <summary>El código estable que se sella en el mensaje.</summary>
    string ChannelCode { get; }

    CapacidadesDeMensajeria Capacidades { get; }

    Task<ResultadoExterno<EnvioDeMensaje>> EnviarAsync(MensajeSaliente mensaje, ContextoDeCanalExterno contexto, CancellationToken ct);

    Task<ResultadoExterno<EstadoDeEntrega>> ConsultarEntregaAsync(string referenciaExterna, ContextoDeCanalExterno contexto, CancellationToken ct);

    Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct);
}

/// <summary>
/// El registro de los canales de mensajería del despliegue (feature 013, T0083; §3.1). Resuelve por el código <b>sellado</b>; un código sin
/// adaptador devuelve un canal que responde <c>ChannelUnavailable</c> («el canal {código} no está instalado»), nunca lanza. (nuevo)
/// </summary>
public interface ICanalesDeMensajeria : IRegistroDeCanalesExternos
{
    IReadOnlyCollection<string> Codigos { get; }

    ICanalDeMensajeria Resolver(string channelCode);
}
