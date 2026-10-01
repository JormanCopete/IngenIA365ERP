using System.Globalization;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces.Files;

namespace IngenIA365ERP.Application.Common.Imports;

/// <summary>
/// Una fila de una hoja, con la conversión de cada tipo de contracts/plantillas.md §0.4 (feature 012, T156). Cada
/// lector devuelve el valor convertido o nulo, y si la celda no cumple su tipo deja el error en el contexto
/// (<c>Import.Cell.Format</c>, <c>Import.Cell.Required</c>, <c>Import.Cell.NotFound</c>) con la hoja, la fila de Excel y
/// la columna: la plantilla sigue leyendo y al final se ven todos los errores juntos. (nuevo)
/// </summary>
public sealed class FilaDeImportacion
{
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    private readonly HojaDeImportacion _hoja;
    private readonly FilaLeida _leida;

    internal FilaDeImportacion(HojaDeImportacion hoja, FilaLeida leida)
    {
        _hoja = hoja;
        _leida = leida;
    }

    /// <summary>El número de la fila en Excel (el encabezado es la 1).</summary>
    public int Numero => _leida.Numero;

    public HojaDeImportacion Hoja => _hoja;

    /// <summary>Si esta fila ya tiene al menos un error (la plantilla puede dejar de procesarla).</summary>
    public bool TieneErrores => _hoja.Contexto.FilaConErrores(this);

    /// <summary>El texto crudo de la celda, recortado; nulo si está vacía o la columna no vino.</summary>
    public string? Crudo(string columna)
    {
        var indice = _hoja.IndiceDe(columna);
        var valor = indice < 0 ? null : _leida[indice];
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }

    public bool EstaVacia(string columna) => Crudo(columna) is null;

    // ---------------------------------------------------------------- errores --

    public void Error(string columna, string codigo, string mensaje) => _hoja.Contexto.Error(this, columna, codigo, mensaje);

    public void Aviso(string columna, string codigo, string mensaje) => _hoja.Contexto.Aviso(this, columna, codigo, mensaje);

    /// <summary>Exige la celda aunque la columna no sea obligatoria en la definición (obligatoria «con PercentOfTax»).</summary>
    public bool Requerido(string columna)
    {
        if (Crudo(columna) is not null) return true;
        Error(columna, ImportErrors.CellRequired, $"La columna «{columna}» es obligatoria en esta fila.");
        return false;
    }

    // ----------------------------------------------------------------- tipos --

    /// <summary>Un código de catálogo (<see cref="CodigoDeCatalogo"/>): patrón, largo y mayúsculas.</summary>
    public string? Codigo(string columna, int? largo = null)
    {
        var crudo = Leer(columna);
        if (crudo is null) return null;
        var maximo = largo ?? _hoja.Columna(columna)?.Largo ?? CodigoDeCatalogo.LargoCorto;
        if (!System.Text.RegularExpressions.Regex.IsMatch(crudo, CodigoDeCatalogo.Patron))
            return Formato(columna, crudo, CodigoDeCatalogo.MensajeDePatron);
        if (crudo.Length > maximo)
            return Formato(columna, crudo, $"El código admite hasta {maximo} caracteres.");
        return CodigoDeCatalogo.Normalizar(crudo);
    }

    /// <summary>Texto libre recortado, con su largo máximo.</summary>
    public string? Texto(string columna, int? largo = null)
    {
        var crudo = Leer(columna);
        if (crudo is null) return null;
        var maximo = largo ?? _hoja.Columna(columna)?.Largo;
        if (maximo is { } m && crudo.Length > m)
            return Formato(columna, crudo, $"Admite hasta {m} caracteres.");
        return crudo;
    }

    public int? Entero(string columna)
    {
        var crudo = Leer(columna);
        if (crudo is null) return null;
        if (int.TryParse(crudo, NumberStyles.AllowLeadingSign, Invariante, out var n)) return n;
        return FormatoNulo<int>(columna, crudo, "Debe ser un número entero, sin decimales ni separador de miles.");
    }

    /// <summary>Hasta 2 decimales.</summary>
    public decimal? Monto(string columna) => Decimal(columna, 2);

    /// <summary>Hasta 4 decimales.</summary>
    public decimal? Cantidad(string columna) => Decimal(columna, 4);

    /// <summary>Costo o factor: hasta 6 decimales.</summary>
    public decimal? Costo(string columna) => Decimal(columna, 6);

