using IngenIA365ERP.Application.Common.Integration.Lending;

namespace IngenIA365ERP.Application.Tests.Common.Integration.Lending;

/// <summary>
/// Doble de <see cref="IReservasDeCartera"/> (feature 013, T0154, R118): registra reservas y liberaciones y responde lo que la prueba
/// configure (<see cref="RespuestaDeReserva"/>; por defecto, como la provisional, <c>NotEnabled</c>). Sólo vive en <c>tests/</c>.
/// </summary>
public sealed class ReservasDeCarteraDePrueba : IReservasDeCartera
{
    public ResultadoDeReservaDto RespuestaDeReserva { get; set; } = ReservasDeCarteraNoHabilitada.NoHabilitada;
    public ResultadoDeReservaDto RespuestaDeLiberacion { get; set; } = ReservasDeCarteraNoHabilitada.NoHabilitada;
    public Exception? Caida { get; set; }

    public List<SolicitudDeReserva> Reservas { get; } = [];
    public List<SolicitudDeLiberacion> Liberaciones { get; } = [];

    /// <summary>Una reserva concedida con número.</summary>
    public static ResultadoDeReservaDto Reservada(string numero = "CR-000123") =>
        new(EstadosDeReserva.Reserved, numero, Guid.NewGuid(), new DateOnly(2026, 12, 31), null, null);

    public Task<ResultadoDeReservaDto> ReservarAsync(SolicitudDeReserva solicitud, CancellationToken ct)
    {
        Reservas.Add(solicitud);
        if (Caida is not null) throw Caida;
        return Task.FromResult(RespuestaDeReserva);
    }

    public Task<ResultadoDeReservaDto> LiberarAsync(SolicitudDeLiberacion solicitud, CancellationToken ct)
    {
        Liberaciones.Add(solicitud);
        if (Caida is not null) throw Caida;
        return Task.FromResult(RespuestaDeLiberacion);
    }
}
