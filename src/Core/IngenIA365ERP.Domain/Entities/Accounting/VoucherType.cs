using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Tipo de comprobante (feature 009, FR-018, FR-019). Los reservados (a un módulo, al cierre, a
/// la apertura, a los activos) vienen sembrados y no se digitan a mano; un código pertenece a
/// un solo módulo. <see cref="NextNumber"/> es el consecutivo: se incrementa en la misma escritura
/// que contabiliza el documento y su <c>RowVersion</c> convierte una carrera en reintento (R3).
/// </summary>
public class VoucherType : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public VoucherUsage Usage { get; set; }

    /// <summary>NOM, CAR, INV, TES, CDT, ACT… sólo cuando <see cref="Usage"/> es <c>Module</c> o <c>Assets</c>.</summary>
    public string? ModuleCode { get; set; }

    public long NextNumber { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    /// <summary>Sembrado: no se elimina ni cambia de uso.</summary>
    public bool IsSeeded { get; set; }
}
