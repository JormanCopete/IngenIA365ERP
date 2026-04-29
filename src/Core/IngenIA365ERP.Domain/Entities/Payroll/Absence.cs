using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_Absences] (nom_ausentismos).</summary>
public class Absence : AuditableEntityLong
{
    public int PayrollCompanyId { get; set; }
    public int EmployeeId { get; set; }
    public int ConceptId { get; set; }
    public decimal SequenceNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime VacationCauseStart { get; set; }
    public DateTime VacationCauseEnd { get; set; }
    public int AbsenceType { get; set; }
    public int DiagnosisCode { get; set; }
    public int IncapacityClass { get; set; }
    public int IsExtension { get; set; }
    public decimal BaseAmount { get; set; }

    [MaxLength(20)]
    public string SerialNumber { get; set; } = string.Empty;

    public int Hours { get; set; }
    public int ExtensionConceptId { get; set; }
    public decimal ExtensionSequence { get; set; }

    // Navigation
    public Employee? Employee { get; set; }
    public PayrollConcept? Concept { get; set; }
}
