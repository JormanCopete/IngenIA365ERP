using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Payroll.Pila;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll.Transactions;

/// <summary>
/// Una generación de la planilla PILA de un período (feature 010, US5; data-model §2.8).
/// Inmutable (Principio XI): validar con bloqueantes deja una fila <c>Validated</c> sin
/// archivo; generar deja <c>Generated</c> con el <c>.txt</c> en <c>COR_Attachments</c>;
/// regenerar crea la versión siguiente y deja ésta <c>Superseded</c> con su archivo; marcar
/// cargada anota el radicado del operador. Nada se edita ni se borra.
/// </summary>
public class PilaGeneration : AuditableEntity
{
    /// <summary>Período de pago de los sistemas distintos a salud (campo 15); el de salud (campo 16) es el siguiente.</summary>
    public short Year { get; set; }
    public byte Month { get; set; }

    public int Version { get; set; }

    public PilaGenerationStatus Status { get; set; }

    /// <summary>Código del layout embebido con que se escribió (<c>at2-v30-2026-07-24</c>).</summary>
    [MaxLength(40)]
    public string LayoutVersion { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; }

    [MaxLength(100)]
    public string GeneratedBy { get; set; } = string.Empty;

    /// <summary>Política <c>Exonerada114_1</c> vigente al primer día del período.</summary>
    public bool ExemptionApplied { get; set; }

    /// <summary>Campo 19: cotizantes únicos.</summary>
    public int ContributorCount { get; set; }

    /// <summary>Campo 20: registros tipo 2.</summary>
    public int LineCount { get; set; }

    public decimal TotalIbcHealth { get; set; }
    public decimal TotalIbcPension { get; set; }
    public decimal TotalIbcWorkRisk { get; set; }
    public decimal TotalIbcFamilyCompensation { get; set; }

    public decimal TotalHealth { get; set; }
    public decimal TotalPension { get; set; }
    public decimal TotalSolidarityFund { get; set; }
    public decimal TotalWorkRisk { get; set; }
    public decimal TotalFamilyCompensation { get; set; }
    public decimal TotalSena { get; set; }
    public decimal TotalIcbf { get; set; }
    public decimal TotalContributions { get; set; }

    /// <summary>Cuadre por subsistema contra los comprobantes <c>NM</c> del mes (FR-027): esperado, archivo, diferencia.</summary>
    public string ReconciliationJson { get; set; } = "[]";

    /// <summary>Verdadero si ningún subsistema difiere del libro.</summary>
    public bool Balanced { get; set; }

    /// <summary>PublicId, versión y tipo de cada corrida aprobada que se sumó.</summary>
    public string SourceRunsJson { get; set; } = "[]";

    [MaxLength(120)]
    public string? FileName { get; set; }

    [MaxLength(64)]
    public string? FileSha256 { get; set; }

    /// <summary><c>COR_Attachments</c> con <c>OwnerEntityType = "PilaGeneration"</c>.</summary>
    public Guid? FileAttachmentPublicId { get; set; }

    public int BlockingIssueCount { get; set; }
    public int WarningCount { get; set; }

    /// <summary>Propuesta con <c>PILA_PLAZO_PAGO_POR_NIT</c>; sólo informativa.</summary>
    public DateOnly? ProposedPaymentDueDate { get; set; }

    public DateTime? UploadedAt { get; set; }

    [MaxLength(100)]
    public string? UploadedBy { get; set; }

    [MaxLength(40)]
    public string? OperatorFilingNumber { get; set; }

    public DateOnly? OperatorFilingDate { get; set; }

    public DateOnly? PaidAt { get; set; }

    public ICollection<PilaGenerationLine> Lines { get; set; } = [];
    public ICollection<PilaIssue> Issues { get; set; } = [];
}

/// <summary>Una fila por registro tipo 2: las columnas tipadas para reportes y cuadre más los 98 campos ya formateados (D-14).</summary>
public class PilaGenerationLine : AuditableEntityLong
{
    public int GenerationId { get; set; }
    public PilaGeneration? Generation { get; set; }

    /// <summary>Campo 2.</summary>
    public int LineNumber { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [MaxLength(2)]
    public string ContributorType { get; set; } = string.Empty;

    [MaxLength(2)]
    public string ContributorSubType { get; set; } = string.Empty;

    /// <summary>Las novedades marcadas en la línea (<c>ING</c>, <c>RET</c>, <c>VSP</c>, <c>IGE</c>…), separadas por coma.</summary>
    [MaxLength(60)]
    public string NoveltyFlags { get; set; } = string.Empty;

    public byte DaysHealth { get; set; }
    public byte DaysPension { get; set; }
    public byte DaysWorkRisk { get; set; }
    public byte DaysFamilyCompensation { get; set; }

    public decimal Salary { get; set; }

    public decimal IbcHealth { get; set; }
    public decimal IbcPension { get; set; }
    public decimal IbcWorkRisk { get; set; }
    public decimal IbcFamilyCompensation { get; set; }

    public decimal HealthRate { get; set; }
    public decimal PensionRate { get; set; }
    public decimal WorkRiskRate { get; set; }
    public decimal FamilyCompensationRate { get; set; }
    public decimal SenaRate { get; set; }
    public decimal IcbfRate { get; set; }

    public decimal Health { get; set; }
    public decimal Pension { get; set; }
    public decimal SolidarityFund { get; set; }
    public decimal SubsistenceFund { get; set; }
    public decimal WorkRisk { get; set; }
    public decimal FamilyCompensation { get; set; }
    public decimal Sena { get; set; }
    public decimal Icbf { get; set; }

    /// <summary>Campo 76: <c>S</c> exonerado de salud empleador, SENA e ICBF.</summary>
    public bool Exempt { get; set; }

    /// <summary>Los 98 campos por número, ya formateados: <c>{"1":"2","2":"00001",…}</c>.</summary>
    public string FieldsJson { get; set; } = "{}";

    /// <summary>La línea de 693 posiciones tal como se escribió.</summary>
    [MaxLength(800)]
    public string RecordText { get; set; } = string.Empty;

    /// <summary>Por campo: valor, origen, de qué salió (línea de corrida, parámetro con vigencia, política) y el redondeo.</summary>
    public string ExplanationJson { get; set; } = "[]";
}
