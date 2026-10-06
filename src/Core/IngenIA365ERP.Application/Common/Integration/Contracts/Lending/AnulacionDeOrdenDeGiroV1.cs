namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>AnulacionDeOrdenDeGiro</c> v1 (interfaces.md §4.2, mensajes.md §11.4; <c>CAR → Treasury</c>): la anulación de una orden por un
/// usuario con <c>Lending.DisbursementOrders.Void</c>. Origen la misma orden, clave <c>Confirmation:{orden:N}</c> (C12).
/// </summary>
public sealed record AnulacionDeOrdenDeGiroV1
{
    public const string Type = "AnulacionDeOrdenDeGiro";

    public Guid OrderPublicId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}
