using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// Causa de baja y ajuste (<c>INV_AdjustmentCauses</c>; feature 012, T202; FR-037, FR-039; data-model §5.10). <see cref="Code"/>
/// es inmutable (<c>ReasonCode</c> de la matriz en <c>Baja</c> y <c>AjusteNegativo</c>, T27). <see cref="IsRequiredBySystem"/>
/// <b>(nuevo)</b> marca las que usa el sistema («diferencia de conteo» para el ajuste de conteo, «reclamación al
/// transportador» para las diferencias de traslado): no se inactivan.
/// </summary>
public class AdjustmentCause : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Vale en un ajuste positivo (la diferencia de conteo vale en los dos sentidos).</summary>
    public bool AllowsPositive { get; set; }

    public bool AllowsNegative { get; set; } = true;

    /// <summary>Daño, hurto, reclamación al transportador: bajas desde la bodega de tránsito.</summary>
    public bool AllowsTransitWriteOff { get; set; }

    /// <summary>Exige soporte (acta de destrucción, denuncia) antes de confirmar.</summary>
    public bool RequiresAttachment { get; set; }

    public bool IsSeeded { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsRequiredBySystem { get; set; }
}
