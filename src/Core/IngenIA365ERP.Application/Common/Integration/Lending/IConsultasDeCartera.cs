using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// Las consultas en proceso de Inventario a Cartera (feature 012, I3, T651; contracts/api.md §23.3; FR-014, FR-060, FR-085;
/// decisiones-transversales T32, §2.16). Inventario usa exactamente dos métodos, y <c>InventarioNoConoceContabilidadNiCartera</c> los vigila:
/// Inventario no lee tablas de Cartera, pregunta por aquí. La feature 014 (T043; FR-095, E-04; contrato-de-cuentas §5) suma el tercero,
/// <see cref="ProductosDeCaptacionAsync"/>, para Personas, Asociados y Nómina. La implementación registrada es la de Cartera
/// (<c>Lending/Integration/ConsultasDeCartera</c>, feature 013, que cerró la entrega IC): antes de activar Cartera responde
/// <see cref="CarteraNoHabilitada"/> y rige el crédito provisional. Las respuestas <b>no se persisten</b> en Inventario: la forma de
/// <c>status</c> y <c>lines</c> la fija la especificación de Cartera.
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

    /// <summary>
    /// Feature 014 (FR-095, E-04): las cuentas de captación, CDAT y aportes de unas personas, con estado y saldo. Lee en bloque y no aplica el alcance
    /// de la Cartera (lo autoriza el permiso del módulo que llama). Antes de activar responde las dos naturalezas <c>NotActivated</c> y listas vacías.
    /// </summary>
    Task<ProductosDeCaptacionDto> ProductosDeCaptacionAsync(ConsultaDeProductosDeCaptacion consulta, CancellationToken ct);
}

/// <summary>
/// Lo que se pregunta de los productos de captación (contrato-de-cuentas §5): las personas, la fecha, si incluye las cuentas canceladas y si incluye
/// las cuentas donde la persona es parte (cotitular, firmante, representante, beneficiario) además de las suyas como titular. (nuevo)
/// </summary>
public sealed record ConsultaDeProductosDeCaptacion(
    IReadOnlyList<Guid> PersonPublicIds, DateOnly Date, bool IncludeClosed = false, bool IncludeAsParty = true);

/// <summary>Los productos de captación de unas personas, con el estado de cada naturaleza. (nuevo)</summary>
public sealed record ProductosDeCaptacionDto(
    EstadoDeCartera DepositsStatus,
    EstadoDeCartera ContributionsStatus,
    IReadOnlyList<ProductosDePersonaDto> People)
{
    /// <summary>La respuesta de antes de activar: las dos naturalezas sin activar y ninguna persona.</summary>
    public static ProductosDeCaptacionDto NoHabilitada { get; } = new(EstadoDeCartera.NotActivated, EstadoDeCartera.NotActivated, []);
}

/// <summary>
/// Los productos de una persona: si tiene alguno vivo como titular o cotitular (bloquea su baja, <c>Person.HasLiveDepositProducts</c>) y si es titular en
/// una carga aplicada sin activar. (nuevo)
/// </summary>
public sealed record ProductosDePersonaDto(
    Guid PersonPublicId,
    bool HasLiveProducts,
    bool MigratedNotActivated,
    IReadOnlyList<ProductoDeCaptacionDto> Products);

/// <summary>Una cuenta de la persona: número, naturaleza, clase, línea, papel, estado, saldo, disponible, vencimiento y cierre. (nuevo)</summary>
public sealed record ProductoDeCaptacionDto(
    Guid AccountPublicId, string Number, NaturalezaDeCuenta Nature, ClaseDeCaptacion? DepositClass,
    string LineCode, string LineName, RolEnLaCuenta Role, EstadoDeCuenta Status,
    decimal Balance, decimal Available, DateOnly? MaturityDate, DateOnly? ClosedOn);

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
