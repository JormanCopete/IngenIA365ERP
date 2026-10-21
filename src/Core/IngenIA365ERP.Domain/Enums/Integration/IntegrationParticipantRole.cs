namespace IngenIA365ERP.Domain.Enums.Integration;

/// <summary>
/// Papel de un participante del registro de integración (feature 013, T16, PM-07; data-model parte 3 §9.2): recaudador
/// de obligaciones o destino de mensajes. Se guarda como int en <c>COR_IntegrationParticipants.Role</c>.
/// </summary>
public enum IntegrationParticipantRole
{
    ObligationsCollector = 1,
    MessageDestination = 2,
}
