using System.Globalization;
using System.Text;

namespace IngenIA365ERP.Domain.Common.Layouts;

/// <summary>Cómo se separan los campos de un registro.</summary>
public enum TipoDeArchivoDeLayout
{
    /// <summary>Ancho fijo: cada campo tiene <c>start</c> y <c>length</c>.</summary>
    FixedWidth = 1,

    /// <summary>Delimitado: cada campo tiene <c>order</c>; <c>length</c> es el largo máximo.</summary>
    Delimited = 2,
}

/// <summary>Cómo se escribe el valor de un campo (adaptadores-y-archivos.md §7.2).</summary>
public enum FormatoDeCampo
{
    /// <summary>Texto tal cual (normalizado si el layout lo pide).</summary>
    Text = 1,

    /// <summary>Entero sin decimales; un valor con fracción no se redondea: es una inconsistencia.</summary>
    Integer = 2,

    /// <summary>Monto con dos decimales implícitos (<c>1234,50</c> → <c>123450</c>).</summary>
    Amount2 = 3,

    /// <summary>Monto con punto y dos decimales (<c>1234.50</c>).</summary>
    AmountPoint2 = 4,

    /// <summary>Fracción con los decimales del campo (<c>decimals</c>), con punto.</summary>
    Rate = 5,

    /// <summary>Fecha con <c>pattern</c> (por defecto <c>yyyyMMdd</c>).</summary>
    Date = 6,
}

/// <summary>Alineación de un campo que no llena su largo.</summary>
public enum AlineacionDeCampo
{
    Left = 1,
    Right = 2,
}

