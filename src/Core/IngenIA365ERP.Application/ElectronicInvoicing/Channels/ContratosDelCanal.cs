using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Channels;

// Feature 012, I4, T700: las piezas del puerto del canal con las firmas de contracts/dian.md §3.1 a §3.3.

/// <summary>
/// Lo que ofrece un canal (contracts/dian.md §3.2). <c>GuardiaDeEmisionFiscal</c> responde <c>Blocked</c> para un tipo que el
/// canal sellado no declara y <c>ConfigureEmissionCommand</c> rechaza un canal sin <see cref="AceptaNumeroDelErp"/>.
/// </summary>
/// <param name="Tipos">Invoice, CreditNote, DebitNote, PosEquivalent, PosAdjustmentNote, SupportDocument, SupportDocumentAdjustmentNote, RadianEvent030, RadianEvent032.</param>
/// <param name="AceptaNumeroDelErp">Obligatorio: un canal sin esto no se admite (el ERP numera).</param>
/// <param name="ContingenciaDelFacturador">Transmite tipo 03 referido al número de papel.</param>
/// <param name="InformaContingenciaDian">Distingue «DIAN no disponible» de un error.</param>
/// <param name="EsAsincrono">La respuesta definitiva puede llegar por consulta.</param>
/// <param name="DevuelvePdf">Devuelve su propia representación (el ERP no la guarda: la representación es la del ERP).</param>
/// <param name="PuedeEnviarCorreo">Puede entregar el documento por correo (<c>EmailDeliveryBy = Channel</c>).</param>
/// <param name="ConsultaRangos">Ofrece GetNumberingRange.</param>
/// <param name="ConsultaAdquirente">Ofrece GetAcquirer (Res. 202/2025).</param>
/// <param name="AdmiteClaveDeIdempotencia"><c>reference_code</c> o equivalente.</param>
public sealed record CapacidadesDelCanal(
    IReadOnlySet<ElectronicDocumentKind> Tipos,
    bool AceptaNumeroDelErp,
    bool ContingenciaDelFacturador,
    bool InformaContingenciaDian,
    bool EsAsincrono,
    bool DevuelvePdf,
    bool PuedeEnviarCorreo,
    bool ConsultaRangos,
    bool ConsultaAdquirente,
    bool AdmiteClaveDeIdempotencia)
{
    /// <summary>¿El canal emite documentos de <paramref name="tipo"/>? (nuevo)</summary>
    public bool Emite(ElectronicDocumentKind tipo) => Tipos.Contains(tipo);
}

/// <summary>
/// El resultado uniforme de toda llamada al canal (contracts/dian.md §3.3). La máquina de estados
/// (<c>TransicionesDelDocumentoElectronico</c>) lo lee; nunca un código crudo del proveedor, que se conserva en
/// <see cref="ProviderCode"/>/<see cref="DianStatusCode"/> para la bitácora.
/// </summary>
/// <param name="Outcome">Validated | ValidatedWithNotices | Rejected | InProcess | NotFound | DianUnavailable | ChannelUnavailable | InvalidData.</param>
/// <param name="UniqueCode">CUFE, CUDE o CUDS.</param>
/// <param name="UniqueCodeKind">«CUFE», «CUDE» o «CUDS».</param>
/// <param name="QrContent">El contenido del QR tal como lo devuelve el canal (en modo proveedor el ERP no lo arma, §13).</param>
/// <param name="ValidatedAt">Cuándo validó la DIAN.</param>
/// <param name="DianDocumentTypeCode">El tipo que el canal realmente emitió (04 en contingencia de la DIAN).</param>
/// <param name="Mensajes">Reglas de rechazo y notificaciones.</param>
/// <param name="Artefactos">XML firmado, <c>ApplicationResponse</c>, <c>AttachedDocument</c>.</param>
/// <param name="ExternalReference">Referencia del canal (trackId).</param>
/// <param name="ProviderCode">Código crudo del proveedor.</param>
/// <param name="DianStatusCode">Código crudo de la DIAN.</param>
/// <param name="DurationMs">Duración de la llamada.</param>
public sealed record ResultadoDeCanal(
    ChannelOutcome Outcome,
    string? UniqueCode,
    string? UniqueCodeKind,
    string? QrContent,
    DateTimeOffset? ValidatedAt,
    string? DianDocumentTypeCode,
    IReadOnlyList<MensajeDelCanal> Mensajes,
    IReadOnlyList<ArtefactoDelCanal> Artefactos,
    string? ExternalReference,
    string? ProviderCode,
    string? DianStatusCode,
    long DurationMs)
{
    /// <summary>¿Trae la respuesta de validación (el <c>ApplicationResponse</c> o su equivalente)? Sin ella no hay <c>Validated</c> (regla 5). (nuevo)</summary>
    public bool TieneRespuestaDeValidacion => Artefactos.Any(a => a.Tipo == TipoDeArtefacto.ApplicationResponse);

    /// <summary>¿Trae código único? (nuevo)</summary>
    public bool TieneCodigoUnico => !string.IsNullOrWhiteSpace(UniqueCode);

    /// <summary>Un resultado sin datos del canal (p. ej. <c>ChannelUnavailable</c> porque la credencial no se pudo leer). (nuevo)</summary>
    public static ResultadoDeCanal Sin(ChannelOutcome outcome, long duracionMs = 0, params MensajeDelCanal[] mensajes) =>
        new(outcome, null, null, null, null, null, mensajes, [], null, null, null, duracionMs);
}

/// <summary>Si un mensaje del canal rechaza o sólo notifica. (nuevo)</summary>
public enum TipoDeMensajeDelCanal
{
    Rechazo = 1,
    Notificacion = 2,
}

