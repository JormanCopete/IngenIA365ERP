using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Una generación de un formato de exógena (feature 009, FR-072..FR-075): versión de trabajo o
/// exportada, con sus líneas por tercero y concepto e inconsistencias. Regenerar reemplaza sólo
/// la de trabajo; las exportadas se conservan con fecha, usuario y archivo.
/// </summary>
public class ExogenousRun : AuditableEntity
{
    public int TaxYear { get; set; }
    public int FormatId { get; set; }
    public ExogenousFormat? Format { get; set; }
    public int Version { get; set; } = 1;
    public ExogenousRunStatus Status { get; set; } = ExogenousRunStatus.Working;
    public DateTime GeneratedAt { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
    public DateTime? ExportedAt { get; set; }
    public string? ExportedBy { get; set; }
    public Guid? XmlAttachmentPublicId { get; set; }
    public int IssueCount { get; set; }
    public int BlockingIssueCount { get; set; }

    public ICollection<ExogenousRunLine> Lines { get; set; } = [];
}

/// <summary>Línea generada: tercero (o menores cuantías), concepto, identificación tal como estaba al generar, valores e inconsistencias.</summary>
public class ExogenousRunLine : AuditableEntityLong
{
    public int RunId { get; set; }
    public ExogenousRun? Run { get; set; }
    public int? PersonId { get; set; }
    public Person? Person { get; set; }
    public int ConceptId { get; set; }
    public ExogenousConcept? Concept { get; set; }

    public string? IdType { get; set; }
    public string IdNumber { get; set; } = string.Empty;
    public string? CheckDigit { get; set; }
    public string? Surname1 { get; set; }
    public string? Surname2 { get; set; }
    public string? Name1 { get; set; }
    public string? Name2 { get; set; }
    public string? BusinessName { get; set; }
    public string? Address { get; set; }
    public string? MunicipalityCode { get; set; }
    public string? DepartmentCode { get; set; }

    public decimal Value1 { get; set; }
    public decimal Value2 { get; set; }
    public decimal Value3 { get; set; }
    public decimal Value4 { get; set; }
    public decimal Value5 { get; set; }
    public decimal Value6 { get; set; }
    public decimal Value7 { get; set; }
    public decimal Value8 { get; set; }
    public decimal Value9 { get; set; }
    public decimal Value10 { get; set; }

    /// <summary>Inconsistencias en JSON: [{ code, message, blocking }].</summary>
    public string? IssuesJson { get; set; }
}
