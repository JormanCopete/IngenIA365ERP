using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// Las consultas en proceso de Inventario a Cartera (feature 012, I3, T651; contracts/api.md §23.3; FR-014, FR-060, FR-085;
/// decisiones-transversales T32, §2.16). Exactamente dos métodos, y <c>InventarioNoConoceContabilidadNiCartera</c> los vigila:
/// Inventario no lee tablas de Cartera, pregunta por aquí. Mientras la entrega IC esté pendiente (D-02) la implementación
/// registrada es <see cref="ConsultasDeCarteraNoHabilitada"/>, que responde <see cref="CarteraNoHabilitada"/>: rige el crédito
/// provisional. Las respuestas son <b>provisionales y no se persisten</b> en Inventario: la forma de <c>status</c> y
/// <c>lines</c> la fija la especificación de Cartera.
/// </summary>
public interface IConsultasDeCartera
{
    /// <summary>
    /// El estado crediticio de la persona para una venta a crédito, con el <see cref="CancellationToken"/> de
    /// <c>Cartera.ConsultaSegundos</c>. Si no hay destino <c>Lending</c> registrado, la marca <see cref="CarteraNoHabilitada"/>.
    /// </summary>
    Task<EstadoCrediticioDto> EstadoCrediticioAsync(ConsultaCrediticia consulta, CancellationToken ct);

    /// <summary>La validación posterior de una <c>VentaACreditoRegistrada</c> (FR-061): pendiente mientras IC no exista.</summary>
    Task<EstadoDeValidacionDto> EstadoDeValidacionAsync(Guid messagePublicId, CancellationToken ct);
}

/// <summary>
/// Lo que se le pregunta a Cartera de una venta a crédito (§23.3): la persona, la clase del medio (asociado o cliente), el valor,
/// la fecha de operación y el canal. (nuevo)
/// </summary>
public sealed record ConsultaCrediticia(Guid PersonPublicId, PaymentMeansClass MeansClass, decimal Amount, DateOnly OperationDate, string? SalesChannelCode);

/// <summary>Una línea de crédito que Cartera ofrece (provisional: la forma la fija D-02). (nuevo)</summary>
public sealed record LineaDeCreditoDto(string Code, string Name, int? MaxTermMonths, int? MaxInstallments)
{
    /// <summary>Feature 013 (DE-12): periodicidades de Comercial con equivalencia en <c>LND_CommercialTermEquivalences</c>.</summary>
    public IReadOnlyList<string> Periodicities { get; init; } = [];
}

/// <summary>
/// La respuesta de <see cref="IConsultasDeCartera.EstadoCrediticioAsync"/> (§23.3): el estado de la persona, el cupo disponible,
/// las líneas y el evento de auditoría que guarda la respuesta como evidencia. Provisional, no persistido.
/// </summary>
public record EstadoCrediticioDto(string Status, decimal? AvailableQuota, IReadOnlyList<LineaDeCreditoDto> Lines, string? EvidenceId)
{
    /// <summary>Feature 013 (DE-12): cupo total otorgado.</summary>
    public decimal? TotalQuota { get; init; }

    /// <summary>Feature 013 (DE-12): días de mora, con <see cref="EstadosCrediticios.InArrears"/>.</summary>
    public int? DaysPastDue { get; init; }

    /// <summary>Feature 013 (DE-12): motivo del bloqueo, con <see cref="EstadosCrediticios.Blocked"/>; en español y sin detalle de listas.</summary>
    public string? BlockReason { get; init; }

    /// <summary>Feature 013 (DE-12): si Cartera admite la venta, según el estado y <c>Cupo.MoraMaximaParaVender</c>.</summary>
    public bool AllowsSale { get; init; }

    /// <summary>¿Respondió Cartera de verdad? Falso en la marca <see cref="CarteraNoHabilitada"/>.</summary>
    public virtual bool LendingEnabled => true;
}

/// <summary>Valores de <see cref="EstadoCrediticioDto.Status"/> (feature 013, interfaces §5.1). (nuevo)</summary>
public static class EstadosCrediticios
{
    public const string Active = "Active";
    public const string InArrears = "InArrears";
    public const string Blocked = "Blocked";
    public const string NoRelationship = "NoRelationship";
    public const string NotEnabled = "NotEnabled";
}

/// <summary>Estados de la validación posterior (§23.2). (nuevo)</summary>
public static class EstadosDeValidacion
{
    public const string Pending = "Pending";
    public const string Validated = "Validated";
    public const string Failed = "Failed";

    /// <summary>Feature 013 (FR-143): la venta se aceptó sin migrar en la activación, o no se pudo convertir en obligación.</summary>
    public const string NotIncorporated = "NotIncorporated";
}

/// <summary>
/// La respuesta de <see cref="IConsultasDeCartera.EstadoDeValidacionAsync"/> (§23.2): <c>Pending</c>, <c>Validated</c> o
/// <c>Failed</c>, cuándo se evaluó y por qué.
/// </summary>
public sealed record EstadoDeValidacionDto(string Status, DateTime? EvaluatedAt, string? Reason);