/// <summary>Un mensaje del canal o de la DIAN: la regla, si rechaza o notifica, el texto crudo y su traducción para la pantalla.</summary>
public sealed record MensajeDelCanal(string Regla, TipoDeMensajeDelCanal Tipo, string Texto, string? Traduccion = null);

/// <summary>Los artefactos que devuelve un canal (contracts/dian.md §3.1 y §12).</summary>
public enum TipoDeArtefacto
{
    XmlFirmado = 1,
    ApplicationResponse = 2,
    AttachedDocument = 3,
}

/// <summary>Un artefacto devuelto por el canal. Se guarda como adjunto del módulo que no se borra (§12).</summary>
public sealed record ArtefactoDelCanal(TipoDeArtefacto Tipo, string NombreDeArchivo, string ContentType, byte[] Bytes);

/// <summary>
/// A qué documento se refiere una consulta o una descarga (contracts/dian.md §3.1): número completo (prefijo + consecutivo),
/// ambiente, código único si se conoce y referencia externa del canal si la hubo.
/// </summary>
public sealed record ReferenciaDeEnvio(
    string Prefix,
    long Consecutive,
    DianEnvironment Environment,
    string? UniqueCode = null,
    string? ExternalReference = null)
{
    /// <summary>El número completo, prefijo + consecutivo.</summary>
    public string Numero => Prefix + Consecutive.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Lo que el canal necesita saber de la cooperativa y de la operación (contracts/dian.md §3.1). Nunca lleva un valor leído de la
/// base que sirva para armar la ruta de las credenciales: <see cref="Credenciales"/> llegan ya resueltas por
/// <see cref="ICredencialesDeCanal"/> con el <c>PublicId</c> de la cooperativa resuelta y el canal sellado (§11).
/// </summary>
/// <param name="TenantPublicId">La cooperativa resuelta.</param>
/// <param name="IssuerTaxId">NIT del emisor.</param>
/// <param name="IssuerCheckDigit">DV del emisor.</param>
/// <param name="Mode">Modo sellado.</param>
/// <param name="Environment">Ambiente sellado.</param>
/// <param name="SoftwareId">Modo propio.</param>
/// <param name="TestSetId">Modo propio.</param>
/// <param name="TechnicalKey">Clave técnica de la resolución, sólo en modo propio y cuando la operación la necesita (§15).</param>
/// <param name="IdempotencyKey">La clave del intento (§6.2), ver <see cref="ClaveDeIdempotencia"/>.</param>
/// <param name="Credenciales">Las credenciales ya resueltas; nulas en <c>CanalSimulado</c>.</param>
public sealed record ContextoDeCanal(
    Guid TenantPublicId,
    string IssuerTaxId,
    string IssuerCheckDigit,
    EmissionMode Mode,
    DianEnvironment Environment,
    string? SoftwareId,
    string? TestSetId,
    string? TechnicalKey,
    string? IdempotencyKey,
    CredencialesDeCanal? Credenciales)
{
    /// <summary>
    /// <c>{tenantPublicId:N}:{Environment}:{Prefix}{Consecutive}:v{VersionNumber}</c> (contracts/dian.md §6.2), p. ej.
    /// <c>3f2a…c91:Testing:SETP990000123:v1</c>. (nuevo)
    /// </summary>
    public static string ClaveDeIdempotencia(Guid tenantPublicId, DianEnvironment ambiente, string prefijo, long consecutivo, int version) =>
        string.Create(CultureInfo.InvariantCulture, $"{tenantPublicId:N}:{ambiente}:{prefijo}{consecutivo}:v{version}");

    /// <summary>El texto sin secretos, para bitácoras y excepciones. (nuevo)</summary>
    public override string ToString() =>
        $"ContextoDeCanal {{ Tenant = {TenantPublicId:N}, Mode = {Mode}, Environment = {Environment}, Credenciales = {(Credenciales is null ? "no" : "sí")} }}";
}

/// <summary>
/// Las credenciales de una cooperativa para un canal, tal como las leyó <see cref="ICredencialesDeCanal"/> del Secret montado
/// (contracts/dian.md §11). El esquema lo declara cada adaptador. <see cref="ToString"/> nunca muestra los valores. (nuevo)
/// </summary>
public sealed record CredencialesDeCanal(string Clave, IReadOnlyDictionary<string, string> Valores)
{
    /// <summary>La referencia que se guarda en <c>CredentialKey</c>: <c>{tenantPublicId}.{channelCode}.json</c>.</summary>
    public static string ClaveDe(Guid tenantPublicId, string channelCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{tenantPublicId:D}.{channelCode}.json");

    public override string ToString() => $"CredencialesDeCanal {{ Clave = {Clave}, Valores = [{Valores.Count} ocultos] }}";
}

/// <summary>
/// Resuelve las credenciales de la cooperativa resuelta para un canal (contracts/dian.md §11). La ruta la arma la
/// implementación (<c>CredencialesEnArchivo</c>) <b>sólo</b> con el <c>PublicId</c> de la cooperativa resuelta
/// (<c>ICurrentTenantService</c>, o la entrada del directorio dentro de <c>IEjecutorEnCooperativa</c>) y el
/// <paramref name="channelCode"/> sellado; nunca con un texto leído de la base. Un archivo ausente o ilegible es un fallo, no una
/// excepción: quien llama lo traduce a <c>ChannelUnavailable</c>. (nuevo)
/// </summary>
public interface ICredencialesDeCanal
{
    Task<Result<CredencialesDeCanal>> ResolverAsync(string channelCode, CancellationToken ct);
}
