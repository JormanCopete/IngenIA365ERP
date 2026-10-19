namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 014, US9 (T158, T159; contracts/api.md §16 de la 014): la carga de migración de aportes y captaciones, su cuadre, las cifras de referencia y
// el estado de la activación por naturaleza. Espejo de los DTOs de la API: los enums llegan como número (la naturaleza: 2 captaciones, 3 aportes; el
// estado de la carga: 1 borrador, 2 aplicada, 3 reemplazada, 4 activada). (nuevo)

/// <summary>Una parte de la carga con su resumen: si es obligatoria, si ya se carga (o con qué historia llega), tandas y filas vigentes.</summary>
public sealed record ParteDeCargaDeCaptacionesDto(string Part, string Name, bool Required, bool Available, string? AvailableWith, int Batches, int Rows);

/// <summary>Un total cargado por línea y concepto.</summary>
public sealed record TotalDeCargaDeCaptacionesDto(string LineCode, string Concept, decimal Amount, int Accounts);

/// <summary>Una carga de aportes o captaciones.</summary>
public sealed record CargaDeCaptacionesDto(
    Guid PublicId,
    int Number,
    int Nature,
    DateOnly CutoffDate,
    int Status,
    string? Description,
    IReadOnlyList<TandaDeMigracionDto> Parts,
    IReadOnlyList<ParteDeCargaDeCaptacionesDto> PartsSummary,
    DateTime? AppliedAt,
    string? AppliedBy,
    DateOnly? ActivationDeadline,
    bool IsBalanced,
    int AccountCount,
    DiferenciasDeLaCargaDto Differences,
    DateTime? ActivatedAt,
    IReadOnlyList<TotalDeCargaDeCaptacionesDto> Totals,
    IReadOnlyList<ErrorEntrePartesDto> Warnings);

/// <summary>El cuerpo del alta: <c>nature</c> es <c>Deposit</c> o <c>Contribution</c>.</summary>
public sealed record CrearCargaDeCaptacionesRequest(string Nature, DateOnly CutoffDate, string? Notes);

/// <summary>Lo que responde aplicar la carga.</summary>
public sealed record CargaDeCaptacionesAplicadaDto(
    Guid PublicId,
    int Nature,
    int Status,
    DateOnly CutoffDate,
    DateOnly ActivationDeadline,
    ResumenDelCuadreDto BalanceCheck,
    Guid? ReplacedLoadPublicId,
    Guid AppliedMessagePublicId,
    int Accounts,
    IReadOnlyList<TotalDeCargaDeCaptacionesDto> Totals,
    IReadOnlyList<ErrorEntrePartesDto> Warnings);

/// <summary>Una fila del cuadre de captaciones: el rol, la línea, la clase de captación o de aporte, la marca, el conjunto de cuentas y las cifras.</summary>
public sealed record FilaDeCuadreDeCaptacionesDto(
    string DimensionKey,
    string Family,
    string? Concept,
    IReadOnlyList<CuentaDelCuadreDto> Accounts,
    decimal Lending,
    decimal Accounting,
    decimal Difference,
    bool Unresolved,
    string? Line,
    string? Nature,
    string? DepositClass,
    string? ContributionClass,
    string? AccountFlag);

/// <summary>Una cifra de referencia: línea, concepto, valor y de dónde sale (texto libre: nunca el nombre del sistema anterior).</summary>
public sealed record CifraDeReferenciaDto(string LineCode, string Concept, decimal Amount, string? Source);

/// <summary>El cotejo de lo cargado con la cifra de referencia (cargado − referencia).</summary>
public sealed record CotejoDeCifraDto(string LineCode, string Concept, decimal Loaded, int Accounts, decimal? Reference, decimal? Difference, string? Source);

public sealed record CifrasDeReferenciaDto(Guid LoadPublicId, int Nature, DateOnly CutoffDate, IReadOnlyList<CifraDeReferenciaDto> Figures,
    IReadOnlyList<CotejoDeCifraDto> Comparison, decimal TotalLoaded, decimal? TotalReference);

public sealed record CifrasDeReferenciaRequest(IReadOnlyList<CifraDeReferenciaDto> Figures);

/// <summary>El cuadre de la carga con sus diferencias y el cotejo con las cifras de referencia.</summary>
public sealed record CuadreDeCaptacionesDto(
    Guid LoadPublicId,
    int Nature,
    DateOnly CutoffDate,
    int CheckVersion,
    bool IsBalanced,
    IReadOnlyList<FilaDeCuadreDeCaptacionesDto> Rows,
    IReadOnlyList<DiferenciaDeMigracionDto> Differences,
    IReadOnlyList<CotejoDeCifraDto> ReferenceFigures);

/// <summary>La carga que tomaría la activación.</summary>
public sealed record CargaDeActivacionDeCaptacionesDto(Guid PublicId, int Number, DateOnly CutoffDate, int Status, bool IsBalanced, int Accounts);

/// <summary>El estado de la activación de una naturaleza; <see cref="Status"/>: <c>Activated</c> o <c>NotActivated</c>.</summary>
public sealed record EstadoDeActivacionDeCaptacionesDto(
    int Nature,
    string Status,
    DateOnly? ActivationDate,
    Guid? ActivationPublicId,
    CargaDeActivacionDeCaptacionesDto? Load,
    DateOnly? ExpectedDate,
    DateOnly? Deadline,
    bool WithoutLoadAllowed,
    IReadOnlyList<RequisitoDeActivacionDto> Requirements,
    IReadOnlyList<AvisoDeActivacionDto> Warnings);

/// <summary>Los textos de la migración de captaciones (las pantallas no nombran al sistema anterior: «código heredado», «cifras de referencia»).</summary>
public static class TextosDeMigracionDeCaptaciones
{
    public const int NaturalezaCaptaciones = 2;
    public const int NaturalezaAportes = 3;

    public static string Naturaleza(int naturaleza) => naturaleza == NaturalezaAportes ? "Aportes" : "Captaciones";

    public static string Concepto(string concepto) => concepto switch
    {
        "Principal" => "Capital",
        "InterestPayable" => "Interés por pagar",
        "ChargeReceivable" => "Cargos por cobrar",
        "ContributionMinimum" => "Aportes mínimos",
        "ContributionOrdinary" => "Aportes ordinarios",
        "ContributionExtraordinary" => "Aportes extraordinarios",
        "ContributionRevaluation" => "Aportes revalorizados",
        "CapitalDeCaptacion" => "Capital de captación",
        "CapitalDeCaptacionInactiva" => "Capital de cuentas inactivas",
        "InteresPorPagar" => "Interés por pagar",
        "AportesSociales" => "Aportes sociales",
        "CargoDeCuentaPorCobrar" => "Cargos por cobrar",
        _ => concepto,
    };

    /// <summary>Los conceptos que admite cada naturaleza en las cifras de referencia.</summary>
    public static IReadOnlyList<string> ConceptosDe(int naturaleza) => naturaleza == NaturalezaAportes
        ? ["ContributionMinimum", "ContributionOrdinary", "ContributionExtraordinary", "ContributionRevaluation"]
        : ["Principal", "InterestPayable", "ChargeReceivable"];
}
