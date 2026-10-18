using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Integration;

/// <summary>
/// <c>LND_ProcessRunErrors</c> (feature 013, C.3; data-model §22.2; FR-066, FR-016): un crédito (o una tanda) que falló en una
/// corrida. No es hecho: <see cref="ResolvedAt"/> lo pone el intento que después tiene éxito. Sin diff de auditoría (T67).
/// </summary>
[SinDiffDeAuditoria]
public class LendingProcessRunError : AuditableEntity
{
    public int ProcessRunId { get; set; }

    /// <summary>Nulo = error de la tanda o de un grupo (p. ej. usura faltante de una modalidad).</summary>
    public int? LoanId { get; set; }

    /// <summary>
    /// Feature 014 (T028; data-model §1.8): la cuenta de captación o aportes que falló en el paso <c>Captaciones</c>; excluyente con
    /// <see cref="LoanId"/> (<c>CK_LND_ProcessRunErrors_Subject</c>).
    /// </summary>
    public int? MemberAccountId { get; set; }

    public int BatchNumber { get; set; }

    public int AttemptNumber { get; set; }

    /// <summary><c>Replica</c>, <c>Reajuste</c>, <c>Causacion</c>, <c>Suspension</c>, <c>Anticipos</c>, <c>Bloqueos</c>, <c>Dimension</c>, <c>Cobranza</c>, <c>Verificacion</c>, <c>CierreLineas</c>, <c>CierreFinal</c>; feature 014: <c>Captaciones</c>.</summary>
    public string Step { get; set; } = string.Empty;

    public string ErrorCode { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public string? DetailJson { get; set; }

    /// <summary><c>Cartera.CreditoSinCausar</c> o <c>Cartera.ValorNacionalFaltante</c>.</summary>
    public Guid? AlertPublicId { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
