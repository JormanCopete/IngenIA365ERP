namespace IngenIA365ERP.Application.ElectronicInvoicing.Channels;

/// <summary>
/// A dónde avisa la emisión cada falla del canal (feature 012, I4; contracts/dian.md §7.2): un <c>ChannelUnavailable</c> definitivo o una
/// espera del POS vencida cuentan para el circuito de la cooperativa y el canal, y un intento que el canal sí atendió lo pone a cero. La
/// implementación es <c>CircuitoDeCanal</c> (T730, en memoria, que abre el evento 03 al llegar a <c>Dian.UmbralFallasCircuito</c>); mientras
/// no esté registrada, la emisión no avisa. La declara aquí la sección de emisión (T712), que es la primera que la llama. (nuevo)
/// </summary>
public interface IRegistroDeFallasDelCanal
{
    /// <summary>Una falla más del canal sellado <paramref name="channelCode"/> en la cooperativa en curso.</summary>
    Task RegistrarFallaAsync(string channelCode, CancellationToken ct);

    /// <summary>El canal respondió: la cuenta de fallas seguidas vuelve a cero.</summary>
    Task RegistrarRespuestaAsync(string channelCode, CancellationToken ct);
}
