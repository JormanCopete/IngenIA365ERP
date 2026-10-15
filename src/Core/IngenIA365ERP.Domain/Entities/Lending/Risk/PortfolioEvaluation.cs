using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Risk;

/// <summary>
/// <c>LND_PortfolioEvaluations</c> (feature 013, K3, T0872; data-model §23.7; FR-102): la evaluación de cartera de un corte (semestral o anual
/// según el tipo de entidad), recomendada por el comité de riesgos y aprobada por el consejo, cada uno con su punto de acta:
/// <code>
/// Draft ──punto del comité──▶ Recommended ──punto del consejo──▶ Approved ──aplicar──▶ Applied
/// Draft · Recommended ──punto que la rechaza──▶ Rejected
/// Draft ──descartar──▶ Discarded
/// </code>
/// Sus líneas (<see cref="PortfolioEvaluationLoan"/>) se editan sólo en <c>Draft</c>. Aplicada, es la candidata <c>Evaluation</c> de cada
/// crédito evaluado desde el cierre del mes de efecto hasta la siguiente evaluación aplicada; nunca reescribe cierres anteriores. No es hecho.
/// Su comportamiento lo completa US14. (nuevo)
/// </summary>
public class PortfolioEvaluation : AuditableEntity
{
    public const string CodigoNoBorrador = "Lending.PortfolioEvaluation.NotDraft";
    public const string CodigoEstadoInvalido = "Lending.PortfolioEvaluation.InvalidStatus";

    /// <summary>Numerador de Cartera; único.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Corte de la evaluación.</summary>
    public DateOnly CutoffDate { get; init; }

    /// <summary>Año del mes cuyo cierre refleja la recalificación.</summary>
    public short EffectiveYear { get; init; }

    /// <summary>Mes cuyo cierre refleja la recalificación.</summary>
    public byte EffectiveMonth { get; init; }

    public PortfolioEvaluationStatus Status { get; private set; } = PortfolioEvaluationStatus.Draft;

    /// <summary>Los criterios de la norma aplicados.</summary>
    public string CriteriaJson { get; init; } = "{}";

    /// <summary><c>Lending.PortfolioEvaluations.Prepare</c>.</summary>
    public int PreparedByUserId { get; init; }

    public DateTime PreparedAt { get; init; }

    /// <summary>El punto del comité de riesgos.</summary>
    public int? RecommendationSessionItemId { get; private set; }

    /// <summary>El punto del consejo.</summary>
    public int? ApprovalSessionItemId { get; private set; }

    /// <summary><c>Lending.PortfolioEvaluations.Apply</c>.</summary>
    public int? AppliedByUserId { get; private set; }

    public DateTime? AppliedAt { get; private set; }

    public int LoanCount { get; private set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0872) --

    private static readonly IReadOnlyDictionary<PortfolioEvaluationStatus, PortfolioEvaluationStatus[]> Transiciones =
        new Dictionary<PortfolioEvaluationStatus, PortfolioEvaluationStatus[]>
        {
            [PortfolioEvaluationStatus.Draft] = [PortfolioEvaluationStatus.Recommended, PortfolioEvaluationStatus.Rejected, PortfolioEvaluationStatus.Discarded],
            [PortfolioEvaluationStatus.Recommended] = [PortfolioEvaluationStatus.Approved, PortfolioEvaluationStatus.Rejected],
            [PortfolioEvaluationStatus.Approved] = [PortfolioEvaluationStatus.Applied],
        };

    public static bool PuedePasar(PortfolioEvaluationStatus desde, PortfolioEvaluationStatus hacia) =>
        Transiciones.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);

    /// <summary>¿Rige como candidata al cierre de <paramref name="anio"/>-<paramref name="mes"/>? Aplicada y con su mes de efecto alcanzado.</summary>
    public bool RigeAlCierre(int anio, int mes) =>
        Status == PortfolioEvaluationStatus.Applied && anio * 12 + mes >= EffectiveYear * 12 + EffectiveMonth;

    /// <summary>Nulo en borrador; si no, <see cref="CodigoNoBorrador"/>.</summary>
    public ProblemaDeCartera? ExigirBorrador() => Status == PortfolioEvaluationStatus.Draft
        ? null
        : ProblemaDeCartera.Con(CodigoNoBorrador, "La evaluación ya no admite cambios: sus líneas se editan sólo en borrador.", "status", Status.ToString());

    public ProblemaDeCartera? PuedeIrA(PortfolioEvaluationStatus hacia) => PuedePasar(Status, hacia)
        ? null
        : new ProblemaDeCartera(CodigoEstadoInvalido, $"La evaluación está en «{Status}» y no puede pasar a «{hacia}».",
            new Dictionary<string, object?> { ["status"] = Status.ToString(), ["requested"] = hacia.ToString() });

    /// <summary>Recalcula el conteo con las líneas; sólo en borrador.</summary>
    public void Recalcular(IEnumerable<PortfolioEvaluationLoan> lineas)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        ReglaDeCarteraException.SiHay(ExigirBorrador());
        LoanCount = lineas.Count();
    }

    /// <summary>Edita una línea de esta evaluación; sólo en borrador.</summary>
    public void EditarLinea(PortfolioEvaluationLoan linea, RatingCategory propuesta, RatingSubcategory? subcategoria, string justificacion,
        string? puntajesJson = null, Guid? soporte = null)
    {
        ArgumentNullException.ThrowIfNull(linea);
        ReglaDeCarteraException.SiHay(ExigirBorrador());
        if (linea.PortfolioEvaluationId != Id)
            throw new ArgumentException("La línea es de otra evaluación.", nameof(linea));
        linea.Fijar(propuesta, subcategoria, justificacion, puntajesJson, soporte);
    }

    /// <summary><c>Draft</c> → <c>Recommended</c> por el punto del comité de riesgos.</summary>
    public void Recomendar(int puntoDelComiteId)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(PortfolioEvaluationStatus.Recommended));
        if (LoanCount == 0)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido, "La evaluación lleva al menos un crédito.", "loanCount", 0));
        Status = PortfolioEvaluationStatus.Recommended;
        RecommendationSessionItemId = puntoDelComiteId;
    }

    /// <summary><c>Recommended</c> → <c>Approved</c> por el punto del consejo.</summary>
    public void Aprobar(int puntoDelConsejoId)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(PortfolioEvaluationStatus.Approved));
        Status = PortfolioEvaluationStatus.Approved;
        ApprovalSessionItemId = puntoDelConsejoId;
    }

    /// <summary>El comité (desde <c>Draft</c>) o el consejo (desde <c>Recommended</c>) la rechazan con su punto de acta.</summary>
    public void Rechazar(int puntoDeActaId)
    {
        var desde = Status;
        ReglaDeCarteraException.SiHay(PuedeIrA(PortfolioEvaluationStatus.Rejected));
        Status = PortfolioEvaluationStatus.Rejected;
        if (desde == PortfolioEvaluationStatus.Draft) RecommendationSessionItemId = puntoDeActaId;
        else ApprovalSessionItemId = puntoDeActaId;
    }

    /// <summary><c>Approved</c> → <c>Applied</c>.</summary>
    public void Aplicar(int usuarioId, DateTime instante)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(PortfolioEvaluationStatus.Applied));
        Status = PortfolioEvaluationStatus.Applied;
        AppliedByUserId = usuarioId;
        AppliedAt = instante;
    }

    /// <summary><c>Draft</c> → <c>Discarded</c>.</summary>
    public void Descartar()
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(PortfolioEvaluationStatus.Discarded));
        Status = PortfolioEvaluationStatus.Discarded;
    }
}