    /// <summary>
    /// Un porcentaje escrito en puntos (hasta 4 decimales) devuelto como <b>fracción</b> (T19): <c>19</c> → 0,19;
    /// <c>0,966</c> → 0,00966.
    /// </summary>
    public decimal? Porcentaje(string columna)
    {
        var puntos = Decimal(columna, 4);
        return puntos is null ? null : puntos.Value / 100m;
    }

    /// <summary><c>yyyy-MM-dd</c> o fecha de Excel (número de serie).</summary>
    public DateOnly? Fecha(string columna)
    {
        var crudo = Leer(columna);
        if (crudo is null) return null;
        if (DateOnly.TryParseExact(crudo, "yyyy-MM-dd", Invariante, DateTimeStyles.None, out var fecha)) return fecha;
        // El lector entrega las celdas de fecha con hora como «yyyy-MM-dd HH:mm:ss».
        if (crudo.Length > 10 && DateOnly.TryParseExact(crudo[..10], "yyyy-MM-dd", Invariante, DateTimeStyles.None, out fecha)) return fecha;
        if (double.TryParse(crudo, NumberStyles.AllowDecimalPoint, Invariante, out var serie) && serie is >= 1 and < 2958466)
            return DateOnly.FromDateTime(DateTime.FromOADate(Math.Floor(serie)));
        return FormatoNulo<DateOnly>(columna, crudo, "Debe ser una fecha AAAA-MM-DD.");
    }

    /// <summary><c>HH:mm</c>, hora de Colombia (también <c>HH:mm:ss</c> y la fracción de día de Excel).</summary>
    public TimeOnly? Hora(string columna)
    {
        var crudo = Leer(columna);
        if (crudo is null) return null;
        string[] formatos = ["HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss"];
        if (TimeOnly.TryParseExact(crudo, formatos, Invariante, DateTimeStyles.None, out var hora)) return hora;
        if (double.TryParse(crudo, NumberStyles.AllowDecimalPoint, Invariante, out var fraccion) && fraccion is >= 0 and < 1)
            return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Math.Round(fraccion * 24 * 60)));
        return FormatoNulo<TimeOnly>(columna, crudo, "Debe ser una hora HH:mm.");
    }

    /// <summary>Sí/no: <c>sí, si, s, x, 1</c> = sí; <c>no, n, 0</c> o vacío = <paramref name="porDefecto"/>.</summary>
    public bool SiNo(string columna, bool porDefecto = false)
    {
        var valor = SiNoIndiferente(columna);
        return valor ?? porDefecto;
    }

    /// <summary>Como <see cref="SiNo"/>, pero vacío = no importa (nulo).</summary>
    public bool? SiNoIndiferente(string columna)
    {
        var crudo = Leer(columna);
        if (crudo is null) return null;
        var valor = ValorSiNo(crudo);
        if (valor is null) Formato(columna, crudo, "Escriba sí o no.");
        return valor;
    }

    /// <summary>Valores separados por coma, en mayúsculas y sin repetir; <c>*</c> = todos (<see cref="EsTodos"/>).</summary>
    public IReadOnlyList<string>? Lista(string columna)
    {
        var crudo = Leer(columna);
        if (crudo is null) return null;
        return crudo.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static bool EsTodos(IReadOnlyList<string>? lista) => lista is [ "*" ];

    /// <summary>
    /// Un valor de <typeparamref name="TEnum"/> por su nombre en inglés o por una etiqueta en español, sin mayúsculas ni
    /// tildes (<c>Inventoriable</c> o <c>inventariable</c>).
    /// </summary>
    public TEnum? Enumeracion<TEnum>(string columna, IReadOnlyDictionary<string, TEnum>? etiquetas = null) where TEnum : struct, Enum
    {
        var crudo = Leer(columna);
        if (crudo is null) return null;
        var buscado = TablaLeida.Normalizar(crudo);
        foreach (var valor in Enum.GetValues<TEnum>())
            if (TablaLeida.Normalizar(valor.ToString()) == buscado) return valor;
        if (etiquetas is not null)
            foreach (var (etiqueta, valor) in etiquetas)
                if (TablaLeida.Normalizar(etiqueta) == buscado) return valor;
        var admitidos = Enum.GetNames<TEnum>().Concat(etiquetas?.Keys ?? []);
        Formato(columna, crudo, $"Admite: {string.Join(", ", admitidos)}.");
        return null;
    }

    /// <summary>
    /// Una referencia a lo que ya existe o cargó otra plantilla, buscada en un catálogo cargado en bloque. Si no está,
    /// <c>Import.Cell.NotFound</c> con lo que se buscó y dónde se crea.
    /// </summary>
    /// <param name="que">Lo que se buscó, con artículo: «un grupo contable».</param>
    /// <param name="dondeSeCrea">Dónde se crea: «en la plantilla de grupos o en Inventario → Grupos contables».</param>
    public T? Referencia<T>(string columna, CatalogoCitado<T> catalogo, string que, string dondeSeCrea)
    {
        var crudo = Leer(columna);
        if (crudo is null) return default;
        if (catalogo.Buscar(crudo, out var valor)) return valor;
        Error(columna, ImportErrors.CellNotFound, $"No hay {que} «{crudo}». Créelo {dondeSeCrea}.");
        return default;
    }

    /// <summary>Una columna que existe en la plantilla pero cuyo valor es de una entrega posterior (§0.4).</summary>
    public void TodaviaNoDisponible(string columna, string entrega) =>
        Error(columna, ImportErrors.CellNotYetAvailable, $"«{Crudo(columna)}» en «{columna}» se habilita con la entrega {entrega}.");

    // ----------------------------------------------------------- auxiliares --

    /// <summary>El texto de la celda; si está vacía y la columna es obligatoria, <c>Import.Cell.Required</c>.</summary>
    private string? Leer(string columna)
    {
        var crudo = Crudo(columna);
        if (crudo is null && _hoja.Columna(columna)?.Obligatoria == true)
            Error(columna, ImportErrors.CellRequired, $"La columna «{columna}» es obligatoria.");
        return crudo;
    }

    private decimal? Decimal(string columna, int decimales)
    {
        var crudo = Leer(columna);
        if (crudo is null) return null;
        var texto = crudo.Replace(" ", string.Empty);
        if (texto.Contains('.') && texto.Contains(','))
            return FormatoNulo<decimal>(columna, crudo, "Use punto o coma decimal, sin separador de miles.");
        texto = texto.Replace(',', '.');
        if (!decimal.TryParse(texto, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Invariante, out var valor))
            return FormatoNulo<decimal>(columna, crudo, "Debe ser un número.");
        if (Escala(valor) > decimales)
            return FormatoNulo<decimal>(columna, crudo, $"Admite hasta {decimales} decimales.");
        return valor;
    }

    /// <summary>Los decimales significativos (sin ceros a la derecha).</summary>
    private static int Escala(decimal valor)
    {
        var normalizado = valor / 1.000000000000000000000000000000000m;
        return (decimal.GetBits(normalizado)[3] >> 16) & 0xFF;
    }

    internal static bool? ValorSiNo(string texto) => TablaLeida.Normalizar(texto) switch
    {
        "si" or "s" or "x" or "1" or "true" or "verdadero" => true,
        "no" or "n" or "0" or "false" or "falso" => false,
        _ => null,
    };

    private string? Formato(string columna, string crudo, string regla)
    {
        Error(columna, ImportErrors.CellFormat, $"«{crudo}» no es válido en «{columna}». {regla}");
        return null;
    }

    private T? FormatoNulo<T>(string columna, string crudo, string regla) where T : struct
    {
        Formato(columna, crudo, regla);
        return null;
    }
}

