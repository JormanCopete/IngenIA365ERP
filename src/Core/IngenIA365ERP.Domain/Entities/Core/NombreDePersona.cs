namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Cómo se muestra el nombre de una persona natural. Es el único sitio que lo arma: listados,
/// buscadores, colillas, informes y documentos de nómina lo piden aquí.
/// Desde la feature 010 (D-06) el nombre tiene cuatro partes (<see cref="Person.FirstName"/>,
/// <see cref="Person.OtherNames"/>, <see cref="Person.LastName"/>, <see cref="Person.SecondLastName"/>).
/// Hasta el 2026-09-25 cada pantalla lo armaba con «primer nombre + primer apellido», así que
/// «WILLIAN ANDRÉS LAGOS PÉREZ» salía como «WILLIAN LAGOS».
///
/// <para>En una consulta EF se usa en la proyección <b>final</b> (EF lo evalúa en memoria). No se
/// usa en <c>Where</c> ni en <c>OrderBy</c>: ahí se filtra y se ordena por las columnas.</para>
/// </summary>
public static class NombreDePersona
{
    /// <summary>Nombres y después apellidos: «WILLIAN ANDRÉS LAGOS PÉREZ». Omite las partes vacías.</summary>
    public static string Completo(string? primerNombre, string? otrosNombres, string? primerApellido, string? segundoApellido) =>
        Unir(primerNombre, otrosNombres, primerApellido, segundoApellido);

    /// <summary>Apellidos y después nombres: «LAGOS PÉREZ WILLIAN ANDRÉS», para listas ordenadas por apellido.</summary>
    public static string ApellidosYNombres(string? primerNombre, string? otrosNombres, string? primerApellido, string? segundoApellido) =>
        Unir(primerApellido, segundoApellido, primerNombre, otrosNombres);

    /// <summary>El nombre completo de una persona ya cargada.</summary>
    public static string Completo(Person p) => Completo(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName);

    private static string Unir(params string?[] partes) =>
        string.Join(' ', partes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
}
