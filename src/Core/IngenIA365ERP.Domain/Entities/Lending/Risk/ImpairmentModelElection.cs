using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Risk;

/// <summary>
/// <c>LND_ImpairmentModelElections</c> (feature 013, K6a, T1031; data-model §23.8; FR-105; R15): la adopción o el desistimiento
/// <b>voluntario</b> del modelo de pérdida esperada por una cooperativa no obligada, con el punto del acta del consejo (sujeto
/// <c>ImpairmentModelChange</c>), el estudio de impacto y el aviso a la Superintendencia. Rige desde <see cref="ValidFrom"/> —el primer cierre al
/// que aplica— y nunca recalcula cierres anteriores; adopción y desistimiento alternan. La obligación (por el perfil o por la regla) no es una
/// elección: la deriva <c>SelectorDeModeloDeDeterioro</c>. Hecho: no se edita ni se borra. (nuevo)
/// </summary>
public class ImpairmentModelElection : AuditableEntity, IHechoInmutable
{
    public ImpairmentModelElectionKind Kind { get; init; }

    /// <summary>El primer cierre al que aplica (<c>UK</c>).</summary>
    public DateOnly ValidFrom { get; init; }

    /// <summary>Los segmentos de pérdida esperada cubiertos, separados por coma (por defecto los tres).</summary>
    public string SegmentCodes { get; init; } = string.Empty;

    /// <summary>El punto del acta del consejo (<c>LND_SessionItems</c>).</summary>
    public int SessionItemId { get; init; }

    public Guid ImpactStudyAttachmentPublicId { get; init; }

    public DateOnly SupervisorNoticeDate { get; init; }

    public Guid SupervisorNoticeAttachmentPublicId { get; init; }

    /// <summary>La antelación del aviso de la <c>ReglaDePerdidaEsperada</c> vigente, sellada (pregunta E2).</summary>
    public int NoticeLeadDays { get; init; }

    /// <summary>La copia de <c>ReglaDePerdidaEsperada</c> de la que salió la antelación (sin FK).</summary>
    public Guid RegulatoryDocumentPublicId { get; init; }

    /// <summary>El resultado de la verificación del historial de mora al registrar (<c>[]</c> para adoptar: con faltantes no se adopta).</summary>
    public string IncompleteHistoryCheckJson { get; init; } = "[]";

    public string Reason { get; init; } = string.Empty;

    /// <summary><c>Lending.ImpairmentModel.Manage</c>.</summary>
    public int RegisteredByUserId { get; init; }

    public DateTime RegisteredAt { get; init; }

    /// <summary>Los segmentos como lista.</summary>
    public IReadOnlyList<string> Segmentos =>
        SegmentCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
