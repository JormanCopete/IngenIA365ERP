using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>VentaACreditoRegistrada</c> v1 (contracts/mensajes.md §8.1): a Cartera, <c>DeliveryMode.Always</c>, uno por
/// pago de clase <c>AssociateCredit</c> o <c>CustomerCredit</c> (cada uno será una obligación);
/// <c>originEventKey</c> <c>Confirmation:{paymentPublicId:N}</c>. Inventario no guarda saldos ni cuotas (FR-062).
/// </summary>
public sealed record VentaACreditoRegistradaV1
{
    public const string Type = "VentaACreditoRegistrada";

    /// <summary><c>Associate</c> o <c>Customer</c>.</summary>
    public string ThirdPartyKind { get; init; } = string.Empty;

    /// <summary>Foto de la contraparte al confirmar; la persona es <c>personPublicId</c> del sobre.</summary>
    public PartySnapshotV1 Person { get; init; } = new();

    public CreditPaymentV1 CreditPayment { get; init; } = new();

    public decimal DocumentTotal { get; init; }

    public decimal AmountDue { get; init; }

    public CreditTermsV1 Terms { get; init; } = new();

    /// <summary>Línea de crédito de Cartera; nula mientras IC esté pendiente.</summary>
    public string? CreditLineCode { get; init; }

    /// <summary>Línea sugerida por el medio (texto, sin llave a <c>LND_*</c>).</summary>
    public string? SuggestedCreditLineCode { get; init; }

    /// <summary>«Pendiente de validar» (FR-061); siempre <c>true</c> en crédito provisional.</summary>
    public bool PendingValidation { get; init; }

    /// <summary>Por qué quedó así. No confundir con <c>origin</c> del sobre.</summary>
    public CreditOrigin Origin { get; init; }

    /// <summary>La aprobación cuando la hubo; nunca el cajero (FR-010).</summary>
    public ApprovalRefV1? Approval { get; init; }

    /// <summary>Evento de auditoría con la respuesta de Cartera (FR-060), cuando se consultó (IC).</summary>
    public string? ConsultationEvidence { get; init; }

    /// <summary><c>Contabilidad</c> (forzado mientras IC esté pendiente) o <c>Cartera</c>, sellado al confirmar.</summary>
    public string AccountsReceivableRecordedBy { get; init; } = string.Empty;

    public string? PointOfSaleCode { get; init; }

    public string? SalesChannelCode { get; init; }
}

/// <summary>Foto de la contraparte (§8.1).</summary>
public sealed record PartySnapshotV1
{
    public string TaxIdType { get; init; } = string.Empty;

    public string TaxId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}

/// <summary>El pago de crédito y el valor financiado (§8.1).</summary>
public sealed record CreditPaymentV1
{
    public Guid PaymentPublicId { get; init; }

    public int LineNumber { get; init; }

    public string PaymentMeansCode { get; init; } = string.Empty;

    public PaymentMeansClass PaymentMeansClass { get; init; }

    public decimal Amount { get; init; }
}
