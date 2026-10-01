using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Lo que <see cref="EmisorDeMensajes"/> necesita para emitir los mensajes de <b>un</b> evento de un origen (feature
/// 012, T7, T9, T078): el origen, la clave del evento, los contenidos, el modo y las cadenas de las que depende.
/// Todos los contenidos de una solicitud forman una unidad por destino (contracts/mensajes.md §9): nacen juntos y
/// tienen las mismas dependencias.
/// </summary>
/// <param name="Origen">El documento o la operación que emite.</param>
/// <param name="OriginEventKey">Uno de los valores de <see cref="ClavesDeEvento"/>, con la forma que admite cada tipo.</param>
/// <param name="Contenidos">Records <c>&lt;Type&gt;V1</c> del catálogo (<c>CatalogoDeMensajesV1</c>); al menos uno.</param>
/// <param name="Modo">Cómo nace la entrega a Contabilidad de un mensaje de negocio: sellado o heredado del original.</param>
/// <param name="CadenasDeLasQueDepende">
/// Los orígenes de un derivado (<c>derivedFrom</c>) y, en un ajuste de costo, el documento afectado. Su propio origen,
/// el relacionado y aquél de quien hereda el modo se agregan solos.
/// </param>
/// <param name="Relacionado">El documento que éste anula, corrige, devuelve o ajusta; nulo en un original.</param>
/// <param name="ValidacionPrevia">Resultado de la validación previa sellado en cada mensaje (SC-021); nulo si no aplica.</param>
/// <param name="KindDelOriginal">El <c>Kind</c> que hereda un <c>DocumentoAnulado</c>; obligatorio si lo hay.</param>
public sealed record SolicitudDeEmision(
    OrigenDeEmision Origen,
    string OriginEventKey,
    IReadOnlyList<object> Contenidos,
    ModoDeEntrega Modo,
    IReadOnlyList<Guid>? CadenasDeLasQueDepende = null,
    DocumentoRelacionado? Relacionado = null,
    PrevalidationOutcome? ValidacionPrevia = null,
    IntegrationMessageKind? KindDelOriginal = null);

/// <summary>
/// El origen de los mensajes: las columnas del sobre que no son del contenido. La clase del documento va como
/// <b>texto</b> (el nombre de <c>DocumentClass</c>): la plataforma no depende de los tipos de Inventario.
/// </summary>
/// <param name="Kind"><c>Document</c> u <c>Operation</c> (cierre, reapertura, reclasificación).</param>
/// <param name="PublicId">Del documento o de la operación.</param>
/// <param name="DocumentClass">Nombre de la clase; con <c>Document</c>.</param>
/// <param name="DocumentTypeCode">Código del tipo de documento; con <c>Document</c>.</param>
/// <param name="Number">Número visible con prefijo, o el rótulo de la operación.</param>
/// <param name="OperationDate">Fecha de operación: la del comprobante, nunca la del lote.</param>
/// <param name="BranchPublicId">Sucursal contable de la operación.</param>
/// <param name="CostCenterPublicId">Centro de costo del documento.</param>
/// <param name="WarehouseCode">Bodega principal (la de origen en un traslado).</param>
/// <param name="PersonPublicId">Tercero del documento.</param>
/// <param name="FiscalUniqueCode">CUFE, CUDE o CUDS si el origen es fiscal y ya lo tiene.</param>
public sealed record OrigenDeEmision(
    MessageOriginKind Kind,
    Guid PublicId,
    string? DocumentClass,
    string? DocumentTypeCode,
    string Number,
    DateOnly OperationDate,
    Guid BranchPublicId,
    Guid? CostCenterPublicId = null,
    string? WarehouseCode = null,
    Guid? PersonPublicId = null,
    string? FiscalUniqueCode = null);

/// <summary>El documento que un mensaje anula, corrige, devuelve o ajusta (<c>related</c>), con su clase como texto.</summary>
public sealed record DocumentoRelacionado(Guid PublicId, string DocumentClass, string Number);

/// <summary>
/// Cómo nace la entrega a Contabilidad de un mensaje de <b>negocio</b> (T9; contracts/mensajes.md §11). Los
/// informativos y los de Cartera nacen siempre <c>Always</c>/<c>Pending</c>, sea cual sea el modo.
/// </summary>
public abstract record ModoDeEntrega
{
    private ModoDeEntrega() { }

    /// <summary>
    /// El modo que el documento selló al confirmar (<c>INV_Documents.PostingMode</c>, o el valor general en una
    /// operación). <see cref="Modo"/> es <c>Online</c>, <c>Batch</c> o <c>NotPosted</c>; con <c>Batch</c>,
    /// <see cref="ScheduleKey"/> es obligatoria (<see cref="ClavesDeLote.Horario"/>) y <see cref="BatchScopeKey"/>
    /// la pone el disparador que la usa. Un original es la raíz de su cadena.
    /// </summary>
    public sealed record Sellado(DeliveryMode Modo, string? ScheduleKey = null, string? BatchScopeKey = null) : ModoDeEntrega;

    /// <summary>
    /// Un relacionado o derivado <b>no lee el parámetro</b>: copia <c>Mode</c> y <c>ScheduleKey</c> de la entrega a
    /// Contabilidad del último mensaje de <see cref="OriginalPublicId"/> (FR-075, FR-079), y su raíz de cadena.
    /// <see cref="BatchScopeKey"/> es la del propio documento (su sesión o su período), no la del original.
    /// </summary>
    public sealed record Heredado(Guid OriginalPublicId, string? BatchScopeKey = null) : ModoDeEntrega;
}
