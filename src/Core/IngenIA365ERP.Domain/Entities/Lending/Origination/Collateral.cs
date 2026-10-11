using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_Collaterals</c> (feature 013; data-model §2.8; FR-026, FR-043, FR-082): garantías reales y de fondo de una solicitud o un
/// crédito. Sólo guarda <c>Pledge</c>, <c>Mortgage</c>, <c>GuaranteeFund</c> y <c>Contributions</c> (<c>CK_LND_Collaterals_Kind</c>):
/// el pagaré vive en <see cref="PromissoryNote"/> y el codeudor es parte. Al menos uno de <see cref="ApplicationId"/> y
/// <see cref="LoanId"/>. No es hecho.
/// </summary>
public class Collateral : AuditableEntity
{
    public CollateralKind Kind { get; set; }

    /// <summary>Constituyente de la garantía.</summary>
    public int OwnerPersonId { get; set; }

    public int? ApplicationId { get; set; }

    /// <summary>Se llena al desembolsar (nulo → valor).</summary>
    public int? LoanId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Value { get; set; }

    /// <summary>Valor admisible para la cobertura.</summary>
    public decimal AdmissibleValue { get; set; }

    public DateOnly? ValuationDate { get; set; }

    public int? AppraiserPersonId { get; set; }

    public string? PolicyNumber { get; set; }

    public int? PolicyInsurerPersonId { get; set; }

    public DateOnly? PolicyExpiresOn { get; set; }

    /// <summary>Matrícula o registro de prenda.</summary>
    public string? RegistrationReference { get; set; }

    /// <summary>Grado de la hipoteca (vivienda exige primero, K6b).</summary>
    public byte? MortgageRank { get; set; }

    /// <summary>Fondo de garantías: cobertura, como fracción.</summary>
    public decimal? FundCoverage { get; set; }

    /// <summary>Fondo de garantías: comisión, como fracción.</summary>
    public decimal? FundFee { get; set; }

    public CollateralStatus Status { get; set; } = CollateralStatus.Constituted;

    public DateOnly ConstitutedOn { get; set; }

    public DateOnly? ValidUntil { get; set; }

    public DateOnly? ReleasedOn { get; set; }

    public int? ReleasedByUserId { get; set; }

    /// <summary>La operación de pago que canceló, ya en firme (FR-082).</summary>
    public long? ReleaseOperationId { get; set; }

    /// <summary>K3 (garante que no consiente, FR-092).</summary>
    public DateOnly? ExtinguishedOn { get; set; }

    public string? ExtinguishReason { get; set; }

    public DateOnly? ExecutedOn { get; set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0432) --

    public const string CodigoTransicionInvalida = "Lending.Collateral.TransitionInvalid";

    /// <summary>Las transiciones de api.md §6.1: <c>Constituted → Valid → Extinguished | Executed</c>; liberar va aparte (tras la firmeza).</summary>
    private static readonly IReadOnlyDictionary<CollateralStatus, CollateralStatus[]> Transiciones = new Dictionary<CollateralStatus, CollateralStatus[]>
    {
        [CollateralStatus.Constituted] = [CollateralStatus.Valid, CollateralStatus.Extinguished],
        [CollateralStatus.Valid] = [CollateralStatus.Extinguished, CollateralStatus.Executed, CollateralStatus.Released],
    };

    /// <summary>
    /// Cuenta para desembolsar (FR-043): constituida o vigente —no liberada, extinguida ni ejecutada— y sin vencer a <paramref name="fecha"/>.
    /// </summary>
    public bool PerfeccionadaA(DateOnly fecha) =>
        !IsDeleted && Status is CollateralStatus.Constituted or CollateralStatus.Valid && (ValidUntil is not { } hasta || hasta >= fecha);

    /// <summary>¿Se puede pasar de <paramref name="desde"/> a <paramref name="hacia"/>?</summary>
    public static bool PuedePasar(CollateralStatus desde, CollateralStatus hacia) =>
        Transiciones.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);

    /// <summary>Nulo si puede ir a <paramref name="hacia"/>; si no, <c>Lending.Collateral.TransitionInvalid</c> con <c>from</c> y <c>to</c>.</summary>
    public ProblemaDeCartera? PuedeIrA(CollateralStatus hacia) => PuedePasar(Status, hacia)
        ? null
        : new ProblemaDeCartera(CodigoTransicionInvalida, $"La garantía está en «{Status}» y no puede pasar a «{hacia}».",
            new Dictionary<string, object?> { ["from"] = Status.ToString(), ["to"] = hacia.ToString() });

    /// <summary>Pasa la garantía de la solicitud al crédito que nació (nulo → valor, una vez).</summary>
    public void AsignarCredito(int loanId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(loanId, 1);
        if (LoanId is { } actual && actual != loanId)
            throw new InvalidOperationException($"La garantía {PublicId} ya respalda otro crédito.");
        LoanId = loanId;
    }
}
