using IngenIA365ERP.Application.ElectronicInvoicing.Channels;

namespace IngenIA365ERP.ElectronicInvoicing.Channels;

/// <summary>
/// El registro de canales del proceso (feature 012, I4, T728; contracts/dian.md §3.1): todos los <see cref="ICanalDeEmisionElectronica"/>
/// registrados en <c>AddElectronicInvoicing</c>, por <c>ChannelCode</c> sin distinguir mayúsculas. Resuelve por el canal <b>sellado</b> en el
/// documento. Un código sellado sin adaptador es un defecto de despliegue y lanza; quien valida una configuración pregunta antes con
/// <see cref="Codigos"/> y responde <c>ElectronicInvoicing.Settings.ChannelUnknown</c>. Dos adaptadores con el mismo código también son un
/// defecto: el contenedor no arranca. (nuevo)
/// </summary>
public sealed class CanalesDeEmision : ICanalesDeEmision
{
    private readonly Dictionary<string, ICanalDeEmisionElectronica> _porCodigo;

    public CanalesDeEmision(IEnumerable<ICanalDeEmisionElectronica> canales)
    {
        ArgumentNullException.ThrowIfNull(canales);
        _porCodigo = new Dictionary<string, ICanalDeEmisionElectronica>(StringComparer.OrdinalIgnoreCase);
        foreach (var canal in canales)
        {
            if (string.IsNullOrWhiteSpace(canal.ChannelCode))
                throw new InvalidOperationException($"El adaptador {canal.GetType().Name} no declara su ChannelCode.");
            if (!_porCodigo.TryAdd(canal.ChannelCode.Trim(), canal))
                throw new InvalidOperationException($"Dos adaptadores declaran el canal «{canal.ChannelCode}».");
        }
        Codigos = _porCodigo.Keys.Order(StringComparer.Ordinal).ToList();
    }

    public IReadOnlyCollection<string> Codigos { get; }

    public ICanalDeEmisionElectronica Resolver(string channelCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelCode);
        return _porCodigo.TryGetValue(channelCode.Trim(), out var canal)
            ? canal
            : throw new InvalidOperationException(
                $"El canal «{channelCode}» está sellado en un documento o una configuración pero ningún adaptador lo registra en este proceso " +
                "(ElectronicInvoicing.Settings.ChannelUnknown).");
    }
}
