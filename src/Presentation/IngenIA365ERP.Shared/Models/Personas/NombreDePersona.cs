namespace IngenIA365ERP.Shared.Models.Personas;

/// <summary>
/// Cómo se muestra el nombre de una persona en el cliente. Es la copia en <c>Shared</c> —que no
/// referencia <c>Domain</c>— de <c>IngenIA365ERP.Domain.Entities.Core.NombreDePersona</c>, con la
/// misma regla: las partes no vacías, recortadas, nombres primero. Se usa sólo cuando la API no
/// manda el nombre ya compuesto (<c>FullName</c>); si lo manda, se muestra ése.
/// Hasta el 2026-09-25 las pantallas lo armaban con «primer nombre + primer apellido», así que
/// «WILLIAN ANDRÉS LAGOS PÉREZ» salía como «WILLIAN LAGOS».
/// </summary>
public static class NombreDePersona
{
    /// <summary>Nombres y después apellidos: «WILLIAN ANDRÉS LAGOS PÉREZ». Omite las partes vacías.</summary>
    public static string Completo(string? primerNombre, string? otrosNombres, string? primerApellido, string? segundoApellido) =>
        Unir(primerNombre, otrosNombres, primerApellido, segundoApellido);

    /// <summary>Apellidos y después nombres: «LAGOS PÉREZ WILLIAN ANDRÉS».</summary>
    public static string ApellidosYNombres(string? primerNombre, string? otrosNombres, string? primerApellido, string? segundoApellido) =>
        Unir(primerApellido, segundoApellido, primerNombre, otrosNombres);

    /// <summary>La razón social si la hay; si no, el nombre completo de la persona natural.</summary>
    public static string Visible(string? razonSocial, string? primerNombre, string? otrosNombres, string? primerApellido, string? segundoApellido) =>
        !string.IsNullOrWhiteSpace(razonSocial)
            ? razonSocial.Trim()
            : Completo(primerNombre, otrosNombres, primerApellido, segundoApellido);

    /// <summary>
    /// El nombre que ya compuso el servidor si viene; si no (una API anterior), lo arma con las partes.
    /// </summary>
    public static string PreferirCompuesto(string? compuesto, string? primerNombre, string? otrosNombres, string? primerApellido, string? segundoApellido) =>
        !string.IsNullOrWhiteSpace(compuesto)
            ? compuesto.Trim()
            : Completo(primerNombre, otrosNombres, primerApellido, segundoApellido);

    private static string Unir(params string?[] partes) =>
        string.Join(' ', partes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
}
