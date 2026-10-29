namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>RemesaDelPuntoDeVentaDespachada</c> v1 (feature 015, segunda ronda del 2026-10-27, D-l; FR-236; enmienda E-03 a la 012): el efectivo del cierre del día del punto de
/// venta que sale hacia el banco en una remesa de Tesorería. Débito al rol <c>CajaDestino</c> con la causa <see cref="Causa"/> (la contadora la lleva a la misma cuenta del
/// efectivo en tránsito de Tesorería) y crédito al <c>MedioDePago</c> del efectivo del punto. Tesorería cancela el tránsito contra el banco al confirmar la remesa
/// (<c>RemesaConfirmada</c>). Comercial no conoce cuentas ni a Tesorería: sólo el <c>PublicId</c> de la remesa que le devolvió el puerto. (nuevo)
/// </summary>
public sealed record RemesaDelPuntoDeVentaDespachadaV1
{
    public const string Type = "RemesaDelPuntoDeVentaDespachada";

    /// <summary>La causa del rol <c>CajaDestino</c> (su <c>ReasonCode</c>).</summary>
    public const string Causa = "Remittance";

    public string Operation { get; init; } = "RemesaDelPuntoDeVenta";

    public string PointOfSaleCode { get; init; } = string.Empty;

    public Guid DayClosePublicId { get; init; }

    public DateOnly OperatingDate { get; init; }

    /// <summary>El medio de efectivo del punto del que sale el dinero.</summary>
    public string PaymentMeansCode { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid RemittancePublicId { get; init; }

    public string RemittanceNumber { get; init; } = string.Empty;

    public string? BagSeal { get; init; }
}
