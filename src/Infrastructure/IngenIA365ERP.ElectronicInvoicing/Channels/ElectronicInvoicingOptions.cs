using IngenIA365ERP.Application.ElectronicInvoicing.Documents;

namespace IngenIA365ERP.ElectronicInvoicing.Channels;

/// <summary>
/// La sección técnica <c>ElectronicInvoicing</c> de <c>appsettings</c> (feature 012, I4, T728; contracts/dian.md §3.3 regla 8, §6.3, §6.4 y
/// §11). Son <b>valores técnicos del proceso</b>, no parámetros de la cooperativa (T10): tiempos del transporte de cada canal, esperas entre
/// intentos, dónde está montado el Secret de credenciales y el ritmo del procesador de fondo. Nunca lleva credenciales. (nuevo)
/// </summary>
public sealed class ElectronicInvoicingOptions
{
    public const string SectionName = "ElectronicInvoicing";

    /// <summary>El directorio del Secret <c>erp-fe-credenciales</c> montado sin <c>subPath</c> (§11).</summary>
    public const string RutaDeCredencialesPorDefecto = "/secrets/facturacion-electronica/";

    /// <summary><c>ElectronicInvoicing:Channels:{channelCode}</c>: tiempos por canal. Un canal sin sección usa <see cref="OpcionesDeCanal"/> por defecto.</summary>
    public Dictionary<string, OpcionesDeCanal> Channels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary><c>ElectronicInvoicing:Retries</c>: las esperas entre intentos (§6.3).</summary>
    public OpcionesDeReintentoDeEmision Retries { get; set; } = new();

    /// <summary><c>ElectronicInvoicing:CredentialsPath</c>.</summary>
    public string CredentialsPath { get; set; } = RutaDeCredencialesPorDefecto;

    /// <summary><c>ElectronicInvoicing:Processor</c>: el procesador de fondo (§6.4).</summary>
    public OpcionesDelProcesador Processor { get; set; } = new();

    /// <summary>Las opciones de <paramref name="channelCode"/>, o las por defecto si no tiene sección.</summary>
    public OpcionesDeCanal Canal(string channelCode) =>
        Channels.TryGetValue(channelCode, out var opciones) && opciones is not null ? opciones : new OpcionesDeCanal();
}

/// <summary>
/// Los tiempos de un canal (§3.3 regla 8): conexión corta y total; y, sólo en <c>SIMULADO</c>, el retardo para ensayar la espera del POS y
/// cuánto dura la «caída» simulada tras un <c>ChannelUnavailable</c>. (nuevo)
/// </summary>
public sealed class OpcionesDeCanal
{
    public int ConnectTimeoutSeconds { get; set; } = 5;

    public int TotalTimeoutSeconds { get; set; } = 30;

    /// <summary>Sólo <c>CanalSimulado</c>: milisegundos que tarda cada llamada (0 = responde en el acto).</summary>
    public int DelayMilliseconds { get; set; }

    /// <summary>
    /// Sólo <c>CanalSimulado</c>: segundos durante los que <c>ProbarAsync</c> responde <c>ChannelUnavailable</c> después del último
    /// <c>ChannelUnavailable</c> de la cooperativa (dígito 5). Así el circuito se abre y se vuelve a cerrar solo, como con un proveedor que
    /// se cae un rato.
    /// </summary>
    public int SimulatedOutageSeconds { get; set; } = 60;
}

/// <summary><c>ElectronicInvoicing:Retries</c> (§6.3): 15 s, 1, 2, 5, 15, 30 y 60 minutos, después cada hora; y el arrendamiento de fila. (nuevo)</summary>
public sealed class OpcionesDeReintentoDeEmision
{
    /// <summary>Las esperas del contrato, en segundos, cuando la sección no trae las suyas.</summary>
    public static IReadOnlyList<int> EsperasDelContrato { get; } = [15, 60, 120, 300, 900, 1800, 3600];

    /// <summary>
    /// Nace vacía a propósito: el enlazador de configuración <b>agrega</b> a una lista que ya tiene elementos, así que un defecto aquí se
    /// sumaría a lo configurado. Vacía = <see cref="EsperasDelContrato"/>.
    /// </summary>
    public List<int> DelaysSeconds { get; set; } = [];

    public int ThenEverySeconds { get; set; } = 3600;

    public int LeaseSeconds { get; set; } = 120;

    /// <summary>Las esperas que usa la emisión (<see cref="EsperasDeReintento"/>). Una lista vacía o valores no positivos toman el defecto.</summary>
    public EsperasDeReintento ComoEsperas()
    {
        var defecto = new OpcionesDeReintentoDeEmision();
        var esperas = (DelaysSeconds ?? []).Where(s => s > 0).Select(s => TimeSpan.FromSeconds(s)).ToList();
        if (esperas.Count == 0) esperas = EsperasDelContrato.Select(s => TimeSpan.FromSeconds(s)).ToList();
        return new EsperasDeReintento(esperas,
            TimeSpan.FromSeconds(ThenEverySeconds > 0 ? ThenEverySeconds : defecto.ThenEverySeconds),
            TimeSpan.FromSeconds(LeaseSeconds > 0 ? LeaseSeconds : defecto.LeaseSeconds));
    }
}

/// <summary><c>ElectronicInvoicing:Processor</c> (§6.4): sondeo, tanda, arrendamiento <c>einvoicing.process</c> y presupuesto por cooperativa. (nuevo)</summary>
public sealed class OpcionesDelProcesador
{
    public int IntervalSeconds { get; set; } = 15;

    public int BatchSize { get; set; } = 50;

    public int LeaseTtlSeconds { get; set; } = 120;

    public int BudgetSeconds { get; set; } = 60;
}
