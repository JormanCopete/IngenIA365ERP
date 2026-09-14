namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>Un valor de catálogo cerrado del formulario de persona: código guardado y texto mostrado.</summary>
public sealed record OpcionDeCatalogo(string Codigo, string Nombre);

/// <summary>
/// Los catálogos cerrados del formulario de persona, en un solo sitio (feature 008). Hasta el
/// 2026-09-13 vivían copiados a mano en Personas, Empleados y Asociados, y las copias
/// divergían. Son códigos del modelo SOLIDO (una o dos letras) que la base guarda tal cual;
/// no son catálogos de tabla, por eso no salen de la API.
/// </summary>
public static class CatalogosDePersona
{
    public static readonly IReadOnlyList<OpcionDeCatalogo> TiposDeDocumento =
    [
        new("C", "Cédula de ciudadanía"),
        new("CE", "Cédula de extranjería"),
        new("TI", "Tarjeta de identidad"),
        new("RC", "Registro civil"),
        new("NI", "NIT"),
        new("PA", "Pasaporte"),
        new("PE", "Permiso especial"),
    ];

    public static readonly IReadOnlyList<OpcionDeCatalogo> TiposDePersona =
    [
        new("01", "Natural"),
        new("02", "Jurídica"),
    ];

    public static readonly IReadOnlyList<OpcionDeCatalogo> Generos =
    [
        new("M", "Masculino"),
        new("F", "Femenino"),
        new("O", "Otro"),
    ];

    public static readonly IReadOnlyList<OpcionDeCatalogo> EstadosCiviles =
    [
        new("S", "Soltero(a)"),
        new("C", "Casado(a)"),
        new("U", "Unión libre"),
        new("D", "Divorciado(a)"),
        new("V", "Viudo(a)"),
    ];

    public static readonly IReadOnlyList<OpcionDeCatalogo> NivelesEducativos =
    [
        new("PR", "Primaria"),
        new("BA", "Bachillerato"),
        new("TE", "Técnico"),
        new("TG", "Tecnológico"),
        new("PG", "Profesional"),
        new("ES", "Especialización"),
        new("MA", "Maestría"),
        new("DR", "Doctorado"),
    ];

    public static readonly IReadOnlyList<OpcionDeCatalogo> Estados =
    [
        new("A", "Activo"),
        new("I", "Inactivo"),
        new("R", "Retirado"),
    ];

    /// <summary>Texto de un código, o el código mismo si no está en el catálogo (datos migrados).</summary>
    public static string Nombre(IReadOnlyList<OpcionDeCatalogo> catalogo, string? codigo) =>
        catalogo.FirstOrDefault(o => o.Codigo == codigo)?.Nombre ?? codigo ?? "";
}
