using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_NumberSequences</c> (feature 013, T0210, T0207; data-model §1.10; F-13, PM-10, T34, C48): los numeradores de Cartera. Sólo
/// <c>NumeradorDeCartera</c> los lee y escribe, al final del orden de cerrojos; la fila que falta la crea el numerador. No es hecho.
/// </summary>
public class LendingNumberSequence : AuditableEntity
{
    public LendingNumberKind Kind { get; set; }

    /// <summary><c>''</c> o el ámbito: clase de operación por nombre, órgano (<c>PublicId:N</c>), clase de documento.</summary>
    public string ScopeKey { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public long NextValue { get; set; } = 1;

    public byte MinDigits { get; set; }
}
