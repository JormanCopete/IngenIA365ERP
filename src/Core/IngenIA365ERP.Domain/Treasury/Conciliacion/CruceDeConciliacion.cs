using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Treasury.Conciliacion;

/// <summary>Una línea del extracto que falta por cruzar: su id, fecha, sentido, valor sin signo y referencia. (nuevo)</summary>
public sealed record LineaACruzar(long Id, Guid PublicId, DateOnly Fecha, StatementLineDirection Sentido, decimal Valor, string? Referencia);

/// <summary>
/// Un registro de los libros que falta por cruzar: su clave (clase y <c>PublicId</c>), fecha, sentido en el banco (abono o cargo), valor y referencia, y la línea del
/// extracto de la que nació si ya la conoce (el recibo de banco y la partida de Recaudo nacen de un abono). (nuevo)
/// </summary>
public sealed record RegistroACruzar(string Clave, DateOnly Fecha, StatementLineDirection Sentido, decimal Valor, string? Referencia, Guid? LineaDeOrigen = null);

/// <summary>Un cruce que propone el motor: la línea, los registros que la explican y la regla. (nuevo)</summary>
public sealed record CrucePropuesto(long LineaId, IReadOnlyList<string> Registros, string Regla);

/// <summary>
/// El motor puro del cruce automático de la conciliación mínima (feature 015, US25, T0275; FR-216; casos dorados <c>cruce-*.json</c>). No lee ni escribe nada: recibe las
/// líneas y los registros que faltan por cruzar y los días de holgura (<c>Conciliacion.DiasDeHolgura</c>) y devuelve los cruces. Reglas, en este orden:
/// <list type="number">
/// <item><b><see cref="ReglaVinculo"/></b>: los registros que nacieron de la línea (recibos y partidas de Recaudo identificados desde ese abono) la explican si suman su
/// valor exacto;</item>
/// <item><b><see cref="ReglaUnoAUno"/></b>: mismo sentido, valor exacto y fecha dentro de la holgura; si hay varios candidatos, desempata la referencia igual; si sigue
/// habiendo más de uno, o el registro tiene otra línea igual de candidata, <b>no cruza</b> (el empate lo resuelve una persona);</item>
/// <item><b><see cref="ReglaGrupo"/></b> (uno a varios, sólo después de uno a uno): los registros con la misma referencia que la línea dentro de la holgura, o todos los
/// del mismo día, que suman exacto su valor, y sólo si ninguna otra línea abierta podría reclamarlos.</item>
/// </list>
/// Nada se cruza dos veces: una línea o un registro cruzado sale del juego. (nuevo)
/// </summary>
public static class CruceDeConciliacion
{
    public const string ReglaVinculo = "StatementLink";
    public const string ReglaUnoAUno = "ValueDateReference";
    public const string ReglaGrupo = "ValueDateReferenceGroup";

    public static IReadOnlyList<CrucePropuesto> Cruzar(IReadOnlyList<LineaACruzar> lineas, IReadOnlyList<RegistroACruzar> registros, int diasDeHolgura)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(registros);
        var holgura = Math.Max(0, diasDeHolgura);
        var lineasLibres = lineas.OrderBy(l => l.Fecha).ThenBy(l => l.Id).ToList();
        var libres = registros.ToList();
        var cruces = new List<CrucePropuesto>();

        void Cruzar(LineaACruzar l, IReadOnlyList<RegistroACruzar> rs, string regla)
        {
            cruces.Add(new CrucePropuesto(l.Id, rs.Select(r => r.Clave).ToList(), regla));
            lineasLibres.Remove(l);
            foreach (var r in rs) libres.Remove(r);
        }

        // 1. El vínculo con la línea de origen.
        foreach (var l in lineasLibres.ToList())
        {
            var hijos = libres.Where(r => r.LineaDeOrigen == l.PublicId && r.Sentido == l.Sentido).ToList();
            if (hijos.Count > 0 && hijos.Sum(r => r.Valor) == l.Valor) Cruzar(l, hijos, ReglaVinculo);
        }

        // 2. Uno a uno: valor exacto, fecha con holgura, la referencia desempata; un empate no cruza.
        bool Candidato(LineaACruzar l, RegistroACruzar r) =>
            r.Sentido == l.Sentido && r.Valor == l.Valor && Math.Abs(r.Fecha.DayNumber - l.Fecha.DayNumber) <= holgura;

        var hubo = true;
        while (hubo)
        {
            hubo = false;
            foreach (var l in lineasLibres.ToList())
            {
                var candidatos = libres.Where(r => Candidato(l, r)).ToList();
                if (candidatos.Count == 0) continue;
                var conReferencia = candidatos.Where(r => MismaReferencia(l.Referencia, r.Referencia)).ToList();
                var elegidos = conReferencia.Count > 0 ? conReferencia : candidatos;
                if (elegidos.Count != 1) continue;
                var r = elegidos[0];
                var otrasLineas = lineasLibres.Where(o => o != l && Candidato(o, r)).ToList();
                var porReferencia = conReferencia.Count > 0;
                // Otra línea igual de candidata para el mismo registro: si la referencia no distingue, es un empate.
                if (otrasLineas.Count > 0 && (!porReferencia || otrasLineas.Any(o => MismaReferencia(o.Referencia, r.Referencia)))) continue;
                Cruzar(l, [r], ReglaUnoAUno);
                hubo = true;
            }
        }

        // 3. Uno a varios: por la misma referencia, o todos los del mismo día; sólo si la suma es exacta y no hay otra línea que los pueda reclamar.
        foreach (var l in lineasLibres.ToList())
        {
            var porReferencia = string.IsNullOrWhiteSpace(l.Referencia)
                ? []
                : libres.Where(r => r.Sentido == l.Sentido && MismaReferencia(l.Referencia, r.Referencia)
                                    && Math.Abs(r.Fecha.DayNumber - l.Fecha.DayNumber) <= holgura).ToList();
            if (porReferencia.Count >= 2 && porReferencia.Sum(r => r.Valor) == l.Valor && !OtraLineaIgual(l, lineasLibres))
            {
                Cruzar(l, porReferencia, ReglaGrupo);
                continue;
            }
            var delDia = libres.Where(r => r.Sentido == l.Sentido && r.Fecha == l.Fecha).ToList();
            if (delDia.Count >= 2 && delDia.Sum(r => r.Valor) == l.Valor && !OtraLineaIgual(l, lineasLibres)
                && !lineasLibres.Any(o => o != l && o.Sentido == l.Sentido && o.Fecha == l.Fecha))
                Cruzar(l, delDia, ReglaGrupo);
        }
        return cruces;
    }

    private static bool OtraLineaIgual(LineaACruzar l, IReadOnlyList<LineaACruzar> abiertas) =>
        abiertas.Any(o => o != l && o.Sentido == l.Sentido && o.Valor == l.Valor && o.Fecha == l.Fecha);

    /// <summary>Dos referencias son la misma si, sin espacios ni ceros a la izquierda y sin distinguir mayúsculas, coinciden; vacía no coincide con nada.</summary>
    public static bool MismaReferencia(string? a, string? b)
    {
        var x = Normalizar(a);
        var y = Normalizar(b);
        return x.Length > 0 && x == y;
    }

    private static string Normalizar(string? r) =>
        string.IsNullOrWhiteSpace(r) ? string.Empty : new string(r.Where(c => !char.IsWhiteSpace(c)).ToArray()).TrimStart('0').ToUpperInvariant();
}
