namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// Lo único de Cartera que Contabilidad conoce, además de la bandeja (feature 013, enmienda A1, C27; contracts/contabilidad.md §16.2):
/// los códigos para validar una regla de la matriz al guardarla y para la plantilla, y las combinaciones en uso para la completitud.
/// Lo implementa <c>DimensionesDeCartera</c> en <c>Application/Lending/Integration</c> (T0657) <b>sin</b> el alcance de oficinas de
/// Cartera: los puertos para otros módulos no lo aplican (T62). Firma estable desde US7 (T0642); hasta que exista la implementación, la
/// matriz se administra con un doble en las pruebas. Lo fija <c>LaCarteraSoloSeTocaPorSusInterfaces</c>. (nuevo)
/// </summary>
public interface IDimensionesDeCartera
{
    /// <summary>Códigos para validar una regla al guardarla y para la plantilla: líneas (también cerradas), modalidades con sus sub-segmentos, conceptos, clases de recaudador y tipos de hecho.</summary>
    Task<CatalogoDeDimensionesDeCarteraDto> CatalogoAsync(CancellationToken ct);

    /// <summary>Combinaciones con saldo o movimiento a una fecha, para la completitud.</summary>
    Task<IReadOnlyList<CombinacionDeCarteraEnUsoDto>> CombinacionesEnUsoAsync(DateOnly fecha, CancellationToken ct);
}

/// <summary>El catálogo de dimensiones de Cartera (§16.2). (nuevo)</summary>
public sealed record CatalogoDeDimensionesDeCarteraDto(
    IReadOnlyList<LineaDeCarteraDto> CreditLines,
    IReadOnlyList<ModalidadDeCarteraDto> Modalities,
    IReadOnlyList<string> Concepts,
    IReadOnlyList<string> CollectorClasses,
    IReadOnlyList<string> FactTypes)
{
    /// <summary>Un catálogo sin nada (cooperativa sin Cartera).</summary>
    public static CatalogoDeDimensionesDeCarteraDto Vacio { get; } = new([], [], [], [], []);
}

/// <summary>Una línea de crédito por su código, con su modalidad y su sub-segmento. (nuevo)</summary>
/// <remarks>
/// Desde la feature 014 (T049; contabilidad.md §1.3) lleva la naturaleza de la línea (<c>LineNature</c> como texto): el catálogo devuelve las de
/// las tres naturalezas. La clase de captación llega con las versiones de línea de captación (K8).
/// </remarks>
public sealed record LineaDeCarteraDto(string Code, string Name, string ModalityCode, string? SubsegmentCode, bool Active, string LineNature = "Placement");

/// <summary>Una modalidad del <c>CatalogoDeModalidades</c> vigente con sus sub-segmentos. (nuevo)</summary>
public sealed record ModalidadDeCarteraDto(string Code, string Name, IReadOnlyList<string> SubsegmentCodes);

/// <summary>Una combinación de saldo o movimiento en uso a una fecha: el rol que mueve y las dimensiones con que lo mueve. (nuevo)</summary>
public sealed record CombinacionDeCarteraEnUsoDto(
    string Role,
    string? CreditLineCode,
    string ModalityCode,
    string? SubsegmentCode,
    string? RiskCategory,
    string? ConceptCode,
    bool IsEmployee,
    bool InGracePeriod,
    bool Restructured,
    Guid BranchPublicId,
    DateOnly LastUsedAt);
