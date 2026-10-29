namespace IngenIA365ERP.Domain.Enums.Integration;

/// <summary>
/// Cómo quedó un mensaje consumido por un módulo con recibo propio (feature 015, T0135, T0174; data-model §6.1, §10.1): <c>TES_InboundMessages</c> y
/// <c>RCD_InboundMessages</c>, molde de <c>LND_InboundMessages</c>. El recibo es único por <c>MessagePublicId</c>: repetir el mensaje no cambia nada. (nuevo)
/// </summary>
public enum InboundMessageOutcome
{
    Processed = 1,
    Rejected = 2,
}
