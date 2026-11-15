namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// Las columnas opcionales del libro auxiliar por cuenta de movimiento (2026-11-15): el mismo catálogo cerrado de
/// <c>Domain.Accounting.ColumnasDelLibroAuxiliar</c> —el servidor rechaza un código que no esté allá—, aquí para pintar las casillas.
/// Comprobante, débito, crédito y saldo van siempre. Lo vigila <c>LasColumnasDelLibroAuxiliarSonLasMismas</c>.
/// </summary>
public static class ColumnasDelLibroAuxiliar
{
    public sealed record Columna(string Codigo, string Nombre, bool PorDefecto);

    public static readonly IReadOnlyList<Columna> Todas =
    [
        new("fecha", "Fecha", true),
        new("tercero", "Tercero", true),
        new("documentoTercero", "Documento del tercero", false),
        new("centroDeCosto", "Centro de costo", true),
        new("documentoCruce", "Documento cruce", true),
        new("descripcion", "Descripción", true),
        new("sucursal", "Sucursal", false),
    ];

    public static readonly IReadOnlyList<string> PorDefecto = Todas.Where(c => c.PorDefecto).Select(c => c.Codigo).ToList();

    /// <summary>Las elegidas en el orden del catálogo; vacía o con códigos desconocidos = las de por defecto.</summary>
    public static IReadOnlyList<string> Normalizar(IEnumerable<string>? elegidas)
    {
        var pedidas = (elegidas ?? []).ToHashSet(StringComparer.Ordinal);
        var validas = Todas.Where(c => pedidas.Contains(c.Codigo)).Select(c => c.Codigo).ToList();
        return validas.Count == 0 ? PorDefecto : validas;
    }
}
