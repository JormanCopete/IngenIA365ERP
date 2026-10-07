using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_BureauQueries</c> (feature 013; data-model §2.7; FR-006, FR-036, T73): una consulta a centrales de riesgo con su evidencia
/// (autorización, canal sellado, informe). Hecho de sólo inserción (<see cref="IHechoInmutable"/>, C42). Sin autorización vigente con
/// evidencia no se consulta.
/// </summary>
public class BureauQuery : AuditableEntity, IHechoInmutable
{
    public int PersonId { get; init; }

    public BureauQueryPurpose Purpose { get; init; }

    public int? ApplicationId { get; init; }

    /// <summary>Central consultada.</summary>
    public string BureauCode { get; init; } = string.Empty;

    /// <summary>Canal sellado.</summary>
    public string ChannelCode { get; init; } = string.Empty;

    /// <summary>Fila de <c>CMP_HabeasDataConsents</c> con finalidad <c>CreditBureauQuery</c> y evidencia (T69), sin FK.</summary>
    public Guid AuthorizationConsentPublicId { get; init; }

    public ExternalOutcome Outcome { get; init; }

    public decimal? Score { get; init; }

    public int? NegativeMarks { get; init; }

    /// <summary>JSON con el resumen del informe.</summary>
    public string? Summary { get; init; }

    /// <summary>Adjunto <c>LendingBureauReport</c> (no borrable), sin FK.</summary>
    public Guid? ReportAttachmentPublicId { get; init; }

    public bool IsSimulated { get; init; }

    public DateTime QueriedAt { get; init; }

    public int QueriedByUserId { get; init; }

    /// <summary>Fecha de la consulta + <c>Centrales.DiasDeVigenciaDeConsulta</c>.</summary>
    public DateOnly ValidUntil { get; init; }
}
