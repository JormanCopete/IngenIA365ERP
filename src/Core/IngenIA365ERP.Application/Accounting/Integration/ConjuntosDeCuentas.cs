namespace IngenIA365ERP.Application.Accounting.Integration;

/// <summary>
/// Los <b>conjuntos de cuentas</b> de una conciliación (feature 012, T516; feature 013, T0644, C82; contracts/contabilidad.md §14.1 de la
/// 013 y §7.2 de la 012): los componentes conexos del grafo combinación ↔ cuenta. Dos combinaciones que comparten una cuenta van juntas, y
/// una combinación que resuelve a dos cuentas (reglas por sucursal o por bodega) las junta: sólo se compara por conjunto. Genérico en la
/// combinación (un grupo contable en Inventario; un rol de saldo con sus dimensiones en Cartera) y en la familia de roles. Puro. (nuevo)
/// </summary>
public static class ConjuntosDeCuentas
{
    /// <summary>
    /// Los componentes conexos de <paramref name="aristas"/>: cada arista une la combinación que devuelve <paramref name="combinacion"/> con
    /// la cuenta de <paramref name="cuenta"/> dentro de su <paramref name="familia"/> (dos familias nunca se juntan). Devuelve, por
    /// componente, sus aristas en el orden recibido; los componentes, en el orden de su primera arista.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<T>> Componentes<T>(
        IReadOnlyList<T> aristas, Func<T, string> combinacion, Func<T, int> cuenta, Func<T, string>? familia = null)
    {
        ArgumentNullException.ThrowIfNull(aristas);
        var padre = new Dictionary<string, string>(StringComparer.Ordinal);
        string Raiz(string x)
        {
            if (!padre.TryGetValue(x, out var p)) { padre[x] = x; return x; }
            if (p == x) return x;
            var r = Raiz(p);
            padre[x] = r;
            return r;
        }
        string Familia(T a) => familia?.Invoke(a) ?? string.Empty;
        string Nodo(T a) => "C:" + Familia(a) + "|" + combinacion(a);

        foreach (var a in aristas)
        {
            var nodo = Raiz(Nodo(a));
            var deCuenta = Raiz("A:" + Familia(a) + "|" + cuenta(a));
            if (nodo != deCuenta) padre[deCuenta] = nodo;
        }
        return aristas.GroupBy(a => Raiz(Nodo(a)))
            .Select(g => (IReadOnlyList<T>)g.ToList())
            .ToList();
    }
}
