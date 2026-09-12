using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>Feature 006 US5: centro de reportes de nómina (<c>/api/reports/payroll/{vista}</c>).</summary>
public sealed partial class NominaClient
{
    public Task<InvitationApiResult<TablaReporteDto>> ReporteAsync(string ruta, CancellationToken ct = default) =>
        EnviarAsync<TablaReporteDto>(HttpMethod.Get, $"/api/reports/payroll/{ruta}", null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DescargarReporteAsync(string ruta, string formato, CancellationToken ct = default) =>
        DescargarAsync($"/api/reports/payroll/{ruta}{(ruta.Contains('?') ? "&" : "?")}format={formato}", ct);
}

public sealed record ColumnaReporteDto(string Nombre, string Tipo, string? Clave)
{
    public bool EsNumerica => Tipo is "Entero" or "Moneda" or "Decimal";
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
