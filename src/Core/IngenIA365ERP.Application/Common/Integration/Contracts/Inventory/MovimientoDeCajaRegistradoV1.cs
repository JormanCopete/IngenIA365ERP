using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>MovimientoDeCajaRegistrado</c> v1 (contracts/mensajes.md §6.14): un retiro, un ingreso de base o una
/// reclasificación entre medios, sin productos (FR-100).
/// </summary>
public sealed record MovimientoDeCajaRegistradoV1
{
    public const string Type = "MovimientoDeCajaRegistrado";

    public string Operation { get; init; } = "MovimientoDeCaja";

    public CashMovementKind MovementKind { get; init; }

    /// <summary>Medio que se mueve; en una reclasificación, el que sale.</summary>
    public string PaymentMeansCode { get; init; } = string.Empty;

    public PaymentMeansClass PaymentMeansClass { get; init; }

    /// <summary>Medio al que se reclasifica; sólo en la reclasificación.</summary>
    public string? DestinationPaymentMeansCode { get; init; }

    public string PointOfSaleCode { get; init; } = string.Empty;

    public string CashRegisterCode { get; init; } = string.Empty;

    public Guid CashSessionPublicId { get; init; }

    /// <summary>La otra punta; nula en la reclasificación. Es la <c>ReasonCode</c> del rol <c>CajaDestino</c>.</summary>
    public CashMovementDestination? Destination { get; init; }

    public string? DestinationPointOfSaleCode { get; init; }

    public string? DestinationCashRegisterCode { get; init; }

    public Guid? DestinationCashSessionPublicId { get; init; }

    public decimal Amount { get; init; }

    public string Reason { get; init; } = string.Empty;
}
