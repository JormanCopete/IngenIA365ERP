using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.Receipts;

/// <summary>
/// Lo que Contabilidad responde a Recaudo (feature 015, T0091; R-22; FR-004; contracts/interfaces.md §3), <b>provisional hasta la fase 16 (US8)</b>, que trae
/// la matriz <c>ACC_ReceiptsPostingRules</c>, la validación previa y los saldos por concepto:
/// <list type="bullet">
/// <item>el estado del período es el de siempre (<see cref="GetAccountingPeriodStatusQuery"/>, el mismo que lee Cartera): sin contabilidad iniciada los hechos
/// de Recaudo nacen <c>NotPosted</c>;</item>
/// <item>la validación previa responde «contabilizable» con la política <see cref="PoliticaProvisional"/>: sin matriz no hay combinación que evaluar;</item>
/// <item>el saldo contable responde vacío: el cuadre diario queda con el saldo contable «sin dato» y lo dice, no inventa un cero.</item>
/// </list>
/// Recaudo no conoce nada de esto: sólo la interfaz (<c>RecaudoNoConoceContabilidad</c>). (nuevo)
/// </summary>
public sealed class ContabilidadParaRecaudo(ISender sender) : IContabilidadParaRecaudo
{
    /// <summary>La política que informa la validación previa mientras no exista la matriz de Recaudo.</summary>
    public const string PoliticaProvisional = "SinMatriz";

    public Task<EvaluacionContableDto> EvaluarAsync(IReadOnlyList<CombinacionDeHecho> combinaciones, CancellationToken ct) =>
        Task.FromResult(new EvaluacionContableDto(true, PoliticaProvisional, []));

    public async Task<EstadoDelPeriodoDto> EstadoDelPeriodoAsync(DateOnly fecha, CancellationToken ct)
    {
        var estado = await sender.Send(new GetAccountingPeriodStatusQuery(fecha), ct);
        return estado.IsSuccess ? estado.Value : new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null);
    }

    public Task<IReadOnlyList<SaldoContableDto>> SaldoContableAsync(SolicitudDeSaldo solicitud, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SaldoContableDto>>([]);

    public Task<IReadOnlyList<CuentaPorCobrarComercialDto>> CuentasPorCobrarComercialesAsync(Guid personPublicId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CuentaPorCobrarComercialDto>>([]);
}
