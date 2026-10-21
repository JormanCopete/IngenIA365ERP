namespace IngenIA365ERP.Domain.Enums.Integration;

/// <summary>
/// Clase de un participante del registro de integración (feature 013, T16, PM-07; data-model parte 3 §9.2).
/// Se guarda como int en <c>COR_IntegrationParticipants.Class</c>; un valor nunca se renumera.
/// </summary>
public enum IntegrationParticipantClass
{
    Receipts = 1,
    Payroll = 2,
    Contributions = 3,
    Treasury = 4,
    Test = 5,
    Accounting = 6,
    Lending = 7,
    Inventory = 8,

    /// <summary>Feature 014 (US10, T098): Asociados, destino de <c>RetiroDeAsociadoAprobado</c> (participante <c>ASO</c>).</summary>
    Associates = 9,
}
