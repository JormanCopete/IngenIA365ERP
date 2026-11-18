using System.Globalization;
using System.Text;

namespace IngenIA365ERP.Domain.Lending.SignatureDocuments;

/// <summary>Los valores con que se arma una plantilla: los campos sueltos y las listas (<c>CODEUDORES</c>, <c>FIRMANTES</c>).</summary>
public sealed record DatosDePlantilla(
    IReadOnlyDictionary<string, string?> Campos,
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string?>>> Listas)
{
    public static DatosDePlantilla Vacios { get; } = new(new Dictionary<string, string?>(), new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string?>>>());
}

/// <summary>El tipo de un bloque del documento armado.</summary>
public enum TipoDeBloque
{
    /// <summary><c># texto</c>: título centrado en negrita.</summary>
    Titulo = 1,

    /// <summary><c>## texto</c>: subtítulo en negrita.</summary>
    Subtitulo = 2,

    /// <summary>Un renglón normal: párrafo justificado.</summary>
    Parrafo = 3,

    /// <summary><c>- texto</c>: un elemento de una lista.</summary>
    Vineta = 4,

    /// <summary>Un renglón vacío: separa.</summary>
    Espacio = 5,

    /// <summary><c>---</c> solo en su renglón: lo que sigue va en otra hoja.</summary>
    SaltoDePagina = 6,

    /// <summary><c>{{FIRMAS}}</c> solo en su renglón: las firmas del deudor y los codeudores.</summary>
    Firmas = 7,

    /// <summary><c>= texto</c>: párrafo centrado.</summary>
    Centrado = 8,
}

/// <summary>Un pedazo de texto con su estilo.</summary>
public sealed record TramoDeTexto(string Texto, bool Negrita);

/// <summary>Un bloque del documento armado.</summary>
public sealed record BloqueDeTexto(TipoDeBloque Tipo, IReadOnlyList<TramoDeTexto> Tramos)
{
    public string TextoPlano => string.Concat(Tramos.Select(t => t.Texto));
}

/// <summary>El documento armado: los bloques y los campos que quedaron vacíos (se imprimieron como espacio para llenar a mano).</summary>
public sealed record TextoArmado(IReadOnlyList<BloqueDeTexto> Bloques, IReadOnlyList<string> CamposVacios);

/// <summary>Un error de una plantilla, con el renglón donde está.</summary>
public sealed record ErrorDePlantilla(int Renglon, string Mensaje)
{
    public override string ToString() => $"Renglón {Renglon}: {Mensaje}";
}

/// <summary>Qué campos y listas existen para una cooperativa: los del sistema más los propios.</summary>
public sealed class CatalogoDeCampos
{
    private readonly HashSet<string> _campos;
    private readonly Dictionary<string, HashSet<string>> _listas;

