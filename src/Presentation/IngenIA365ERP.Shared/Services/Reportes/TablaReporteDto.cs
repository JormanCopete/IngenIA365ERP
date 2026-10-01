namespace IngenIA365ERP.Shared.Services.Reportes;

/// <summary>
/// Espejo de <c>TablaExportable</c> (Application/Common/Reports): lo que pintan las pantallas de
/// reportes de nómina y contabilidad. Nació en Nómina (feature 006); desde la 009 E2 vive aquí
/// porque contabilidad usa las mismas tablas. Una columna cuya <see cref="Clave"/> empieza con «_»
/// es <b>oculta</b>: lleva claves de profundización y no se pinta.
/// </summary>
public sealed record ColumnaReporteDto(string Nombre, string Tipo, string? Clave)
{
    public bool EsNumerica => Tipo is "Entero" or "Moneda" or "Decimal" or "Porcentaje" or "Cantidad" or "Costo";
    public bool EsOculta => Clave is not null && Clave.StartsWith('_');
}

public sealed record FilaReporteDto(IReadOnlyList<System.Text.Json.JsonElement> Valores, string? Seccion, bool Resaltada);

public sealed record TablaReporteDto(
    string Titulo,
    string Subtitulo,
    IReadOnlyList<ColumnaReporteDto> Columnas,
    IReadOnlyList<FilaReporteDto> Filas,
    FilaReporteDto? Totales,
    IReadOnlyList<string> Notas)
{
    /// <summary>Formatea un valor JSON según el tipo de su columna, igual que los exportadores.</summary>
    public static string Texto(System.Text.Json.JsonElement v, string tipo)
    {
        var cultura = System.Globalization.CultureInfo.GetCultureInfo("es-CO");
        switch (v.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined: return string.Empty;
            case System.Text.Json.JsonValueKind.Number:
                var d = v.GetDecimal();
                return tipo switch
                {
                    "Moneda" => d.ToString("N0", cultura),
                    "Entero" => d.ToString("N0", cultura),
                    "Decimal" => d.ToString("0.##", cultura),
                    "Porcentaje" => d.ToString("N2", cultura) + " %",
                    // Feature 012 (T157): cantidades a 4 decimales y costos o factores a 6, como en los archivos.
                    "Cantidad" => d.ToString("N4", cultura),
                    "Costo" => d.ToString("N6", cultura),
                    _ => d.ToString("N2", cultura),
                };
            case System.Text.Json.JsonValueKind.True: return "Sí";
            case System.Text.Json.JsonValueKind.False: return "No";
            default:
                var s = v.GetString() ?? string.Empty;
                if (tipo == "Fecha" && DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var f))
                    return f.ToString("dd/MM/yyyy", cultura);
                return s;
        }
    }
}
