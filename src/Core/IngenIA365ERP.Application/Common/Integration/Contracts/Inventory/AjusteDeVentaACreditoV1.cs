namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>AjusteDeVentaACredito</c> v1 (contracts/mensajes.md §8.2): a Cartera, uno por pago de crédito del original
/// afectado por una nota, devolución, anulación o reemplazo; <c>originEventKey</c>
/// <c>Confirmation:{originalCreditPaymentPublicId:N}</c>. Una nota que reintegra sólo por medios de contado no lo
/// emite.
/// </summary>
public sealed record AjusteDeVentaACreditoV1
{
    public const string Type = "AjusteDeVentaACredito";

    /// <summary>
    /// <c>CreditNote</c>, <c>Return</c>, <c>DebitNote</c>, <c>Voiding</c>, <c>VoidingByDianRejection</c> o
    /// <c>Replacement</c>: texto con valores cerrados.
    /// </summary>
    public string AdjustmentClass { get; init; } = string.Empty;

    /// <summary>Efecto con signo sobre el valor financiado: negativo reduce, positivo aumenta.</summary>
    public decimal Amount { get; init; }

    /// <summary><c>messageId</c> de la <see cref="VentaACreditoRegistradaV1"/> que ajusta.</summary>
    public Guid OriginalMessageId { get; init; }

    public Guid OriginalCreditPaymentPublicId { get; init; }

    /// <summary>Igual a <c>related</c> del sobre.</summary>
    public DocumentRefV1 OriginalDocument { get; init; } = new();

    public string PaymentMeansCode { get; init; } = string.Empty;

    /// <summary>En <c>DebitNote</c> y <c>Replacement</c>, las condiciones del valor nuevo.</summary>
    public CreditTermsV1? Terms { get; init; }

    public string? Reason { get; init; }

    /// <summary>El mismo sello del original: nunca cambia dentro de una cadena.</summary>
    public string AccountsReceivableRecordedBy { get; init; } = string.Empty;
}
