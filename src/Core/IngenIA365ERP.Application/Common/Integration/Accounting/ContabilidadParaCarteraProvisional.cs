using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration.Accounting;

/// <summary>
/// El <see cref="IContabilidadParaCartera"/> provisional (feature 013, T0227, C106): el comportamiento mínimo que necesitan las piezas
/// de Cartera que se usan antes de US7 (la fecha contable, el modo de paso, la validación previa y la activación de prueba). Lee
/// la 009 en proceso y no escribe nada:
/// <list type="bullet">
/// <item><see cref="EstadoDelPeriodoAsync"/>: el estado <b>real</b> del mes sobre <c>ACC_Setup</c> y <c>ACC_Periods</c> (sin contabilidad
/// iniciada, «no iniciada»), y el primer mes abierto desde la fecha (un mes que no existe no se salta, contabilidad.md §8);</item>
/// <item><see cref="CuentasBancariasAsync"/>: las cuentas del plan con banco, por el mismo handler de <see cref="ListBankAccountsQuery"/>;</item>
/// <item><see cref="EvaluarAsync"/>: todavía no hay matriz de Cartera, así que <b>no aplica</b>: responde
/// <see cref="CodigoEvaluacionNoDisponible"/>, que <c>ValidacionPreviaContableDeCartera</c> sella <c>NotApplicable</c> sin hallazgos;</item>
/// <item><see cref="SaldosDeCuentasMapeadasAsync"/>: vacío, con todas las combinaciones sin resolver;</item>
/// <item><see cref="IndicadoresDeLaEntidadAsync"/>: «no disponible» (ceros y sin indicadores registrados).</item>
/// </list>
/// US7 la reemplaza en <c>DependencyInjection</c> por el adaptador real <c>ContabilidadParaCartera</c>, con la misma firma. (nuevo)
/// </summary>
public sealed class ContabilidadParaCarteraProvisional(IApplicationDbContext db) : IContabilidadParaCartera
{
    /// <summary>
    /// El código con que el provisional dice «no hay con qué evaluar»: la matriz de Cartera nace con US7. No es «sin respuesta»: la
    /// validación previa lo sella <c>NotApplicable</c>. El adaptador real nunca lo devuelve.
    /// </summary>
    public const string CodigoEvaluacionNoDisponible = "Accounting.LendingRules.NotAvailable";

    /// <summary>El tope de meses que recorre la búsqueda del primer mes abierto (diez años; nunca se llega en la práctica).</summary>
    private const int MesesMaximos = 120;

    public Task<Result<ResultadoDeEvaluacionContableDto>> EvaluarAsync(IReadOnlyList<MensajeContableDto> hechos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(hechos);
        return Task.FromResult(Result.Failure<ResultadoDeEvaluacionContableDto>(new Error(CodigoEvaluacionNoDisponible,
            "La matriz contable de Cartera todavía no existe en este despliegue: la evaluación no aplica.")));
    }

    public Task<Result<SaldosMapeadosDeCarteraDto>> SaldosDeCuentasMapeadasAsync(
        DateOnly corte, IReadOnlyList<CombinacionDeSaldo> combinaciones, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(combinaciones);
        return Task.FromResult(Result.Success(new SaldosMapeadosDeCarteraDto([], Enumerable.Range(0, combinaciones.Count).ToList())));
    }

    public async Task<Result<EstadoDelPeriodoDto>> EstadoDelPeriodoAsync(DateOnly fecha, CancellationToken ct)
    {
        var iniciada = await db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted, ct);
        if (!iniciada) return Result.Success(new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null));

        var inicioDelMes = new DateOnly(fecha.Year, fecha.Month, 1);
        var periodos = await db.AccountingPeriods.AsNoTracking()
            .Where(p => !p.IsDeleted && p.EndDate >= inicioDelMes)
            .OrderBy(p => p.StartDate)
            .Select(p => new { p.StartDate, p.EndDate, p.Status })
            .ToListAsync(ct);

        var delMes = periodos.FirstOrDefault(p => p.StartDate <= fecha && p.EndDate >= fecha);
        var estado = delMes is null ? EstadoDePeriodoContable.NotFound
            : delMes.Status == PeriodStatus.Open ? EstadoDePeriodoContable.Open
            : EstadoDePeriodoContable.Closed;

        // El primer mes abierto desde la fecha: se saltan los cerrados; uno que no existe no se salta (la fecha contable queda en él).
        DateOnly? primero = null;
        var mes = inicioDelMes;
        for (var i = 0; i < MesesMaximos && primero is null; i++, mes = mes.AddMonths(1))
        {
            var periodo = periodos.FirstOrDefault(p => p.StartDate <= mes && p.EndDate >= mes);
            if (periodo is null || periodo.Status == PeriodStatus.Open) primero = mes;
        }

        return Result.Success(new EstadoDelPeriodoDto(true, estado, primero));
    }

    public Task<Result<IndicadoresDeLaEntidadDto>> IndicadoresDeLaEntidadAsync(DateOnly fecha, CancellationToken ct) =>
        Task.FromResult(Result.Success(new IndicadoresDeLaEntidadDto(fecha, 0m, 0m, null, null)));

    public async Task<Result<IReadOnlyList<CuentaBancariaDeOrigenDto>>> CuentasBancariasAsync(Guid? bancoPublicId, CancellationToken ct)
    {
        var r = await new ListBankAccountsQueryHandler(db).Handle(new ListBankAccountsQuery(bancoPublicId), ct);
        if (r.IsFailure) return Result.Failure<IReadOnlyList<CuentaBancariaDeOrigenDto>>(r.Error);
        return Result.Success<IReadOnlyList<CuentaBancariaDeOrigenDto>>(r.Value
            .Select(c => new CuentaBancariaDeOrigenDto(c.AccountPublicId, c.Code, c.Name, c.BankPublicId, c.BankName, c.BankAccountNumber,
                c.BankTransferCode))
            .ToList());
    }
}
