namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// La implementación por defecto de <see cref="IConsultasDeCartera"/> (feature 012, I3, T652; T32) (nuevo): siempre
/// <see cref="CarteraNoHabilitada"/> y la validación siempre <c>Pending</c> con el texto «Pendiente: el destino aún no está
/// disponible (IC)». Se registra en <c>DependencyInjection</c> mientras no exista el destino <c>Lending</c> ni fecha en
/// <c>Cartera.IntegracionHabilitadaDesde</c>; la entrega IC (T661, bloqueada por D-02) la reemplaza por la real.
/// </summary>
public sealed class ConsultasDeCarteraNoHabilitada : IConsultasDeCartera
{
    public Task<EstadoCrediticioDto> EstadoCrediticioAsync(ConsultaCrediticia consulta, CancellationToken ct) =>
        Task.FromResult<EstadoCrediticioDto>(CarteraNoHabilitada.Instancia);

    public Task<EstadoDeValidacionDto> EstadoDeValidacionAsync(Guid messagePublicId, CancellationToken ct) =>
        Task.FromResult(new EstadoDeValidacionDto(EstadosDeValidacion.Pending, null, CarteraNoHabilitada.TextoPendiente));
}