/// <summary>
/// La definición de un archivo plano como dato (feature 013, K5, US16; adaptadores-y-archivos.md §7.1–§7.2; T79): registros por
/// código (cabecera, detalle, totales u otros), cada uno con sus campos, en ancho fijo o delimitado, con su codificación y su fin de
/// línea. No sabe de Cartera ni del SICSES: un campo dice de qué ruta (<see cref="CampoDeLayout.Path"/>) sale su valor o qué constante
/// lleva, y el motor (<see cref="ValidadorDeLayout"/>, <see cref="EscritorPorLayout"/>) lo valida y lo escribe. La PILA de la 010
/// conserva su propio layout (<c>PilaLayout</c>): éste es el genérico. (nuevo)
/// </summary>
/// <param name="NormalizeText">Si es verdadero, los campos <see cref="FormatoDeCampo.Text"/> van en mayúsculas, sin tildes (Ñ → N) y con
/// lo que no sea ASCII imprimible convertido en espacio, como la PILA.</param>
public sealed record LayoutDeArchivo(
    string Code,
    string Version,
    string Source,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    TipoDeArchivoDeLayout FileKind,
    string? Delimiter,
    string Encoding,
    string LineEnding,
    bool NormalizeText,
    IReadOnlyList<RegistroDeLayout> Records)
{
    /// <summary>Todos los campos de todos los registros.</summary>
    public int TotalFields => Records.Sum(r => r.Fields.Count);

    /// <summary>Campos cotejados (<c>verified = true</c>).</summary>
    public int VerifiedFields => Records.Sum(r => r.Fields.Count(f => f.Verified));

    /// <summary>Campos sin cotejar: con alguno, el layout sigue vigente pero cada generación lleva su advertencia (§7.3).</summary>
    public int UnverifiedFields => TotalFields - VerifiedFields;

    public bool HasUnverifiedFields => UnverifiedFields > 0;

    public bool IsValidAt(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);

    /// <summary>El registro con ese código (sin distinguir mayúsculas), o nulo.</summary>
    public RegistroDeLayout? Registro(string codigo) =>
        Records.FirstOrDefault(r => string.Equals(r.Code, codigo, StringComparison.OrdinalIgnoreCase));

    /// <summary>La secuencia que separa líneas: <c>\r\n</c> o <c>\n</c>.</summary>
    public string FinDeLinea => LineEnding.Equals("LF", StringComparison.OrdinalIgnoreCase) ? "\n" : "\r\n";

    /// <summary>La codificación de los bytes (UTF-8 nunca escribe la marca de orden).</summary>
    /// <summary>
    /// La codificación por su nombre con las páginas de códigos de Windows disponibles (feature 015, segunda ronda del 2026-10-27: el reporte de transacciones a la UIAF va
    /// en ANSI CP1252, <c>windows-1252</c>, que .NET no trae sin <see cref="CodePagesEncodingProvider"/>; antes sólo lo registraba el escritor de planos bancarios).
    /// </summary>
    public static Encoding Obtener(string nombre)
    {
        System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return System.Text.Encoding.GetEncoding(nombre);
    }

    public Encoding Codificacion()
    {
        var e = Obtener(Encoding);
        return e is UTF8Encoding ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false) : e;
    }

    /// <summary>
    /// Revisa la definición: registros únicos y con campos; números de campo consecutivos desde 1; en ancho fijo, campos contiguos desde
    /// la posición 1 que suman el largo declarado; en delimitado, orden consecutivo desde 1 y delimitador; cada campo con su tipo (A o N),
    /// un largo positivo, a lo sumo uno de <c>path</c> y <c>constant</c>, origen si es obligatorio, decimales si es tasa, un patrón de
    /// fecha válido y la constante entre los valores admitidos. Devuelve los problemas en texto llano; vacío si está bien.
    /// </summary>
    public IReadOnlyList<string> ValidarDefinicion()
    {
        var errores = new List<string>();
        if (string.IsNullOrWhiteSpace(Code)) errores.Add("El layout no tiene código.");
        if (string.IsNullOrWhiteSpace(Version)) errores.Add($"{Code}: el layout no tiene versión.");
        if (string.IsNullOrWhiteSpace(Source)) errores.Add($"{Code}: el layout no dice su fuente.");
        if (ValidTo is { } hasta && hasta < ValidFrom) errores.Add($"{Code}: la vigencia termina ({hasta:yyyy-MM-dd}) antes de empezar ({ValidFrom:yyyy-MM-dd}).");
        if (FileKind == TipoDeArchivoDeLayout.Delimited && string.IsNullOrEmpty(Delimiter))
            errores.Add($"{Code}: un archivo delimitado necesita su delimitador.");
        if (!LineEnding.Equals("CRLF", StringComparison.OrdinalIgnoreCase) && !LineEnding.Equals("LF", StringComparison.OrdinalIgnoreCase))
            errores.Add($"{Code}: fin de línea «{LineEnding}» desconocido (CRLF o LF).");
        try { _ = Obtener(Encoding); }
        catch (ArgumentException) { errores.Add($"{Code}: codificación «{Encoding}» desconocida."); }

        if (Records.Count == 0) errores.Add($"{Code}: el layout no tiene registros.");
        foreach (var repetido in Records.GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errores.Add($"{Code}: el registro «{repetido.Key}» está repetido.");

        foreach (var r in Records)
        {
            var donde = $"{Code}, registro «{r.Code}»";
            if (r.Fields.Count == 0) { errores.Add($"{donde}: no tiene campos."); continue; }

            var numeros = r.Fields.Select(f => f.Number).OrderBy(n => n).ToList();
            for (var i = 0; i < numeros.Count; i++)
                if (numeros[i] != i + 1) { errores.Add($"{donde}: los campos no son consecutivos desde 1 (falta o sobra el {i + 1})."); break; }

            if (FileKind == TipoDeArchivoDeLayout.FixedWidth)
            {
                var esperado = 1;
                foreach (var f in r.Fields.OrderBy(f => f.Start))
                {
                    if (f.Start != esperado)
                        errores.Add($"{donde}, campo {f.Number} «{f.Name}»: empieza en {f.Start} y debía empezar en {esperado}.");
                    esperado = f.Start + f.Length;
                }
                var total = r.Fields.Sum(f => f.Length);
                if (r.Length is { } largo && total != largo)
                    errores.Add($"{donde}: los campos suman {total} posiciones y el registro declara {largo}.");
            }
            else
            {
                var ordenes = r.Fields.Select(f => f.Order).OrderBy(n => n).ToList();
                for (var i = 0; i < ordenes.Count; i++)
                    if (ordenes[i] != i + 1) { errores.Add($"{donde}: el orden de los campos no es consecutivo desde 1 (falta o sobra el {i + 1})."); break; }
            }

            foreach (var f in r.Fields)
                errores.AddRange(f.ValidarDefinicion(donde));
        }
        return errores;
    }
}

/// <summary>Un tipo de registro del archivo (cabecera, detalle, totales…).</summary>
/// <param name="Repeat">Documental: cuántas veces aparece (<c>Once</c>, <c>PorCredito</c>, <c>PorTitular</c>…). La cantidad real la
/// deciden las filas que entrega la proyección.</param>
/// <param name="Length">En ancho fijo, el largo del registro (la suma de sus campos); nulo en delimitado.</param>
public sealed record RegistroDeLayout(string Code, string? Repeat, int? Length, IReadOnlyList<CampoDeLayout> Fields)
{
    /// <summary>Los campos en el orden en que se escriben.</summary>
    public IEnumerable<CampoDeLayout> EnOrden(TipoDeArchivoDeLayout tipo) =>
        tipo == TipoDeArchivoDeLayout.FixedWidth ? Fields.OrderBy(f => f.Start) : Fields.OrderBy(f => f.Order);
}

