using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_ScreeningResults</c> (feature 013; data-model §2.6; FR-036, FR-049, FR-051, T70): cada consulta de Cartera a listas
/// restrictivas (radicación, venta, cupo, re-escaneo) con las versiones de carga consultadas. Hecho de sólo inserción
/// (<see cref="IHechoInmutable"/>, C42); la liberación del oficial se escribe una sola vez (<see cref="EscrituraUnicaAttribute"/>).
/// <c>Match</c> y <c>PossibleMatch</c> sin liberar bloquean originar y vender.
/// </summary>
public class ScreeningResult : AuditableEntity, IHechoInmutable
{
    public int PersonId { get; init; }

    public ScreeningContext Context { get; init; }

    /// <summary>Según el contexto.</summary>
    public int? ApplicationId { get; init; }

    /// <summary>Según el contexto.</summary>
    public int? CreditLimitId { get; init; }

    /// <summary>Pago de la venta evaluada (dato de Comercial, sin FK).</summary>
    public Guid? SalePaymentPublicId { get; init; }

    /// <summary>Canal sellado: <c>CARGA-PROPIA</c> o <c>SIMULADO</c> (T71).</summary>
    public string ChannelCode { get; init; } = string.Empty;

    /// <summary>JSON: lista, versión y PublicId de cada carga consultada.</summary>
    public string ListVersions { get; init; } = "[]";

    public ScreeningOutcome Outcome { get; init; }

    /// <summary>JSON: entrada, documento o nombre, puntaje (fracción).</summary>
    public string? Matches { get; init; }

    public bool IsSimulated { get; init; }

    public DateTime QueriedAt { get; init; }

    /// <summary>Nulo = proceso (venta).</summary>
    public int? QueriedByUserId { get; init; }

    [EscrituraUnica]
    public DateTime? ReleasedAt { get; set; }

    /// <summary>Nunca quien radicó (segregación, C89).</summary>
    [EscrituraUnica]
    public int? ReleasedByUserId { get; set; }

    [EscrituraUnica]
    public string? ReleaseReason { get; set; }
}
