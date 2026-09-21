namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>Origen de un descuento propuesto en la definitiva (FR-018a): préstamo de la cooperativa (Cartera), libranza de un tercero, otro.</summary>
public enum SettlementDeductionKind
{
    CooperativeLoan = 1,
    ThirdPartyLibranza = 2,
    Other = 3,
}
