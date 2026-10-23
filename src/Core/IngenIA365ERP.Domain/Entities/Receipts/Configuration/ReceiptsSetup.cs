using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Configuration;

/// <summary>
/// <c>RCD_Setup</c> (feature 015, T0036; data-model §1.1; FR-150, FR-185): la fila única de Recaudo en cada base de cooperativa. Es el nivel 1 del
/// cerrojo (compartido en toda operación que mueve dinero) y guarda el estado del módulo. La siembra <c>RecaudoNucleo</c> con <c>Id = 1</c>,
/// <see cref="ReceiptsModuleStatus.NotActivated"/>, la hora de corte por defecto y las versiones 1 de los contratos; nunca se da de baja. Sólo
/// la activación la pasa a <see cref="ReceiptsModuleStatus.Active"/>. No es hecho: es control.
/// </summary>
public class ReceiptsSetup : AuditableEntity
{
    /// <summary>El Id de la fila única que siembra la migración.</summary>
    public const int IdDeLaFilaUnica = 1;

    public ReceiptsModuleStatus Status { get; set; } = ReceiptsModuleStatus.NotActivated;

    /// <summary>La fecha (de Colombia) desde la que rige la activación; nula sin activar.</summary>
    public DateOnly? ActivatedOn { get; set; }

    /// <summary>La hora de corte del día de Recaudo: copia de <c>Recaudo.HoraDeCorte</c> vigente al activar (FR-185).</summary>
    public TimeOnly CutoffTime { get; set; } = new(18, 0);

    /// <summary>La versión del contrato de obligaciones que la operación usa (1 en R0; la 2 la adopta R1).</summary>
    public short ContractVersionObligations { get; set; } = 1;

    /// <summary>La versión del contrato de cuentas que la operación usa (1 en R0).</summary>
    public short ContractVersionAccounts { get; set; } = 1;

    public bool Activo => Status == ReceiptsModuleStatus.Active;
}
