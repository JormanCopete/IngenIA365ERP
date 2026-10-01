using System.Globalization;

namespace IngenIA365ERP.Domain.Inventory.Analytics;

/// <summary>La clase ABC de un producto. No se guarda: se calcula a una fecha (el conteo guarda el texto en <c>CountScopeJson</c>). (nuevo)</summary>
public enum ClaseAbc { A = 1, B = 2, C = 3 }

/// <summary>El resultado de interpretar el texto de <c>Informes.UmbralesAbc</c>. (nuevo)</summary>
public sealed record UmbralesInterpretados(bool Admitido, UmbralesAbc? Umbrales, string? Codigo, string? Mensaje);

/// <summary>
/// Los umbrales del análisis ABC, en porcentaje, que suman 100 (<c>Informes.UmbralesAbc</c>, «A/B/C»; FR-086). El valor por defecto
/// lo trae el catálogo de parámetros, no este tipo. (nuevo)
/// </summary>
public sealed record UmbralesAbc(decimal A, decimal B, decimal C)
{
    /// <summary>El código con que se rechaza un texto de umbrales (el mismo del lector de parámetros).</summary>
    public const string CodigoNoAdmitido = "Parameters.ValueNotAllowed";

    private const decimal Total = 100m;

    /// <summary>
    /// Lee «A/B/C» (enteros de hasta tres cifras, espacios alrededor admitidos): los tres suman 100 y A es mayor que cero. Lo demás es
    /// <see cref="CodigoNoAdmitido"/> con el motivo; nunca cae a un defecto.
    /// </summary>
    public static UmbralesInterpretados Interpretar(string? texto)
    {
        var partes = (texto ?? string.Empty).Split('/', StringSplitOptions.TrimEntries);
        if (partes.Length != 3 || partes.Any(p => p.Length is 0 or > 3 || !p.All(char.IsAsciiDigit)))
            return NoAdmitido($"Informes.UmbralesAbc tiene la forma «A/B/C» con porcentajes enteros; «{texto}» no la tiene.");

        var valores = partes.Select(p => decimal.Parse(p, NumberStyles.None, CultureInfo.InvariantCulture)).ToArray();
        if (valores.Sum() != Total)
            return NoAdmitido($"Los umbrales de Informes.UmbralesAbc suman 100; «{texto}» suma {valores.Sum()}.");
        if (valores[0] <= 0m)
            return NoAdmitido("El umbral de la clase A de Informes.UmbralesAbc es mayor que cero.");

        return new UmbralesInterpretados(true, new UmbralesAbc(valores[0], valores[1], valores[2]), null, null);
    }

    private static UmbralesInterpretados NoAdmitido(string mensaje) => new(false, null, CodigoNoAdmitido, mensaje);
}

/// <summary>Lo que se clasifica: una clave (el producto) y su valor (ventas o consumo al costo, en pesos). (nuevo)</summary>
public sealed record ValorParaAbc<TClave>(TClave Clave, decimal Valor);

/// <summary>
/// Una fila del análisis ABC: la clave, su valor, su participación y el acumulado —en porcentaje, a dos decimales— y su clase. En el
/// orden de la clasificación. (nuevo)
/// </summary>
public sealed record FilaAbc<TClave>(TClave Clave, decimal Valor, decimal Participacion, decimal Acumulado, ClaseAbc Clase);

/// <summary>
/// La clasificación ABC (feature 012, I6, T916; FR-040, FR-086; contracts/api.md §27 vista <c>abc</c>): pura y sin IO, y la
/// <b>única</b> del módulo —la usan el conteo cíclico por clase (<c>OpenPhysicalCountCommand</c> con <c>scope = AbcClass</c>) y la
/// vista <c>abc</c> de US17, que no crean otra—. Reglas:
/// <list type="bullet">
/// <item>se ordena por valor de mayor a menor (a igual valor, por clave) y se acumula la participación sobre el total de los valores
/// positivos;</item>
/// <item>un producto es A si el acumulado <b>con él</b> no pasa del umbral A, B si no pasa de A + B, y C si no: el que cruza un umbral
/// pasa a la clase siguiente. El primero siempre es A, aunque él solo pase del umbral (un producto que es el 95 % del valor es el
/// primero que hay que contar);</item>
/// <item>los <b>empates</b> van juntos: todo el grupo de igual valor toma la clase que le daría el acumulado con su primer miembro;</item>
/// <item>un valor cero o negativo es C y participa con cero; si todo vale cero, todo es C.</item>
/// </list>
/// Los porcentajes se muestran a dos decimales; la clasificación usa los valores exactos. (nuevo)
/// </summary>
public static class ClasificacionAbc
{
    private const int DecimalesDePorcentaje = 2;
    private const decimal Cien = 100m;

    public static IReadOnlyList<FilaAbc<TClave>> Clasificar<TClave>(IReadOnlyList<ValorParaAbc<TClave>> valores, UmbralesAbc umbrales)
    {
        ArgumentNullException.ThrowIfNull(valores);
        ArgumentNullException.ThrowIfNull(umbrales);

        var ordenados = valores
            .OrderByDescending(v => v.Valor)
            .ThenBy(v => v.Clave, Comparer<TClave>.Default)
            .ToList();
        var total = ordenados.Where(v => v.Valor > 0m).Sum(v => v.Valor);
        var filas = new List<FilaAbc<TClave>>(ordenados.Count);
        var acumulado = 0m;

        var primero = true;
        foreach (var grupo in ordenados.GroupBy(v => v.Valor))
        {
            var conElPrimero = total > 0m ? acumulado + grupo.Key * Cien / total : 0m;
            var clase = grupo.Key <= 0m || total == 0m ? ClaseAbc.C
                : primero || conElPrimero <= umbrales.A ? ClaseAbc.A
                : conElPrimero <= umbrales.A + umbrales.B ? ClaseAbc.B
                : ClaseAbc.C;
            primero = false;
            foreach (var v in grupo)
            {
                var participacion = v.Valor > 0m && total > 0m ? v.Valor * Cien / total : 0m;
                acumulado += participacion;
                filas.Add(new FilaAbc<TClave>(v.Clave, v.Valor, Porcentaje(participacion), Porcentaje(acumulado), clase));
            }
        }

        return filas;
    }

    private static decimal Porcentaje(decimal valor) => Math.Round(valor, DecimalesDePorcentaje, MidpointRounding.AwayFromZero);
}
