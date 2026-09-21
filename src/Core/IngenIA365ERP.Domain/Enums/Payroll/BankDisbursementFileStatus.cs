namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>Ciclo del archivo de dispersión (feature 010, US8): generado → enviado (paga a todos) o anulado (nada pagado).</summary>
public enum BankDisbursementFileStatus
{
    Generated = 0,
    Sent = 1,
    Voided = 2,
}
