using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_ConnectedGroups</c> (feature 013, T0213; DT §7.10 C112; FR-041, SC-022): un grupo conectado registrado por el jefe de
/// cartera con su fundamento (vinculación económica o jurídica, codeudor común, vinculado). La concentración se mide por deudor
/// <b>y</b> por grupo con el mismo límite. Hecho (<see cref="IHechoInmutable"/>): sólo su terminación se escribe después.
/// </summary>
public class ConnectedGroup : AuditableEntity, IHechoInmutable
{
    public string Name { get; init; } = string.Empty;

    public ConnectedGroupBasis Basis { get; init; }

    /// <summary>Fundamento escrito del grupo; obligatorio.</summary>
    public string Grounds { get; init; } = string.Empty;

    public Guid? SupportAttachmentPublicId { get; init; }

    public int RegisteredByUserId { get; init; }

    public DateOnly ValidFrom { get; init; }

    [EscrituraUnica]
    public DateTime? EndedAt { get; set; }

    [EscrituraUnica]
    public string? EndReason { get; set; }
}
