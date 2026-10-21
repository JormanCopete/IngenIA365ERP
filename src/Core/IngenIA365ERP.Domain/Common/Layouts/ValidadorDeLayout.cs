using System.Globalization;
using System.Text;

namespace IngenIA365ERP.Domain.Common.Layouts;

/// <summary>Severidad de una inconsistencia: la bloqueante impide escribir el archivo; la advertencia se reconoce.</summary>
public enum SeveridadDeInconsistencia
{
    Blocking = 1,
    Warning = 2,
}

/// <summary>
/// Una fila del archivo como la entrega la proyección: el código del registro (<c>Header</c>, <c>Detail</c>, <c>Totals</c>…), la
/// referencia del crédito que la origina (para decir dónde está el problema) y los valores por ruta. Una ruta que no viene se trata
/// como vacía. (nuevo)
/// </summary>
public sealed record FilaDeLayout(string Registro, string? Credito, IReadOnlyDictionary<string, object?> Valores)
{
    public object? Valor(string ruta) => Valores.TryGetValue(ruta, out var v) ? v : null;
}

/// <summary>
/// Lo que el validador encontró en una fila: severidad, código estable (<c>Layout.*</c>), registro, campo (número y nombre), crédito,
/// ruta del valor, índice de la fila (desde 0) y un mensaje para la persona. (nuevo)
/// </summary>
public sealed record InconsistenciaDeLayout(
    SeveridadDeInconsistencia Severidad,
    string Codigo,
    string Registro,
    int? Campo,
    string? NombreDelCampo,
    string? Credito,
    string? Ruta,
    int Fila,
    string Mensaje);

/// <summary>Los códigos que devuelve <see cref="ValidadorDeLayout"/>.</summary>
public static class CodigosDeLayout
{
    public const string RegistroDesconocido = "Layout.RegistroDesconocido";
    public const string CampoObligatorioVacio = "Layout.CampoObligatorioVacio";
    public const string ValorNoPermitido = "Layout.ValorNoPermitido";
    public const string LargoExcedido = "Layout.LargoExcedido";
    public const string ValorNoNumerico = "Layout.ValorNoNumerico";
    public const string NoEsEntero = "Layout.NoEsEntero";
    public const string DemasiadosDecimales = "Layout.DemasiadosDecimales";
    public const string FechaInvalida = "Layout.FechaInvalida";
    public const string ContieneDelimitador = "Layout.ContieneDelimitador";
    public const string CaracterNoRepresentable = "Layout.CaracterNoRepresentable";
}

