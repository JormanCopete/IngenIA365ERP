using System.Globalization;
using System.Text;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;

namespace IngenIA365ERP.Application.Implantacion.Cotejo;

/// <summary>Qué trae una columna del archivo de referencia: un texto (llave, documento, nombre, estado), un importe en pesos o un entero.</summary>
public enum ClaseDeColumnaDeReferencia
{
    Texto = 0,
    Importe = 1,
    Entero = 2,
}

/// <summary>
/// Una columna de la plantilla de una opción del cotejo. Se reconoce por su nombre o por cualquiera de sus <see cref="Alias"/>, sin distinguir
/// mayúsculas, tildes ni espacios (<see cref="TablaLeida.Normalizar"/>). Una columna opcional que el archivo no trae no se compara.
/// </summary>
public sealed record ColumnaDeReferencia(string Nombre, bool Obligatoria, ClaseDeColumnaDeReferencia Clase, string Descripcion, string Ejemplo,
    params string[] Alias);

/// <summary>Una fila leída del archivo: su número en el archivo y un valor por columna de la plantilla (nulo si la columna no vino).</summary>
public sealed record FilaDeReferencia(int Numero, IReadOnlyDictionary<string, object?> Valores)
{
    public string Texto(string columna) => Valores.GetValueOrDefault(columna) as string ?? string.Empty;

    public decimal? Importe(string columna) => Valores.GetValueOrDefault(columna) as decimal?;

    public bool Trae(string columna) => Valores.ContainsKey(columna);
}

/// <summary>El archivo leído: sus filas y qué columnas opcionales trajo.</summary>
public sealed record ArchivoDeReferencia(IReadOnlyList<FilaDeReferencia> Filas, IReadOnlySet<string> Columnas)
{
    public bool Trae(string columna) => Columnas.Contains(columna);
}

/// <summary>
/// La lectura del archivo de cifras de referencia de una opción del cotejo (nuevo). La primera hoja, encabezados en la fila 1 (la plantilla los
/// trae así). Los importes se aceptan como los exportan las hojas de cálculo y los informes: con o sin separador de miles, con punto o coma
/// decimal, con «$» y negativos con signo o entre paréntesis. Un importe vacío en una columna que vino es cero. Toda fila con un error lo
/// reporta con su número y columna y, si hay alguno, no se coteja nada: <c>Implantacion.Cotejo.ArchivoInvalido</c> con <c>data.errors[]</c>.
/// </summary>
public static class LecturaDeReferencia
{
    public const string ArchivoInvalidoCodigo = "Implantacion.Cotejo.ArchivoInvalido";

    /// <summary>Más filas que esto ya no es una cooperativa sino un archivo mal armado: se rechaza antes de calcular.</summary>
    public const int MaximoDeFilas = 200_000;

    public static Error ArchivoInvalido(IReadOnlyList<ErrorDeFila> errores) =>
        new ErrorConDatos(ArchivoInvalidoCodigo,
            $"El archivo tiene {errores.Count} error(es); corríjalos y vuelva a cotejar. No se cotejó nada.",
            new { errors = errores.OrderBy(e => e.Row).ThenBy(e => e.Column, StringComparer.Ordinal).Take(500).ToList(), total = errores.Count });

    public static Result<ArchivoDeReferencia> Leer(TablaLeida tabla, IReadOnlyList<ColumnaDeReferencia> columnas)
    {
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var c in columnas)
        {
            var i = new[] { c.Nombre }.Concat(c.Alias).Select(tabla.IndiceDe).FirstOrDefault(x => x >= 0, -1);
            if (i >= 0) indices[c.Nombre] = i;
            else if (c.Obligatoria) return Result.Failure<ArchivoDeReferencia>(ArchivosTabulares.SinEncabezado(c.Nombre));
        }

        var filas = tabla.Filas.Where(f => !f.EstaVacia).ToList();
        if (filas.Count == 0) return Result.Failure<ArchivoDeReferencia>(ArchivosTabulares.Vacio);
        if (filas.Count > MaximoDeFilas)
            return Result.Failure<ArchivoDeReferencia>(new Error(ArchivoInvalidoCodigo, $"El archivo trae {filas.Count:N0} filas; el máximo es {MaximoDeFilas:N0}."));

