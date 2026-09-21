using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll.Transactions;

/// <summary>
/// Archivo de dispersión bancaria generado desde la relación de pago de una corrida aprobada
/// (feature 010, US8; data-model §2.10). Inmutable (Principio XI): lo que se escribió queda
/// tal cual con el formato con que se escribió; «marcar enviado» deja pagados a los de sus
/// líneas en una sola acción y bloquea la reversión de la corrida como la marca manual; un
/// archivo <c>Generated</c> que no se envió se anula (<c>Voided</c>), nunca se borra.
/// </summary>
public class BankDisbursementFile : AuditableEntity
{
    public int PayrollRunId { get; set; }
    public PayrollRun? PayrollRun { get; set; }

    /// <summary>Banco pagador (el del formato, o el elegido si el formato es genérico).</summary>
    public int? BankId { get; set; }
    public Bank? Bank { get; set; }

    /// <summary>El formato vigente con que se escribió (<c>COR_BankFileFormats</c>).</summary>
    public int FormatId { get; set; }
    public BankFileFormat? Format { get; set; }

    /// <summary>Código del formato al generar, por si el formato cambia de nombre después.</summary>
    [MaxLength(20)]
    public string FormatCode { get; set; } = string.Empty;

    public BankDisbursementFileStatus Status { get; set; } = BankDisbursementFileStatus.Generated;

    public DateTime GeneratedAt { get; set; }

    [MaxLength(100)]
    public string GeneratedBy { get; set; } = string.Empty;

    /// <summary>La que va al archivo y a <c>PayrollPayment.PaidAt</c> salvo que «marcar enviado» traiga otra.</summary>
    public DateOnly PaymentDate { get; set; }

    /// <summary>Consecutivo diario de archivos de la cooperativa (origen <c>Sequence</c>).</summary>
    public int Sequence { get; set; }

    /// <summary>Referencia del lote (origen <c>BatchReference</c>); va a <c>PayrollPayment.Reference</c> si el banco no da otra.</summary>
    [MaxLength(60)]
    public string? Reference { get; set; }

    /// <summary>Cuenta contable bancaria de origen (cuenta del plan con banco), si el formato la usa.</summary>
    public int? SourceAccountId { get; set; }

    [MaxLength(30)]
    public string? SourceAccountNumber { get; set; }

    public int LineCount { get; set; }
    public decimal TotalAmount { get; set; }

    public int ExcludedCount { get; set; }

    /// <summary>Quiénes quedaron fuera y por qué (JSON de <c>{ employeePublicId, name, reasonCode, reason }</c>).</summary>
    public string? ExcludedJson { get; set; }

    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string FileSha256 { get; set; } = string.Empty;

    /// <summary>El archivo en <c>COR_Attachments</c> (<c>OwnerEntityType = "BankDisbursementFile"</c>).</summary>
    public Guid? FileAttachmentPublicId { get; set; }

    public DateTime? SentAt { get; set; }

    [MaxLength(100)]
    public string? SentBy { get; set; }

    /// <summary>Referencia que devolvió el banco al recibir el archivo.</summary>
    [MaxLength(60)]
    public string? BankReference { get; set; }

    [MaxLength(300)]
    public string? SentNotes { get; set; }

    public DateTime? VoidedAt { get; set; }

    [MaxLength(100)]
    public string? VoidedBy { get; set; }

    [MaxLength(300)]
    public string? VoidReason { get; set; }

    public ICollection<BankDisbursementFileLine> Lines { get; set; } = [];
}

/// <summary>Una línea de detalle del archivo: qué empleado, a qué cuenta, cuánto y el texto exacto que se escribió.</summary>
public class BankDisbursementFileLine : AuditableEntity
{
    public int FileId { get; set; }
    public BankDisbursementFile? File { get; set; }

    /// <summary>1..N en el detalle.</summary>
    public int LineNumber { get; set; }

    public int PayrollRunEmployeeId { get; set; }
    public PayrollRunEmployee? RunEmployee { get; set; }

    public int EmployeeId { get; set; }

    public int? DestinationBankId { get; set; }

    /// <summary>1 ahorros, 2 corriente.</summary>
    public int AccountType { get; set; }

    [MaxLength(25)]
    public string? AccountNumber { get; set; }

    public decimal Amount { get; set; }

    [MaxLength(600)]
    public string RecordText { get; set; } = string.Empty;

    /// <summary>La marca de pago que dejó «marcar enviado» (se enlaza por navegación para que nazca en la misma transacción).</summary>
    public int? PayrollPaymentId { get; set; }
    public PayrollPayment? Payment { get; set; }
}
