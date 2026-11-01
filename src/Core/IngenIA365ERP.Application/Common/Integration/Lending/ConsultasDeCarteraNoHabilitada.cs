namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// La implementación por defecto de <see cref="IConsultasDeCartera"/> (feature 012, I3, T652; T32) (nuevo): siempre
/// <see cref="CarteraNoHabilitada"/> y la validación siempre <c>Pending</c> con el texto «Pendiente: el destino aún no está
/// disponible». Ya no se registra: desde la feature 013 la registrada es la de Cartera, que responde lo mismo antes de activar; ésta
/// queda para las pruebas que no montan Cartera.
/// </summary>
public sealed class ConsultasDeCarteraNoHabilitada : IConsultasDeCartera
{
    public Task<EstadoCrediticioDto> EstadoCrediticioAsync(ConsultaCrediticia consulta, CancellationToken ct) =>
        Task.FromResult<EstadoCrediticioDto>(CarteraNoHabilitada.Instancia);

    public Task<EstadoDeValidacionDto> EstadoDeValidacionAsync(Guid messagePublicId, CancellationToken ct) =>
        Task.FromResult(new EstadoDeValidacionDto(EstadosDeValidacion.Pending, null, CarteraNoHabilitada.TextoPendiente));

    /// <summary>Feature 014 (T043; contrato-de-cuentas §5): las dos naturalezas sin activar y listas vacías.</summary>
    public Task<ProductosDeCaptacionDto> ProductosDeCaptacionAsync(ConsultaDeProductosDeCaptacion consulta, CancellationToken ct) =>
        Task.FromResult(ProductosDeCaptacionDto.NoHabilitada);
}
