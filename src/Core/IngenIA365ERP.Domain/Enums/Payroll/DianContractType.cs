namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// <c>TipoContrato</c> del documento de nómina electrónica (anexo técnico DIAN, tabla 5.5.2).
/// El <c>ContractType</c> heredado del sistema anterior es otro código y no se reinterpreta: la ficha
/// lleva los dos.
/// </summary>
public enum DianContractType
{
    FixedTerm = 1,
    Indefinite = 2,
    WorkOrLabor = 3,
    Apprenticeship = 4,
    Internship = 5,
}
