using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Payroll.ElectronicPayroll;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll.Transactions;

/// <summary>
/// Documento soporte de pago de nómina electrónica (102) o nota de ajuste (103) de un
/// empleado y un mes (feature 010, US6; data-model §2.9). Inmutable: un rechazado se
/// conserva y se genera otro con número nuevo; un aceptado cuya fuente cambió se ajusta con
/// una nota y pasa a <c>Superseded</c> sólo cuando la nota es aceptada. El número nunca se
/// reutiliza. Nace en N2 (D-12); la lógica llega con N3.
/// </summary>
public class ElectronicPayrollDocument : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public short Year { get; set; }
    public byte Month { get; set; }

    /// <summary>102 individual, 103 ajuste.</summary>
    public short DocumentType { get; set; }

    public AdjustmentNoteType? NoteType { get; set; }

    public int? AdjustsDocumentId { get; set; }
    public ElectronicPayrollDocument? AdjustsDocument { get; set; }

    public int? ReplacedByDocumentId { get; set; }

    public int NumberingRangeId { get; set; }
    public ElectronicPayrollNumberingRange? NumberingRange { get; set; }

    [MaxLength(10)]
    public string Prefix { get; set; } = string.Empty;

    public long Consecutive { get; set; }

    /// <summary>Prefijo + consecutivo (<c>NumeroSecuenciaXML</c>).</summary>
    [MaxLength(30)]
    public string Number { get; set; } = string.Empty;

    public DianEnvironment Environment { get; set; }

    public DateOnly GenerationDate { get; set; }
    public TimeOnly GenerationTime { get; set; }

    [MaxLength(96)]
    public string? Cune { get; set; }

    public ElectronicPayrollDocumentStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public int? LastTransmissionId { get; set; }

    public decimal TotalAccrued { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }

    public int WorkedDays { get; set; }

    [MaxLength(400)]
    public string PaymentDatesJson { get; set; } = "[]";

    public string SourceRunsJson { get; set; } = "[]";

    /// <summary>SHA-256 de las líneas fuente normalizadas: si cambia tras <c>Accepted</c>, hace falta nota de ajuste.</summary>
    [MaxLength(64)]
    public string SourceFingerprint { get; set; } = string.Empty;

    public Guid? UnsignedXmlAttachmentPublicId { get; set; }
    public Guid? SignedXmlAttachmentPublicId { get; set; }
    public Guid? ZipAttachmentPublicId { get; set; }
    public Guid? ApplicationResponseAttachmentPublicId { get; set; }
    public Guid? GraphicPdfAttachmentPublicId { get; set; }

    [MaxLength(40)]
    public string? ZipKey { get; set; }

    [MaxLength(2)]
    public string? DianStatusCode { get; set; }

    [MaxLength(300)]
    public string? DianStatusDescription { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public string? TranslatedErrorsJson { get; set; }

    [MaxLength(200)]
    public string? QrUrl { get; set; }

    public ICollection<ElectronicPayrollTransmission> Transmissions { get; set; } = [];
}

/// <summary>Un intento contra el servicio central (envío, set de pruebas, consulta de estado, firma).</summary>
public class ElectronicPayrollTransmission : AuditableEntity
{
    public int DocumentId { get; set; }
    public ElectronicPayrollDocument? Document { get; set; }

    public int Attempt { get; set; }

    public TransmissionOperation Operation { get; set; }

    public DateTime RequestedAt { get; set; }

    [MaxLength(100)]
    public string RequestedBy { get; set; } = string.Empty;

    public DateTime? CompletedAt { get; set; }

    public int? DurationMs { get; set; }

    public DianEnvironment Environment { get; set; }

    public Guid ServiceCorrelationId { get; set; }

    public short? ServiceHttpStatus { get; set; }

    public TransmissionOutcome Outcome { get; set; }

    [MaxLength(2)]
    public string? DianStatusCode { get; set; }

    public bool? IsValid { get; set; }

    [MaxLength(300)]
    public string? StatusDescription { get; set; }

    [MaxLength(1000)]
    public string? StatusMessage { get; set; }

    public string? RawErrorsJson { get; set; }

    public string? TranslatedErrorsJson { get; set; }
}
