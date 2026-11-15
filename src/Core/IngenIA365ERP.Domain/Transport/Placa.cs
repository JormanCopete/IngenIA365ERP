using System.Text;

namespace IngenIA365ERP.Domain.Transport;

/// <summary>
/// La placa de un vehículo normalizada (feature 020, R3; FR-005): mayúsculas, sin espacios, guiones ni puntos. «abc-123», «ABC 123»
/// y «ABC123» son la misma placa. Válida: de 5 a 10 caracteres, letras ASCII y dígitos. La unicidad entre vivos se mide sobre la
/// forma normalizada, que es la que se guarda.
/// </summary>
public static class Placa
{
    public const int LargoMinimo = 5;
    public const int LargoMaximo = 10;

    /// <summary>La forma normalizada, o cadena vacía si no queda nada.</summary>
    public static string Normalizar(string? placa)
    {
        if (string.IsNullOrWhiteSpace(placa)) return string.Empty;
        var sb = new StringBuilder(placa.Length);
        foreach (var c in placa.Trim().ToUpperInvariant())
        {
            if (c is ' ' or '-' or '.' or '_' or '\t') continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>¿Es una placa válida una vez normalizada?</summary>
    public static bool EsValida(string? placa)
    {
        var p = Normalizar(placa);
        return p.Length is >= LargoMinimo and <= LargoMaximo && p.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9');
    }
}
