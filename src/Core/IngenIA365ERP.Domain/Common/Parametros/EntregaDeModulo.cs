using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Common.Parametros;

/// <summary>
/// Una entrega de una línea de producto (feature 013, T43, R17; decisiones-transversales §2.6): el <see cref="Modulo"/> de la
/// línea (<c>INV</c> para el comercio de la feature 012, <c>LND</c> para Cartera) y el <see cref="Orden"/> en que sale. Reemplaza a
/// <see cref="EntregaDelComercio"/> en <see cref="DefinicionDeParametro.DisponibleDesde"/> y
/// <see cref="DefinicionDeParametro.ValoresDesde"/>: una clave de Cartera marcada K2 no se ofrece ni se acepta hasta que
/// <see cref="CatalogoDeCartera.EntregaVigente"/> la alcance, y las de Inventario siguen gobernadas por la suya.
///
/// <para>
/// <see cref="EntregaDelComercio"/> y <see cref="EntregaDeCartera"/> siguen siendo los nombres tipados de sus líneas y se
/// convierten solos (<c>EntregaDelComercio.I5</c> → <c>("INV", 6)</c>, <c>EntregaDeCartera.K2</c> → <c>("LND", 2)</c>): el código
/// del comercio que compara entregas entre sí (<c>entrega &gt;= EntregaDelComercio.I6</c>) no cambia. Dos entregas de líneas
/// distintas no se comparan (lanza): «K2 es posterior a I4» no significa nada. No se guarda en ninguna tabla. (nuevo)
/// </para>
/// </summary>
public readonly record struct EntregaDeModulo(string Modulo, int Orden, string Nombre) : IComparable<EntregaDeModulo>
{
    /// <summary>La línea del comercio (feature 012): <see cref="EntregaDelComercio"/>.</summary>
    public const string LineaDelComercio = "INV";

    /// <summary>La línea de Cartera (feature 013): <see cref="EntregaDeCartera"/>.</summary>
    public const string LineaDeCartera = "LND";

    public static implicit operator EntregaDeModulo(EntregaDelComercio entrega) =>
        new(LineaDelComercio, (int)entrega, entrega.ToString());

    public static implicit operator EntregaDeModulo(EntregaDeCartera entrega) =>
        new(LineaDeCartera, (int)entrega, entrega.ToString());

    public int CompareTo(EntregaDeModulo other)
    {
        MismaLinea(this, other);
        return Orden.CompareTo(other.Orden);
    }

    public static bool operator <(EntregaDeModulo a, EntregaDeModulo b) => a.CompareTo(b) < 0;
    public static bool operator >(EntregaDeModulo a, EntregaDeModulo b) => a.CompareTo(b) > 0;
    public static bool operator <=(EntregaDeModulo a, EntregaDeModulo b) => a.CompareTo(b) <= 0;
    public static bool operator >=(EntregaDeModulo a, EntregaDeModulo b) => a.CompareTo(b) >= 0;

    /// <summary>El nombre de la entrega («I5», «K2»), que es lo que muestran la pantalla y <c>availableFrom</c>.</summary>
    public override string ToString() => Nombre;

    private static void MismaLinea(EntregaDeModulo a, EntregaDeModulo b)
    {
        if (!string.Equals(a.Modulo, b.Modulo, StringComparison.Ordinal))
            throw new InvalidOperationException($"No se comparan entregas de líneas distintas ({a.Modulo} {a.Nombre} y {b.Modulo} {b.Nombre}).");
    }
}
