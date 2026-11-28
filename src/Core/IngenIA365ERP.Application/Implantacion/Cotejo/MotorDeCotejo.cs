using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Reports;

namespace IngenIA365ERP.Application.Implantacion.Cotejo;

/// <summary>Cómo se compara una medida: un importe en pesos, un número entero (días de mora) o un texto (calificación, estado).</summary>
public enum ClaseDeMedida
{
    Moneda = 0,
    Entero = 1,
    Texto = 2,
}

/// <summary>Una medida que se compara partida por partida. <see cref="Tolerancia"/> es la diferencia absoluta que todavía cuadra (0 = exacto).</summary>
public sealed record MedidaDeCotejo(string Nombre, ClaseDeMedida Clase, decimal Tolerancia = 0m)
{
    public bool EsNumerica => Clase != ClaseDeMedida.Texto;
}

/// <summary>
/// Lo que se coteja en una opción: el nombre de la llave (cuenta, crédito, documento), los atributos que la describen (nombre, línea, tercero:
/// salen del ERP o, si no está, de la referencia), las medidas y cómo se llama el grupo del resumen (clase, línea, estado). Con
/// <see cref="OrdenDeCodigo"/> las llaves se ordenan como texto (los códigos del plan: 1, 11, 1105, 2); si no, como números.
/// </summary>
public sealed record DefinicionDeCotejo(
    string Titulo,
    string NombreDeLlave,
    IReadOnlyList<string> Atributos,
    IReadOnlyList<MedidaDeCotejo> Medidas,
    string NombreDelGrupo,
    bool OrdenDeCodigo = false);

/// <summary>
/// Una partida de un lado: su llave ya normalizada (<see cref="Llave"/>, la que empareja), la que se muestra (<see cref="LlaveVisible"/>), su grupo,
/// sus atributos y un valor por medida, en el orden de la definición: <c>decimal?</c> en las numéricas y <c>string?</c> en las de texto. En la
/// referencia un valor nulo es «no vino en el archivo» y esa medida no se compara; en el ERP un nulo numérico vale cero.
/// </summary>
public sealed record PartidaDeCotejo(string Llave, string LlaveVisible, string? Grupo, IReadOnlyList<string?> Atributos, IReadOnlyList<object?> Valores,
    string? Nota = null);

public enum EstadoDeCotejo
{
    Cuadra = 0,
    Diferencia = 1,
    SoloEnElErp = 2,
    SoloEnLaReferencia = 3,
}

/// <summary>El resultado de una llave: las dos partidas (una puede faltar), las diferencias numéricas por medida y el estado.</summary>
public sealed record ResultadoDePartida(
    string Llave,
    string LlaveVisible,
    string Grupo,
    IReadOnlyList<string?> Atributos,
    PartidaDeCotejo? Erp,
    PartidaDeCotejo? Referencia,
    IReadOnlyList<decimal?> Diferencias,
    IReadOnlyList<bool> Distintas,
    EstadoDeCotejo Estado,
    string? Observacion);

public sealed record ResultadoDeCotejo(DefinicionDeCotejo Definicion, IReadOnlyList<ResultadoDePartida> Partidas)
{
    public int Contar(EstadoDeCotejo estado) => Partidas.Count(p => p.Estado == estado);
    public bool Cuadra => Partidas.All(p => p.Estado == EstadoDeCotejo.Cuadra);
}

/// <summary>
/// El motor del cotejo con cifras de referencia (2026-11-28, pedido del dueño para la implantación): puro, sin base ni reloj. Empareja las partidas
/// del ERP con las del archivo de referencia por llave, compara medida por medida y clasifica cada llave: <b>cuadra</b>, <b>diferencia</b>, <b>sólo
/// en el ERP</b> o <b>sólo en la referencia</b>. Una partida que falta de un lado y del otro está toda en cero cuadra: un saldo en cero y una
/// ausencia dicen lo mismo. Las llaves tienen que ser únicas en cada lado (la lectura del archivo y cada fuente lo garantizan).
/// <see cref="Resumen"/> y <see cref="Detalle"/> lo vuelven tabla para la pantalla y el Excel. (nuevo)
/// </summary>
public static class MotorDeCotejo
{
    public const string SinGrupo = "(sin grupo)";

