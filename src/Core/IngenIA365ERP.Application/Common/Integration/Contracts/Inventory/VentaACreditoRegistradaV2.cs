using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>VentaACreditoRegistrada</c> v2 (feature 013, T0175, T18; contracts/mensajes.md §12.2 de la 013): la v1
/// (<see cref="VentaACreditoRegistradaV1"/>) con dos campos más al final, <see cref="ReservedLoanNumber"/> y
/// <see cref="ReservationPublicId"/>. Es de Comercial (<c>OriginModule = INV</c>, destino <c>Lending</c>, sin proceso): Comercial emite
/// sólo v2 desde E-02 y Cartera acepta v1 y v2 mientras quede alguna entrega v1. <see cref="CreditLineCode"/> ya existía en v1 y llega
/// lleno cuando Cartera respondió la consulta. Mismo <c>Type</c> que la v1: la versión la dice el catálogo. (nuevo)
/// </summary>
public sealed record VentaACreditoRegistradaV2
{
    public const string Type = VentaACreditoRegistradaV1.Type;

    /// <summary><c>Associate</c> o <c>Customer</c>.</summary>
    public string ThirdPartyKind { get; init; } = string.Empty;

    /// <summary>Foto de la contraparte al confirmar; la persona es <c>personPublicId</c> del sobre.</summary>
    public PartySnapshotV1 Person { get; init; } = new();

    public CreditPaymentV1 CreditPayment { get; init; } = new();

    public decimal DocumentTotal { get; init; }

    public decimal AmountDue { get; init; }

    public CreditTermsV1 Terms { get; init; } = new();

    /// <summary>La línea de crédito elegida; nula si Cartera no respondió la consulta.</summary>
    public string? CreditLineCode { get; init; }

    /// <summary>Línea sugerida por el medio (texto, sin llave a <c>LND_*</c>).</summary>
    public string? SuggestedCreditLineCode { get; init; }

    /// <summary>«Pendiente de validar» (FR-061): falso sólo si Cartera validó la consulta.</summary>
    public bool PendingValidation { get; init; }

    /// <summary>Por qué quedó así. No confundir con <c>origin</c> del sobre.</summary>
    public CreditOrigin Origin { get; init; }

    /// <summary>La aprobación cuando la hubo; nunca el cajero (FR-010).</summary>
    public ApprovalRefV1? Approval { get; init; }

    /// <summary>El <c>EvidenceId</c> de la consulta auditada de cupo (FR-060), cuando Cartera respondió.</summary>
    public string? ConsultationEvidence { get; init; }

    /// <summary><c>Contabilidad</c> o <c>Cartera</c>, sellado al confirmar; nunca cambia en la cadena.</summary>
    public string AccountsReceivableRecordedBy { get; init; } = string.Empty;

    public string? PointOfSaleCode { get; init; }

    public string? SalesChannelCode { get; init; }

    /// <summary>El número reservado e impreso en la factura (FR-057); nulo si no hubo reserva (D26).</summary>
    public string? ReservedLoanNumber { get; init; }

    /// <summary>La reserva (<c>LND_LoanNumberReservations</c>); si no existe o no coincide, Cartera asigna otro número (C20).</summary>
    public Guid? ReservationPublicId { get; init; }
}
