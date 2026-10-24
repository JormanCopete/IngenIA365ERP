using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.Receipts;

/// <summary>
/// Lo que Contabilidad responde a Recaudo (feature 015, T0091, T0259; R-22, R-23; FR-004, FR-209; contracts/interfaces.md §3):
/// <list type="bullet">
/// <item>la validación previa por la matriz <c>ACC_ReceiptsPostingRules</c> (<see cref="EvaluacionPreviaDeModulo"/>), con la política
/// <c>Contabilidad.FaltaDeRegla</c> de <c>RCD</c>;</item>
/// <item>el estado del período es el de siempre (<see cref="GetAccountingPeriodStatusQuery"/>, el mismo que lee Cartera);</item>
/// <item>el saldo contable de un rol (caja por oficina, partidas, saldos a favor) a una fecha, por <see cref="SaldosPorRol"/>: sin regla para el rol, «sin dato».</item>
/// </list>
/// Recaudo no conoce nada de esto: sólo la interfaz (<c>RecaudoNoConoceContabilidad</c>). Las cuentas por cobrar comerciales llegan con R1. Sin evaluador (las
/// pruebas que construyen la clase a mano) responde como antes de la fase 16: «contabilizable» y saldo «sin dato». (nuevo)
/// </summary>
public sealed class ContabilidadParaRecaudo(
    ISender sender, IApplicationDbContext? db = null, ResolutorDeLaMatriz? resolutor = null, EvaluacionPreviaDeModulo? evaluacion = null)
    : IContabilidadParaRecaudo
{
    /// <summary>La política que informa la validación previa sin evaluador.</summary>
    public const string PoliticaProvisional = "SinMatriz";

    public Task<EvaluacionContableDto> EvaluarAsync(IReadOnlyList<CombinacionDeHecho> combinaciones, CancellationToken ct) =>
        evaluacion is null
            ? Task.FromResult(new EvaluacionContableDto(true, PoliticaProvisional, []))
            : evaluacion.EvaluarAsync(MatricesDeRecaudoYTesoreria.Recaudo, combinaciones, ct);

    public async Task<EstadoDelPeriodoDto> EstadoDelPeriodoAsync(DateOnly fecha, CancellationToken ct)
    {
        var estado = await sender.Send(new GetAccountingPeriodStatusQuery(fecha), ct);
        return estado.IsSuccess ? estado.Value : new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null);
    }

    public Task<IReadOnlyList<SaldoContableDto>> SaldoContableAsync(SolicitudDeSaldo solicitud, CancellationToken ct) =>
        db is null || resolutor is null
            ? Task.FromResult<IReadOnlyList<SaldoContableDto>>([])
            : SaldosPorRol.SaldoAsync(db, resolutor, MatricesDeRecaudoYTesoreria.Recaudo, solicitud, ct);

    public Task<IReadOnlyList<CuentaPorCobrarComercialDto>> CuentasPorCobrarComercialesAsync(Guid personPublicId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CuentaPorCobrarComercialDto>>([]);

    /// <summary>La completitud de la matriz de Recaudo (fase 19, T0303): la misma consulta que la pantalla de completitud.</summary>
    public async Task<CompletitudContableDeModuloDto> CompletitudDeLaMatrizAsync(DateOnly fecha, CancellationToken ct) =>
        await CompletitudDeModulo.LeerAsync(sender, Posting.ModuloContable.Recaudo, await EstadoDelPeriodoAsync(fecha, ct), fecha, ct);
}

/// <summary>Lo común de la completitud de Recaudo y Tesorería para su activación (fase 19, T0303). (nuevo)</summary>
public static class CompletitudDeModulo
{
    public static async Task<CompletitudContableDeModuloDto> LeerAsync(ISender sender, string modulo, EstadoDelPeriodoDto estado, DateOnly fecha, CancellationToken ct)
    {
        if (!estado.AccountingInitialized) return new CompletitudContableDeModuloDto(false, false, 100m, 0);
        var r = await sender.Send(new GetModuleCompletenessQuery(modulo, fecha), ct);
        if (r.IsFailure) return new CompletitudContableDeModuloDto(true, false, 0m, 0);
        return new CompletitudContableDeModuloDto(true, r.Value.NotPosted, r.Value.Percent, r.Value.Total - r.Value.Complete);
    }
}
