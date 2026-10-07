using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_CreditApplicationParties</c> (feature 013; data-model §2.2; FR-035, FR-036, FR-043): solicitante, codeudores y avalistas de
/// la solicitud con los datos de capacidad de cada uno. Exactamente un <see cref="CreditPartyRole.Applicant"/>, que es el
/// <c>ApplicantPersonId</c> de la solicitud. Se edita en <c>Draft</c> y <c>Returned</c>; no es hecho.
/// </summary>
public class CreditApplicationParty : AuditableEntity
{
    public int ApplicationId { get; set; }

    public int PersonId { get; set; }

    public CreditPartyRole Role { get; set; }

    public int Order { get; set; }

    /// <summary>Ingreso mensual declarado con soporte (FR-036).</summary>
    public decimal? MonthlyIncome { get; set; }

    /// <summary>Gasto mensual declarado con soporte (FR-036).</summary>
    public decimal? MonthlyExpenses { get; set; }

    /// <summary>Cuotas con terceros, declaradas o de centrales.</summary>
    public decimal? ExternalInstallments { get; set; }
}
