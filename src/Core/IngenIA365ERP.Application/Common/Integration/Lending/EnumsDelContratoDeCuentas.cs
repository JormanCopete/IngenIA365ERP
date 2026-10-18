namespace IngenIA365ERP.Application.Common.Integration.Lending;

// Enums del contrato de cuentas (feature 014, T039; contracts/contrato-de-cuentas.md §3.2). Tipo en español, valores en inglés idénticos a los del
// dominio de captaciones (CH40): el contrato traduce por número. Los vigila LosEnumsDelContratoNumeranComoElDominio. ClaseDeRecaudador.Treasury = 6
// vive con los demás enums de la 013 (EnumsDelContratoDeCartera.cs, research E04).

/// <summary>La naturaleza de una cuenta (equivale a <c>LineNature</c> sin <c>Placement</c>). (nuevo)</summary>
public enum NaturalezaDeCuenta
{
    Deposit = 2,
    Contribution = 3,
}

/// <summary>La clase de una cuenta de captación (equivale a <c>DepositClass</c>). (nuevo)</summary>
public enum ClaseDeCaptacion
{
    Demand = 1,
    Contractual = 2,
    Permanent = 3,
    TermDeposit = 4,
}

/// <summary>El estado de una cuenta (equivale a <c>MemberAccountStatus</c>). (nuevo)</summary>
public enum EstadoDeCuenta
{
    Opening = 1,
    Active = 2,
    Inactive = 3,
    Matured = 4,
    Closing = 5,
    Closed = 6,
}

/// <summary>El concepto de saldo de una cuenta (equivale a <c>MemberAccountConcept</c>). (nuevo)</summary>
public enum ConceptoDeCuenta
{
    Principal = 1,
    InterestPayable = 2,
    Withholding = 3,
    Gmf = 4,
    ContributionMinimum = 5,
    ContributionOrdinary = 6,
    ContributionExtraordinary = 7,
    ContributionRevaluation = 8,
    ChargeReceivable = 9,
    Bonus = 10,
}

/// <summary>El papel de la persona en la cuenta (equivale a <c>MemberAccountPartyRole</c>). (nuevo)</summary>
public enum RolEnLaCuenta
{
    Holder = 1,
    CoHolder = 2,
    Signatory = 3,
    LegalRepresentative = 4,
    Beneficiary = 5,
}

/// <summary>
/// Cómo se aplica una consignación (§3.2): al saldo, a las cuotas exigibles en orden de vencimiento, a cuotas nombradas, o el fondeo exacto de un CDAT
/// en apertura. (nuevo)
/// </summary>
public enum ModalidadDeConsignacion
{
    Free = 1,
    Installments = 2,
    SpecificInstallments = 3,
    TermDepositFunding = 4,
}
