using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Channels;

/// <summary>
/// El puerto del canal de emisión electrónica (feature 012, I4, T700; contracts/dian.md §3.1; decisiones-transversales T40):
/// lo único que la plataforma sabe de un proveedor tecnológico, del servicio central de la 010 o de <c>CanalSimulado</c>. Cada
/// adaptador vive en <c>Infrastructure/IngenIA365ERP.ElectronicInvoicing</c> y traduce el canónico a su formato; Domain y
/// Application nunca referencian ese proyecto ni un SDK de proveedor (<c>ElProveedorTecnologicoSoloLoConoceSuAdaptador</c>).
///
/// <para>
/// Reglas que todo adaptador cumple (§3.3; las verifica <c>ConformidadDelCanal</c>): nunca lanza por un rechazo de negocio
/// (<see cref="Domain.Enums.ElectronicInvoicing.ChannelOutcome.Rejected"/> o <c>InvalidData</c>); ninguna excepción de transporte sube
/// (<c>ChannelUnavailable</c> si la petición no salió, <c>InProcess</c> si pudo llegar); «ya existe» se traduce a una consulta;
/// <c>Validated</c> exige código único y respuesta de validación; las notificaciones solas no rechazan; sin reintentos HTTP
/// automáticos en <see cref="EmitirAsync"/>.
/// </para>
/// </summary>
public interface ICanalDeEmisionElectronica
{
    /// <summary>Código estable del adaptador; es el que se sella en cada documento («SIMULADO», el del proveedor, «SERVICIO-CENTRAL»).</summary>
    string ChannelCode { get; }

    /// <summary>Qué tipos y qué operaciones ofrece el canal (§3.2).</summary>
    CapacidadesDelCanal Capacidades { get; }

    /// <summary>Emisión normal y transmisión diferida de las contingencias 03 y 04: el canónico dice cuál es.</summary>
    Task<ResultadoDeCanal> EmitirAsync(DocumentoElectronicoCanonico documento, ContextoDeCanal contexto, CancellationToken ct);

    /// <summary>Por número y ambiente, o por código único o referencia externa (trackId).</summary>
    Task<ResultadoDeCanal> ConsultarEstadoAsync(ReferenciaDeEnvio referencia, ContextoDeCanal contexto, CancellationToken ct);

    /// <summary>Eventos RADIAN 030 y 032 (I5).</summary>
    Task<ResultadoDeCanal> EmitirEventoAsync(EventoRadianCanonico evento, ContextoDeCanal contexto, CancellationToken ct);

    /// <summary><see cref="TipoDeArtefacto.XmlFirmado"/>, <see cref="TipoDeArtefacto.ApplicationResponse"/> o <see cref="TipoDeArtefacto.AttachedDocument"/>. Idempotente.</summary>
    Task<ResultadoDeCanal> DescargarArtefactoAsync(ReferenciaDeEnvio referencia, TipoDeArtefacto tipo, ContextoDeCanal contexto, CancellationToken ct);

    /// <summary>Credenciales y salida: al verificar la configuración (§10.2) y en el sondeo del circuito (§7.2).</summary>
    Task<ResultadoDeCanal> ProbarAsync(ContextoDeCanal contexto, CancellationToken ct);

    /// <summary>Opcional según <see cref="CapacidadesDelCanal.ConsultaRangos"/>: GetNumberingRange. Sólo verifica y propone, nunca crea resoluciones.</summary>
    Task<ResultadoDeCanal> ConsultarRangosAsync(ContextoDeCanal contexto, CancellationToken ct);

    /// <summary>Opcional según <see cref="CapacidadesDelCanal.ConsultaAdquirente"/>: GetAcquirer (Res. 202/2025).</summary>
    Task<ResultadoDeCanal> ConsultarAdquirenteAsync(string tipoDeIdentificacionDian, string numero, ContextoDeCanal contexto, CancellationToken ct);
}

/// <summary>
/// El registro de los canales (T700; contracts/dian.md §3.1). Resuelve **por el canal sellado en el documento**, nunca por la
/// configuración de hoy: los reenvíos, los casos a y b y la transmisión de contingencia de un documento salen por el canal con
/// que se numeró (FR-064, §10.5). (nuevo)
/// </summary>
public interface ICanalesDeEmision
{
    /// <summary>
    /// El adaptador de <paramref name="channelCode"/>. Un código sellado sin adaptador es un defecto de despliegue, no un dato:
    /// la implementación lanza. Quien valida una configuración pregunta antes con <see cref="Codigos"/>.
    /// </summary>
    ICanalDeEmisionElectronica Resolver(string channelCode);

    /// <summary>Los códigos de los canales registrados (para validar una configuración, <c>ElectronicInvoicing.Settings.ChannelUnknown</c>). (nuevo)</summary>
    IReadOnlyCollection<string> Codigos { get; }
}
