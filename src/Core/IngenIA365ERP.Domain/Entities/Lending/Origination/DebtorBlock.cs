using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

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

    // ------------------------------------------------------------------------------------------- comportamiento (T0388) --

    public const string CodigoBloqueado = "Lending.Debtor.Blocked";
    public const string CodigoYaLevantado = "Lending.Block.AlreadyLifted";

    /// <summary>Vigente: no se ha levantado. Impide originar y vender a crédito (FR-060).</summary>
    public bool Vigente => LiftedAt is null && !IsDeleted;

    /// <summary>Los que una persona pone a mano (con <c>Lending.Blocks.Manage</c> y motivo): el manual y el embargo.</summary>
    public static bool EsManual(DebtorBlockReason motivo) => motivo is DebtorBlockReason.Manual or DebtorBlockReason.Garnishment;

    /// <summary>
    /// Un bloqueo nuevo (data-model §2.12). Los manuales llevan motivo escrito; los automáticos los pone la operación que los causa
    /// (<see cref="IsAutomatic"/>) con su disparador en <see cref="SourceKey"/>.
    /// </summary>
    public static DebtorBlock Nuevo(int personId, DebtorBlockReason motivo, string sourceKey, DateOnly desde, string? notas, int? creadoPor,
        int? loanId = null, Guid? soporte = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        if (!Enum.IsDefined(motivo)) throw new ArgumentException("Motivo de bloqueo desconocido.", nameof(motivo));
        if (EsManual(motivo) && string.IsNullOrWhiteSpace(notas))
            throw new ArgumentException("Un bloqueo manual o un embargo lleva su motivo.", nameof(notas));
        return new DebtorBlock
        {
            PersonId = personId,
            Reason = motivo,
            IsAutomatic = !EsManual(motivo),
            SourceKey = sourceKey.Trim(),
            StartedOn = desde,
            Notes = notas?.Trim(),
            CreatedByUserId = creadoPor,
            LoanId = loanId,
            SupportAttachmentPublicId = soporte,
        };
    }

    /// <summary>El levantamiento, una sola vez (<see cref="EscrituraUnicaAttribute"/>, C42), con fecha, quién y motivo.</summary>
    public void Levantar(DateOnly fecha, DateTime instante, int? usuarioId, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (!Vigente)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoYaLevantado, "El bloqueo ya se levantó.", "liftedOn", LiftedOn));
        LiftedOn = fecha;
        LiftedAt = instante;
        LiftedByUserId = usuarioId;
        LiftReason = motivo.Trim();
    }
}
