namespace IngenIA365ERP.Application.Common.Integration.Savings;

/// <summary>
/// El saldo de aportes y de ahorro permanente que Cartera consume (feature 013; contracts/interfaces.md §8.1; DT §2.9; D19). Lo implementa
/// la feature de Aportes y Ahorros; hasta entonces rige <see cref="ConsultaDeAportesNoDisponible"/>, que responde «no disponible» y Cartera
/// usa lo cargado por la plantilla <c>lending.contribution-balances</c> (<c>SaldoDeAportesVigente</c>). Cartera no lee
/// <c>Entities.Savings</c> ni <c>Application.Savings</c>: sólo este puerto. (nuevo)
/// </summary>
public interface IConsultaDeAportes
{
    Task<SaldosDeAportesDto> SaldosAsync(SolicitudDeSaldoDeAportes solicitud, CancellationToken ct);
}

/// <summary>Los saldos de unas personas a una fecha. (nuevo)</summary>
public sealed record SolicitudDeSaldoDeAportes(IReadOnlyList<Guid> PersonPublicIds, DateOnly Date);

/// <summary><see cref="Available"/> falso = «no disponible» (D19): quien llama usa lo cargado por plantilla. (nuevo)</summary>
public sealed record SaldosDeAportesDto(bool Available, IReadOnlyList<SaldoDeAportesDto> Balances)
{
    public static SaldosDeAportesDto NoDisponible { get; } = new(false, []);
}

/// <summary>El saldo de una persona con su fecha de corte. (nuevo)</summary>
public sealed record SaldoDeAportesDto(Guid PersonPublicId, decimal Contributions, decimal PermanentSavings, DateOnly AsOf);

/// <summary>
/// La implementación provisional (D19, fijada en DT §2.9): Aportes y Ahorros todavía no ofrece la consulta, así que responde «no
/// disponible» sin saldos. (nuevo)
/// </summary>
public sealed class ConsultaDeAportesNoDisponible : IConsultaDeAportes
{
    public Task<SaldosDeAportesDto> SaldosAsync(SolicitudDeSaldoDeAportes solicitud, CancellationToken ct) =>
        Task.FromResult(SaldosDeAportesDto.NoDisponible);
}
