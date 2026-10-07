using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_DebtorBlocks</c> (feature 013; data-model §2.12; FR-052, FR-060, FR-097, FR-142): bloqueos y embargos del deudor; impiden
/// originar y vender a crédito. Hecho de sólo inserción (<see cref="IHechoInmutable"/>, C42) con el levantamiento escrito una sola vez
/// (<see cref="EscrituraUnicaAttribute"/>). Un solo bloqueo vigente por (persona, motivo, disparador).
/// </summary>
public class DebtorBlock : AuditableEntity, IHechoInmutable
{
    public int PersonId { get; init; }

    /// <summary>Crédito que lo originó.</summary>
    public int? LoanId { get; init; }

    public DebtorBlockReason Reason { get; init; }

    public bool IsAutomatic { get; init; }

    /// <summary>Disparador (PublicId del crédito, del resultado de listas, del cupo…); en los manuales, el PublicId del bloqueo.</summary>
    public string SourceKey { get; init; } = string.Empty;

    public DateOnly StartedOn { get; init; }

    public string? Notes { get; init; }

    /// <summary>Soporte del embargo (adjunto, sin FK).</summary>
    public Guid? SupportAttachmentPublicId { get; init; }

    /// <summary>Nulo = proceso.</summary>
    public int? CreatedByUserId { get; init; }

    [EscrituraUnica]
    public DateOnly? LiftedOn { get; set; }

    [EscrituraUnica]
    public DateTime? LiftedAt { get; set; }

    [EscrituraUnica]
    public int? LiftedByUserId { get; set; }

    [EscrituraUnica]
    public string? LiftReason { get; set; }
}
