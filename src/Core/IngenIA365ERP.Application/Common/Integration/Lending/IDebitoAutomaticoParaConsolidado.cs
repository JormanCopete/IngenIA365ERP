namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>Lo debitado automáticamente de las cuentas para pagar créditos un día. (nuevo)</summary>
public sealed record DebitoAutomaticoDelDiaDto(DateOnly Date, decimal Amount, int Operations);

/// <summary>
/// La lectura del débito automático de Cartera para el consolidado de Recaudo (feature 015, R1, T0400; D26; FR-081): las operaciones <c>DirectDebit</c> vivas (no
/// reversadas) por fecha valor. El débito automático sigue en Cartera; Recaudo sólo lo muestra. Síncrona, de sólo lectura. (nuevo)
/// </summary>
public interface IDebitoAutomaticoParaConsolidado
{
    Task<IReadOnlyList<DebitoAutomaticoDelDiaDto>> PorDiaAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
}
