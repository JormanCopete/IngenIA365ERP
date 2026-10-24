using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>
/// La consulta a listas de las personas de una operación de caja (feature 015, T0115; FR-093, FR-203): el gancho de <c>ConfirmacionDeRecibo</c> que completa la
/// fase 15 (US7, cumplimiento) sobre la consulta común de listas. Hasta entonces responde <see cref="ScreeningOutcomeR.Clear"/>: la operación no se detiene por
/// listas. (nuevo)
/// </summary>
public interface IListasDeRecaudo
{
    Task<ScreeningOutcomeR> EvaluarAsync(IReadOnlyCollection<int> personIds, Guid operacionPublicId, CancellationToken ct);
}

/// <summary>El gancho sin consulta hasta la fase 15. (nuevo)</summary>
public sealed class ListasDeRecaudoSinConsulta : IListasDeRecaudo
{
    public Task<ScreeningOutcomeR> EvaluarAsync(IReadOnlyCollection<int> personIds, Guid operacionPublicId, CancellationToken ct) =>
        Task.FromResult(ScreeningOutcomeR.Clear);
}