/// <summary>Un campo de un registro.</summary>
/// <param name="Order">Posición en un archivo delimitado (desde 1); cero en ancho fijo.</param>
/// <param name="Start">Posición inicial en ancho fijo (desde 1); cero en delimitado.</param>
/// <param name="Length">Largo fijo, o largo máximo en delimitado.</param>
/// <param name="Kind"><c>A</c> alfanumérico (izquierda, espacios) o <c>N</c> numérico (derecha, ceros): da el relleno por defecto.</param>
/// <param name="Path">Ruta del valor en la fila que entrega la proyección (<c>Deudor.NumeroDeDocumento</c>).</param>
/// <param name="Constant">Valor fijo en vez de <paramref name="Path"/>.</param>
/// <param name="Pad">Carácter de relleno; nulo = el del tipo. En delimitado sólo se rellena si se declara.</param>
/// <param name="Align">Alineación; nula = la del tipo.</param>
/// <param name="Decimals">Decimales de una tasa (<see cref="FormatoDeCampo.Rate"/>).</param>
/// <param name="Pattern">Patrón de una fecha; nulo = <c>yyyyMMdd</c>.</param>
/// <param name="Verified">Cotejado contra el instructivo y un archivo aceptado (§7.3).</param>
public sealed record CampoDeLayout(
    int Number,
    string Name,
    int Order,
    int Start,
    int Length,
    string Kind,
    FormatoDeCampo Format,
    bool Required,
    string? Path,
    string? Constant,
    IReadOnlyList<string> AllowedValues,
    char? Pad,
    AlineacionDeCampo? Align,
    int? Decimals,
    string? Pattern,
    bool Verified)
{
    /// <summary>Patrón de fecha cuando el campo no declara el suyo.</summary>
    public const string PatronDeFechaPorDefecto = "yyyyMMdd";

    public bool EsNumerico => Kind == "N";

    public char RellenoEfectivo => Pad ?? (EsNumerico ? '0' : ' ');

    public AlineacionDeCampo AlineacionEfectiva => Align ?? (EsNumerico ? AlineacionDeCampo.Right : AlineacionDeCampo.Left);

    public string PatronDeFecha => string.IsNullOrWhiteSpace(Pattern) ? PatronDeFechaPorDefecto : Pattern;

    internal IEnumerable<string> ValidarDefinicion(string registro)
    {
        var donde = $"{registro}, campo {Number} «{Name}»";
        if (string.IsNullOrWhiteSpace(Name)) yield return $"{registro}, campo {Number}: no tiene nombre.";
        if (Length <= 0) yield return $"{donde}: largo inválido ({Length}).";
        if (Kind is not ("A" or "N")) yield return $"{donde}: tipo «{Kind}» desconocido (A o N).";
        if (Path is not null && Constant is not null) yield return $"{donde}: lleva «path» y «constant» a la vez.";
        if (Required && Path is null && string.IsNullOrEmpty(Constant)) yield return $"{donde}: es obligatorio y no dice de dónde sale («path» o «constant»).";
        if (Format == FormatoDeCampo.Rate && Decimals is not >= 0) yield return $"{donde}: una tasa necesita «decimals».";
        if (Decimals is < 0) yield return $"{donde}: «decimals» negativo.";
        if (Format == FormatoDeCampo.Date)
        {
            string? problema = null;
            try
            {
                var muestra = new DateOnly(2027, 12, 31).ToString(PatronDeFecha, CultureInfo.InvariantCulture);
                if (!DateOnly.TryParseExact(muestra, PatronDeFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var leida) || leida != new DateOnly(2027, 12, 31))
                    problema = $"{donde}: el patrón de fecha «{PatronDeFecha}» no identifica un día.";
            }
            catch (FormatException) { problema = $"{donde}: patrón de fecha «{PatronDeFecha}» inválido."; }
            if (problema is not null) yield return problema;
        }
        if (Constant is not null && AllowedValues.Count > 0 && !AllowedValues.Contains(Constant, StringComparer.Ordinal))
            yield return $"{donde}: la constante «{Constant}» no está entre los valores admitidos.";
        if (Constant is not null && Constant.Length > Length)
            yield return $"{donde}: la constante «{Constant}» excede el largo ({Length}).";
    }
}
