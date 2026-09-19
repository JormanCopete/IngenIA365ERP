namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// Espejo en el cliente de <c>Application.Accounting.Accounts.LongitudDeAuxiliar</c> (feature 009,
/// aclaración del dueño del 2026-09-18): la longitud del código es una regla fija por nivel, no
/// una configuración. Niveles del catálogo con largo exacto (2, 4 y 6 dígitos para grupo, cuenta y
/// subcuenta propias); nivel 5: 7 a 9 dígitos; nivel 6: 10 a 12. Sirve para los textos de ayuda
/// del plan y de la configuración; quien valida es el servidor.
/// </summary>
public static class LongitudDeAuxiliar
{
    public const int Maximo = 12;

    public static (int Minimo, int Maximo) Rango(int nivel) => nivel switch
    {
        2 => (2, 2),
        3 => (4, 4),
        4 => (6, 6),
        5 => (7, 9),
        6 => (10, 12),
        _ => (0, 0),
    };

    /// <summary>«entre 7 y 9 dígitos» o «6 dígitos».</summary>
    public static string Texto(int nivel)
    {
        var (min, max) = Rango(nivel);
        return min == 0 ? "la longitud de su nivel" : min == max ? $"{min} dígitos" : $"entre {min} y {max} dígitos";
    }
}
