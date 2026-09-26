namespace IngenIA365ERP.Domain.Enums.Integration;

/// <summary>
/// Destinos de los mensajes de integración (feature 012, T039; decisiones-transversales §2.5).
/// Son texto y no enum a propósito: un destino nuevo es un consumidor nuevo, no un cambio de
/// esquema, y el texto es el que queda guardado en cada entrega.
/// </summary>
public static class IntegrationDestinations
{
    public const string Accounting = "Accounting";
    public const string Lending = "Lending";
}