    public static ResultadoDeCotejo Comparar(DefinicionDeCotejo definicion, IReadOnlyList<PartidaDeCotejo> erp, IReadOnlyList<PartidaDeCotejo> referencia)
    {
        ArgumentNullException.ThrowIfNull(definicion);
        var delErp = Indexar(erp, "del ERP", definicion);
        var deLaReferencia = Indexar(referencia, "de la referencia", definicion);

        var llaves = delErp.Keys.Union(deLaReferencia.Keys, StringComparer.Ordinal).ToList();
        var resultado = new List<ResultadoDePartida>(llaves.Count);
        foreach (var llave in llaves)
            resultado.Add(CompararUna(definicion, llave, delErp.GetValueOrDefault(llave), deLaReferencia.GetValueOrDefault(llave)));

        return new ResultadoDeCotejo(definicion, resultado
            .OrderBy(r => r.Grupo, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(r => r.LlaveVisible, definicion.OrdenDeCodigo ? StringComparer.Ordinal : ComparadorNatural.Instancia)
            .ToList());
    }

    private static Dictionary<string, PartidaDeCotejo> Indexar(IReadOnlyList<PartidaDeCotejo> partidas, string lado, DefinicionDeCotejo d)
    {
        var indice = new Dictionary<string, PartidaDeCotejo>(StringComparer.Ordinal);
        foreach (var p in partidas)
        {
            if (p.Valores.Count != d.Medidas.Count)
                throw new ArgumentException($"La partida {p.LlaveVisible} {lado} trae {p.Valores.Count} valores y la definición {d.Medidas.Count} medidas.");
            if (!indice.TryAdd(p.Llave, p))
                throw new ArgumentException($"La llave {p.LlaveVisible} se repite en las partidas {lado}.");
        }
        return indice;
    }

    private static ResultadoDePartida CompararUna(DefinicionDeCotejo d, string llave, PartidaDeCotejo? erp, PartidaDeCotejo? referencia)
    {
        var n = d.Medidas.Count;
        var diferencias = new decimal?[n];
        var distintas = new bool[n];
        var principal = erp ?? referencia!;
        var atributos = Enumerable.Range(0, d.Atributos.Count)
            .Select(i => Atributo(erp, i) ?? Atributo(referencia, i))
            .ToList();
        var grupo = erp?.Grupo ?? referencia?.Grupo;
        var observaciones = new List<string>();
        if (!string.IsNullOrWhiteSpace(erp?.Nota)) observaciones.Add(erp.Nota!);
        if (!string.IsNullOrWhiteSpace(referencia?.Nota)) observaciones.Add(referencia.Nota!);

        EstadoDeCotejo estado;
        if (erp is not null && referencia is not null)
        {
            for (var i = 0; i < n; i++)
            {
                var m = d.Medidas[i];
                var r = referencia.Valores[i];
                if (r is null) continue; // la referencia no trae esta medida: no se compara
                if (m.EsNumerica)
                {
                    var dif = Numero(erp.Valores[i]) - Numero(r);
                    diferencias[i] = dif;
                    distintas[i] = Math.Abs(dif) > m.Tolerancia;
                }
                else
                {
                    distintas[i] = TablaLeida.Normalizar(erp.Valores[i] as string) != TablaLeida.Normalizar(r as string);
                }
            }
            estado = distintas.Any(x => x) ? EstadoDeCotejo.Diferencia : EstadoDeCotejo.Cuadra;
            if (estado == EstadoDeCotejo.Diferencia)
                observaciones.Insert(0, "Difiere en: " + string.Join(", ", d.Medidas.Where((_, i) => distintas[i]).Select(m => m.Nombre).Select(x => x.ToLowerInvariant())) + ".");
        }
        else
        {
            var presente = erp ?? referencia!;
            var signo = erp is not null ? 1m : -1m;
            var enCero = true;
            for (var i = 0; i < n; i++)
            {
                var m = d.Medidas[i];
                var v = presente.Valores[i];
                if (m.EsNumerica)
                {
                    if (v is null) continue;
                    var valor = Numero(v);
                    diferencias[i] = signo * valor;
                    distintas[i] = Math.Abs(valor) > m.Tolerancia;
                    enCero &= !distintas[i];
                }
                else if (!string.IsNullOrWhiteSpace(v as string))
                {
                    distintas[i] = true;
                    enCero = false;
                }
            }
            if (enCero)
            {
                estado = EstadoDeCotejo.Cuadra;
                observaciones.Insert(0, erp is null ? "En cero en la referencia; el ERP no la tiene." : "En cero en el ERP; la referencia no la trae.");
            }
            else
            {
                estado = erp is null ? EstadoDeCotejo.SoloEnLaReferencia : EstadoDeCotejo.SoloEnElErp;
            }
        }

        return new ResultadoDePartida(llave, principal.LlaveVisible, string.IsNullOrWhiteSpace(grupo) ? SinGrupo : grupo!, atributos, erp, referencia,
            diferencias, distintas, estado, observaciones.Count == 0 ? null : string.Join(" ", observaciones));
    }

    private static string? Atributo(PartidaDeCotejo? p, int i) =>
        p is null || i >= p.Atributos.Count || string.IsNullOrWhiteSpace(p.Atributos[i]) ? null : p.Atributos[i];

    private static decimal Numero(object? valor) => valor switch
    {
        null => 0m,
        decimal d => d,
        int i => i,
        long l => l,
        _ => 0m,
    };

    public static string Texto(EstadoDeCotejo estado) => estado switch
    {
        EstadoDeCotejo.Cuadra => "Cuadra",
        EstadoDeCotejo.Diferencia => "Diferencia",
        EstadoDeCotejo.SoloEnElErp => "Sólo en el ERP",
        EstadoDeCotejo.SoloEnLaReferencia => "Sólo en la referencia",
        _ => estado.ToString(),
    };

    // ---- Tablas ----------------------------------------------------------------------------------------------------------------------------------

    /// <summary>La clave oculta del grupo en el resumen: un clic en la fila abre el detalle de ese grupo.</summary>
    public const string ClaveGrupo = "_grupo";

    /// <summary>La clave oculta del estado en el detalle (Cuadra, Diferencia, SoloEnElErp, SoloEnLaReferencia) para colorear en la pantalla.</summary>
    public const string ClaveEstado = "_estado";

    /// <summary>
    /// El resumen: una fila por grupo con cuántas partidas tiene cada lado, cuántas cuadran, difieren o están de un solo lado, y las sumas de cada
    /// medida numérica en el ERP, en la referencia y su diferencia (sólo de las partidas que la referencia trae con esa medida, más las del ERP solo).
    /// </summary>
    public static TablaExportable Resumen(ResultadoDeCotejo r, string subtitulo, IReadOnlyList<string> notas)
    {
        var d = r.Definicion;
        var numericas = d.Medidas.Select((m, i) => (m, i)).Where(x => x.m.EsNumerica).ToList();
        var columnas = new List<ColumnaExportable>
        {
            new(d.NombreDelGrupo),
            new("Partidas en el ERP", TipoDeColumna.Entero),
            new("Partidas en la referencia", TipoDeColumna.Entero),
            new("Cuadran", TipoDeColumna.Entero),
            new("Con diferencia", TipoDeColumna.Entero),
            new("Sólo en el ERP", TipoDeColumna.Entero),
            new("Sólo en la referencia", TipoDeColumna.Entero),
        };
        foreach (var (m, _) in numericas)
        {
            var tipo = Tipo(m);
            columnas.Add(new ColumnaExportable($"{m.Nombre} ERP", tipo));
            columnas.Add(new ColumnaExportable($"{m.Nombre} referencia", tipo));
            columnas.Add(new ColumnaExportable($"{m.Nombre} diferencia", tipo));
        }
        columnas.Add(new ColumnaExportable("Grupo", Clave: ClaveGrupo));

        object?[] Fila(string etiqueta, IReadOnlyList<ResultadoDePartida> ps, string? grupo)
        {
            var v = new List<object?>
            {
                etiqueta,
                ps.Count(p => p.Erp is not null),
                ps.Count(p => p.Referencia is not null),
                ps.Count(p => p.Estado == EstadoDeCotejo.Cuadra),
                ps.Count(p => p.Estado == EstadoDeCotejo.Diferencia),
                ps.Count(p => p.Estado == EstadoDeCotejo.SoloEnElErp),
                ps.Count(p => p.Estado == EstadoDeCotejo.SoloEnLaReferencia),
            };
            foreach (var (_, i) in numericas)
            {
                var deErp = ps.Where(p => p.Erp is not null).Sum(p => Numero(p.Erp!.Valores[i]));
                var deRef = ps.Where(p => p.Referencia is not null).Sum(p => Numero(p.Referencia!.Valores[i]));
                v.Add(deErp);
                v.Add(deRef);
                v.Add(deErp - deRef);
            }
            v.Add(grupo);
            return v.ToArray();
        }

        var filas = r.Partidas.GroupBy(p => p.Grupo)
            .OrderBy(g => g.Key, ComparadorNatural.Instancia)
            .Select(g => new FilaExportable(Fila(g.Key, g.ToList(), g.Key), Resaltada: g.Any(p => p.Estado != EstadoDeCotejo.Cuadra)))
            .ToList();
        var totales = new FilaExportable(Fila("Total", r.Partidas, null), Resaltada: true);
        return new TablaExportable($"{d.Titulo} — resumen", subtitulo, columnas, filas, totales, notas);
    }

    /// <summary>
    /// El detalle: una fila por llave con sus atributos, cada medida en el ERP, en la referencia y su diferencia, el estado y la observación. La
    /// sección es el grupo (en Excel, una hoja por grupo además de la completa).
    /// </summary>
    public static TablaExportable Detalle(ResultadoDeCotejo r, string subtitulo, IReadOnlyList<string> notas, bool soloDiferencias, string? grupo)
    {
        var d = r.Definicion;
        var columnas = new List<ColumnaExportable> { new(d.NombreDelGrupo), new(d.NombreDeLlave) };
        columnas.AddRange(d.Atributos.Select(a => new ColumnaExportable(a)));
        foreach (var m in d.Medidas)
        {
            var tipo = Tipo(m);
            columnas.Add(new ColumnaExportable($"{m.Nombre} ERP", tipo));
            columnas.Add(new ColumnaExportable($"{m.Nombre} referencia", tipo));
            if (m.EsNumerica) columnas.Add(new ColumnaExportable($"{m.Nombre} diferencia", tipo));
        }
        columnas.Add(new ColumnaExportable("Estado"));
        columnas.Add(new ColumnaExportable("Observación"));
        columnas.Add(new ColumnaExportable("Estado (clave)", Clave: ClaveEstado));

        var partidas = r.Partidas.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(grupo)) partidas = partidas.Where(p => string.Equals(p.Grupo, grupo, StringComparison.Ordinal));
        if (soloDiferencias) partidas = partidas.Where(p => p.Estado != EstadoDeCotejo.Cuadra);

        var filas = new List<FilaExportable>();
        foreach (var p in partidas)
        {
            var v = new List<object?> { p.Grupo, p.LlaveVisible };
            v.AddRange(p.Atributos);
            for (var i = 0; i < d.Medidas.Count; i++)
            {
                var m = d.Medidas[i];
                v.Add(Valor(m, p.Erp?.Valores[i], p.Erp is null));
                v.Add(Valor(m, p.Referencia?.Valores[i], p.Referencia is null));
                if (m.EsNumerica) v.Add(p.Diferencias[i]);
            }
            v.Add(Texto(p.Estado));
            v.Add(p.Observacion);
            v.Add(p.Estado.ToString());
            filas.Add(new FilaExportable(v, p.Grupo, Resaltada: p.Estado != EstadoDeCotejo.Cuadra));
        }

        var visibles = filas.Count;
        var totales = new object?[columnas.Count];
        totales[0] = "Total";
        totales[1] = $"{visibles} partida(s)";
        var col = 2 + d.Atributos.Count;
        var incluidas = partidas.ToList();
        foreach (var (m, i) in d.Medidas.Select((m, i) => (m, i)))
        {
            if (m.Clase == ClaseDeMedida.Moneda)
            {
                totales[col] = incluidas.Where(p => p.Erp is not null).Sum(p => Numero(p.Erp!.Valores[i]));
                totales[col + 1] = incluidas.Where(p => p.Referencia is not null).Sum(p => Numero(p.Referencia!.Valores[i]));
                totales[col + 2] = incluidas.Sum(p => p.Diferencias[i] ?? 0m);
            }
            col += m.EsNumerica ? 3 : 2;
        }

        var titulo = string.IsNullOrWhiteSpace(grupo) ? $"{d.Titulo} — detalle" : $"{d.Titulo} — detalle de {grupo}";
        return new TablaExportable(titulo, subtitulo, columnas, filas, new FilaExportable(totales, Resaltada: true), notas);
    }

