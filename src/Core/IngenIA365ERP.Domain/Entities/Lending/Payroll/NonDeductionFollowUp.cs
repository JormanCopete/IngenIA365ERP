using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Payroll;

/// <summary>
/// <c>LND_NonDeductionFollowUps</c> (feature 013, K2; data-model §24.10; FR-115): un paso del seguimiento de un no aplicado —el aviso al deudor,
/// la inclusión en el archivo siguiente, el paso a caja o a cobranza, el cierre—. El estado del no aplicado es su último seguimiento; queda
/// resuelto sólo cuando la cuota se cubre (lo dice la reconstrucción). Automático (la regla del convenio, que aplica el consumidor de
/// <c>ResultadoDeDescuentos</c> o <c>AplicarAsync</c> en la nómina propia) o de una persona con motivo. Hecho de sólo inserción
/// (<see cref="IHechoInmutable"/>). (nuevo)
/// </summary>
public class NonDeductionFollowUp : AuditableEntity, IHechoInmutable
{
    public int InstallmentNonDeductionId { get; init; }

    public NonDeductionFollowUpAction Action { get; init; }

    /// <summary>La regla del convenio, aplicada por el consumidor; falso = una persona con <c>Lending.PayrollNotApplied.Manage</c>.</summary>
    public bool IsAutomatic { get; init; }

    /// <summary><c>DebtorNotified</c>: el informativo por los canales autorizados.</summary>
    public int? DebtorMessageId { get; init; }

    /// <summary><c>IncludedInNextFile</c>: el archivo que la llevó.</summary>
    public int? PayrollDeductionFileId { get; init; }

    /// <summary>Obligatorio si no es automático.</summary>
    public string? Reason { get; init; }

    public int? ActorUserId { get; init; }

    public DateTime OccurredAt { get; init; }

    /// <summary>¿Cumple las reglas de §24.10? Una persona da motivo y se identifica; la automática no lleva actor.</summary>
    public bool Valido => IsAutomatic
        ? ActorUserId is null
        : ActorUserId is not null && !string.IsNullOrWhiteSpace(Reason);
}
