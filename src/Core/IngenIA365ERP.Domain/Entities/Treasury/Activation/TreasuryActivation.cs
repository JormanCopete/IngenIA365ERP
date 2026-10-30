using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Treasury.Activation;

/// <summary>
/// <c>TES_Activations</c> (feature 015, T0183b; R-40; TR42; FR-147, FR-150, FR-159; data-model §15.1): una activación de Tesorería, con la misma forma que
/// <c>RCD_Activations</c>: la mínima del bloque 2 (<see cref="IsMinimal"/>, sin cuadre) y la completa de la fase 19. (nuevo)
/// </summary>
public class TreasuryActivation : AuditableEntity
{
    public DateOnly ActivatedOn { get; set; }

    public DateOnly CutoffDate { get; set; }

    public int ActivatedByUserId { get; set; }

    public string RequirementsJson { get; set; } = "[]";

    public int? OpeningSquareId { get; set; }

    public Guid? GoLiveChecklistPublicId { get; set; }

    public bool IsMinimal { get; set; }
}
