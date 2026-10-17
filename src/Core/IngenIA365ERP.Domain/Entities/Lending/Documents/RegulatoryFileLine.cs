using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Documents;

/// <summary>
/// <c>LND_RegulatoryFileLines</c> (feature 013, K5, US16; data-model §28.2; FR-123, FR-137): lo reportado —o excluido, con su motivo— por
/// obligación y titular (deudor o codeudor) en cada archivo de una central. Es la base de las novedades del archivo siguiente y de los retiros de
/// lo reportado sin aviso. Sólo hay líneas de archivos <c>CreditBureau</c>: las filas del SICSES no se guardan, se derivan de las líneas del cierre.
/// Hecho de sólo inserción (<see cref="IHechoInmutable"/>, <c>bigint</c>, C43) y <see cref="SinDiffDeAuditoriaAttribute"/>: la auditoría lleva un
/// evento resumen por archivo (C46). (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class RegulatoryFileLine : AuditableEntityLong, IHechoInmutable
{
    /// <summary>Largo del motivo de exclusión (<c>nvarchar(40)</c>).</summary>
    public const int LargoDelMotivo = 40;

    /// <summary>Largo del código de estado reportado (<c>nvarchar(20)</c>).</summary>
    public const int LargoDelEstado = 20;

    public int RegulatoryFileId { get; init; }

    public int LineNumber { get; init; }

    public int LoanId { get; init; }

    /// <summary>El titular reportado: deudor o codeudor, cada uno con su autorización.</summary>
    public int PersonId { get; init; }

    /// <summary>Falso = excluido con <see cref="ExclusionReason"/>.</summary>
    public bool Included { get; init; }

    /// <summary>Uno de <see cref="MotivosDeExclusionDeCentral"/>; nulo si va incluido.</summary>
    public string? ExclusionReason { get; init; }

    public string? ReportedStatusCode { get; init; }

    public int DaysPastDue { get; init; }

    public decimal? BalanceReported { get; init; }

    /// <summary>Dato negativo: exige aviso previo <c>Complete</c> del episodio (adaptadores-y-archivos.md §5.5).</summary>
    public bool IsNegative { get; init; }

    public DateOnly? EpisodeStartDate { get; init; }

    /// <summary>El aviso válido del episodio.</summary>
    public int? PriorNoticeId { get; init; }

    /// <summary>Reclamo en trámite: va incluido con su marca.</summary>
    public bool HasClaimInProcess { get; init; }

    /// <summary>Retiro de un reporte negativo hecho sin aviso (migrado o de un archivo anterior).</summary>
    public bool IsWithdrawalOfUnnoticedReport { get; init; }
}

/// <summary>Los motivos de exclusión de una línea de central (data-model §28.2; T0990).</summary>
public static class MotivosDeExclusionDeCentral
{
    public const string NoReportAuthorization = "NoReportAuthorization";
    public const string NoPriorNoticeEvidence = "NoPriorNoticeEvidence";
    public const string DeceasedClaimInProcess = "DeceasedClaimInProcess";
    public const string Insolvency = "Insolvency";
    public const string PaymasterArrearsWithinThreshold = "PaymasterArrearsWithinThreshold";

    public static readonly IReadOnlyList<string> Todos =
        [NoReportAuthorization, NoPriorNoticeEvidence, DeceasedClaimInProcess, Insolvency, PaymasterArrearsWithinThreshold];
}
