namespace IngenIA365ERP.Application.Accounting.Accounts;

/// <summary>
/// Feature 009 (FR-008, aclaración del dueño del 2026-09-18): la longitud del código de una
/// auxiliar ya no se configura; es una regla fija por nivel. El catálogo llega al nivel 4 con
/// 6 dígitos; una auxiliar de nivel 5 lleva entre 7 y 9 dígitos y una sub-auxiliar de nivel 6,
/// hija de una de nivel 5, entre 10 y 12. Con movimiento en el nivel 5 sólo existe el primer
/// rango. Lo consultan la creación de cuentas, la pantalla del plan y la configuración.
/// </summary>
public static class LongitudDeAuxiliar
{
    public const int Maximo = 12;

    /// <summary>Mínimo y máximo de dígitos del código para un nivel de auxiliar (5 o 6).</summary>
    public static (int Minimo, int Maximo) Rango(int nivel) => nivel switch
    {
        5 => (7, 9),
        6 => (10, 12),
        _ => throw new ArgumentOutOfRangeException(nameof(nivel), nivel, "Sólo los niveles 5 y 6 son auxiliares."),
    };

    public static bool EsAuxiliar(int nivel) => nivel is 5 or 6;

    /// <summary>«entre 7 y 9 dígitos», para pantallas y mensajes.</summary>
    public static string Texto(int nivel)
    {
        var (min, max) = Rango(nivel);
        return $"entre {min} y {max} dígitos";
    }

    /// <summary>Null si el código cabe en el rango de su nivel; si no, la frase que lo explica.</summary>
    public static string? Reparo(string codigo, int nivel)
    {
        if (!EsAuxiliar(nivel)) return $"El nivel {nivel} no es de auxiliar.";
        var (min, max) = Rango(nivel);
        return codigo.Length >= min && codigo.Length <= max
            ? null
            : $"Una cuenta de nivel {nivel} lleva {Texto(nivel)}; {codigo} tiene {codigo.Length}.";
    }
}
