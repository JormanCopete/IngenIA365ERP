using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>RetiroDeAsociadoAprobado</c> v1 (feature 014, US10, T098; contracts/mensajes.md §1.2 #34 y §5.1; <c>CAR → Associates</c>; FR-076, FR-124, enmienda
/// E-04): el retiro del asociado al aprobarlo el órgano con acta, para que Asociados cambie el estado. Origen <c>MembershipWithdrawal</c>, clave
/// <c>Confirmation</c>, tercero el asociado; lo origina la persona que aprueba (no admite proceso), en el mismo guardado que la aprobación. Lo consume
/// <c>DestinoAsociados</c> (<c>ConsumeMembershipWithdrawalApprovedCommand</c>). (nuevo)
/// </summary>
public sealed record RetiroDeAsociadoAprobadoV1
{
    public const string Type = "RetiroDeAsociadoAprobado";

    public Guid WithdrawalPublicId { get; init; }

    public string WithdrawalNumber { get; init; } = string.Empty;

    public Guid PersonPublicId { get; init; }

    /// <summary>Valor de <c>MembershipWithdrawalCause</c>: <c>Voluntary</c>, <c>Exclusion</c>, <c>Death</c> u <c>Other</c>.</summary>
    public string Cause { get; init; } = string.Empty;

    /// <summary>La causa configurada cuando es <c>Other</c>.</summary>
    public string? CauseDetail { get; init; }

    /// <summary>
    /// Decisión del dueño del 2026-10-18 (D46): el código del motivo de retiro de Asociados (<c>COR_WithdrawalReasons.LegacyCode</c>) que la cooperativa
    /// le asigna a la causa en <c>Captaciones.MotivosDeRetiroDeAsociados</c>; nulo sin mapeo.
    /// </summary>
    public string? WithdrawalReasonCode { get; init; }

    /// <summary>La fecha del retiro que Asociados registra.</summary>
    public DateOnly EffectiveDate { get; init; }

    public MembershipWithdrawalApprovalV1 Approval { get; init; } = new();

    public UserRefV1 ApprovedBy { get; init; } = new();
}

/// <summary>El punto de acta que aprobó el retiro (mensajes.md §5.1, <c>approval</c>).</summary>
public sealed record MembershipWithdrawalApprovalV1
{
    public Guid SessionItemPublicId { get; init; }

    public string? SessionNumber { get; init; }

    /// <summary>El órgano que decidió (consejo, junta).</summary>
    public string GoverningBody { get; init; } = string.Empty;

    public DateOnly DecidedOn { get; init; }
}
