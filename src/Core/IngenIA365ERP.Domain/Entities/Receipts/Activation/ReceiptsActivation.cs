using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Receipts.Activation;

/// <summary>
/// <c>RCD_Activations</c> (feature 015, T0183b; R-40; TR42; FR-147, FR-150, FR-159; data-model §15.1): una activación de Recaudo. Nace con la <b>activación mínima</b>
/// del bloque 2 (<see cref="IsMinimal"/> = <c>true</c>, sin cuadre: <see cref="OpeningSquareId"/> nulo, sólo los requisitos básicos); la fase 19 suma el cuadre y
/// la deja en <c>false</c>, y en producción no hay activación mínima. Una sola vigente (la última). La FK de <see cref="OpeningSquareId"/> llega con el par
/// <c>RecaudoYTesoreriaPuestaEnMarcha</c>. (nuevo)
/// </summary>
public class ReceiptsActivation : AuditableEntity
{
    public DateOnly ActivatedOn { get; set; }

    /// <summary>La fecha de corte: la activación rige el día siguiente (FR-159).</summary>
    public DateOnly CutoffDate { get; set; }

    public int ActivatedByUserId { get; set; }

    /// <summary>Lo verificado al activar (la lista de requisitos con su estado).</summary>
    public string RequirementsJson { get; set; } = "[]";

    public int? OpeningSquareId { get; set; }

    public Guid? GoLiveChecklistPublicId { get; set; }

    public bool IsMinimal { get; set; }
}
