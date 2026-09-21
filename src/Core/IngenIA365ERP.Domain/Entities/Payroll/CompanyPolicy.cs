using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_CompanyPolicies]. Una decisión de la cooperativa con vigencia (feature
/// 010, R4; data-model §2.1): clave del catálogo cerrado <c>CompanyPolicyKeys</c>, valor como
/// texto que el lector tipa, desde/hasta. Dos vigencias de la misma clave no se cruzan (regla
/// del comando; el índice único es <c>(Key, ValidFrom)</c>). Las claves que hoy viven en
/// <c>COR_SystemSettings</c> (<c>Payroll.ApplyEmployerExemption</c>,
/// <c>Payroll.AllowSameUserApproval</c>) se copian aquí por dato; el lector cae a esa tabla
/// sólo si la clave no existe.
/// </summary>
public class CompanyPolicy : AuditableEntity
{
    [MaxLength(60)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Texto: <c>true</c>/<c>false</c>, un valor de enumeración, una fecha <c>yyyy-MM-dd</c> o un JSON corto.</summary>
    [MaxLength(400)]
    public string Value { get; set; } = string.Empty;

    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    /// <summary>Quién decidió y por qué (la contadora).</summary>
    [MaxLength(300)]
    public string? Notes { get; set; }

    public bool IsValidAt(DateOnly date) =>
        ValidFrom <= date && (ValidTo is null || ValidTo >= date);
}
