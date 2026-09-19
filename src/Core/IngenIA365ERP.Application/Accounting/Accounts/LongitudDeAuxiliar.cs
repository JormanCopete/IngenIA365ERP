namespace IngenIA365ERP.Application.Accounting.Accounts;

/// <summary>
/// Feature 009 (FR-008, aclaración del dueño del 2026-09-18): la longitud del código de una
/// cuenta es una regla fija por nivel, no una configuración. Los niveles del catálogo llevan
/// largo exacto (clase 1, grupo 2, cuenta 4, subcuenta 6 dígitos); una auxiliar de nivel 5
/// lleva entre 7 y 9 dígitos y una sub-auxiliar de nivel 6, hija de una de nivel 5, entre 10 y
/// 12. Con movimiento en el nivel 5 sólo existe el primer rango. Lo consultan la creación de
/// cuentas, la pantalla del plan y la configuración.
///
/// <para>
/// Los niveles 2 a 4 los crea la empresa sólo como <b>cuenta propia</b> bajo un nodo del
/// catálogo que no trae hijos (el CUIF solidario deja así 127 cuentas y 6 grupos: reservas,
/// fondos sociales, provisiones, excedentes…); donde el catálogo sí define subcuentas, se
/// respetan y las auxiliares cuelgan de ellas.
/// </para>
/// </summary>
public static class LongitudDeAuxiliar
{
    public const int Maximo = 12;

    /// <summary>Mínimo y máximo de dígitos del código para un nivel (2 a 6).</summary>
    public static (int Minimo, int Maximo) Rango(int nivel) => nivel switch
    {
        2 => (2, 2),
        3 => (4, 4),
        4 => (6, 6),
        5 => (7, 9),
        6 => (10, 12),
        _ => throw new ArgumentOutOfRangeException(nameof(nivel), nivel, "Los niveles que crea la empresa van del 2 al 6."),
    };

    public static bool EsAuxiliar(int nivel) => nivel is 5 or 6;

    /// <summary>«entre 7 y 9 dígitos» o «6 dígitos», para pantallas y mensajes.</summary>
    public static string Texto(int nivel)
    {
        var (min, max) = Rango(nivel);
        return min == max ? $"{min} dígitos" : $"entre {min} y {max} dígitos";
    }

    /// <summary>Null si el código cabe en el rango de su nivel; si no, la frase que lo explica.</summary>
    public static string? Reparo(string codigo, int nivel)
    {
        if (nivel is < 2 or > 6) return $"El nivel {nivel} no lo crea la empresa.";
        var (min, max) = Rango(nivel);
        return codigo.Length >= min && codigo.Length <= max
            ? null
            : $"Una cuenta de nivel {nivel} lleva {Texto(nivel)}; {codigo} tiene {codigo.Length}.";
    }
}
