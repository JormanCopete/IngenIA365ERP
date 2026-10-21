using System.Globalization;
using IngenIA365ERP.Domain.Enums.ExternalChannels;

namespace IngenIA365ERP.Application.Common.ExternalChannels;

// Feature 013 (T0079; T71, T72; contracts/adaptadores-y-archivos.md §2, §3.1): lo común a los cuatro puertos externos —mensajería,
// depósito de valores, centrales de riesgo y listas restrictivas—. Los tipos siguen el precedente de ResultadoDeCanal de la 012:
// nombres en español y sin el sufijo Dto, que T1 reserva para los puertos entre módulos.

/// <summary>
/// El resultado uniforme de toda llamada a un adaptador externo (§3.1). Un adaptador <b>nunca lanza</b> por un rechazo de negocio ni por
/// el transporte: lo dice <see cref="Outcome"/>. <see cref="ChannelCode"/> es el canal que respondió y se <b>sella</b> en la operación;
/// <see cref="Simulado"/> lo lee <c>GuardiaDeCanalesExternos</c> para quitarle el efecto legal en producción (§3.6). (nuevo)
/// </summary>
/// <param name="Outcome">Ok | Rejected | InProcess | NotFound | ChannelUnavailable | InvalidData.</param>
/// <param name="Valor">Sólo con <c>Ok</c> (y con <c>InProcess</c> cuando hay datos parciales).</param>
/// <param name="ChannelCode">El canal que respondió.</param>
/// <param name="Simulado"><c>true</c> en todo canal <c>SIMULADO</c>.</param>
/// <param name="ReferenciaExterna">Id del proveedor: mensaje, pagaré, consulta.</param>
/// <param name="CodigoDelProveedor">Código crudo, para soporte.</param>
/// <param name="Detalle">Texto del proveedor o del adaptador; nunca se muestra tal cual al usuario ni lleva un secreto.</param>
/// <param name="Artefactos">Archivos que devolvió el proveedor (informe crudo, certificado).</param>
/// <param name="DurationMs">Duración de la llamada.</param>
public sealed record ResultadoExterno<T>(
    ExternalOutcome Outcome,
    T? Valor,
    string ChannelCode,
    bool Simulado,
    string? ReferenciaExterna,
    string? CodigoDelProveedor,
    string? Detalle,
    IReadOnlyList<ArtefactoExterno> Artefactos,
    long DurationMs)
{
    /// <summary>¿Respondió <c>Ok</c>?</summary>
    public bool EsOk => Outcome == ExternalOutcome.Ok;

    /// <summary>Un <c>Ok</c> con su valor.</summary>
    public static ResultadoExterno<T> Ok(T valor, string channelCode, bool simulado, string? referenciaExterna = null, long duracionMs = 0,
        IReadOnlyList<ArtefactoExterno>? artefactos = null) =>
        new(ExternalOutcome.Ok, valor, channelCode, simulado, referenciaExterna, null, null, artefactos ?? [], duracionMs);

    /// <summary>Un resultado sin valor (rechazo, no disponible, dato inválido, en proceso sin datos).</summary>
    public static ResultadoExterno<T> Sin(ExternalOutcome outcome, string channelCode, bool simulado, string? detalle = null,
        string? referenciaExterna = null, string? codigoDelProveedor = null, long duracionMs = 0) =>
        new(outcome, default, channelCode, simulado, referenciaExterna, codigoDelProveedor, detalle, [], duracionMs);

    /// <summary>
    /// Regla 7 de §3.1: un código sellado sin adaptador en el despliegue responde <c>ChannelUnavailable</c> con «el canal {código} no está
    /// instalado»; nunca lanza.
    /// </summary>
    public static ResultadoExterno<T> NoInstalado(string channelCode) =>
        Sin(ExternalOutcome.ChannelUnavailable, channelCode, false, $"El canal {channelCode} no está instalado en este despliegue.");
}

/// <summary>Un archivo devuelto por un proveedor externo (informe crudo de una central, certificado de custodia…). (nuevo)</summary>
public sealed record ArtefactoExterno(string Tipo, string NombreDeArchivo, string ContentType, byte[] Bytes)
{
    public override string ToString() => $"ArtefactoExterno {{ Tipo = {Tipo}, NombreDeArchivo = {NombreDeArchivo}, Bytes = {Bytes.Length} }}";
}

/// <summary>
/// Lo que un adaptador externo necesita saber de la cooperativa y de la operación (§3.1). Nunca lleva un texto leído de la base que sirva
/// para armar la ruta de la credencial: <see cref="Credencial"/> llega ya resuelta por <see cref="ICredencialesDeCanalExterno"/>. (nuevo)
/// </summary>
/// <param name="TenantPublicId">La cooperativa resuelta.</param>
/// <param name="ChannelCode">El sellado (o el de la configuración vigente al crear la operación).</param>
/// <param name="BureauCode">Sólo centrales.</param>
/// <param name="Credencial">Nula en <c>SIMULADO</c> y <c>CARGA-PROPIA</c>.</param>
/// <param name="ClaveDeIdempotencia">El <c>PublicId</c> de la operación que llama.</param>
/// <param name="AmbienteDePruebas"><c>CanalesExternosOptions.AmbienteDePruebas</c>.</param>
public sealed record ContextoDeCanalExterno(
    Guid TenantPublicId,
    string ChannelCode,
    string? BureauCode,
    CredencialDeCanalExterno? Credencial,
    string ClaveDeIdempotencia,
    bool AmbienteDePruebas)
{
    /// <summary>El texto sin secretos, para bitácoras y excepciones.</summary>
    public override string ToString() =>
        $"ContextoDeCanalExterno {{ Tenant = {TenantPublicId:N}, Canal = {ChannelCode}, Credencial = {(Credencial is null ? "no" : "sí")} }}";
}

/// <summary>
/// La credencial de una cooperativa para un canal externo, tal como se leyó del Secret montado (§3.4). El esquema lo declara cada
/// adaptador. <see cref="ToString"/> nunca muestra los valores. (nuevo)
/// </summary>
public sealed record CredencialDeCanalExterno(string Clave, IReadOnlyDictionary<string, string> Valores)
{
    /// <summary>La referencia que se guarda en <c>CredentialKey</c>: <c>{tenantPublicId}.{channelCode}.json</c>.</summary>
    public static string ClaveDe(Guid tenantPublicId, string channelCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{tenantPublicId:D}.{channelCode}.json");

    /// <summary>Una credencial sin valores (los canales que no piden archivo).</summary>
    public static CredencialDeCanalExterno Vacia(Guid tenantPublicId, string channelCode) =>
        new(ClaveDe(tenantPublicId, channelCode), new Dictionary<string, string>(StringComparer.Ordinal));

    public override string ToString() => $"CredencialDeCanalExterno {{ Clave = {Clave}, Valores = [{Valores.Count} ocultos] }}";
}
