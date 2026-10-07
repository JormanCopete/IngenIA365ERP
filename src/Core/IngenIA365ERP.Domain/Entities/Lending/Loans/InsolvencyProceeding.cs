using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Loans;

/// <summary>
/// <c>LND_InsolvencyProceedings</c> (feature 013; data-model §25.8; FR-098; T102; datos en K1, proceso en K3): proceso de insolvencia
/// del deudor. <c>Negotiating</c> suspende causación, cobro, mensajes de cobro y jurídico de <b>todos</b> sus créditos y bloquea
/// crédito nuevo; uno solo en negociación por persona. La calificación mantenida se deriva del último cierre anterior a
/// <see cref="StartDate"/>. No es hecho. La columna <c>AgreementNoveltyId</c> nace con K3.
/// </summary>
public class InsolvencyProceeding : AuditableEntity
{
    /// <summary>El deudor; único filtrado a <c>Negotiating</c> y vivo.</summary>
    public int PersonId { get; set; }

    /// <summary>Obligatorio salvo en lo migrado por la parte <c>blocks</c> (C64), que se completa al tramitarlo en K3.</summary>
    public InsolvencyRegime? Regime { get; set; }

    /// <summary>Juzgado, Superintendencia o centro de conciliación; obligatoria salvo en lo migrado (C64).</summary>
    public string? Authority { get; set; }

    public string? CaseNumber { get; set; }

    /// <summary>Inicio de la negociación: desde aquí se suspenden causación y cobro y se mantiene la calificación.</summary>
    public DateOnly StartDate { get; set; }

    public Guid? SupportAttachmentPublicId { get; set; }

    public InsolvencyProceedingStatus Status { get; set; } = InsolvencyProceedingStatus.Negotiating;

    /// <summary>Desde aquí rige la calificación que fija la norma.</summary>
    public DateOnly? FailedOn { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? CloseReason { get; set; }

    public int? MigrationLoadId { get; set; }
}