/// <summary>
/// Valida las filas contra el layout (feature 013, K5, US16; adaptadores-y-archivos.md §7.1): obligatorio vacío, valor fuera de los
/// admitidos, largo excedido, número o fecha que no se pueden escribir y delimitador dentro del texto son <b>bloqueantes</b>; un carácter
/// que la codificación del archivo no representa es <b>advertencia</b> (se escribiría como <c>?</c>). Puro y determinista: no lee nada
/// fuera de lo que recibe. (nuevo)
/// </summary>
public static class ValidadorDeLayout
{
    public static IReadOnlyList<InconsistenciaDeLayout> Validar(LayoutDeArchivo layout, IReadOnlyList<FilaDeLayout> filas)
    {
        var codificacion = layout.Codificacion();
        var resultado = new List<InconsistenciaDeLayout>();
        for (var i = 0; i < filas.Count; i++)
        {
            var fila = filas[i];
            var registro = layout.Registro(fila.Registro);
            if (registro is null)
            {
                resultado.Add(new(SeveridadDeInconsistencia.Blocking, CodigosDeLayout.RegistroDesconocido, fila.Registro, null, null, fila.Credito, null, i,
                    $"El layout {layout.Code} no tiene el registro «{fila.Registro}»."));
                continue;
            }
            foreach (var campo in registro.EnOrden(layout.FileKind))
            {
                var r = FormateadorDeCampo.Crudo(layout, campo, fila);
                InconsistenciaDeLayout Nueva(SeveridadDeInconsistencia s, string codigo, string mensaje) =>
                    new(s, codigo, registro.Code, campo.Number, campo.Name, fila.Credito, campo.Path, i, mensaje);

                if (r.Error is { } error)
                {
                    resultado.Add(Nueva(SeveridadDeInconsistencia.Blocking, error.Codigo, $"Campo {campo.Number} «{campo.Name}»: {error.Mensaje}"));
                    continue;
                }
                var texto = r.Texto!;
                if (texto.Length == 0)
                {
                    if (campo.Required)
                        resultado.Add(Nueva(SeveridadDeInconsistencia.Blocking, CodigosDeLayout.CampoObligatorioVacio, $"Campo {campo.Number} «{campo.Name}»: es obligatorio y está vacío."));
                    continue;
                }
                if (texto.Length > campo.Length)
                    resultado.Add(Nueva(SeveridadDeInconsistencia.Blocking, CodigosDeLayout.LargoExcedido,
                        $"Campo {campo.Number} «{campo.Name}»: «{texto}» tiene {texto.Length} caracteres y el campo admite {campo.Length}."));
                if (campo.AllowedValues.Count > 0 && !campo.AllowedValues.Contains(texto, StringComparer.Ordinal))
                    resultado.Add(Nueva(SeveridadDeInconsistencia.Blocking, CodigosDeLayout.ValorNoPermitido,
                        $"Campo {campo.Number} «{campo.Name}»: «{texto}» no es un valor admitido ({string.Join(", ", campo.AllowedValues)})."));
                if (layout.FileKind == TipoDeArchivoDeLayout.Delimited
                    && (texto.Contains(layout.Delimiter!, StringComparison.Ordinal) || texto.Contains('\r') || texto.Contains('\n')))
                    resultado.Add(Nueva(SeveridadDeInconsistencia.Blocking, CodigosDeLayout.ContieneDelimitador,
                        $"Campo {campo.Number} «{campo.Name}»: el valor contiene el delimitador «{layout.Delimiter}» o un salto de línea."));
                if (!Representable(codificacion, texto))
                    resultado.Add(Nueva(SeveridadDeInconsistencia.Warning, CodigosDeLayout.CaracterNoRepresentable,
                        $"Campo {campo.Number} «{campo.Name}»: «{texto}» tiene caracteres que la codificación {layout.Encoding} no representa."));
            }
        }
        return resultado;
    }

    private static bool Representable(Encoding codificacion, string texto)
    {
        if (codificacion is UTF8Encoding or UnicodeEncoding or UTF32Encoding) return true;
        return codificacion.GetString(codificacion.GetBytes(texto)) == texto;
    }
}

/// <summary>
/// Convierte el valor de un campo en texto según su formato (adaptadores-y-archivos.md §7.2). Nunca redondea: un entero con fracción o
/// un monto con más decimales de los que caben es un error, no un valor ajustado en silencio. (nuevo)
/// </summary>
public static class FormateadorDeCampo
{
    public sealed record ErrorDeCampo(string Codigo, string Mensaje);

    public readonly record struct Resultado(string? Texto, ErrorDeCampo? Error);

    /// <summary>El valor sin relleno: la constante o el de la ruta, formateado; vacío si no hay valor.</summary>
    public static Resultado Crudo(LayoutDeArchivo layout, CampoDeLayout campo, FilaDeLayout fila)
    {
        object? valor = campo.Constant ?? (campo.Path is null ? null : fila.Valor(campo.Path));
        if (valor is null || valor is string s && string.IsNullOrWhiteSpace(s)) return new(string.Empty, null);
        return Formatear(layout, campo, valor);
    }

