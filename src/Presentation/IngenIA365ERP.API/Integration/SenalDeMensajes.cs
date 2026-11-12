using System.Threading.Channels;
using IngenIA365ERP.API.Middleware;
using IngenIA365ERP.Application.Common.Integration;

namespace IngenIA365ERP.API.Integration;

/// <summary>
/// <see cref="ISenalDeMensajes"/> de la API (feature 012, T10, T078): un <see cref="Channel{T}"/> en proceso, uno
/// por réplica. Es sólo un despertador: el despachador (I2) sondea igual cada pocos segundos, así que un aviso
/// perdido retrasa una entrega, nunca la pierde. Por eso el canal es acotado y, lleno, descarta el aviso más viejo:
/// con uno basta para despertar, y un pico de ventas no puede hacer crecer la memoria sin límite ni bloquear el
/// <c>SaveChanges</c> que avisa.
/// <para>
/// Feature 023 (US7): también anota la <b>cooperativa</b> del guardado en <see cref="Cooperativas"/>, para que
/// <see cref="AvisadorDeIntegracion"/> lo lleve a la cola y la atienda cualquier réplica —no sólo la que guardó—. Se lee de la petición
/// o del trabajo de fondo en curso, con el mismo criterio que <see cref="TenantContextAccessor"/>.
/// </para>
/// </summary>
public sealed class SenalDeMensajes(IHttpContextAccessor? http = null) : ISenalDeMensajes
{
    /// <summary>Avisos pendientes como máximo antes de empezar a descartar los más viejos.</summary>
    public const int Capacidad = 1024;

    private readonly Channel<Guid> _canal = Channel.CreateBounded<Guid>(new BoundedChannelOptions(Capacidad)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false,
    });

    private readonly Channel<Guid> _cooperativas = Channel.CreateBounded<Guid>(new BoundedChannelOptions(Capacidad)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false,
    });

    private readonly TenantContextAccessor? _cooperativa = http is null ? null : new TenantContextAccessor(http);

    public ChannelReader<Guid> Avisos => _canal.Reader;

    /// <summary>Feature 023: las cooperativas que guardaron mensajes (una por guardado), para la cola.</summary>
    public ChannelReader<Guid> Cooperativas => _cooperativas.Reader;

    public void Avisar(IReadOnlyCollection<Guid> mensajes)
    {
        ArgumentNullException.ThrowIfNull(mensajes);
        foreach (var mensaje in mensajes)
            _canal.Writer.TryWrite(mensaje);
        if (mensajes.Count > 0 && Guid.TryParse(_cooperativa?.TenantId, out var cooperativa))
            _cooperativas.Writer.TryWrite(cooperativa);
    }
}
