namespace IngenIA365ERP.ExternalChannels;

/// <summary>
/// La sección técnica <c>ExternalChannels</c> de <c>appsettings</c> (feature 013, T0081, T0082; contracts/adaptadores-y-archivos.md §3.1
/// regla 5, §3.4). Son <b>valores técnicos del proceso</b>, no parámetros de la cooperativa: tiempos del transporte por canal y dónde está
/// montado el Secret <c>erp-canales-externos</c>. Nunca lleva credenciales. (nuevo)
/// </summary>
public sealed class ExternalChannelsOptions
{
    public const string SectionName = "ExternalChannels";

    /// <summary>El directorio del Secret <c>erp-canales-externos</c>, montado sin <c>subPath</c> (§3.4).</summary>
    public const string RutaDeCredencialesPorDefecto = "/secrets/canales-externos/";

    /// <summary><c>ExternalChannels:CredentialsPath</c>.</summary>
    public string CredentialsPath { get; set; } = RutaDeCredencialesPorDefecto;

    /// <summary><c>ExternalChannels:Channels:{channelCode}</c>: tiempos por canal. Un canal sin sección usa los de <see cref="OpcionesDeCanalExterno"/>.</summary>
    public Dictionary<string, OpcionesDeCanalExterno> Channels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Las opciones de <paramref name="channelCode"/>, o las por defecto si no tiene sección.</summary>
    public OpcionesDeCanalExterno Canal(string channelCode) =>
        Channels.TryGetValue(channelCode, out var opciones) && opciones is not null ? opciones : new OpcionesDeCanalExterno();
}

/// <summary>
/// Los tiempos de un canal externo (§3.1 regla 5): conexión y total; y, sólo en los simulados, cuánto tarda cada llamada. Un simulado cuyo
/// retardo pasa el tiempo total se comporta como un proveedor que no contestó a tiempo: el envío queda <c>InProcess</c> (pudo haber
/// llegado) y una consulta, <c>ChannelUnavailable</c>. (nuevo)
/// </summary>
public sealed class OpcionesDeCanalExterno
{
    public int ConnectTimeoutSeconds { get; set; } = 5;

    public int TotalTimeoutSeconds { get; set; } = 30;

    /// <summary>Sólo los simulados: milisegundos que tarda cada llamada (0 = responde en el acto).</summary>
    public int DelayMilliseconds { get; set; }

    /// <summary>El tiempo total en milisegundos.</summary>
    public int TotalTimeoutMilliseconds => Math.Max(1, TotalTimeoutSeconds) * 1000;
}
