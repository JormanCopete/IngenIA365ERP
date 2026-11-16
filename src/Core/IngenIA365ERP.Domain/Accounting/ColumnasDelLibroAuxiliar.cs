namespace IngenIA365ERP.Domain.Accounting;

/// <summary>
/// 2026-11-15 (pedido del dueño): las columnas opcionales de las líneas del libro auxiliar por cuenta de movimiento
/// (<c>/contabilidad/libro-auxiliar</c> con «Sólo cuentas de movimiento»). Cada usuario elige las suyas y quedan en su preferencia
/// <c>contabilidad.libro-auxiliar.columnas</c>; la lista es cerrada porque la preferencia la escribe el propio usuario y sin ella sería un
/// almacén libre. Comprobante, débito, crédito y saldo van siempre. Molde: <see cref="Lending.PersonStatement.ColumnasDelEstadoDeCuenta"/>.
/// </summary>
public static class ColumnasDelLibroAuxiliar
{
    public sealed record Columna(string Codigo, string Nombre, bool PorDefecto);

    public const string Fecha = "fecha";
    public const string Tercero = "tercero";
    public const string Documento = "documentoTercero";
    public const string CentroDeCosto = "centroDeCosto";
    public const string DocumentoCruce = "documentoCruce";
    public const string Descripcion = "descripcion";
    public const string Sucursal = "sucursal";

    public static readonly IReadOnlyList<Columna> Todas =
    [
        new(Fecha, "Fecha", true),
        new(Tercero, "Tercero", true),
        new(Documento, "Documento del tercero", false),
        new(CentroDeCosto, "Centro de costo", true),
        new(DocumentoCruce, "Documento cruce", true),
        new(Descripcion, "Descripción", true),
        new(Sucursal, "Sucursal", false),
    ];

    public static readonly IReadOnlySet<string> Codigos = Todas.Select(c => c.Codigo).ToHashSet(StringComparer.Ordinal);

    public static readonly IReadOnlyList<string> PorDefecto = Todas.Where(c => c.PorDefecto).Select(c => c.Codigo).ToList();

    /// <summary>Las elegidas en el orden del catálogo, sin repetir ni desconocidas; vacía o nula = las de por defecto.</summary>
    public static IReadOnlyList<string> Normalizar(IEnumerable<string>? elegidas)
    {
        var pedidas = (elegidas ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToHashSet(StringComparer.Ordinal);
        var validas = Todas.Where(c => pedidas.Contains(c.Codigo)).Select(c => c.Codigo).ToList();
        return validas.Count == 0 ? PorDefecto : validas;
    }
}
