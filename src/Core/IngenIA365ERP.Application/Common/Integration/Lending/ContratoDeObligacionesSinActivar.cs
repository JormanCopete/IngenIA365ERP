namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// La implementación provisional de <see cref="IContratoDeObligaciones"/> mientras Cartera no exista en el despliegue (feature 013,
/// T0152; T85, contracts/interfaces.md §3.17): listar y simular responden <see cref="ResultadoDelContrato.Answered"/> con el valor
/// vacío y el código <see cref="CodigoNoActivada"/>; aplicar, reversar y resolver responden
/// <see cref="ResultadoDelContrato.Rejected"/> con ese código en <b>cada</b> ítem. No escribe nada (quien recauda decide qué hacer
/// con el rechazo). La real la reemplaza en <c>DependencyInjection</c> cuando nace <c>CarteraNucleo</c>. (nuevo)
/// </summary>
public sealed class ContratoDeObligacionesSinActivar : IContratoDeObligaciones
{
    /// <summary>El código de «Cartera sin activar» (data-model §10.1).</summary>
    public const string CodigoNoActivada = "Lending.NotActivated";

    /// <summary>El mensaje de data-model §10.1.</summary>
    public const string MensajeNoActivada =
        "Cartera todavía no está activada en esta cooperativa. Esta operación estará disponible cuando se registre la activación.";

    public Task<RespuestaDelContratoDto<ListadoDeObligacionesDto>> ListarAsync(SolicitudDeListado solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var vacio = new ListadoDeObligacionesDto(EstadoDeCartera.NotActivated, solicitud.PersonPublicId, solicitud.Date, []);
        return Task.FromResult(new RespuestaDelContratoDto<ListadoDeObligacionesDto>(ResultadoDelContrato.Answered, CodigoNoActivada, MensajeNoActivada, vacio));
    }

    public Task<RespuestaDelContratoDto<SimulacionDePagoDto>> SimularAsync(SolicitudDeSimulacion solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        return Task.FromResult(new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Answered, CodigoNoActivada, MensajeNoActivada, null));
    }

    public Task<RespuestaDeAplicacionDto> AplicarAsync(SolicitudDeAplicacion solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var pagos = solicitud.Payments.Select(_ => Rechazo<ComprobanteDeAplicacionDto>()).ToList();
        var noDescontados = solicitud.NotDeducted.Select(_ => Rechazo<NoDescontadoRegistradoDto>()).ToList();
        return Task.FromResult(new RespuestaDeAplicacionDto(pagos, noDescontados));
    }

    public Task<RespuestaDelContratoDto<ComprobanteDeReversoDto>> ReversarAsync(SolicitudDeReverso solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        return Task.FromResult(Rechazo<ComprobanteDeReversoDto>());
    }

    public Task<RespuestaDelContratoDto<ComprobanteDeResolucionDto>> ResolverSaldoAFavorAsync(SolicitudDeResolucion solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        return Task.FromResult(Rechazo<ComprobanteDeResolucionDto>());
    }

    private static RespuestaDelContratoDto<T> Rechazo<T>() =>
        new(ResultadoDelContrato.Rejected, CodigoNoActivada, MensajeNoActivada, default);
}