    /// <param name="propios">Los códigos de los campos propios (los de pesos ya con su <c>_LETRAS</c>).</param>
    public CatalogoDeCampos(IEnumerable<string> propios)
    {
        _campos = CamposDelSistema.Campos.Select(c => c.Codigo).Concat(propios).ToHashSet(StringComparer.Ordinal);
        _listas = CamposDelSistema.Listas.ToDictionary(l => l.Codigo, l => l.Campos.Select(c => c.Codigo).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    /// <summary>Sólo los campos del sistema.</summary>
    public static CatalogoDeCampos DelSistema { get; } = new([]);

    public bool EsCampo(string codigo) => _campos.Contains(codigo);

    public bool EsLista(string codigo) => _listas.ContainsKey(codigo);

    public bool EsCampoDeLista(string lista, string codigo) => _listas.TryGetValue(lista, out var campos) && campos.Contains(codigo);
}

/// <summary>
/// El lenguaje de las plantillas de los documentos para firma (2026-11-18, pedido del dueño). Puro: no lee la base.
/// <list type="bullet">
/// <item><c>{{CAMPO}}</c> pone el valor; vacío, imprime una raya para llenarlo a mano y lo informa. <c>{{CAMPO:MAYUSCULAS}}</c> y
/// <c>{{CAMPO:MINUSCULAS}}</c> cambian las letras.</item>
/// <item><c>{{BLANCO}}</c> o <c>{{BLANCO:40}}</c>: un espacio en blanco del largo dicho (el pagaré en blanco).</item>
/// <item><c>{{#SI CAMPO}} … {{#SINO}} … {{/SI}}</c> y <c>{{#NO CAMPO}} … {{/NO}}</c>: un campo es verdadero si tiene valor y no es «NO», «0» ni
/// «FALSE»; una lista, si tiene elementos.</item>
/// <item><c>{{#CADA CODEUDORES}} … {{/CADA}}</c>: repite el texto por cada elemento, con sus campos y <c>{{ORDEN}}</c>.</item>
/// <item>Por renglón: <c># </c> título, <c>## </c> subtítulo, <c>- </c> viñeta, <c>= </c> centrado, <c>---</c> otra hoja, <c>{{FIRMAS}}</c> las firmas;
/// <c>**texto**</c> va en negrita. Una etiqueta de bloque sola en su renglón no deja renglón vacío.</item>
/// </list>
/// Los valores no se interpretan: un <c>**</c> o un <c>#</c> en el nombre de una persona se imprime tal cual. (nuevo)
/// </summary>
public static class PlantillaDeFirma
{
    /// <summary>Lo que se imprime en lugar de un campo vacío.</summary>
    public const string Raya = "____________________";

    public const int BlancoPorDefecto = 25;
    public const int BlancoMinimo = 3;
    public const int BlancoMaximo = 80;

    private const string Mayusculas = "MAYUSCULAS";
    private const string Minusculas = "MINUSCULAS";

    // ---------------------------------------------------------------------------------------------------- análisis --

    private abstract record Nodo(int Renglon);

    private sealed record NodoTexto(string Texto, int Renglon) : Nodo(Renglon);

    private sealed record NodoCampo(string Codigo, string? Argumento, int Renglon) : Nodo(Renglon);

    private sealed record NodoSi(string Codigo, bool Negado, List<Nodo> Entonces, List<Nodo> Sino, int Renglon) : Nodo(Renglon);

    private sealed record NodoCada(string Lista, List<Nodo> Cuerpo, int Renglon) : Nodo(Renglon);

    private sealed class Marco(string clase, Nodo nodo)
    {
        public string Clase { get; } = clase;
        public Nodo Nodo { get; } = nodo;
        public bool EnSino { get; set; }

        public List<Nodo> Destino => Nodo switch
        {
            NodoSi si => EnSino ? si.Sino : si.Entonces,
            NodoCada cada => cada.Cuerpo,
            _ => throw new InvalidOperationException(),
        };
    }

    private static (List<Nodo> Nodos, List<ErrorDePlantilla> Errores) Analizar(string? texto)
    {
        texto = (texto ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var errores = new List<ErrorDePlantilla>();
        var raiz = new List<Nodo>();
        var pila = new Stack<Marco>();
        List<Nodo> Destino() => pila.Count == 0 ? raiz : pila.Peek().Destino;
        int Renglon(int indice) => 1 + texto.AsSpan(0, Math.Min(indice, texto.Length)).Count('\n');

        var pos = 0;
        while (pos < texto.Length)
        {
            var abre = texto.IndexOf("{{", pos, StringComparison.Ordinal);
            if (abre < 0)
            {
                Destino().Add(new NodoTexto(texto[pos..], Renglon(pos)));
                break;
            }

            var cierra = texto.IndexOf("}}", abre + 2, StringComparison.Ordinal);
            if (cierra < 0)
            {
                errores.Add(new(Renglon(abre), "Falta cerrar «{{» con «}}»."));
                Destino().Add(new NodoTexto(texto[pos..], Renglon(pos)));
                break;
            }

            var etiqueta = texto[(abre + 2)..cierra].Trim();
            var renglon = Renglon(abre);
            var esDeBloque = etiqueta.StartsWith('#') || etiqueta.StartsWith('/');

            // Una etiqueta de bloque sola en su renglón se lleva el renglón entero: no deja renglones vacíos.
            var inicioDelRenglon = abre == 0 ? 0 : texto.LastIndexOf('\n', abre - 1) + 1;
            var finDelRenglon = texto.IndexOf('\n', cierra + 2);
            var sola = esDeBloque && inicioDelRenglon >= pos
                && string.IsNullOrWhiteSpace(texto[inicioDelRenglon..abre])
                && string.IsNullOrWhiteSpace(texto[(cierra + 2)..(finDelRenglon < 0 ? texto.Length : finDelRenglon)]);
            var antes = sola ? texto[pos..inicioDelRenglon] : texto[pos..abre];
            if (antes.Length > 0) Destino().Add(new NodoTexto(antes, Renglon(pos)));
            pos = sola ? (finDelRenglon < 0 ? texto.Length : finDelRenglon + 1) : cierra + 2;

            if (etiqueta.Length == 0)
            {
                errores.Add(new(renglon, "Hay unas llaves «{{}}» vacías."));
                continue;
            }

            if (etiqueta.StartsWith('#'))
            {
                var partes = etiqueta[1..].Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var palabra = partes.Length > 0 ? partes[0].ToUpperInvariant() : string.Empty;
                var nombre = partes.Length > 1 ? partes[1].ToUpperInvariant() : string.Empty;
                switch (palabra)
                {
                    case "SI" or "NO":
                        if (nombre.Length == 0) { errores.Add(new(renglon, $"«{{{{#{palabra}}}}}» necesita el campo que se mira: {{{{#{palabra} CAMPO}}}}.")); break; }
                        var si = new NodoSi(nombre, palabra == "NO", [], [], renglon);
                        Destino().Add(si);
                        pila.Push(new Marco(palabra, si));
                        break;
                    case "CADA":
                        if (nombre.Length == 0) { errores.Add(new(renglon, "«{{#CADA}}» necesita la lista que se recorre: {{#CADA CODEUDORES}}.")); break; }
                        var cada = new NodoCada(nombre, [], renglon);
                        Destino().Add(cada);
                        pila.Push(new Marco("CADA", cada));
                        break;
                    case "SINO":
                        if (pila.Count == 0 || pila.Peek().Clase != "SI" || pila.Peek().EnSino)
                            errores.Add(new(renglon, "«{{#SINO}}» sólo va dentro de un «{{#SI …}}» y una sola vez."));
                        else
                            pila.Peek().EnSino = true;
                        break;
                    default:
                        errores.Add(new(renglon, $"«{{{{#{palabra}}}}}» no existe. Los bloques son #SI, #SINO, #NO y #CADA."));
                        break;
                }

                continue;
            }

            if (etiqueta.StartsWith('/'))
            {
                var palabra = etiqueta[1..].Trim().ToUpperInvariant();
                if (pila.Count == 0 || pila.Peek().Clase != palabra)
                    errores.Add(new(renglon, pila.Count == 0
                        ? $"«{{{{/{palabra}}}}}» cierra un bloque que no se abrió."
                        : $"«{{{{/{palabra}}}}}» no cierra el bloque abierto: falta «{{{{/{pila.Peek().Clase}}}}}»."));
                else
                    pila.Pop();
                continue;
            }

            var dosPuntos = etiqueta.IndexOf(':');
            var codigo = (dosPuntos < 0 ? etiqueta : etiqueta[..dosPuntos]).Trim().ToUpperInvariant();
            var argumento = dosPuntos < 0 ? null : etiqueta[(dosPuntos + 1)..].Trim().ToUpperInvariant();
            if (!codigo.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_'))
            {
                errores.Add(new(renglon, $"«{{{{{etiqueta}}}}}» no es un campo: un campo lleva mayúsculas, números y guion bajo."));
                continue;
            }

            if (codigo == CamposDelSistema.Firmas
                && !string.Equals(texto[inicioDelRenglon..(finDelRenglon < 0 ? texto.Length : finDelRenglon)].Trim(), texto[abre..(cierra + 2)], StringComparison.Ordinal))
                errores.Add(new(renglon, "«{{FIRMAS}}» va solo en su renglón."));
            Destino().Add(new NodoCampo(codigo, argumento, renglon));
        }

        foreach (var abierto in pila)
            errores.Add(new(abierto.Nodo.Renglon, $"Falta cerrar «{{{{#{abierto.Clase} …}}}}» con «{{{{/{abierto.Clase}}}}}»."));
        return (raiz, errores);
    }

    // --------------------------------------------------------------------------------------------------- validación --

    /// <summary>Los errores de la plantilla: llaves y bloques mal cerrados, campos y listas que no existen, modificadores desconocidos.</summary>
    public static IReadOnlyList<ErrorDePlantilla> Validar(string? texto, CatalogoDeCampos catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        var (nodos, errores) = Analizar(texto);
        Revisar(nodos, null, catalogo, errores);
        return errores.OrderBy(e => e.Renglon).ToList();
    }

    /// <summary>Los campos y listas que usa la plantilla (para saber qué campos propios pedir al generar).</summary>
    public static IReadOnlySet<string> CamposUsados(string? texto)
    {
        var usados = new HashSet<string>(StringComparer.Ordinal);
        void Recorrer(IEnumerable<Nodo> nodos)
        {
            foreach (var n in nodos)
            {
                switch (n)
                {
                    case NodoCampo c: usados.Add(c.Codigo); break;
                    case NodoSi s: usados.Add(s.Codigo); Recorrer(s.Entonces); Recorrer(s.Sino); break;
                    case NodoCada c: usados.Add(c.Lista); Recorrer(c.Cuerpo); break;
                }
            }
        }

        Recorrer(Analizar(texto).Nodos);
        return usados;
    }

    private static void Revisar(IEnumerable<Nodo> nodos, string? lista, CatalogoDeCampos catalogo, List<ErrorDePlantilla> errores)
    {
        bool Existe(string codigo) => catalogo.EsCampo(codigo)
            || (lista is not null && (codigo == CamposDelSistema.Orden || catalogo.EsCampoDeLista(lista, codigo)));

        foreach (var nodo in nodos)
        {
            switch (nodo)
            {
                case NodoCampo { Codigo: CamposDelSistema.Blanco } blanco:
                    if (blanco.Argumento is { } largo
                        && (!int.TryParse(largo, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n is < BlancoMinimo or > BlancoMaximo))
                        errores.Add(new(blanco.Renglon, $"El largo de «{{{{BLANCO:{largo}}}}}» va de {BlancoMinimo} a {BlancoMaximo}."));
                    break;
                case NodoCampo { Codigo: CamposDelSistema.Firmas } firmas:
                    if (lista is not null) errores.Add(new(firmas.Renglon, "«{{FIRMAS}}» no va dentro de un «{{#CADA}}»."));
                    break;
                case NodoCampo campo:
                    if (!Existe(campo.Codigo))
                        errores.Add(new(campo.Renglon, catalogo.EsLista(campo.Codigo)
                            ? $"«{campo.Codigo}» es una lista: se recorre con «{{{{#CADA {campo.Codigo}}}}} … {{{{/CADA}}}}»."
                            : lista is null && CamposDelSistema.Listas.Any(l => l.Campos.Any(c => c.Codigo == campo.Codigo))
                                ? $"«{campo.Codigo}» sólo existe dentro de «{{{{#CADA …}}}}» de su lista."
                                : $"El campo «{campo.Codigo}» no existe. Use uno del sistema o créelo en «Campos propios»."));
                    if (campo.Argumento is { } modificador && modificador is not (Mayusculas or Minusculas))
                        errores.Add(new(campo.Renglon, $"«:{modificador}» no existe: los modificadores son :MAYUSCULAS y :MINUSCULAS."));
                    break;
                case NodoSi si:
                    if (!Existe(si.Codigo) && !catalogo.EsLista(si.Codigo))
                        errores.Add(new(si.Renglon, $"El campo «{si.Codigo}» del «{{{{#{(si.Negado ? "NO" : "SI")}}}}}» no existe."));
                    Revisar(si.Entonces, lista, catalogo, errores);
                    Revisar(si.Sino, lista, catalogo, errores);
                    break;
                case NodoCada cada:
                    if (lista is not null)
                        errores.Add(new(cada.Renglon, "Un «{{#CADA}}» no va dentro de otro."));
                    else if (!catalogo.EsLista(cada.Lista))
                        errores.Add(new(cada.Renglon, $"«{cada.Lista}» no es una lista. Las listas son {string.Join(" y ", CamposDelSistema.Listas.Select(l => l.Codigo))}."));
                    Revisar(cada.Cuerpo, cada.Lista, catalogo, errores);
                    break;
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------- armado --

    private abstract record Segmento;

    private sealed record Literal(string Texto) : Segmento;

    private sealed record Valor(string Texto) : Segmento;

    private sealed record MarcaDeFirmas : Segmento;

    /// <summary>
    /// Arma la plantilla con los datos. La plantilla debe estar validada: con errores de forma lanza <see cref="InvalidOperationException"/>; un campo
    /// desconocido se trata como vacío.
    /// </summary>
    public static TextoArmado Armar(string? texto, DatosDePlantilla datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var (nodos, errores) = Analizar(texto);
        if (errores.Count > 0) throw new InvalidOperationException($"La plantilla tiene errores: {string.Join("; ", errores)}");

        var vacios = new List<string>();
        var segmentos = new List<Segmento>();
        Evaluar(nodos, datos, null, segmentos, vacios);
        return new TextoArmado(Bloques(segmentos), vacios.Distinct(StringComparer.Ordinal).ToList());
    }

    /// <summary>¿El valor cuenta como verdadero en un <c>{{#SI}}</c>?</summary>
    public static bool EsVerdadero(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && valor.Trim().ToUpperInvariant() is not ("NO" or "N" or "0" or "FALSE" or "FALSO");

    private static string? Buscar(DatosDePlantilla datos, IReadOnlyDictionary<string, string?>? elemento, string codigo) =>
        elemento is not null && elemento.TryGetValue(codigo, out var propio) ? propio
        : datos.Campos.TryGetValue(codigo, out var valor) ? valor : null;

    private static void Evaluar(IEnumerable<Nodo> nodos, DatosDePlantilla datos, IReadOnlyDictionary<string, string?>? elemento, List<Segmento> salida,
        List<string> vacios)
    {
        foreach (var nodo in nodos)
        {
            switch (nodo)
            {
                case NodoTexto t:
                    salida.Add(new Literal(t.Texto));
                    break;
                case NodoCampo { Codigo: CamposDelSistema.Blanco } blanco:
                    var largo = blanco.Argumento is { } a && int.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : BlancoPorDefecto;
                    salida.Add(new Valor(new string('_', Math.Clamp(largo, BlancoMinimo, BlancoMaximo))));
                    break;
                case NodoCampo { Codigo: CamposDelSistema.Firmas }:
                    salida.Add(new MarcaDeFirmas());
                    break;
                case NodoCampo campo:
                    var valor = Buscar(datos, elemento, campo.Codigo);
                    if (string.IsNullOrWhiteSpace(valor))
                    {
                        vacios.Add(campo.Codigo);
                        salida.Add(new Valor(Raya));
                        break;
                    }

                    valor = string.Join(' ', valor.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    salida.Add(new Valor(campo.Argumento switch
                    {
                        Mayusculas => valor.ToUpper(CultureInfo.GetCultureInfo("es-CO")),
                        Minusculas => valor.ToLower(CultureInfo.GetCultureInfo("es-CO")),
                        _ => valor,
                    }));
                    break;
                case NodoSi si:
                    var verdadero = elemento is null && datos.Listas.TryGetValue(si.Codigo, out var lista)
                        ? lista.Count > 0
                        : EsVerdadero(Buscar(datos, elemento, si.Codigo));
                    Evaluar(verdadero != si.Negado ? si.Entonces : si.Sino, datos, elemento, salida, vacios);
                    break;
                case NodoCada cada:
                    var elementos = datos.Listas.TryGetValue(cada.Lista, out var l) ? l : [];
                    for (var i = 0; i < elementos.Count; i++)
                    {
                        var conOrden = new Dictionary<string, string?>(elementos[i], StringComparer.Ordinal)
                        {
                            [CamposDelSistema.Orden] = (i + 1).ToString(CultureInfo.InvariantCulture),
                        };
                        Evaluar(cada.Cuerpo, datos, conOrden, salida, vacios);
                    }

                    break;
            }
        }
    }

    private static List<BloqueDeTexto> Bloques(List<Segmento> segmentos)
    {
        // Renglones: los saltos sólo vienen de la plantilla (los valores ya no tienen).
        var renglones = new List<List<Segmento>> { new() };
        foreach (var s in segmentos)
        {
            if (s is not Literal literal)
            {
                renglones[^1].Add(s);
                continue;
            }

            var partes = literal.Texto.Split('\n');
            for (var i = 0; i < partes.Length; i++)
            {
                if (i > 0) renglones.Add([]);
                if (partes[i].Length > 0) renglones[^1].Add(new Literal(partes[i]));
            }
        }

        var bloques = new List<BloqueDeTexto>();
        foreach (var renglon in renglones)
        {
            if (renglon.Any(s => s is MarcaDeFirmas))
            {
                bloques.Add(new BloqueDeTexto(TipoDeBloque.Firmas, []));
                continue;
            }

            if (renglon.All(s => s is Literal) && string.Concat(renglon.Cast<Literal>().Select(l => l.Texto)).Trim() == "---")
            {
                bloques.Add(new BloqueDeTexto(TipoDeBloque.SaltoDePagina, []));
                continue;
            }

            var tipo = TipoDeBloque.Parrafo;
            if (renglon.Count > 0 && renglon[0] is Literal primero)
            {
                var inicio = primero.Texto.TrimStart();
                (tipo, var prefijo) = inicio switch
                {
                    _ when inicio.StartsWith("## ", StringComparison.Ordinal) => (TipoDeBloque.Subtitulo, 3),
                    _ when inicio.StartsWith("# ", StringComparison.Ordinal) => (TipoDeBloque.Titulo, 2),
                    _ when inicio.StartsWith("- ", StringComparison.Ordinal) => (TipoDeBloque.Vineta, 2),
                    _ when inicio.StartsWith("= ", StringComparison.Ordinal) => (TipoDeBloque.Centrado, 2),
                    _ => (TipoDeBloque.Parrafo, 0),
                };
                if (prefijo > 0) renglon[0] = new Literal(inicio[prefijo..]);
            }

            var tramos = Tramos(renglon);
            if (tramos.All(t => string.IsNullOrWhiteSpace(t.Texto)))
            {
                bloques.Add(new BloqueDeTexto(TipoDeBloque.Espacio, []));
                continue;
            }

            bloques.Add(new BloqueDeTexto(tipo, tramos));
        }

        // Los espacios seguidos cuentan como uno; al principio y al final sobran.
        var limpios = new List<BloqueDeTexto>();
        foreach (var b in bloques)
        {
            if (b.Tipo == TipoDeBloque.Espacio && (limpios.Count == 0 || limpios[^1].Tipo is TipoDeBloque.Espacio or TipoDeBloque.SaltoDePagina)) continue;
            limpios.Add(b);
        }

        while (limpios.Count > 0 && limpios[^1].Tipo == TipoDeBloque.Espacio) limpios.RemoveAt(limpios.Count - 1);
        return limpios;
    }

    private static List<TramoDeTexto> Tramos(List<Segmento> renglon)
    {
        var tramos = new List<TramoDeTexto>();
        var negrita = false;
        var actual = new StringBuilder();
        void Cortar()
        {
            if (actual.Length == 0) return;
            var texto = actual.ToString();
            if (tramos.Count > 0 && tramos[^1].Negrita == negrita)
                tramos[^1] = tramos[^1] with { Texto = tramos[^1].Texto + texto };
            else
                tramos.Add(new TramoDeTexto(texto, negrita));
            actual.Clear();
        }

        foreach (var s in renglon)
        {
            switch (s)
            {
                case Valor v:
                    actual.Append(v.Texto);
                    break;
                case Literal l:
                    var partes = l.Texto.Split("**");
                    for (var i = 0; i < partes.Length; i++)
                    {
                        if (i > 0)
                        {
                            Cortar();
                            negrita = !negrita;
                        }

                        actual.Append(partes[i]);
                    }

                    break;
            }
        }

        Cortar();
        if (tramos.Count > 0) tramos[0] = tramos[0] with { Texto = tramos[0].Texto.TrimStart() };
        if (tramos.Count > 0) tramos[^1] = tramos[^1] with { Texto = tramos[^1].Texto.TrimEnd() };
        return tramos;
    }
}
