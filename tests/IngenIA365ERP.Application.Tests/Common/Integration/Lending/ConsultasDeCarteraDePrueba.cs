using IngenIA365ERP.Application.Common.Integration.Lending;

namespace IngenIA365ERP.Application.Tests.Common.Integration.Lending;

/// <summary>
/// Doble de <see cref="IConsultasDeCartera"/> (feature 013, T0116, R118): responde el estado crediticio que la prueba configure (por
/// defecto, como la provisional, <see cref="CarteraNoHabilitada"/>) y registra cada consulta. <see cref="Caida"/> hace lanzar la consulta
/// (Cartera no responde). Sólo vive en <c>tests/</c>.
/// </summary>
public sealed class ConsultasDeCarteraDePrueba : IConsultasDeCartera
{
    public EstadoCrediticioDto Respuesta { get; set; } = CarteraNoHabilitada.Instancia;
    public Exception? Caida { get; set; }
    public List<ConsultaCrediticia> Consultas { get; } = [];

    /// <summary>Una respuesta activa con cupo, que admite la venta y trae su evidencia.</summary>
    public static EstadoCrediticioDto Activa(decimal cupo = 5_000_000m, string evidencia = "evt-0001") =>
        new(EstadosCrediticios.Active, cupo, [new LineaDeCreditoDto("CONSUMO", "Consumo", 12, 12)], evidencia) { TotalQuota = cupo, AllowsSale = true };

    public Task<EstadoCrediticioDto> EstadoCrediticioAsync(ConsultaCrediticia consulta, CancellationToken ct)
    {
        Consultas.Add(consulta);
        if (Caida is not null) throw Caida;
        return Task.FromResult(Respuesta);
    }

    public Task<EstadoDeValidacionDto> EstadoDeValidacionAsync(Guid messagePublicId, CancellationToken ct) =>
        Task.FromResult(new EstadoDeValidacionDto(EstadosDeValidacion.Pending, null, null));

    /// <summary>Feature 014 (T043): los productos de captación que la prueba configure; por defecto, como la provisional.</summary>
    public ProductosDeCaptacionDto Productos { get; set; } = ProductosDeCaptacionDto.NoHabilitada;

    public Task<ProductosDeCaptacionDto> ProductosDeCaptacionAsync(ConsultaDeProductosDeCaptacion consulta, CancellationToken ct) =>
        Task.FromResult(Productos);
}
