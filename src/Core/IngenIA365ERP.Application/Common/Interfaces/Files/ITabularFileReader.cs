using System.Globalization;
using System.Text;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Interfaces.Files;

/// <summary>Una fila leída de un archivo tabular; <see cref="Numero"/> es el de la fila en el archivo (para señalar errores).</summary>
public sealed record FilaLeida(int Numero, IReadOnlyList<string?> Celdas)
{
    public string? this[int indice] => indice >= 0 && indice < Celdas.Count ? Celdas[indice] : null;

    public bool EstaVacia => Celdas.All(string.IsNullOrWhiteSpace);
}

/// <summary>
/// Lo que devuelve el lector: encabezados (de la última fila de encabezado) y filas con sus
/// celdas como texto crudo. Quien la consume decide qué columna es qué (por nombre o por
/// posición) y cómo interpretar cada celda.
/// </summary>
public sealed record TablaLeida(IReadOnlyList<string> Encabezados, IReadOnlyList<FilaLeida> Filas, string Formato)
{
    /// <summary>Índice de la columna con ese encabezado (sin distinguir mayúsculas, tildes ni espacios), o −1.</summary>
    public int IndiceDe(string encabezado)
    {
        var buscado = Normalizar(encabezado);
        for (var i = 0; i < Encabezados.Count; i++)
            if (Normalizar(Encabezados[i]) == buscado) return i;
        return -1;
    }

    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var ch in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && ch != ' ' && ch != '_')
                sb.Append(ch);
        return sb.ToString();
    }
}

/// <summary>
/// Lee un archivo tabular (hoja de cálculo o texto separado) a celdas de texto. La
/// implementación vive en la API (ClosedXML, Principio II); Application sólo conoce este
/// contrato. Lo usan el importador de catálogos, la apertura y el extracto bancario (feature 009, R13).
/// </summary>
public interface ITabularFileReader
{
    Task<Result<TablaLeida>> LeerAsync(byte[] contenido, string nombreArchivo, int filasDeEncabezado = 1, CancellationToken ct = default);
}

public static class ArchivosTabulares
{
    public static readonly Error Vacio = new("Archivo.Vacio", "El archivo está vacío.");

    public static Error Ilegible(string detalle) =>
        new("Archivo.Ilegible", $"No se pudo leer el archivo: {detalle}");

    public static Error SinEncabezado(string columna) =>
        new("Archivo.ColumnaFaltante", $"El archivo no trae la columna «{columna}».");
}
