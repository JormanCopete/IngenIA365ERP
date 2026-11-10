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

    // Feature 013 (DT §2.6, §2.12; data-model parte 3 §9.3; PM-06, CH9, CH10): destinos de los hechos de Cartera.
    public const string Inventory = "Inventory";
    public const string Treasury = "Treasury";
    public const string Receipts = "Receipts";

    // Feature 014 (US10, T098; contracts/mensajes.md §1.4, research C16, enmienda E-04): Asociados recibe el retiro aprobado en la Cartera.
    public const string Associates = "Associates";

    // Feature 020 (R12, R18; contracts/mensajes.md de la 020): Transporte escucha pagos como destino opcional y Conectores entrega al
    // sistema externo la solicitud de un servicio pagado.
    public const string Transport = "Transport";
    public const string Connectors = "Connectors";
}
