using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Domain.Entities.Integration.Transactions;

/// <summary>
/// Un mensaje de negocio emitido (<c>COR_IntegrationMessages</c>; feature 012, T7, T9; data-model §19;
/// contracts/mensajes.md §4). Es un <b>hecho</b>: lo agrega <c>EmisorDeMensajes</c> dentro del
/// <c>SaveChanges</c> que confirma su documento y no se modifica nunca (Principio XI). Lo que cambia es su
/// entrega por destino (<see cref="IntegrationMessageDelivery"/>).
///
/// <para>
/// <see cref="BaseEntityLong.PublicId"/> es el <c>MessageId</c> del sobre y la clave de idempotencia del
/// destino. El <c>Id bigint</c> es el <b>orden</b> (un relacionado siempre se emite después de su original)
/// y no sale del módulo: <b>nunca</b> aparece en JSON ni en una respuesta HTTP (Principio VI).
/// </para>
///
/// <para>
/// <see cref="OriginDocumentClass"/> y <see cref="RelatedDocumentClass"/> guardan el nombre de la clase como
/// texto: la plataforma no depende de los tipos de Inventario y mañana emiten otros módulos.
/// <see cref="PayloadSha256"/> es el SHA-256 de los bytes exactos de <see cref="PayloadJson"/>: el consumidor
/// compara contra lo guardado y nunca reserializa.
/// </para>
/// </summary>
public class IntegrationMessage : AuditableEntityLong, IHechoInmutable
{
    /// <summary>Uno de los veinte tipos de contracts/mensajes.md §1 (<c>VentaFacturada</c>…).</summary>
    public string Type { get; init; } = string.Empty;

    public short Version { get; init; } = 1;

    public IntegrationMessageKind Kind { get; init; }

    /// <summary>Siempre <c>INV</c> por ahora.</summary>
    public string OriginModule { get; init; } = string.Empty;

    public MessageOriginKind OriginKind { get; init; }

    /// <summary>El documento o la operación que lo origina.</summary>
    public Guid OriginPublicId { get; init; }

    public string? OriginDocumentClass { get; init; }

    public string? OriginDocumentTypeCode { get; init; }

    public string? OriginNumber { get; init; }

    /// <summary>Qué evento del origen lo produjo (contracts/mensajes.md §10.1). Con origen y tipo, es único.</summary>
    public string OriginEventKey { get; init; } = string.Empty;

    /// <summary>CUFE, CUDE o CUDS si el origen es fiscal y ya lo tiene.</summary>
    public string? FiscalUniqueCode { get; init; }

    /// <summary>El documento que éste anula, corrige, devuelve o ajusta; nulo en un original.</summary>
    public Guid? RelatedPublicId { get; init; }

    public string? RelatedDocumentClass { get; init; }

    public string? RelatedNumber { get; init; }

    /// <summary>La raíz de la cadena: aquél cuyo modo de paso sellado heredó la entrega.</summary>
    public Guid ChainRootPublicId { get; init; }

    /// <summary>La fecha del documento; es la fecha del comprobante, nunca la del lote.</summary>
    public DateOnly OperationDate { get; init; }

    public Guid BranchPublicId { get; init; }

    public Guid? CostCenterPublicId { get; init; }

    public string? WarehouseCode { get; init; }

    public Guid? PersonPublicId { get; init; }

    public string Currency { get; init; } = "COP";

    public decimal ExchangeRate { get; init; } = 1m;

    /// <summary>El contenido <c>&lt;Type&gt;V1</c> serializado con <c>OpcionesDeMensajes</c>: texto UTF-8 exacto.</summary>
    public string PayloadJson { get; init; } = string.Empty;

    /// <summary>SHA-256 de los bytes UTF-8 de <see cref="PayloadJson"/>, hexadecimal en minúsculas.</summary>
    public string PayloadSha256 { get; init; } = string.Empty;

    /// <summary>Resultado de la validación previa sellado al confirmar (SC-021); no es contenido de negocio.</summary>
    public PrevalidationOutcome? PrevalidationOutcome { get; init; }

    /// <summary>Quien confirmó u ordenó: un <b>dato</b>, nunca un actor (FR-083).</summary>
    public Guid OriginUserCentralId { get; init; }

    public string OriginUserName { get; init; } = string.Empty;

    /// <summary>Instante UTC del <c>SaveChanges</c> que lo emitió.</summary>
    public DateTime EmittedAt { get; init; }
}