    /// <summary>El texto ajustado al campo: en ancho fijo, relleno hasta su largo con la alineación y el carácter del campo.</summary>
    public static string Ajustar(LayoutDeArchivo layout, CampoDeLayout campo, string texto)
    {
        var rellenar = layout.FileKind == TipoDeArchivoDeLayout.FixedWidth || campo.Pad is not null;
        if (!rellenar || texto.Length >= campo.Length) return texto;
        var relleno = campo.RellenoEfectivo;
        if (campo.AlineacionEfectiva == AlineacionDeCampo.Left) return texto.PadRight(campo.Length, relleno);
        // Un negativo con relleno de ceros lleva el signo delante: «-000123», no «000-123».
        if (relleno == '0' && texto.StartsWith('-')) return "-" + texto[1..].PadLeft(campo.Length - 1, '0');
        return texto.PadLeft(campo.Length, relleno);
    }

    private static Resultado Formatear(LayoutDeArchivo layout, CampoDeLayout campo, object valor)
    {
        switch (campo.Format)
        {
            case FormatoDeCampo.Integer:
            {
                if (valor is bool b) return new(b ? "1" : "0", null);
                if (!ComoDecimal(valor, out var d)) return NoNumerico(valor);
                if (decimal.Truncate(d) != d) return new(null, new(CodigosDeLayout.NoEsEntero, $"«{Texto(valor)}» no es un entero."));
                return new(d.ToString("0", CultureInfo.InvariantCulture), null);
            }
            case FormatoDeCampo.Amount2:
            case FormatoDeCampo.AmountPoint2:
            {
                if (!ComoDecimal(valor, out var d)) return NoNumerico(valor);
                if (decimal.Round(d, 2) != d) return Decimales(valor, 2);
                return campo.Format == FormatoDeCampo.Amount2
                    ? new((d * 100).ToString("0", CultureInfo.InvariantCulture), null)
                    : new(d.ToString("0.00", CultureInfo.InvariantCulture), null);
            }
            case FormatoDeCampo.Rate:
            {
                if (!ComoDecimal(valor, out var d)) return NoNumerico(valor);
                var decimales = campo.Decimals ?? 0;
                if (decimal.Round(d, decimales) != d) return Decimales(valor, decimales);
                var patron = decimales == 0 ? "0" : "0." + new string('0', decimales);
                return new(d.ToString(patron, CultureInfo.InvariantCulture), null);
            }
            case FormatoDeCampo.Date:
            {
                DateOnly? fecha = valor switch
                {
                    DateOnly d => d,
                    DateTime dt => DateOnly.FromDateTime(dt),
                    DateTimeOffset dto => DateOnly.FromDateTime(dto.DateTime),
                    string s when DateOnly.TryParseExact(s.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var p) => p,
                    _ => null,
                };
                return fecha is { } f
                    ? new(f.ToString(campo.PatronDeFecha, CultureInfo.InvariantCulture), null)
                    : new(null, new(CodigosDeLayout.FechaInvalida, $"«{Texto(valor)}» no es una fecha."));
            }
            default:
            {
                var texto = Texto(valor).Trim();
                return new(layout.NormalizeText ? Normalizar(texto) : texto, null);
            }
        }
    }

    /// <summary>Mayúsculas, sin tildes ni diéresis (Ñ → N) y lo que no sea ASCII imprimible, espacio (como la PILA).</summary>
    public static string Normalizar(string texto)
    {
        var descompuesto = texto.ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var ch in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch is >= (char)32 and <= (char)126 ? ch : ' ');
        }
        return sb.ToString();
    }

    private static Resultado NoNumerico(object valor) =>
        new(null, new(CodigosDeLayout.ValorNoNumerico, $"«{Texto(valor)}» no es un número."));

    private static Resultado Decimales(object valor, int decimales) =>
        new(null, new(CodigosDeLayout.DemasiadosDecimales, $"«{Texto(valor)}» tiene más de {decimales} decimales; el archivo no redondea."));

    private static string Texto(object valor) =>
        valor is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : valor.ToString() ?? string.Empty;

    private static bool ComoDecimal(object valor, out decimal d)
    {
        switch (valor)
        {
            case decimal x: d = x; return true;
            case int x: d = x; return true;
            case long x: d = x; return true;
            case short x: d = x; return true;
            case byte x: d = x; return true;
            case string s: return decimal.TryParse(s.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out d);
            default: d = 0; return false;
        }
    }
}
