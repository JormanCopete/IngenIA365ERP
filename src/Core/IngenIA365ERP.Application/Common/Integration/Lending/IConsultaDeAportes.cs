namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// El saldo de aportes y de ahorro permanente de unas personas (feature 013, D19; feature 014, T041; research R20; contrato-de-cuentas §4). Nació en la
/// 013 como puerto <b>consumido</b> (en <c>Common.Integration.Savings</c>) y la 014 lo muda aquí y lo <b>ofrece</b>: la implementación real es la de
/// la Cartera (<c>Application/Lending/Integration/ConsultaDeAportesDeCartera</c>, T084), registrada por cooperativa desde la activación de aportes;
/// antes rige <see cref="ConsultaDeAportesNoDisponible"/>, que responde «no disponible» y la Cartera usa lo cargado por la plantilla
/// <c>lending.contribution-balances</c> (<c>SaldoDeAportesVigente</c>). Quien llama no lee tablas de la Cartera: sólo este puerto.
///
/// <para>Versión 2 desde la 014: suma <see cref="SaldoDeAportesDto.Minimum"/> y <see cref="SaldoDeAportesDto.InstallmentsInArrears"/> como propiedades
/// <c>init</c> al final, así que quien compila contra la 1 no se rompe y las ve en cero; un consumidor que dependa de ellas declara la 2.</para>
/// </summary>
public interface IConsultaDeAportes
{
    /// <summary>2 desde la 014: suma <c>Minimum</c> e <c>InstallmentsInArrears</c>; la 1 sigue para quien no la adopte.</summary>
    const int VersionDelContrato = 2;

    Task<SaldosDeAportesDto> SaldosAsync(SolicitudDeSaldoDeAportes solicitud, CancellationToken ct);
}

/// <summary>Los saldos de unas personas a una fecha. (nuevo)</summary>
public sealed record SolicitudDeSaldoDeAportes(IReadOnlyList<Guid> PersonPublicIds, DateOnly Date);

/// <summary><see cref="Available"/> falso = «no disponible» (D19): quien llama usa lo cargado por plantilla. (nuevo)</summary>
public sealed record SaldosDeAportesDto(bool Available, IReadOnlyList<SaldoDeAportesDto> Balances)
{
    public static SaldosDeAportesDto NoDisponible { get; } = new(false, []);
}

/// <summary>El saldo de una persona con su fecha de corte; la versión 2 suma el mínimo y las cuotas vencidas. (nuevo)</summary>
public sealed record SaldoDeAportesDto(Guid PersonPublicId, decimal Contributions, decimal PermanentSavings, DateOnly AsOf)
{
    /// <summary>v2: mínimo no reducible del asociado a la fecha.</summary>
    public decimal Minimum { get; init; }

    /// <summary>v2: cuotas exigibles vencidas y no pagadas (aportes y permanente) a la fecha.</summary>
    public int InstallmentsInArrears { get; init; }
}

/// <summary>
/// La implementación provisional (D19, fijada en DT §2.9 de la 013): para una cooperativa sin aportes activados responde «no disponible» sin saldos.
/// (nuevo)
/// </summary>
public sealed class ConsultaDeAportesNoDisponible : IConsultaDeAportes
{
    public Task<SaldosDeAportesDto> SaldosAsync(SolicitudDeSaldoDeAportes solicitud, CancellationToken ct) =>
        Task.FromResult(SaldosDeAportesDto.NoDisponible);
}
