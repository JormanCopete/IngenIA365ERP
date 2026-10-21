using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_PrepaymentPreferences</c> (feature 013; data-model §2.14; FR-035, FR-049, FR-075, D24): preferencia del deudor para imputar
/// abonos extraordinarios a capital, de un crédito o de un cupo rotativo (exactamente uno, <c>CK_LND_PrepaymentPreferences_Scope</c>).
/// Hecho versionado de sólo inserción (<see cref="IHechoInmutable"/>): rige desde que se registra y se lee la última con
/// <c>RegisteredAt ≤ R</c> (T24).
/// </summary>
public class PrepaymentPreference : AuditableEntity, IHechoInmutable
{
    public int PersonId { get; init; }

    public int? LoanId { get; init; }

    public int? CreditLimitId { get; init; }

    /// <summary>Nulo = revocada: un abono sin interacción queda como saldo a favor (FR-075).</summary>
    public PrepaymentChoice? Choice { get; init; }

    public int VersionNumber { get; init; }

    public DateTime RegisteredAt { get; init; }

    public PreferenceOrigin Origin { get; init; }

    public int? ApplicationId { get; init; }

    /// <summary>El cambio en un crédito es una operación <c>PreferenceChange</c>.</summary>
    public long? OperationId { get; init; }

    /// <summary>Constancia de la instrucción (adjunto, sin FK).</summary>
    public Guid? EvidenceAttachmentPublicId { get; init; }

    public int? RecordedByUserId { get; init; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0388) --

    /// <summary>
    /// Una versión de la preferencia (data-model §2.14): de un crédito <b>o</b> de un cupo, exactamente uno (<c>CK_LND_PrepaymentPreferences_Scope</c>).
    /// Por eso la que el deudor elige al radicar viaja en <c>CreditApplication.PrepaymentChoice</c> y nace como fila cuando nace el crédito
    /// o el cupo, con <c>Origin = Application</c> y la solicitud (US3, US2 cupos).
    /// </summary>
    public static PrepaymentPreference Registrar(int personId, int? loanId, int? creditLimitId, PrepaymentChoice? eleccion, int numero,
        DateTime registradaEl, PreferenceOrigin origen, int? usuarioId, int? applicationId = null, long? operationId = null, Guid? evidencia = null)
    {
        if ((loanId is null) == (creditLimitId is null))
            throw new ArgumentException("La preferencia es de un crédito o de un cupo, exactamente uno.");
        if (numero < 1) throw new ArgumentOutOfRangeException(nameof(numero), "La versión empieza en 1.");
        return new PrepaymentPreference
        {
            PersonId = personId,
            LoanId = loanId,
            CreditLimitId = creditLimitId,
            Choice = eleccion,
            VersionNumber = numero,
            RegisteredAt = registradaEl,
            Origin = origen,
            ApplicationId = applicationId,
            OperationId = operationId,
            EvidenceAttachmentPublicId = evidencia,
            RecordedByUserId = usuarioId,
        };
    }

    /// <summary>La que rige al instante <paramref name="hasta"/> (la última con <c>RegisteredAt ≤ R</c>, T24).</summary>
    public static PrepaymentPreference? Vigente(IEnumerable<PrepaymentPreference> versiones, DateTime hasta) =>
        versiones.Where(p => p.RegisteredAt <= hasta && !p.IsDeleted).OrderByDescending(p => p.RegisteredAt).ThenByDescending(p => p.VersionNumber)
            .FirstOrDefault();
}
