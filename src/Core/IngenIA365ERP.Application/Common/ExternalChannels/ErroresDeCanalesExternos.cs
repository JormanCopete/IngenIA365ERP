using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.ExternalChannels;

namespace IngenIA365ERP.Application.Common.ExternalChannels;

/// <summary>
/// Los errores de los canales externos (feature 013, T0079, T0080; T72; contracts/api.md §21.2, contracts/adaptadores-y-archivos.md §3.3;
/// data-model parte 3 §10.2). El código «canal desconocido» es <c>Core.ExternalChannel.ChannelUnknown</c>, el de api.md §21.2 (el contrato
/// de adaptadores lo escribe <c>UnknownChannel</c>; manda el contrato HTTP, como en la 012 con <c>Settings.ChannelUnknown</c>). (nuevo)
/// </summary>
public static class ErroresDeCanalesExternos
{
    public const string CodigoSeCruza = "Core.ExternalChannel.Overlaps";
    public const string CodigoCanalDesconocido = "Core.ExternalChannel.ChannelUnknown";
    public const string CodigoFaltaLaClave = "Core.ExternalChannel.CredentialKeyRequired";
    public const string CodigoClaveNoCoincide = "Core.ExternalChannel.CredentialMismatch";
    public const string CodigoSinCredencial = "Core.ExternalChannel.CredentialMissing";
    public const string CodigoNoEncontrada = "Core.ExternalChannel.NotFound";

    /// <summary>El código del resultado simulado sin efecto legal en producción (T73).</summary>
    public const string CodigoSimuladoEnProduccion = "Lending.Adapter.SimulatedInProduction";

    public static Error SeCruza(string settingKey, DateOnly validFrom) =>
        new ErrorConDatos(CodigoSeCruza, "Ya hay una configuración vigente para ese canal en esas fechas.", new { settingKey, validFrom });

    public static Error CanalDesconocido(ExternalChannelKind kind, string channelCode, IReadOnlyCollection<string> disponibles) =>
        new ErrorConDatos(CodigoCanalDesconocido,
            disponibles.Count == 0
                ? $"Este despliegue no tiene instalado ningún canal para {kind}: no se puede configurar «{channelCode}»."
                : $"El canal «{channelCode}» no está instalado para {kind}. Elija uno de: {string.Join(", ", disponibles)}.",
            new { kind, available = disponibles });

    public static Error FaltaLaClave(string channelCode) =>
        new(CodigoFaltaLaClave,
            $"El canal {channelCode} pide credencial: indique la clave del Secret de canales externos (credentialKey).");

    public static Error ClaveNoCoincide(string channelCode, string esperada) =>
        new ErrorConDatos(CodigoClaveNoCoincide,
            $"La credencial configurada para el canal {channelCode} no corresponde a esta cooperativa (se esperaba «{esperada}»).",
            new { channelCode, expected = esperada });

    public static Error SinCredencial(string channelCode, string detalle) =>
        new ErrorConDatos(CodigoSinCredencial, detalle, new { channelCode });

    public static Error NoEncontrada(Guid publicId) =>
        new ErrorConDatos(CodigoNoEncontrada, "La configuración del canal externo no existe.", new { publicId });
}
