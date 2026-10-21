using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.Lending;

/// <summary>
/// El adaptador real de <see cref="IContabilidadParaCartera"/> (feature 013, T0650; contracts/contabilidad.md §16.1; molde
/// <c>ContabilidadParaInventario</c>): lo único de Contabilidad que Cartera conoce. Corre en proceso, sin HTTP, y delega en las consultas de
/// §1.1 por <see cref="ISender"/> para que pasen por la validación de siempre —la de <see cref="GetLendingAccountBalancesQuery"/> rechaza en
/// <c>CombinacionDeSaldo.Role</c> un rol de resultado o desconocido (C29)—; <see cref="CuentasBancariasAsync"/> reutiliza
/// <see cref="ListBankAccountsQuery"/>. No pide permisos: lo usa Cartera dentro de una operación ya autorizada. Reemplaza en DI a
/// <c>ContabilidadParaCarteraProvisional</c> con la misma firma (C106). (nuevo)
/// </summary>
public sealed class ContabilidadParaCartera(ISender sender) : IContabilidadParaCartera
{
    public Task<Result<ResultadoDeEvaluacionContableDto>> EvaluarAsync(IReadOnlyList<MensajeContableDto> hechos, CancellationToken ct) =>
        sender.Send(new EvaluateLendingPostingQuery(hechos), ct);

    public Task<Result<SaldosMapeadosDeCarteraDto>> SaldosDeCuentasMapeadasAsync(
        DateOnly corte, IReadOnlyList<CombinacionDeSaldo> combinaciones, CancellationToken ct) =>
        sender.Send(new GetLendingAccountBalancesQuery(corte, combinaciones), ct);

    public Task<Result<EstadoDelPeriodoDto>> EstadoDelPeriodoAsync(DateOnly fecha, CancellationToken ct) =>
        sender.Send(new GetAccountingPeriodStatusQuery(fecha), ct);

    public Task<Result<IndicadoresDeLaEntidadDto>> IndicadoresDeLaEntidadAsync(DateOnly fecha, CancellationToken ct) =>
        sender.Send(new GetEntityIndicatorsQuery(fecha), ct);

    public async Task<Result<IReadOnlyList<CuentaBancariaDeOrigenDto>>> CuentasBancariasAsync(Guid? bancoPublicId, CancellationToken ct)
    {
        var r = await sender.Send(new ListBankAccountsQuery(bancoPublicId), ct);
        if (r.IsFailure) return Result.Failure<IReadOnlyList<CuentaBancariaDeOrigenDto>>(r.Error);
        return Result.Success<IReadOnlyList<CuentaBancariaDeOrigenDto>>(r.Value
            .Select(c => new CuentaBancariaDeOrigenDto(c.AccountPublicId, c.Code, c.Name, c.BankPublicId, c.BankName, c.BankAccountNumber, c.BankTransferCode))
            .ToList());
    }
}
