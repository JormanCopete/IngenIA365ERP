using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La fecha contable de un hecho de Cartera (feature 013, T0228, C106; T36, FR-012; contracts/contabilidad.md §8). Cartera la calcula
/// <b>al emitir</b> y la pone en <c>Origin.OperationDate</c>; Contabilidad fecha el comprobante con ella y nunca la corre.
///
/// <para>
/// Esta es la <b>mitad contable</b>, sobre <see cref="IContabilidadParaCartera.EstadoDelPeriodoAsync"/>: la fecha valor si su mes está
/// abierto; si Contabilidad lo cerró, el primer día del primer mes abierto desde él; un mes que no existe no se salta (la fecha queda en
/// él y el consumidor reintenta); sin contabilidad iniciada, la fecha valor (el hecho sale <c>NoPasa</c>). US3 y US7 la completan y
/// US9 suma la mitad de los cierres de Cartera (un mes con cierre vigente de Cartera también se salta), sin cambiar la firma.
/// </para>
/// <para>
/// Es una lectura en proceso sobre la misma base: si falla, falla la operación (Principio IX), sin fecha supuesta. (nuevo)
/// </para>
/// </summary>
public sealed class FechaContable(IContabilidadParaCartera contabilidad)
{
    /// <summary>La fecha contable de un hecho con fecha valor <paramref name="fechaValor"/>.</summary>
    public async Task<Result<DateOnly>> CalcularAsync(DateOnly fechaValor, CancellationToken ct)
    {
        var estado = await contabilidad.EstadoDelPeriodoAsync(fechaValor, ct);
        if (estado.IsFailure) return Result.Failure<DateOnly>(estado.Error);

        var e = estado.Value;
        if (!e.AccountingInitialized || e.State != EstadoDePeriodoContable.Closed) return Result.Success(fechaValor);
        return Result.Success(e.FirstOpenMonthFrom ?? new DateOnly(fechaValor.Year, fechaValor.Month, 1).AddMonths(1));
    }
}
