using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Activation;

/// <summary>
/// <c>TES_Setup</c> (feature 015, T0036; data-model §9.1; FR-150, FR-159): la fila única de la Tesorería nueva en cada base de cooperativa. Es el
/// nivel 1 del cerrojo de Tesorería (compartido) y guarda el estado del módulo. La siembra <c>TesoreriaNucleo</c> con <c>Id = 1</c> y
/// <see cref="TreasuryModuleStatus.NotActivated"/>; nunca se da de baja. Vive en <c>Entities/Treasury/Activation</c>, no en la raíz, que es
/// de lo heredado (<c>TRS_</c>).
/// </summary>
public class TreasurySetup : AuditableEntity
{
    /// <summary>El Id de la fila única que siembra la migración.</summary>
    public const int IdDeLaFilaUnica = 1;

    public TreasuryModuleStatus Status { get; set; } = TreasuryModuleStatus.NotActivated;

    /// <summary>La fecha (de Colombia) desde la que rige la activación; nula sin activar.</summary>
    public DateOnly? ActivatedOn { get; set; }

    /// <summary>Sellado al activar de <c>Lotes.TerceraPersonaParaResultado</c> (D18): el resultado de un lote lo registra una tercera persona.</summary>
    public bool ResultWithThirdPerson { get; set; }

    public bool Activo => Status == TreasuryModuleStatus.Active;
}
