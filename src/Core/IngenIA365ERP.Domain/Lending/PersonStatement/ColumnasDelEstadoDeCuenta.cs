namespace IngenIA365ERP.Domain.Lending.PersonStatement;

/// <summary>
/// 2026-11-13 (pedido del dueño): las columnas que el estado de cuenta de una persona (<c>/cartera/estado-de-cuenta</c>) puede mostrar por cada
/// producto —crédito, aportes, ahorro—. Cada usuario elige las suyas y quedan en su preferencia <c>cartera.estado-de-cuenta.columnas</c>; la lista
/// es cerrada porque la preferencia la escribe el propio usuario y sin ella sería un almacén libre. La pantalla (Shared) repite estos códigos con
/// sus nombres. El tipo y el valor de cada columna los pone la aplicación. (nuevo)
/// </summary>
public static class ColumnasDelEstadoDeCuenta
{
    public sealed record Columna(string Codigo, string Nombre, bool PorDefecto);

    public static readonly IReadOnlyList<Columna> Todas =
    [
        new("producto", "Producto", true),
        new("numero", "Número", true),
        new("rol", "Rol", false),
        new("estado", "Estado", true),
        new("fecha", "Fecha del crédito o apertura", true),
        new("valorInicial", "Valor inicial", true),
        new("saldo", "Saldo", true),
        new("paraCancelar", "Total para cancelar", false),
        new("cuota", "Cuota", true),
        new("periodicidad", "Periodicidad", true),
        new("plazo", "Plazo (cuotas)", true),
        new("plazoMeses", "Plazo (meses)", false),
        new("tasa", "Tasa E.A.", true),
        new("tipoDeCuota", "Tipo de cuota", true),
        new("primerVencimiento", "Primer vencimiento", true),
        new("proximoVencimiento", "Próximo vencimiento", false),
        new("valorProximaCuota", "Valor próxima cuota", false),
        new("ultimoVencimiento", "Último vencimiento", false),
        new("cuotasPagadas", "Cuotas pagadas", false),
        new("cuotasPendientes", "Cuotas pendientes", true),
        new("cuotasVencidas", "Cuotas vencidas", false),
        new("cuotasAnticipadas", "Cuotas anticipadas", false),
        new("cuotasExtras", "Cuotas extras", false),
        new("diasDeMora", "Días de mora", true),
        new("valorVencido", "Valor vencido", true),
        new("interesCorriente", "Interés corriente", false),
        new("interesDeMora", "Interés de mora", false),
        new("seguro", "Seguro", false),
        new("anticipo", "Anticipo / saldo a favor", false),
        new("disponible", "Disponible", false),
        new("calificacion", "Calificación", false),
        new("oficina", "Oficina", false),
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
