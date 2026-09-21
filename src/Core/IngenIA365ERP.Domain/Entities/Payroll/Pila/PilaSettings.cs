using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll.Pila;

/// <summary>
/// Datos del aportante que la Resolución 2388 de 2016 pide en el registro tipo 1 y la empresa
/// no tiene (feature 010, US5; data-model §2.8). Fila única por cooperativa. El NIT y el
/// dígito se leen de <c>COR_Companies</c>; aquí va lo que sólo la PILA necesita.
/// </summary>
public class PilaSettings : AuditableEntity
{
    /// <summary>Campo 21 del registro 1: <c>1</c> empleador.</summary>
    [MaxLength(2)]
    public string ContributorType { get; set; } = "1";

    /// <summary>Clase de aportante: <c>A</c> ≥ 200 cotizantes, <c>B</c> &lt; 200, <c>C</c>, <c>D</c>, <c>I</c>.</summary>
    [MaxLength(1)]
    public string ContributorClass { get; set; } = "B";

    /// <summary>Campo 11: <c>U</c> planilla única, <c>S</c> por sucursal.</summary>
    [MaxLength(1)]
    public string PresentationForm { get; set; } = "U";

    [MaxLength(10)]
    public string? BranchCode { get; set; }

    [MaxLength(40)]
    public string? BranchName { get; set; }

    /// <summary>Campo 14: código PILA de la ARL del aportante.</summary>
    [MaxLength(6)]
    public string? ArlPilaCode { get; set; }

    /// <summary>Departamento (2) + municipio (3) DANE de la sede; es el defecto de la ficha (campos 9-10 del registro 2).</summary>
    [MaxLength(5)]
    public string? MunicipalityDaneCode { get; set; }

    /// <summary>Actividad económica ARL (Decreto 768/2022, 7 posiciones); defecto de la ficha (campo 98).</summary>
    [MaxLength(7)]
    public string? EconomicActivityCode { get; set; }

    /// <summary>Informativo: «Aportes en Línea».</summary>
    [MaxLength(40)]
    public string? OperatorName { get; set; }

    /// <summary>Campo 22 del registro 1: código del operador de información (lo confirma el operador).</summary>
    [MaxLength(2)]
    public string? OperatorCode { get; set; }

    /// <summary>Campo 8: <c>E</c> empleados (N y A quedan fuera).</summary>
    [MaxLength(1)]
    public string PlanillaType { get; set; } = "E";
}
