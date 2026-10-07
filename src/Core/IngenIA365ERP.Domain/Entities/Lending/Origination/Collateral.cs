using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_Collaterals</c> (feature 013; data-model §2.8; FR-026, FR-043, FR-082): garantías reales y de fondo de una solicitud o un
/// crédito. Sólo guarda <c>Pledge</c>, <c>Mortgage</c>, <c>GuaranteeFund</c> y <c>Contributions</c> (<c>CK_LND_Collaterals_Kind</c>):
/// el pagaré vive en <see cref="PromissoryNote"/> y el codeudor es parte. Al menos uno de <see cref="ApplicationId"/> y
/// <see cref="LoanId"/>. No es hecho.
/// </summary>
public class Collateral : AuditableEntity
{
    public CollateralKind Kind { get; set; }

    /// <summary>Constituyente de la garantía.</summary>
    public int OwnerPersonId { get; set; }

    public int? ApplicationId { get; set; }

    /// <summary>Se llena al desembolsar (nulo → valor).</summary>
    public int? LoanId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Value { get; set; }

    /// <summary>Valor admisible para la cobertura.</summary>
    public decimal AdmissibleValue { get; set; }

    public DateOnly? ValuationDate { get; set; }

    public int? AppraiserPersonId { get; set; }

    public string? PolicyNumber { get; set; }

    public int? PolicyInsurerPersonId { get; set; }

    public DateOnly? PolicyExpiresOn { get; set; }

    /// <summary>Matrícula o registro de prenda.</summary>
    public string? RegistrationReference { get; set; }

    /// <summary>Grado de la hipoteca (vivienda exige primero, K6b).</summary>
    public byte? MortgageRank { get; set; }

    /// <summary>Fondo de garantías: cobertura, como fracción.</summary>
    public decimal? FundCoverage { get; set; }

    /// <summary>Fondo de garantías: comisión, como fracción.</summary>
    public decimal? FundFee { get; set; }

    public CollateralStatus Status { get; set; } = CollateralStatus.Constituted;

    public DateOnly ConstitutedOn { get; set; }

    public DateOnly? ValidUntil { get; set; }

    public DateOnly? ReleasedOn { get; set; }

    public int? ReleasedByUserId { get; set; }

    /// <summary>La operación de pago que canceló, ya en firme (FR-082).</summary>
    public long? ReleaseOperationId { get; set; }

    /// <summary>K3 (garante que no consiente, FR-092).</summary>
    public DateOnly? ExtinguishedOn { get; set; }

    public string? ExtinguishReason { get; set; }

    public DateOnly? ExecutedOn { get; set; }
}