/// <summary>
/// Un catálogo citado por una plantilla, cargado en bloque antes de leer las filas (§0.3: «nunca una consulta por
/// fila»): varias claves (código, nombre) apuntan al mismo valor, comparadas sin mayúsculas, tildes ni espacios. (nuevo)
/// </summary>
public sealed class CatalogoCitado<T>
{
    private readonly Dictionary<string, T> _valores = new(StringComparer.Ordinal);

    public int Count => _valores.Count;

    /// <summary>Agrega una clave; la primera gana si se repite.</summary>
    public CatalogoCitado<T> Agregar(string? clave, T valor)
    {
        var normalizada = TablaLeida.Normalizar(clave);
        if (normalizada.Length > 0) _valores.TryAdd(normalizada, valor);
        return this;
    }

    public bool Buscar(string? clave, out T valor)
    {
        if (_valores.TryGetValue(TablaLeida.Normalizar(clave), out var encontrado))
        {
            valor = encontrado;
            return true;
        }
        valor = default!;
        return false;
    }

    public static CatalogoCitado<T> Desde<TFuente>(IEnumerable<TFuente> fuente, Func<TFuente, T> valor, params Func<TFuente, string?>[] claves)
    {
        var catalogo = new CatalogoCitado<T>();
        foreach (var item in fuente)
            foreach (var clave in claves)
                catalogo.Agregar(clave(item), valor(item));
        return catalogo;
    }
}
