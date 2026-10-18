using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_Activations</c> (feature 013, K1; data-model §30.1; FR-089, FR-105, FR-106, FR-141–FR-143, D17, T84): el registro de la
/// activación de Cartera, una sola por cooperativa y sin desactivación. Hecho (<see cref="IHechoInmutable"/>) que escribe
/// <c>ActivateLendingCommand</c> junto con todo lo que nace de la carga, en una transacción explícita.
/// </summary>
public class LendingActivation : AuditableEntity, IHechoInmutable
{
    /// <summary>La carga <c>Applied</c> (puede no traer créditos); única.</summary>
    public int MigrationLoadId { get; init; }

    public DateOnly CutoffDate { get; init; }

    /// <summary>Corte + 1, aunque se registre después.</summary>
    public DateOnly ActivationDate { get; init; }

    public int RegisteredByUserId { get; init; }

    public DateTime RegisteredAt { get; init; }

    /// <summary>Acta de activación, obligatoria (C61).</summary>
    public int SessionItemId { get; init; }

    public int CheckVersion { get; init; }

    public string BalanceCheckSha256 { get; init; } = string.Empty;

    public string SalesReconciliationSha256 { get; init; } = string.Empty;

    /// <summary><c>IHostEnvironment.IsProduction()</c> sellado: dice qué requisitos se exigieron.</summary>
    public bool IsProductionEnvironment { get; init; }

    /// <summary>JSON: cada requisito con su resultado y evidencia.</summary>
    public string RequirementsJson { get; init; } = "[]";

    /// <summary>Ambiente de prueba sin K6a con cooperativa obligada (FR-105).</summary>
    public bool ObligatoryModelUnavailable { get; init; }

    /// <summary>JSON: créditos, saldos iniciales, castigados, cupos, bloqueos, autorizaciones, avisos, reclamos, vinculados, garantías.</summary>
    public string CountsJson { get; init; } = "{}";

    public int NonDeductionsMarked { get; init; }

    public int CreditLimitsToRevalidate { get; init; }

    public string? ProvisionalCollectionsFileSha256 { get; init; }

    public int ProvisionalCollectionsCount { get; init; }

    /// <summary>Adjunto de la revisión previa de <c>lending.provisional-collections</c> (C62), sin FK.</summary>
    public Guid? ProvisionalCollectionsReviewAttachmentPublicId { get; init; }

    /// <summary>JSON: lo que Comercial debe ajustar (FR-142, D16).</summary>
    public string CommercialAdjustmentsJson { get; init; } = "{}";

    /// <summary>El aviso de <c>DESC_CARTERA</c> sin cuentas, que la pantalla consulta a Nómina.</summary>
    public bool PayrollWarningAcknowledged { get; init; }

    /// <summary><c>CarteraActivada</c> (bandeja, sin FK).</summary>
    public Guid ActivatedMessagePublicId { get; init; }

    /// <summary>
    /// Feature 014 (T028; data-model §1.8, R21): la naturaleza que activa —créditos, captaciones o aportes—, una vez cada una
    /// (<c>UK_LND_Activations_Nature</c>). Toda activación de la 013 es de colocación (defecto).
    /// </summary>
    public LineNature Nature { get; init; } = LineNature.Placement;
}
