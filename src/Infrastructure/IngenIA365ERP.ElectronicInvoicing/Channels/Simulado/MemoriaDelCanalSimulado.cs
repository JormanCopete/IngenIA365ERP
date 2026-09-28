using System.Collections.Concurrent;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Domain.Enums.Dian;

namespace IngenIA365ERP.ElectronicInvoicing.Channels.Simulado;

/// <summary>
/// Lo que «recibió» <see cref="CanalSimulado"/>, por cooperativa, ambiente y número (feature 012, I4, T727; contracts/dian.md §3.3 regla 3 y
/// §3.4). Singleton del proceso: así un reenvío o una consulta de otra petición encuentran lo mismo, igual que en un proveedor real. Se
/// pierde al reiniciar (entonces la consulta responde <c>NotFound</c> y el reenvío valida, que es la regla del ambiguo). (nuevo)
/// </summary>
public sealed class MemoriaDelCanalSimulado
{
    private readonly ConcurrentDictionary<string, RegistroSimulado> _porNumero = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _ultimaCaida = new();

    internal RegistroSimulado Obtener(Guid cooperativa, DianEnvironment ambiente, string numero) =>
        _porNumero.GetOrAdd(Clave(cooperativa, ambiente, numero), _ => new RegistroSimulado());

    internal RegistroSimulado? Buscar(Guid cooperativa, DianEnvironment ambiente, string numero) =>
        _porNumero.TryGetValue(Clave(cooperativa, ambiente, numero), out var r) ? r : null;

    /// <summary>Cuántas veces llegó al «transporte» el número (para la regla 7 de la batería de conformidad).</summary>
    public int Recepciones(Guid cooperativa, DianEnvironment ambiente, string numero) => Buscar(cooperativa, ambiente, numero)?.Recepciones ?? 0;

    internal void AnotarCaida(Guid cooperativa, DateTimeOffset cuando) => _ultimaCaida[cooperativa] = cuando;

    internal DateTimeOffset? UltimaCaida(Guid cooperativa) => _ultimaCaida.TryGetValue(cooperativa, out var c) ? c : null;

    private static string Clave(Guid cooperativa, DianEnvironment ambiente, string numero) => $"{cooperativa:N}:{ambiente}:{numero}";
}

/// <summary>El estado simulado de un número ante la «DIAN». (nuevo)</summary>
internal enum EstadoSimulado
{
    /// <summary>Nunca llegó, o llegó a medias (dígito 3 antes de la primera consulta).</summary>
    NoRecibido = 0,

    /// <summary>Dígito 3: la emisión quedó ambigua; la primera consulta responde <c>NotFound</c>.</summary>
    Ambiguo = 1,

    /// <summary>Dígito 4: el canal lo firmó con la DIAN caída; al transmitirlo se valida.</summary>
    FirmadoEnContingencia = 2,

    Validado = 3,

    ValidadoConNotificaciones = 4,

    Rechazado = 5,
}

/// <summary>Lo guardado de un número. Se toca bajo su propio candado. (nuevo)</summary>
internal sealed class RegistroSimulado
{
    public object Candado { get; } = new();

    public EstadoSimulado Estado { get; set; }

    public int Recepciones { get; set; }

    /// <summary>Dígito 3: ya respondió <c>InProcess</c> una vez; el reenvío se valida.</summary>
    public bool YaQuedoAmbiguo { get; set; }

    /// <summary>La versión que se rechazó (dígito 1); una versión posterior se valida.</summary>
    public int VersionRechazada { get; set; }

    public string? CodigoUnico { get; set; }

    public string? TipoDeCodigo { get; set; }

    public string? TipoDian { get; set; }

    public string? ReferenciaExterna { get; set; }

    public DateTimeOffset? ValidadoEn { get; set; }

    public IReadOnlyList<MensajeDelCanal> Mensajes { get; set; } = [];

    public IReadOnlyList<ArtefactoDelCanal> Artefactos { get; set; } = [];

    public string? Qr { get; set; }
}
