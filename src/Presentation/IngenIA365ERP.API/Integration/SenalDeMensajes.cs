using System.Threading.Channels;
using IngenIA365ERP.Application.Common.Integration;

namespace IngenIA365ERP.API.Integration;

/// <summary>
/// <see cref="ISenalDeMensajes"/> de la API (feature 012, T10, T078): un <see cref="Channel{T}"/> en proceso, uno
/// por réplica. Es sólo un despertador: el despachador (I2) sondea igual cada pocos segundos, así que un aviso
/// perdido retrasa una entrega, nunca la pierde. Por eso el canal es acotado y, lleno, descarta el aviso más viejo:
/// con uno basta para despertar, y un pico de ventas no puede hacer crecer la memoria sin límite ni bloquear el
/// <c>SaveChanges</c> que avisa.
/// </summary>
public sealed class SenalDeMensajes : ISenalDeMensajes
{
    /// <summary>Avisos pendientes como máximo antes de empezar a descartar los más viejos.</summary>
    public const int Capacidad = 1024;

    private readonly Channel<Guid> _canal = Channel.CreateBounded<Guid>(new BoundedChannelOptions(Capacidad)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false,
    });

    public ChannelReader<Guid> Avisos => _canal.Reader;

    public void Avisar(IReadOnlyCollection<Guid> mensajes)
    {
        ArgumentNullException.ThrowIfNull(mensajes);
        foreach (var mensaje in mensajes)
            _canal.Writer.TryWrite(mensaje);
    }
}
