using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll.Pila;

/// <summary>
/// Una inconsistencia encontrada al validar o generar la planilla (feature 010, US5;
/// data-model §2.8), con la severidad del operador, el campo del registro, el empleado y la
/// ruta de la pantalla donde se corrige.
/// </summary>
public class PilaIssue : AuditableEntity
{
    public int GenerationId { get; set; }
    public PilaGeneration? Generation { get; set; }

    public PilaIssueSeverity Severity { get; set; }

    [MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Campo del registro tipo 2 (o tipo 1) al que se refiere; nulo si es general.</summary>
    public byte? FieldNumber { get; set; }

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Ruta de la pantalla donde se corrige (ficha, catálogo, datos del aportante).</summary>
    [MaxLength(200)]
    public string? LinkRoute { get; set; }
}
