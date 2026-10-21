using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Loans;

/// <summary>
/// <c>LND_InsolvencyProceedings</c> (feature 013; data-model §25.8; FR-098; T102; datos en K1, proceso en K3): proceso de insolvencia
/// del deudor. <c>Negotiating</c> suspende causación, cobro, mensajes de cobro y jurídico de <b>todos</b> sus créditos y bloquea
/// crédito nuevo; uno solo en negociación por persona. La calificación mantenida se deriva del último cierre anterior a
/// <see cref="StartDate"/>. No es hecho. La columna <c>AgreementNoveltyId</c> nace con K3 (<c>CarteraNovedades</c>), con el trámite por
/// pantalla: el fracaso (<see cref="RegistrarFracaso"/>) y el acuerdo (<see cref="CerrarConAcuerdo"/>).
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

    /// <summary>K3: la novedad que tramitó el acuerdo como reestructuración (FR-098).</summary>
    public int? AgreementNoveltyId { get; set; }

    // ------------------------------------------------------------------------------------------- comportamiento (K3) --

    public const string CodigoNoNegociando = "Lending.Insolvency.NotNegotiating";

    /// <summary>¿Suspende causación, cobro y crédito nuevo a <paramref name="dia"/>? En negociación y desde su inicio.</summary>
    public bool Suspende(DateOnly dia) => Status == InsolvencyProceedingStatus.Negotiating && !IsDeleted && dia >= StartDate;

    /// <summary><c>Negotiating</c> → <c>Failed</c>: desde <paramref name="fecha"/> rige la calificación que fija la norma.</summary>
    public void RegistrarFracaso(DateOnly fecha, DateTime instante, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ExigirNegociando();
        if (fecha < StartDate)
            throw new ArgumentException("La negociación no fracasa antes de empezar.", nameof(fecha));
        Status = InsolvencyProceedingStatus.Failed;
        FailedOn = fecha;
        ClosedAt = instante;
        CloseReason = motivo.Trim();
    }

    /// <summary><c>Negotiating</c> → <c>Agreement</c>: lo cierra la novedad de reestructuración que tramitó el acuerdo, al aplicarse.</summary>
    public void CerrarConAcuerdo(int novedadId, DateTime instante)
    {
        ExigirNegociando();
        Status = InsolvencyProceedingStatus.Agreement;
        AgreementNoveltyId = novedadId;
        ClosedAt = instante;
        CloseReason = "Acuerdo tramitado como reestructuración";
    }

    private void ExigirNegociando()
    {
        if (Status != InsolvencyProceedingStatus.Negotiating)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoNoNegociando,
                "El proceso de insolvencia ya no está en negociación.", "status", Status.ToString()));
    }
}
