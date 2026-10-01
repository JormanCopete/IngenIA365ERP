using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// El sobre de todo mensaje de integración, igual para los veinte tipos (feature 012, T8, T075;
/// contracts/mensajes.md §4). Sus campos son columnas inmutables de <c>COR_IntegrationMessages</c>; el
/// contenido <c>&lt;Type&gt;V1</c> es <c>PayloadJson</c>. El consumidor recibe los dos juntos.
///
/// <para>
/// El orden de las propiedades es el del contrato: con <see cref="OpcionesDeMensajes"/> el JSON sale en ese
/// orden, byte a byte (<c>ContratosDeMensajesTests</c>). <see cref="Payload"/> se declara <c>object</c> para
/// que se escriba con su tipo real; al leer llega como <c>JsonElement</c> y lo interpreta quien conoce el
/// <see cref="Type"/> (I2, <c>IMensajesEntrantes</c>). La cooperativa, el modo de paso y el resultado de la
/// validación previa <b>no</b> van en el sobre (§4, «Lo que no va en el sobre»).
/// </para>
/// </summary>
public sealed record IntegrationEnvelopeV1
{
    /// <summary><c>COR_IntegrationMessages.PublicId</c>; clave de idempotencia del destino.</summary>
    public Guid MessageId { get; init; }

    /// <summary>Uno de los veinte de §1.</summary>
    public string Type { get; init; } = string.Empty;

    public int Version { get; init; } = 1;

    public IntegrationMessageKind Kind { get; init; }

    /// <summary>Siempre <c>"INV"</c>.</summary>
    public string OriginModule { get; init; } = "INV";

    /// <summary>Qué evento del origen lo produjo (§10.1).</summary>
    public string OriginEventKey { get; init; } = string.Empty;

    public MessageOriginV1 Origin { get; init; } = new();

    /// <summary>El documento que éste anula, corrige, devuelve o ajusta; nulo en un original.</summary>
    public DocumentRefV1? Related { get; init; }

    public Guid ChainRootPublicId { get; init; }

    public Guid BranchPublicId { get; init; }

    public Guid? CostCenterPublicId { get; init; }

    public string? WarehouseCode { get; init; }

    public Guid? PersonPublicId { get; init; }

    public string Currency { get; init; } = "COP";

    public decimal ExchangeRate { get; init; } = 1m;

    /// <summary>Quien confirmó u ordenó: dato, nunca actor (FR-083).</summary>
    public UserRefV1 OriginUser { get; init; } = new();

    /// <summary>Instante UTC del <c>SaveChanges</c> que lo emitió.</summary>
    public DateTimeOffset EmittedAt { get; init; }

    /// <summary>El contenido <c>&lt;Type&gt;V1</c> (§6 a §8).</summary>
    public object? Payload { get; init; }
}

/// <summary>
/// <c>origin</c> del sobre (contracts/mensajes.md §4): el documento o la operación que origina el mensaje.
/// <see cref="DocumentClass"/> y <see cref="DocumentTypeCode"/> van con <c>Kind = Document</c>.
/// </summary>
public sealed record MessageOriginV1
{
    public MessageOriginKind Kind { get; init; }

    public DocumentClass? DocumentClass { get; init; }

    public string? DocumentTypeCode { get; init; }

    /// <summary>Número visible con prefijo, o el rótulo de la operación (<c>"2026-10"</c>).</summary>
    public string Number { get; init; } = string.Empty;

    public Guid PublicId { get; init; }

    public DateOnly OperationDate { get; init; }

    /// <summary>CUFE, CUDE o CUDS del documento fiscal que emite la cooperativa.</summary>
    public string? FiscalUniqueCode { get; init; }
}