    private static object? Valor(MedidaDeCotejo m, object? valor, bool ausente) =>
        ausente ? null : m.Clase switch
        {
            ClaseDeMedida.Texto => valor as string,
            ClaseDeMedida.Entero => valor is null ? null : (object)(int)Math.Round(Numero(valor)),
            _ => valor is null ? null : Numero(valor),
        };

    private static TipoDeColumna Tipo(MedidaDeCotejo m) => m.Clase switch
    {
        ClaseDeMedida.Moneda => TipoDeColumna.Moneda,
        ClaseDeMedida.Entero => TipoDeColumna.Entero,
        _ => TipoDeColumna.Texto,
    };
}

/// <summary>Ordena «2» antes que «10» y los códigos de cuenta como el plan (por dígitos, de izquierda a derecha). (nuevo)</summary>
public sealed class ComparadorNatural : IComparer<string?>
{
    public static readonly ComparadorNatural Instancia = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        var esNumeroX = x.Length > 0 && x.All(char.IsDigit);
        var esNumeroY = y.Length > 0 && y.All(char.IsDigit);
        if (esNumeroX && esNumeroY)
        {
            var a = x.TrimStart('0');
            var b = y.TrimStart('0');
            var porLargo = a.Length.CompareTo(b.Length);
            return porLargo != 0 ? porLargo : string.CompareOrdinal(a, b);
        }
        return string.Compare(x, y, StringComparison.CurrentCultureIgnoreCase);
    }
}
