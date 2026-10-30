using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>
/// La consulta a listas de las personas de una operación de caja (feature 015, T0115; FR-093, FR-203): el gancho de <c>ConfirmacionDeRecibo</c> que completa la
/// fase 15 (US14, cumplimiento) sobre la consulta común de listas: lo implementa <c>Receipts.Compliance.ListasDeRecaudo</c> (la coincidencia exacta es vinculante y
/// bloquea; la posible, no vinculante). (nuevo)
/// </summary>
public interface IListasDeRecaudo
{
    Task<ScreeningOutcomeR> EvaluarAsync(IReadOnlyCollection<int> personIds, Guid operacionPublicId, CancellationToken ct);
}
