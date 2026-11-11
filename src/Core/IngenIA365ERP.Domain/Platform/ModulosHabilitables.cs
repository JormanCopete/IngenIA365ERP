namespace IngenIA365ERP.Domain.Platform;

/// <summary>
/// Los módulos que el maestro habilita por cooperativa (feature 020, R1; FR-001 a FR-004). Es un catálogo fijo en código: un módulo
/// habilitable nuevo es una versión nueva, no un dato. Cada uno dice su código (el que se guarda en <c>ADM_TenantModules.ModuleCode</c>),
/// su nombre, el prefijo de sus permisos —que se retiran de la sesión con el módulo apagado— y el de sus rutas.
/// </summary>
public static class ModulosHabilitables
{
    /// <summary>Transporte: vehículos, conductores, servicios por vehículo y su facturación.</summary>
    public const string Transporte = "TRN";

    public static IReadOnlyList<ModuloHabilitable> Todos { get; } =
    [
        new(Transporte, "Transporte", "Transport.", "/api/transport"),
    ];

    private static readonly Dictionary<string, ModuloHabilitable> PorCodigo =
        Todos.ToDictionary(m => m.Codigo, StringComparer.OrdinalIgnoreCase);

    /// <summary>El módulo con ese código, o nulo si no es habilitable.</summary>
    public static ModuloHabilitable? Buscar(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? null : PorCodigo.GetValueOrDefault(codigo.Trim());

    /// <summary>¿Es un código de módulo habilitable?</summary>
    public static bool Existe(string? codigo) => Buscar(codigo) is not null;

    /// <summary>El módulo al que pertenece un permiso, o nulo si el permiso no es de un módulo habilitable.</summary>
    public static ModuloHabilitable? DelPermiso(string? permiso) =>
        string.IsNullOrEmpty(permiso) ? null : Todos.FirstOrDefault(m => permiso.StartsWith(m.PrefijoDePermisos, StringComparison.Ordinal));
}

/// <param name="Codigo">Tres letras, en mayúsculas.</param>
/// <param name="Nombre">El que ve el maestro.</param>
/// <param name="PrefijoDePermisos">Con el punto final (<c>Transport.</c>).</param>
/// <param name="PrefijoDeRutas">El de su grupo de la API (<c>/api/transport</c>).</param>
public sealed record ModuloHabilitable(string Codigo, string Nombre, string PrefijoDePermisos, string PrefijoDeRutas);
