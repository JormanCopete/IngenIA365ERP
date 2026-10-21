using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Integration;

/// <summary>
/// Un contrato o tipo de mensaje que un participante adopta, con su versión y su vigencia
/// (<c>COR_IntegrationParticipantAdoptions</c>; feature 013, T16; data-model parte 3 §4.6). Hecho de sólo inserción: se cierra
/// con <see cref="ValidTo"/>, una sola vez.
/// </summary>
public class IntegrationParticipantAdoption : AuditableEntity, IHechoInmutable
{
    public int ParticipantId { get; init; }

    public IntegrationParticipant? Participant { get; init; }

    /// <summary>El contrato (<c>ProveedorDeObligaciones</c>) o el tipo de mensaje (<c>OrdenDeGiroEmitida</c>…).</summary>
    public string ContractName { get; init; } = string.Empty;

    public short Version { get; init; }

    public DateOnly ValidFrom { get; init; }

    [EscrituraUnica]
    public DateOnly? ValidTo { get; set; }

    public string Reason { get; init; } = string.Empty;

    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}
