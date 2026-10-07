using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Schedules;

/// <summary>
/// <c>LND_RepaymentScheduleVersions</c> (feature 013; data-model §4.4; FR-023, FR-034, FR-067, FR-075, T29): materialización del plan
/// de pagos por versión. Proyección inmutable (<see cref="IHechoInmutable"/>): la autoridad es la reconstrucción (T23).
/// <c>GeneradorDePlan</c> regenera desde la decisión; Σ capital de las cuotas = <see cref="PrincipalBase"/> al centavo (SC-017). La
/// numeración de cuotas es estable entre versiones. Rige desde <see cref="EffectiveValueDate"/> (no es <c>ValidFrom</c>).
/// </summary>
public class RepaymentScheduleVersion : AuditableEntity, IHechoInmutable
{
    public int LoanId { get; init; }

    /// <summary>Navegación para que <c>RegistroDeCartera</c> enlace en el mismo guardado una fila nueva con su <c>Loan</c> todavía sin Id.</summary>
    public IngenIA365ERP.Domain.Entities.Lending.Loans.Loan? Loan { get; init; }

    public int VersionNumber { get; init; }

    public int TermsVersionId { get; init; }

    /// <summary>Navegación para que <c>RegistroDeCartera</c> enlace en el mismo guardado una fila nueva con su <c>LoanTermsVersion</c> todavía sin Id.</summary>
    public IngenIA365ERP.Domain.Entities.Lending.Loans.LoanTermsVersion? TermsVersion { get; init; }

    /// <summary>Tramo, abono con elección, reajuste, nota de Comercial, novedad o reverso que la regeneró (<c>LND_LoanOperations</c>, bigint).</summary>
    public long OperationId { get; init; }

    /// <summary>Navegación para que <c>RegistroDeCartera</c> enlace en el mismo guardado una fila nueva con su <c>LoanOperation</c> todavía sin Id.</summary>
    public IngenIA365ERP.Domain.Entities.Lending.Operations.LoanOperation? Operation { get; init; }

    public DateOnly EffectiveValueDate { get; init; }

    public DateTime RegisteredAt { get; init; }

    /// <summary>Capital sobre el que se generó.</summary>
    public decimal PrincipalBase { get; init; }

    /// <summary>Primera cuota regenerada; las anteriores se copian tal cual.</summary>
    public int FirstRegeneratedInstallment { get; init; }

    public int InstallmentCount { get; init; }

    /// <summary>Plan cargado en la migración: capital por cuota como vino (D5).</summary>
    public bool IsMigratedPlan { get; init; }

    public string RulesVersion { get; init; } = string.Empty;

    public string InputsSha256 { get; init; } = string.Empty;
}
