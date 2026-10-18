using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

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

    // ------------------------------------------------------------------- feature 014 (T053; data-model §1.2; FR-116, R21) --
    // Cada naturaleza se activa una vez, después de los créditos, y no se desactiva. Sólo las escriben ActivateDepositsCommand y
    // ActivateContributionsCommand (null → valor). Sin la fecha de su naturaleza, las operaciones de esa naturaleza responden «no habilitado».

    /// <summary>Día siguiente al corte de la carga de captaciones activada (o el día elegido en la activación sin carga); nulo = sin captaciones.</summary>
    public DateOnly? DepositsActivationDate { get; set; }

    /// <summary>La fila de <c>LND_Activations</c> de las captaciones.</summary>
    public int? DepositsActivationId { get; set; }

    public DateTime? DepositsActivatedAt { get; set; }

    /// <summary>Ídem para aportes; nulo = sin aportes.</summary>
    public DateOnly? ContributionsActivationDate { get; set; }

    /// <summary>La fila de <c>LND_Activations</c> de los aportes.</summary>
    public int? ContributionsActivationId { get; set; }

    public DateTime? ContributionsActivatedAt { get; set; }

    /// <summary>La fecha de activación de una naturaleza: créditos, captaciones o aportes.</summary>
    public DateOnly? FechaDeActivacion(LineNature naturaleza) => naturaleza switch
    {
        LineNature.Placement => ActivationDate,
        LineNature.Deposit => DepositsActivationDate,
        LineNature.Contribution => ContributionsActivationDate,
        _ => null,
    };

    /// <summary>¿Rige la naturaleza el día <paramref name="fecha"/>? (activada con fecha ≤ <paramref name="fecha"/>).</summary>
    public bool ActivadaEn(LineNature naturaleza, DateOnly fecha) => FechaDeActivacion(naturaleza) is { } desde && desde <= fecha;
}
