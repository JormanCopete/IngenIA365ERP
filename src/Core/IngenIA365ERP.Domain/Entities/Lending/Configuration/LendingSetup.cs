using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_Setup</c> (feature 013, T0210; data-model §1.1; FR-107, FR-142, T34): la fila única de Cartera en cada base de
/// cooperativa. Es la primera fila del cerrojo (compartida en toda operación, exclusiva al cerrar o reabrir un mes) y guarda la
/// activación vigente. La siembra <c>CarteraNucleo</c> con <c>Id = 1</c>; nunca se da de baja. Sin <see cref="ActivationDate"/>,
/// Cartera está desplegada sin activar (T85). Sólo <c>ActivateLendingCommand</c> la escribe, una vez. No es hecho: es control.
/// </summary>
public class LendingSetup : AuditableEntity
{
    /// <summary>El Id de la fila única que siembra la migración.</summary>
    public const int IdDeLaFilaUnica = 1;

    /// <summary>Día siguiente al corte de la carga activada (FR-142); nulo = sin activar.</summary>
    public DateOnly? ActivationDate { get; set; }

    /// <summary>El registro de la activación (<c>LND_Activations</c>), escrito en la misma transacción (C60).</summary>
    public int? ActivationId { get; set; }

    /// <summary>Instante del registro de la activación.</summary>
    public DateTime? ActivatedAt { get; set; }
}
