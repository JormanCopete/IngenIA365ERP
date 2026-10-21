using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_InstallmentNonDeductions</c> (feature 013; data-model §24.9 y §7.12; FR-069, FR-112, FR-115, FR-142; T92): lo que no se
/// aplicó de una cuota por nómina propia o pagaduría. Es la marca «no aplicado» del crédito y su estado de libranza; no mueve el libro
/// y la cuota <b>sigue exigible</b>. Hecho de sólo inserción (<see cref="IHechoInmutable"/>). La escriben <c>AplicarAsync</c> en la
/// transacción de Nómina (idempotente por la clave), la activación y, desde K2, el consumo de <c>ResultadoDeDescuentos</c>. La columna
/// <c>PayrollDeductionResultId</c> y su único nacen con K2.
/// </summary>
public class InstallmentNonDeduction : AuditableEntity, IHechoInmutable
{
    public int LoanId { get; init; }

    /// <summary>La cuota (<c>LND_RepaymentInstallments</c>, bigint; C43).</summary>
    public long RepaymentInstallmentId { get; init; }

    public int InstallmentNumber { get; init; }

    public DateOnly DueDate { get; init; }

    /// <summary><c>Contract</c> (el <c>NoDescontado</c> de Nómina), <c>Activation</c> («Cartera sin activar»), <c>PaymasterResult</c> (K2).</summary>
    public NonDeductionOrigin Origin { get; init; }

    /// <summary>En <c>Activation</c>: <c>Other</c> con «Cartera sin activar».</summary>
    public NonDeductionReason Reason { get; init; }

    public string? ReasonText { get; init; }

    public decimal ExpectedAmount { get; init; }

    public decimal NotDeductedAmount { get; init; }

    /// <summary>En <c>Contract</c>: el recaudador <c>Payroll</c>.</summary>
    public string? CollectorCode { get; init; }

    /// <summary>En <c>Contract</c>: <c>NOM:{runPublicId:N}:{obligationPublicId:N}</c> (T21); única con la cuota si no es nula.</summary>
    public string? ContractOperationKey { get; init; }

    /// <summary>En <c>Activation</c>; única con la cuota si no es nula.</summary>
    public int? ActivationId { get; init; }

    /// <summary>
    /// En <c>PaymasterResult</c> (K2, <c>CarteraLibranza</c>): el resultado que lo originó; único si no es nulo. El enlace va del no aplicado al
    /// resultado para insertar los dos hechos en el mismo guardado.
    /// </summary>
    public int? PayrollDeductionResultId { get; init; }

    /// <summary><c>Cartera.CuotaNoDescontadaPorNomina</c> (K1) o <c>Cartera.NoAplicadoDePagaduria</c> (K2); sin FK.</summary>
    public Guid? AlertPublicId { get; init; }
}
