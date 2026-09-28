using System.Globalization;

namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// Lo que entra por el campo de lectura del POS (feature 012, I3, T640; FR-059, contracts/api.md §20.2): un código exacto (de barras, de
/// empaque o de producto) que decide el servidor, o «3*código» para leer tres. «3*» solo queda esperando la lectura siguiente. Un
/// multiplicador que no es un número positivo es un error; lo que no empieza por un número es un código aunque tenga asterisco. (nuevo)
/// </summary>
public sealed record LecturaDelPos(string? Codigo, decimal? Cantidad, string? Error)
{
    public static readonly LecturaDelPos Nada = new(null, null, null);

    /// <summary>Sólo el multiplicador («4*»): la pantalla lo guarda para la lectura siguiente.</summary>
    public bool EsSoloMultiplicador => Codigo is null && Cantidad is not null && Error is null;

    public static LecturaDelPos Interpretar(string? texto)
    {
        var t = texto?.Trim() ?? string.Empty;
        if (t.Length == 0) return Nada;

        var asterisco = t.IndexOf('*');
        if (asterisco <= 0) return new(t, null, null);

        var prefijo = t[..asterisco].Trim().Replace(',', '.');
        if (!EsNumero(prefijo)) return new(t, null, null);
        if (!decimal.TryParse(prefijo, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var cantidad) || cantidad <= 0m)
            return new(null, null, $"«{t[..asterisco].Trim()}» no es una cantidad: el multiplicador va antes del asterisco y es mayor que cero.");

        var codigo = t[(asterisco + 1)..].Trim();
        return new(codigo.Length == 0 ? null : codigo, cantidad, null);
    }

    /// <summary>Aplica el multiplicador que esperaba si la lectura no trae el suyo.</summary>
    public LecturaDelPos ConMultiplicador(decimal? pendiente) => Cantidad is null && pendiente is not null && Codigo is not null ? this with { Cantidad = pendiente } : this;

    private static bool EsNumero(string s) =>
        s.Length > 0 && s.All(c => char.IsDigit(c) || c is '.' || c is '-') && s.Any(char.IsDigit);
}