        var errores = new List<ErrorDeFila>();
        var leidas = new List<FilaDeReferencia>(filas.Count);
        foreach (var fila in filas)
        {
            var valores = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var c in columnas)
            {
                if (!indices.TryGetValue(c.Nombre, out var i)) continue;
                var crudo = fila[i]?.Trim();
                switch (c.Clase)
                {
                    case ClaseDeColumnaDeReferencia.Texto:
                        if (c.Obligatoria && string.IsNullOrWhiteSpace(crudo))
                            errores.Add(new ErrorDeFila(fila.Numero, c.Nombre, "Implantacion.Cotejo.Vacio", $"La columna «{c.Nombre}» es obligatoria."));
                        valores[c.Nombre] = crudo ?? string.Empty;
                        break;
                    case ClaseDeColumnaDeReferencia.Importe:
                    case ClaseDeColumnaDeReferencia.Entero:
                        var numero = Numero(crudo);
                        if (numero is null)
                        {
                            errores.Add(new ErrorDeFila(fila.Numero, c.Nombre, "Implantacion.Cotejo.ImporteInvalido", $"«{crudo}» no es un número."));
                            break;
                        }
                        if (c.Clase == ClaseDeColumnaDeReferencia.Entero && decimal.Truncate(numero.Value) != numero.Value)
                        {
                            errores.Add(new ErrorDeFila(fila.Numero, c.Nombre, "Implantacion.Cotejo.ImporteInvalido", $"«{crudo}» no es un número entero."));
                            break;
                        }
                        valores[c.Nombre] = numero.Value;
                        break;
                }
            }
            leidas.Add(new FilaDeReferencia(fila.Numero, valores));
        }
        if (errores.Count > 0) return Result.Failure<ArchivoDeReferencia>(ArchivoInvalido(errores));
        return Result.Success(new ArchivoDeReferencia(leidas, indices.Keys.ToHashSet(StringComparer.Ordinal)));
    }

    /// <summary>
    /// Un número como lo exporta una hoja de cálculo o un informe. Vacío = 0. Con punto y coma, el último es el decimal; un separador repetido es de
    /// miles; uno solo seguido de exactamente tres dígitos también es de miles («150.000» son ciento cincuenta mil: un saldo en pesos no lleva
    /// tres decimales); cualquier otro separador solo es el decimal («1234.5», «1234,56»).
    /// </summary>
    public static decimal? Numero(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return 0m;
        var s = texto.Trim().Replace("$", string.Empty).Replace(" ", string.Empty).Replace(" ", string.Empty);
        var negativo = false;
        if (s.StartsWith('(') && s.EndsWith(')')) { negativo = true; s = s[1..^1]; }
        if (s.StartsWith('-')) { negativo = !negativo; s = s[1..]; }
        else if (s.StartsWith('+')) s = s[1..];
        if (s.EndsWith('-')) { negativo = !negativo; s = s[..^1]; }
        if (s.Length == 0) return null;

        var puntos = s.Count(c => c == '.');
        var comas = s.Count(c => c == ',');
        string normal;
        if (puntos > 0 && comas > 0)
        {
            var decimalEs = s.LastIndexOf('.') > s.LastIndexOf(',') ? '.' : ',';
            var miles = decimalEs == '.' ? ',' : '.';
            normal = s.Replace(miles.ToString(), string.Empty).Replace(decimalEs, '.');
        }
        else if (puntos > 1) normal = s.Replace(".", string.Empty);
        else if (comas > 1) normal = s.Replace(",", string.Empty);
        else if (puntos + comas == 1 && s.Length - s.IndexOfAny(['.', ',']) - 1 == 3) normal = s.Replace(".", string.Empty).Replace(",", string.Empty);
        else if (comas == 1) normal = s.Replace(',', '.');
        else normal = s;

        if (!decimal.TryParse(normal, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor)) return null;
        return negativo ? -valor : valor;
    }

    /// <summary>
    /// Un documento de identidad comparable: sin puntos, espacios ni guiones, en mayúsculas y sin ceros a la izquierda; un NIT con dígito de
    /// verificación («900123456-7») se compara sin él.
    /// </summary>
    public static string Documento(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var s = texto.Trim();
        var guion = s.LastIndexOf('-');
        if (guion > 0 && s.Length - guion - 1 == 1 && char.IsDigit(s[^1])) s = s[..guion];
        return Llave(s);
    }

    /// <summary>Una llave comparable (número de crédito o de cuenta): sólo letras y dígitos, en mayúsculas, sin ceros a la izquierda.</summary>
    public static string Llave(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var sb = new StringBuilder(texto.Length);
        foreach (var ch in texto.Trim().ToUpperInvariant())
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        var limpio = sb.ToString().TrimStart('0');
        return limpio.Length == 0 && sb.Length > 0 ? "0" : limpio;
    }

    /// <summary>La plantilla de una opción: sólo encabezados (la API la vuelve <c>.xlsx</c>) y, en las notas, una línea por columna.</summary>
    public static TablaExportable Plantilla(string titulo, IReadOnlyList<ColumnaDeReferencia> columnas, IReadOnlyList<string> instrucciones)
    {
        var cols = columnas.Select(c => new ColumnaExportable(c.Nombre, c.Clase switch
        {
            ClaseDeColumnaDeReferencia.Importe => TipoDeColumna.Moneda,
            ClaseDeColumnaDeReferencia.Entero => TipoDeColumna.Entero,
            _ => TipoDeColumna.Texto,
        })).ToList();
        var notas = new List<string>(instrucciones)
        {
            string.Empty,
            "Columnas (la fila 1 lleva los encabezados; el orden no importa):",
        };
        notas.AddRange(columnas.Select(c =>
            $"• {c.Nombre} — {(c.Obligatoria ? "obligatoria" : "opcional: si no la trae, no se compara")}. {c.Descripcion} Ejemplo: {c.Ejemplo}."));
        notas.Add(string.Empty);
        notas.Add("Importes: con o sin separador de miles, con punto o coma decimal (a lo sumo dos decimales: «150.000» se lee ciento cincuenta mil); negativos con «-» o entre paréntesis. Vacío es cero.");
        return new TablaExportable($"Plantilla de cifras de referencia — {titulo}", "Llene la hoja «Datos» y súbala en la opción de cotejo.", cols, [], null, notas);
    }
}
