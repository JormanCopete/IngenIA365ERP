namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// La implementación provisional de <see cref="IReservasDeCartera"/> mientras Cartera no exista en el despliegue (feature 013, T0152;
/// contracts/interfaces.md §5.2): reservar y liberar responden <see cref="EstadosDeReserva.NotEnabled"/>, y Comercial sigue con el
/// crédito provisional como hoy (§5.3). No escribe nada. La real la reemplaza en <c>DependencyInjection</c>. (nuevo)
/// </summary>
public sealed class ReservasDeCarteraNoHabilitada : IReservasDeCartera
{
    /// <summary>Lo que responde la marca: sin número ni reserva.</summary>
    public static ResultadoDeReservaDto NoHabilitada { get; } = new(
        EstadosDeReserva.NotEnabled, null, null, null, EstadosDeReserva.NotEnabled, CarteraNoHabilitada.TextoPendiente);

    public Task<ResultadoDeReservaDto> ReservarAsync(SolicitudDeReserva solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        return Task.FromResult(NoHabilitada);
    }

    public Task<ResultadoDeReservaDto> LiberarAsync(SolicitudDeLiberacion solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        return Task.FromResult(NoHabilitada);
    }
}
