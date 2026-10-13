using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Domain.Entities.Accounting.Transactions;

/// <summary>
/// El recibo de un hecho de Cartera procesado por Contabilidad (<c>ACC_LendingPostings</c>; feature 013, T0634, T51; data-model parte 3
/// §5.3; contracts/contabilidad.md §4.3). Uno por hecho —único <c>MessagePublicId</c> sin filtro: la colisión se traduce a
/// <c>AlreadyProcessed</c>— y escrito en el <b>mismo</b> <c>SaveChanges</c> que el comprobante. Es también el vínculo comprobante ↔
/// hechos: en un resumido, los hechos de un comprobante son las filas con su <see cref="AccountingDocumentId"/>.
///
/// <para>
/// Es un hecho (<see cref="IHechoInmutable"/>) y vive en <c>Entities/Accounting/Transactions</c>, así lo cubre
/// <c>PrincipioXI_ContableImmutable</c> sin cambios. Contabilidad no depende de Cartera: el origen, el crédito y el hecho original van
/// como el texto y los <c>PublicId</c> del sobre, sin llave a tablas <c>LND_</c>. Los informativos, los de valor cero y los traslados
/// cuyas dos puntas resolvieron lo mismo no tienen comprobante (<see cref="NoVoucherReason"/>, C21).
/// </para>
/// </summary>
public class LendingPosting : AuditableEntityLong, IHechoInmutable
{
    public const string SinComprobanteInformativo = "Informational";
    public const string SinComprobanteValorCero = "ZeroValue";

    /// <summary>Las dos puntas de un traslado resolvieron la misma cuenta, sucursal, tercero y cruce (C21, T55).</summary>
    public const string SinComprobanteMismasCuentas = "SameAccounts";

    public Guid MessagePublicId { get; init; }

    public string MessageType { get; init; } = string.Empty;

    public short MessageVersion { get; init; } = 1;

    public IntegrationMessageKind MessageKind { get; init; }

    /// <summary><c>CAR</c>.</summary>
    public string SourceModule { get; init; } = string.Empty;

    /// <summary>La operación del crédito, la parte de una corrida o de un cierre, la orden de giro, la activación, la carga o el cierre.</summary>
    public Guid SourcePublicId { get; init; }

    public string? SourceDocumentClass { get; init; }

    public string? SourceDocumentTypeCode { get; init; }

    public string? SourceDocumentNumber { get; init; }

    /// <summary>El crédito del hecho; nulo en las partes y en los informativos.</summary>
    public Guid? LoanPublicId { get; init; }

    public string? LoanNumber { get; init; }

    /// <summary>El hecho original de un reverso, de un ajuste de Comercial o de la parte del cierre que se revierte (T12, T55).</summary>
    public Guid? RelatedMessagePublicId { get; init; }

    /// <summary>La fecha contable de Cartera (T36): la del comprobante.</summary>
    public DateOnly OperationDate { get; init; }

    /// <summary>Nulo en informativos, valor cero o mismas cuentas.</summary>
    public long? AccountingDocumentId { get; init; }

    public AccountingDocument? AccountingDocument { get; init; }

    /// <summary><see cref="SinComprobanteInformativo"/>, <see cref="SinComprobanteValorCero"/> o <see cref="SinComprobanteMismasCuentas"/>; nulo si hay comprobante.</summary>
    public string? NoVoucherReason { get; init; }

    /// <summary>El <c>COR_IntegrationBatches</c> por el que pasó, si pasó por uno.</summary>
    public Guid? BatchPublicId { get; init; }

    /// <summary>Quién originó el hecho: una persona o el proceso de la cooperativa (T11). Dato, nunca actor.</summary>
    public ActorKind OriginActorKind { get; init; } = ActorKind.Person;

    /// <summary>Nulo cuando el hecho lo originó el proceso.</summary>
    public Guid? OriginUserCentralId { get; init; }

    public string OriginUserName { get; init; } = string.Empty;

    public ActorKind ActorKind { get; init; }

    public int? ActorUserId { get; init; }

    /// <summary>«Proceso de integración» o la persona que ordenó el lote o el reproceso (§22).</summary>
    public string ActorName { get; init; } = string.Empty;

    public DateTime ProcessedAt { get; init; }
}
