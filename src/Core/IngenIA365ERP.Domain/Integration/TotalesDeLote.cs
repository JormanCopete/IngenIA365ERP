namespace IngenIA365ERP.Domain.Integration;

/// <summary>
/// Lo que un lote deja al cerrarse, leído de sus entregas y de los comprobantes que devolvió el destino (feature 012, T477;
/// lo arma <c>CloseIntegrationBatchCommand</c>).
/// </summary>
public sealed record TotalesDeLote(
    int MessageCount,
    int DocumentCount,
    int ProcessedCount,
    int RejectedCount,
    int VoucherCount,
    decimal TotalDebit,
    decimal TotalCredit,
    string? ResultSummaryJson);
